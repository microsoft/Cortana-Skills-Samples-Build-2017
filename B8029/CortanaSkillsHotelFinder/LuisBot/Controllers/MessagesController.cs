namespace LuisBot
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Configuration;
    using System.Web.Http;
    using Dialogs;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Connector;
    using Services;
    using Microsoft.Bot.Builder.Dialogs.Internals;
    using Microsoft.Bot.Builder.Internals.Fibers;
    using Autofac;
    using System.Threading;

    /// <summary>
    /// Activity mapper that automatically populates activity.speak for speech enabled channels.
    /// </summary>
    public sealed class TextToSpeakActivityMapper : IMessageActivityMapper
    {
        private readonly IChannelCapability capability;

        public TextToSpeakActivityMapper(IChannelCapability capability)
        {
            SetField.NotNull(out this.capability, nameof(capability), capability);

        }

        public IMessageActivity Map(IMessageActivity message)
        {
            // only set the speak if it is not set by the developer.
            if (this.capability.SupportsSpeak() && !string.IsNullOrEmpty(message.Text) && string.IsNullOrEmpty(message.Speak))
            {
                message.Speak = message.Text;
                // Setting to ExpectingInput - safe to do in this sample, since this ActivityMapper is here to support FormFlow
                // and we are setting Speak and InputHint explicitly for all other responses.
                message.InputHint = InputHints.ExpectingInput;
            }
            return message;
        }
    }

    [BotAuthentication]
    public class MessagesController : ApiController
    {
        private static readonly bool IsSpellCorrectionEnabled = bool.Parse(WebConfigurationManager.AppSettings["IsSpellCorrectionEnabled"]);

        private readonly BingSpellCheckService spellService = new BingSpellCheckService();

        private readonly static IContainer Container;

        static MessagesController()
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule(new DialogModule());

            // Add TextToSpeak mapper to list of mappers
            builder
                .RegisterType<TextToSpeakActivityMapper>()
                .As<IMessageActivityMapper>()
                .PreserveExistingDefaults()
                .InstancePerLifetimeScope();

            builder
                .RegisterInstance(new RootLuisDialog())
                .AsSelf()
                .As<IDialog<object>>();

            Container = builder.Build();
        }
        /// <summary>
        /// POST: api/Messages
        /// Receive a message from a user and reply to it
        /// </summary>
        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {
            if (activity != null)
            {
                // one of these will have an interface and process it
                switch (activity.GetActivityType())
                {
                    case ActivityTypes.Message:
                        if (IsSpellCorrectionEnabled)
                        {
                            try
                            {
                                activity.Text = await this.spellService.GetCorrectedTextAsync(activity.Text);
                            }
                            catch (Exception ex)
                            {
                                Trace.TraceError(ex.ToString());
                            }
                        }

                        using (var scope = DialogModule.BeginLifetimeScope(Container, activity))
                        {
                            var task = scope.Resolve<IPostToBot>();
                            await task.PostAsync(activity, CancellationToken.None);
                        }
                        break;
                    case ActivityTypes.EndOfConversation:
                        // delete Conversation and PrivateConversation data when cortana channel sending EndOfConversation
                        if (activity.ChannelId == ChannelIds.Cortana)
                        {
                            using (var scope = DialogModule.BeginLifetimeScope(Container, activity))
                            {
                                var botData = scope.Resolve<IBotData>();
                                await botData.LoadAsync(CancellationToken.None);
                                botData.ConversationData.Clear();
                                botData.PrivateConversationData.Clear();
                                await botData.FlushAsync(CancellationToken.None);
                            }
                        }
                        break;
                    case ActivityTypes.ConversationUpdate:
                    case ActivityTypes.ContactRelationUpdate:
                    case ActivityTypes.Typing:
                    case ActivityTypes.DeleteUserData:
                    case ActivityTypes.Ping:
                    default:
                        Trace.TraceError($"Unknown activity type ignored: {activity.GetActivityType()}");
                        break;
                }
            }
            return new HttpResponseMessage(System.Net.HttpStatusCode.Accepted);

        }
    }
}