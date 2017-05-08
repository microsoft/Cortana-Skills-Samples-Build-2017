var restify = require('restify');
var builder = require('botbuilder');
var request = require('request');
var utils = require('./utils');

// Setup restify server
var server = restify.createServer();
server.listen(process.env.port || process.env.PORT || 3978, () => {
    console.log('%s listening to %s', server.name, server.url);
});

// Create chat connector
var connector = new builder.ChatConnector({
    appId: process.env.MICROSOFT_APP_ID,
    appPassword: process.env.MICROSOFT_APP_PASSWORD
});

// Listen for messages from users
server.post('/api/messages', connector.listen());

// Create bot with root dialog
var bot = new builder.UniversalBot(connector, (session) => {

    // Get access token from Cortana request
    var tokenEntity = session.message.entities.find((e) => {
        return e.type === 'AuthorizationToken';
    });

    // For connected accounts, Cortana will ALWAYS send a token
    // If the token doesn't exist, then this is a non-Cortana channel
    if (!tokenEntity) {
        // Send message that info is not available
        session.say('Failed to get info', 'Sorry, I couldn\'t get your info. Try again later on Cortana.', {
            inputHint: builder.InputHint.ignoringInput
        }).endConversation();
        return;
    }

    // Use access token to get user info from Live API
    var url = 'https://apis.live.net/v5.0/me?access_token=' + tokenEntity.token;
    request.get(url, (err, response, body) => {
        if (response.statusCode === 401) {
            // Access token isn't valid, present the oauth flow again
            var msg = new builder.Message(session)
                .addAttachment({
                    contentType: 'application/vnd.microsoft.card.oauth',
                    content: {}
                });
            session.endConversation(msg);
            return;
        }

        if (err || response.statusCode !== 200) {
            // API call failed, present an error
            session.say('Failed to connect to profile', 'Sorry, I couldn\'t connect to your profile. Try again later.', {
                inputHint: builder.InputHint.ignoringInput
            }).endConversation();
            return;
        }

        // Extract useful info from API response
        var bodyObj = JSON.parse(body);
        var name = bodyObj.first_name;
        var month = bodyObj.birth_month;
        var day = bodyObj.birth_day;

        if (!name || !month || !day) {
            // Name or birthday not available in response, present an error
            session.say('Failed to get name or birthday', 'Sorry, I couldn\'t get your name or birthday. Try adding more info to your profile.', {
                inputHint: builder.InputHint.ignoringInput
            }).endConversation();
            return;
        }

        var daysUntilBirthday = utils.daysUntil(month, day);

        // Format spoken text
        // Special responses if birthday is today or tomorrow
        var spokenText = 'Hi ' + name + '. There are ' + daysUntilBirthday + ' days until your birthday.';
        if (daysUntilBirthday === 0) {
            spokenText = 'Today is your birthday. Happy birthday, ' + name;
        } else if (daysUntilBirthday === 1) {
            spokenText = 'Hi ' + name + '. Tomorrow is your birthday.';
        }

        session.say(daysUntilBirthday + ' day(s) until your birthday', spokenText, {
            inputHint: builder.InputHint.ignoringInput
        }).endConversation();
    });
});