namespace LuisBot.Dialogs
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Web;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Builder.FormFlow;
    using Microsoft.Bot.Builder.Luis;
    using Microsoft.Bot.Builder.Luis.Models;
    using Microsoft.Bot.Connector;
    using System.Text;

    [Serializable]
    [LuisModel("YourModelId", "YourSubscriptionKey")]
    public class RootLuisDialog : LuisDialog<object>
    {
        private const string EntityGeographyCity = "builtin.geography.city";

        private const string EntityHotelName = "Hotel";

        private const string EntityAirportCode = "AirportCode";

        private IList<string> titleOptions = new List<string> { "“Very stylish, great stay, great staff”", "“good hotel, awful meals”", "“Needs more attention to little things”", "“Lovely small hotel ideally situated to explore the area.”", "“Positive surprise”", "“Beautiful suite and resort”" };

        /// <summary>
        /// Need to override the LuisDialog.MessageReceived method so that we can detect when the user invokes the skill without
        /// specifying a phrase, for example: "Open Hotel Finder", or "Ask Hotel Finder". In these cases, the message received will be an empty string
        /// </summary>
        /// <param name="context"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        protected override async Task MessageReceived(IDialogContext context, IAwaitable<IMessageActivity> item)
        {
            // Check for empty query
            var message = await item;
            if (message.Text == null)
            {
                // Return the Help/Welcome
                await Help(context, null);
            }
            else
            {
                await base.MessageReceived(context, item);
            }
        }

        [LuisIntent("")]
        [LuisIntent("None")]
        public async Task None(IDialogContext context, LuisResult result)
        {
            var response = context.MakeMessage();
            response.Text = $"Sorry, I did not understand '{result.Query}'. Use 'help' if you need assistance.";
            response.Speak = $"Sorry, I did not understand '{result.Query}'. Say 'help' if you need assistance.";
            response.InputHint = InputHints.AcceptingInput;

            await context.PostAsync(response);

            context.Wait(this.MessageReceived);
        }

        [LuisIntent("SearchHotels")]
        public async Task Search(IDialogContext context, IAwaitable<IMessageActivity> activity, LuisResult result)
        {
            bool isWelcomeDone = false;
            context.ConversationData.TryGetValue<bool>("WelcomeDone", out isWelcomeDone);

            // Did we already do this? Has the user followed up an initial query with another one?
            if (!isWelcomeDone)
            {
                var response = context.MakeMessage();
                // For display text, use Summary to display large font, italics - this is to emphasize this
                // is the Skill speaking, not Cortana
                // Continue the displayed text using the Text property of the response message
                response.Summary = $"Welcome to the Hotels finder!";
                response.Text = $"We are analyzing your message: '{(await activity).Text}'...";
                // Speak is what is spoken out
                response.Speak = @"<speak version=""1.0"" xml:lang=""en-US"">Welcome to the Hotels finder!<break time=""1000ms""/></speak>"; ;
                // InputHint influences how the microphone behaves
                response.InputHint = InputHints.IgnoringInput;
                // Post the response message
                await context.PostAsync(response);

                // Set a flag in conversation data to record that we already sent out the Welcome message
                context.ConversationData.SetValue<bool>("WelcomeDone", true);
            }

            // Check that the user has specified either a destination city, or an airport code
            var hotelsQuery = new HotelsQuery();

            EntityRecommendation cityEntityRecommendation;

            if (result.TryFindEntity(EntityGeographyCity, out cityEntityRecommendation))
            {
                cityEntityRecommendation.Type = "Destination";
            }

            // Use a FormDialog to query the user for missing destination, if necessary
            var hotelsFormDialog = new FormDialog<HotelsQuery>(hotelsQuery, this.BuildHotelsForm, FormOptions.PromptInStart, result.Entities);

            context.Call(hotelsFormDialog, this.ResumeAfterHotelsFormDialog);
        }

        [LuisIntent("ShowHotelsReviews")]
        public async Task Reviews(IDialogContext context, LuisResult result)
        {
            EntityRecommendation hotelEntityRecommendation;

            if (result.TryFindEntity(EntityHotelName, out hotelEntityRecommendation))
            {
                await context.PostAsync($"Looking for reviews of '{hotelEntityRecommendation.Entity}'...");

                var resultMessage = context.MakeMessage();
                resultMessage.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                resultMessage.Attachments = new List<Attachment>();

                for (int i = 0; i < 5; i++)
                {
                    var random = new Random(i);
                    ThumbnailCard thumbnailCard = new ThumbnailCard()
                    {
                        Title = this.titleOptions[random.Next(0, this.titleOptions.Count - 1)],
                        Text = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Mauris odio magna, sodales vel ligula sit amet, vulputate vehicula velit. Nulla quis consectetur neque, sed commodo metus.",
                        Images = new List<CardImage>()
                        {
                            new CardImage() { Url = "https://upload.wikimedia.org/wikipedia/en/e/ee/Unknown-person.gif" }
                        },
                    };

                    resultMessage.Attachments.Add(thumbnailCard.ToAttachment());
                }

                await context.PostAsync(resultMessage);
            }

            context.Wait(this.MessageReceived);
        }

        [LuisIntent("Help")]
        public async Task Help(IDialogContext context, LuisResult result)
        {
            var response = context.MakeMessage();
            response.Summary = "Hi! Try asking me things like 'search for hotels in Seattle', 'search for hotels near LAX airport' or 'show me the reviews of The Bot Resort'";
            response.Speak = @"<speak version=""1.0"" xml:lang=""en-US"">Hi! Try asking me things like 'search for hotels in Seattle', "
                + @"'search for hotels near<break time=""100ms""/>" + SSMLHelper.SayAs("characters", "LAX")
                + @" <break time=""200ms""/>airport', or 'show me the reviews of The Bot Resort'</speak>";
            response.InputHint = InputHints.ExpectingInput;

            await context.PostAsync(response);

            context.Wait(this.MessageReceived);
        }

        [LuisIntent("Goodbye")]
        public async Task Goodbye(IDialogContext context, LuisResult result)
        {
            var goodByeMessage = context.MakeMessage();
            goodByeMessage.Summary = goodByeMessage.Speak = "Thanks for using Hotel Finder!";
            goodByeMessage.InputHint = InputHints.IgnoringInput;
            await context.PostAsync(goodByeMessage);

            var completeMessage = context.MakeMessage();
            completeMessage.Type = ActivityTypes.EndOfConversation;
            completeMessage.AsEndOfConversationActivity().Code = EndOfConversationCodes.CompletedSuccessfully;

            await context.PostAsync(completeMessage);

            context.Done(default(object));
        }

        private IForm<HotelsQuery> BuildHotelsForm()
        {
            OnCompletionAsyncDelegate<HotelsQuery> processHotelsSearch = async (context, state) =>
            {
                var message = "Searching for hotels";
                var speech = @"<speak version=""1.0"" xml:lang=""en-US"">Searching for hotels";
                if (!string.IsNullOrEmpty(state.Destination))
                {
                    state.Destination = state.Destination.Capitalize();
                    message += $" in { state.Destination}...";
                    speech += $" in { state.Destination}...";
                }
                else if (!string.IsNullOrEmpty(state.AirportCode))
                {
                    message += $" near {state.AirportCode.ToUpperInvariant()} airport...";
                    speech += $@" near<break time=""100ms""/>{SSMLHelper.SayAs("characters",state.AirportCode.ToUpperInvariant())} <break time=""200ms""/>airport";
                }
                speech += "</speak>";

                var response = context.MakeMessage();
                response.Summary = message;
                response.Speak = speech;
                response.InputHint = InputHints.IgnoringInput;
                await context.PostAsync(response);
            };

            return new FormBuilder<HotelsQuery>()
                .Field(nameof(HotelsQuery.Destination), (state) => string.IsNullOrEmpty(state.AirportCode))
                .Field(nameof(HotelsQuery.AirportCode), (state) => string.IsNullOrEmpty(state.Destination))
                .OnCompletion(processHotelsSearch)
                .Build();
        }

        private async Task ResumeAfterHotelsFormDialog(IDialogContext context, IAwaitable<HotelsQuery> result)
        {
            try
            {
                var searchQuery = await result;

                searchQuery.Destination = searchQuery.Destination.Capitalize();
                var hotels = await this.GetHotelsAsync(searchQuery);

                // We show results differently depending on whether this is a Voice-only, or Voice+screen client
                bool HasDisplay = true;
                var messageActivity = context.Activity.AsMessageActivity();
                if (messageActivity.Entities != null)
                {
                    foreach (var entity in messageActivity.Entities)
                    {
                        if (entity.Type == "DeviceInfo")
                        {
                            dynamic deviceInfo = entity.Properties;
                            HasDisplay = (bool)deviceInfo.supportsDisplay;
                        }
                    }
                }
                if (HasDisplay)
                {
                    await PresentResultsVisual(context, hotels);
                }
                else
                {
                    await PresentResultsVoiceOnly(context, hotels);
                }
            }
            catch (FormCanceledException ex)
            {
                string reply;

                if (ex.InnerException == null)
                {
                    reply = "You have canceled the operation.";
                }
                else
                {
                    reply = $"Oops! Something went wrong :( Technical Details: {ex.InnerException.Message}";
                }
                var errorMessage = context.MakeMessage();
                errorMessage.Text = errorMessage.Speak = reply;
                errorMessage.InputHint = InputHints.IgnoringInput;
                await context.PostAsync(errorMessage);
            }
        }

        private async Task PresentResultsVisual(IDialogContext context, IEnumerable<Hotel> hotels)
        {
            var progressMessage = context.MakeMessage();
            progressMessage.Summary = progressMessage.Speak = $"I found {hotels.Count()} hotels. Showing them for you now:";
            progressMessage.InputHint = InputHints.IgnoringInput;
            await context.PostAsync(progressMessage);

            var resultMessage = context.MakeMessage();
            resultMessage.InputHint = InputHints.AcceptingInput;
            resultMessage.AttachmentLayout = AttachmentLayoutTypes.Carousel;
            resultMessage.Attachments = new List<Attachment>();

            foreach (var hotel in hotels)
            {
                HeroCard heroCard = new HeroCard()
                {
                    Title = hotel.Name,
                    Subtitle = $"{hotel.Rating} stars. {hotel.NumberOfReviews} reviews. From ${hotel.PriceStarting} per night.",
                    Images = new List<CardImage>()
                        {
                            new CardImage() { Url = hotel.Image }
                        },
                    Buttons = new List<CardAction>()
                        {
                            new CardAction()
                            {
                                Title = "More details",
                                Type = ActionTypes.OpenUrl,
                                Value = $"https://www.bing.com/search?q=hotels+in+" + HttpUtility.UrlEncode(hotel.Location)
                            }
                        }
                };

                resultMessage.Attachments.Add(heroCard.ToAttachment());
            }

            await context.PostAsync(resultMessage);
        }

        private async Task PresentResultsVoiceOnly(IDialogContext context, IEnumerable<Hotel> hotels)
        {
            // For voice, we'll limit results to first three otherwise it gets to be too long going through a long list using voice.
            // Aa well designed skill would offer the user the option to hear "Next Results" if the first ones don't interest them.
            // Not implemented in this sample.

            var hotelList = hotels.ToList();
            context.ConversationData.SetValue<List<Hotel>>("Hotels", hotelList);

            // Array of strings for the PromptDialog.Choice buttons - though note these are not spoken, just for debugging use
            var descriptions = new List<string>();

            // Build the spoken prompt listing the results
            var speakText = new StringBuilder();

            speakText.Append($"Here are the first three results: ");
            for (int count = 1; count < 4; count++)
            {
                var hotel = hotelList[count - 1];
                descriptions.Add($"{hotel.Name}");
                //speakText.Append($"{count}: {hotel.Name}, {hotel.Rating} stars, from ${hotel.PriceStarting} per night. ");
                speakText.Append($"{count}, {hotel.Name}, from ${hotel.PriceStarting}. ");
            }
            // Send the spoken message listing the options separately from the PromptDialog
            // Currently, PromptDialog built-in recognizer does not work if you have too long a 'speak' 
            // phrase (bug) before the user speaks their choice, so say most ahead of the choice dialog
            var resultsMessage = context.MakeMessage();
            resultsMessage.Speak = speakText.ToString();
            resultsMessage.InputHint = InputHints.IgnoringInput;
            await context.PostAsync(resultsMessage);

            // Define the choices, plus synonyms for each choice - include the hotel name
            var choices = new Dictionary<string, IReadOnlyList<string>>()
             {
                { "1", new List<string> { "one", hotelList[0].Name, hotelList[0].Name.ToLowerInvariant() } },
                { "2", new List<string> { "two", hotelList[1].Name, hotelList[1].Name.ToLowerInvariant() } },
                { "3", new List<string> { "three", hotelList[2].Name, hotelList[2].Name.ToLowerInvariant() } },
            };

            var promptOptions = new PromptOptionsWithSynonyms<string>(
                prompt: "notused", // prompt is not spoken
                choices: choices,
                descriptions: descriptions,
                speak: SSMLHelper.Speak($"Which one do you want to hear more about?"));

            PromptDialog.Choice(context, HotelChoiceReceivedAsync, promptOptions);
        }

        private async Task HotelChoiceReceivedAsync(IDialogContext context, IAwaitable<string> result)
        {

            int choiceIndex = 0;
            int.TryParse(await result, out choiceIndex);

            List<Hotel> hotelList;
            if (context.ConversationData.TryGetValue<List<Hotel>>("Hotels", out hotelList))
            {
                var hotel = hotelList[choiceIndex - 1];
                var resultsMessage = context.MakeMessage();
                resultsMessage.Speak = $"You chose: {hotel.Name}, {hotel.Rating} stars, from ${hotel.PriceStarting} per night. ";
                resultsMessage.InputHint = InputHints.IgnoringInput;
                await context.PostAsync(resultsMessage);

                StringBuilder bld = new StringBuilder("Here are some recent reviews: ");

                for (int i = 0; i < 3; i++)
                {
                    var random = new Random(i);
                    bld.AppendLine(this.titleOptions[random.Next(0, this.titleOptions.Count - 1)]);
                }
                var endMessage = context.MakeMessage();
                endMessage.Speak = bld.ToString();
                endMessage.InputHint = InputHints.AcceptingInput; // We're basically done, but they could ask another query if they wanted
                await context.PostAsync(endMessage);
            }

            context.Wait(this.MessageReceived);
        }

        private async Task<IEnumerable<Hotel>> GetHotelsAsync(HotelsQuery searchQuery)
        {
            var hotelNames = new List<string>()
                {"Excellent", "Splendid", "Supreme", "Excelsior", "High Class" };
            var hotels = new List<Hotel>();

            // Filling the hotels results manually just for demo purposes
            for (int i = 1; i <= 5; i++)
            {
                var random = new Random(i);
                Hotel hotel = new Hotel()
                {
                    //Name = $"{searchQuery.Destination ?? searchQuery.AirportCode} Hotel {i}",
                    Name = $"{hotelNames[i-1]} Hotel",
                    Location = searchQuery.Destination ?? searchQuery.AirportCode,
                    Rating = random.Next(1, 5),
                    NumberOfReviews = random.Next(0, 5000),
                    PriceStarting = random.Next(80, 450),
                    Image = $"https://placeholdit.imgix.net/~text?txtsize=35&txt=Hotel+{i}&w=500&h=260"
                };

                hotels.Add(hotel);
            }

            hotels.Sort((h1, h2) => h1.PriceStarting.CompareTo(h2.PriceStarting));

            // Waste some time to simulate database search
            await Task.Delay(3000);

            return hotels;
        }
    }

    static class StringExtensions
    {
        public static string Capitalize(this string input)
        {
            var output = string.Empty;
            if (!string.IsNullOrEmpty(input))
            {
                output = input.Substring(0, 1).ToUpper() + input.Substring(1);
            }
            // Strip out periods 
            output = output.Replace(".", "");

            return output;
        }
    }
}
