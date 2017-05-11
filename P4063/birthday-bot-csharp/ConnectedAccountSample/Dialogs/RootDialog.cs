using System;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using System.Net.Http;
using Newtonsoft.Json;

namespace ConnectedAccountSample.Dialogs
{
    [Serializable]
    public class RootDialog : IDialog<object>
    {
        public Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);

            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;

            var ShowText = String.Empty;
            var SpokenText = String.Empty;

            // Is the user auth'd?
            string authAccessToken = String.Empty;

            if (activity.Entities != null)
            {
                foreach (var entity in activity.Entities)
                {
                    if (entity.Type == "AuthorizationToken")
                    {
                        dynamic authResult = entity.Properties;
                        authAccessToken = authResult.token;
                    }
                }
            }

            if (String.IsNullOrEmpty(authAccessToken))
            {
                ShowText = "Error: Cortana did not send expected authorization token";
                SpokenText = "Error invoking the Birthday Tracker. Cortana did not send the expected authorization token";
            }
            else
            {
                // Use access token to get user info from Live API
                var url = "https://apis.live.net/v5.0/me?access_token=" + authAccessToken;
                using (var client = new HttpClient())
                {
                    // Alternative way of passing an access_token is in the Authorization header
                    // Example:
                    // client.DefaultRequestHeaders.Add("Authorization", "Bearer " + authAccessToken);

                    var response = await client.GetAsync(url);
                    if (!response.IsSuccessStatusCode)
                    {
                        // API call failed, present an error
                        // return our reply to the user
                        ShowText = "Failed to connect to profile";
                        SpokenText = "Sorry, I couldn\'t connect to your profile";
                    }
                    else
                    {
                        var responseString = await response.Content.ReadAsStringAsync();

                        // Extract useful info from API response 
                        dynamic data = JsonConvert.DeserializeObject(responseString);
                        try
                        {
                            // Extract useful info from API response
                            var name = (string)data.first_name;
                            var month = (int)data.birth_month;
                            var day = (int)data.birth_day;

                                var birthdayDate = new DateTime(DateTime.Today.Year, (int)month, (int)day);
                                int daysUntilBirthday = (int)(birthdayDate - DateTime.Today).TotalDays;
                                // Have we already passed the birthday?
                                if (daysUntilBirthday < 0)
                                {
                                    daysUntilBirthday = (int)(birthdayDate.AddYears(1) - DateTime.Today).TotalDays;
                                }

                                // Format spoken text
                                // Special responses if birthday is today or tomorrow
                                SpokenText = ShowText = $"Hi {name}. There are {daysUntilBirthday} days until your birthday.";
                            if (daysUntilBirthday == 0)
                            {
                                SpokenText = ShowText = $"Today is your birthday. Happy birthday, {name}";
                            }
                            else if (daysUntilBirthday == 1)
                            {
                                SpokenText = ShowText = $"Hi {name}. Tomorrow is your birthday.";
                            }                           
                        }
                        catch (Exception)
                        {
                            // Name or birthday not available in response, present an error
                            ShowText = "Sorry, I couldn\'t get your info. Failed to get name or birthday";
                            SpokenText = "Sorry, I couldn\'t get your info";
                        }
                    }
                }
            }

            // return our reply to the user
            Activity reply = activity.CreateReply(ShowText);
            reply.Speak = SpokenText;
            reply.InputHint = InputHints.IgnoringInput;
            await context.PostAsync(reply);

            // End the conversation
            var endReply = activity.CreateReply(string.Empty);
            endReply.Type = ActivityTypes.EndOfConversation;
            endReply.AsEndOfConversationActivity().Code = EndOfConversationCodes.CompletedSuccessfully;
            await context.PostAsync(endReply);

            // We're done
            context.Done(default(object));
        }
    }
}