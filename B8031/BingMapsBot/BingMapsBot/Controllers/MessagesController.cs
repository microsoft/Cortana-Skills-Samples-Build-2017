using Microsoft.Bot.Builder.CognitiveServices.QnAMaker;
using Microsoft.Bot.Connector;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Configuration;
using System.Web.Http;

namespace BingMapsBot
{
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        private string BingMapsKey = "Your_Bing_Maps_Key";

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
                            var address = hub.Value<string>("address");

                            reply.Speak = "Here is your location";
                            reply.Attachments.Add(new HeroCard(
                                    title: "Your Location",
                                    text: address,
                                    images: new List<CardImage>() {
                                        new CardImage($"http://dev.virtualearth.net/REST/v1/Imagery/Map/Road/{lat},{lon}/15?mapSize=400,200&pp={lat},{lon}&key={BingMapsKey}")
                                    },
                                    buttons: new List<CardAction>
                                    {
                                        new CardAction("openUrl",  "Show in Map app", null, $"bingmaps:?where={lat},{lon}")
                                    }).ToAttachment());
                        }
                    }

                    if (string.IsNullOrEmpty(reply.Speak))
                    {
                        reply.Speak = "Unable to find your location.";
                        reply.Text = reply.Speak;
                    }
                }
                else if (text.Contains("map of "))
                {
                    var query = text.Substring(text.LastIndexOf("of ") + 3);

                    reply.ChannelData = JObject.FromObject(new
                    {
                        action = new { type = "LaunchUri", uri = $"bingmaps:?where={query}" }
                    });
                }
                else if (text.Contains("call support"))
                {
                    reply.ChannelData = JObject.FromObject(new
                    {
                        action = new { type = "LaunchUri", uri = "tel:1234567890" }
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
                        if (result.Answers[0].Answer.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        {
                            reply.ChannelData = JObject.FromObject(new
                            {
                                action = new { type = "LaunchUri", uri = result.Answers[0].Answer }
                            });
                        }
                        else
                        {
                            reply.Speak = result.Answers[0].Answer;
                        }
                    }
                    else
                    {
                        reply.Speak = "Sorry, but I'm unable to find any details on that.";                        
                    }

                    reply.Text = reply.Speak;
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