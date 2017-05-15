using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using Microsoft.Bot.Builder.CognitiveServices.QnAMaker;
using System.Web.Configuration;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Bot_Application1
{
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        /// <summary>
        /// POST: api/Messages
        /// Receive a message from a user and reply to it
        /// </summary>
        /// 
        private string BingMapsKey = "AiIIAV0L36pK7jQQvusmBgu_eYia2x-VKieS1rZVK20FPt73fpevIwuGCIA7yrv3";

        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {
            if (activity.Type == ActivityTypes.Message)
            {
                ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl));
                Activity reply = activity.CreateReply();

                var text = activity.Text.ToLowerInvariant();

                if (text.Contains("show my location"))
                {
                    var userInfo = activity.Entities.FirstOrDefault(e => e.Type.Equals("UserInfo"));
                    if (userInfo != null)
                    {
                        var currentLocation = userInfo.Properties["CurrentLocation"];
                        if (currentLocation != null)
                        {
                            var hub = currentLocation["Hub"];
                            var lat = hub.Value<double>("Latitude");
                            var lon = hub.Value<double>("Longitude");
                            var address = hub.Value<string>("Address");

                            reply.Speak = "Here is your location";
                            reply.Attachments.Add(new HeroCard(title: "Your location",
                                text: address,
                                images: new List<CardImage>() {
                                    new CardImage($"http://dev.virtualearth.net/REST/v1/Imagery/Map/Road/{lat},{lon}/15?mapSize=400,200&pp={lat},{lon}&key={BingMapsKey}")
                                }, buttons: new List<CardAction>
                                {
                                    new CardAction("openUrl", "Show in Map app", null, $"bingmaps:?where={lat},{lon}")
                                }
                                ).ToAttachment());
                        }
                    }
                }
                else if (text.Contains("call support"))
                {
                    reply.ChannelData = JObject.FromObject(new
                    {
                        action = new { Type = "LaunchUri", uri = "tel:123467890" }
                    });
                }
                else
                {
                    var QnAService = new QnAMakerService(new QnAMakerAttribute(
                        WebConfigurationManager.AppSettings["BingMapsQnAKey"],
                        WebConfigurationManager.AppSettings["BingMapsQnAId"]));

                    var result = await QnAService.QueryServiceAsync(activity.Text);

                    if (result.Answers != null && result.Answers.Count > 0)
                    {
                        reply.Speak = result.Answers[0].Answer;
                    }
                    else
                    {
                        reply.Speak = "Sorry, I didn't find anything";
                    }
                }
                await connector.Conversations.ReplyToActivityAsync(reply);
            }
            else
            {
                HandleSystemMessage(activity);
            }
            var response = Request.CreateResponse(HttpStatusCode.OK);
            return response;
        }

        private Activity HandleSystemMessage(Activity message)
        {
            if (message.Type == ActivityTypes.DeleteUserData)
            {
                // Implement user deletion here
                // If we handle user deletion, return a real message
            }
            else if (message.Type == ActivityTypes.ConversationUpdate)
            {
                // Handle conversation state changes, like members being added and removed
                // Use Activity.MembersAdded and Activity.MembersRemoved and Activity.Action for info
                // Not available in all channels
            }
            else if (message.Type == ActivityTypes.ContactRelationUpdate)
            {
                // Handle add/remove from contact lists
                // Activity.From + Activity.Action represent what happened
            }
            else if (message.Type == ActivityTypes.Typing)
            {
                // Handle knowing tha the user is typing
            }
            else if (message.Type == ActivityTypes.Ping)
            {
            }

            return null;
        }
    }
}