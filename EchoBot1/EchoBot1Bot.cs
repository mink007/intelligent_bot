// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;


namespace EchoBot1
{
	/// <summary>
	/// Represents a bot that processes incoming activities.
	/// For each user interaction, an instance of this class is created and the OnTurnAsync method is called.
	/// This is a Transient lifetime service.  Transient lifetime services are created
	/// each time they're requested. For each Activity received, a new instance of this
	/// class is created. Objects that are expensive to construct, or have a lifetime
	/// beyond the single turn, should be carefully managed.
	/// For example, the <see cref="MemoryStorage"/> object and associated
	/// <see cref="IStatePropertyAccessor{T}"/> object are created with a singleton lifetime.
	/// </summary>
	/// <seealso cref="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-2.1"/>
	/// 

	public class WelcomeUserState
	{
		/// <summary>
		/// Gets or sets whether the user has been welcomed in the conversation.
		/// </summary>
		/// <value>The user has been welcomed in the conversation.</value>
		public bool DidBotWelcomeUser { get; set; } = false;
	}

	public class WelcomeUserStateAccessors
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="WelcomeUserStateAccessors"/> class.
		/// Contains the <see cref="UserState"/> and associated <see cref="IStatePropertyAccessor{T}"/>.
		/// </summary>
		/// <param name="userState">The state object that stores the counter.</param>
		public WelcomeUserStateAccessors( UserState userState )
		{
			UserState = userState ?? throw new System.ArgumentNullException( nameof( userState ) );
		}

		/// <summary>
		/// Gets the <see cref="IStatePropertyAccessor{T}"/> name used for the <see cref="BotBuilderSamples.WelcomeUserState"/> accessor.
		/// </summary>
		/// <remarks>Accessors require a unique name.</remarks>
		/// <value>The accessor name for the WelcomeUser state.</value>
		public static string WelcomeUserName { get; } = $"{nameof( WelcomeUserStateAccessors )}.WelcomeUserState";

		/// <summary>
		/// Gets or sets the <see cref="IStatePropertyAccessor{T}"/> for DidBotWelcome.
		/// </summary>
		/// <value>
		/// The accessor stores if the bot has welcomed the user or not.
		/// </value>
		public IStatePropertyAccessor<WelcomeUserState> WelcomeUserState { get; set; }

		/// <summary>
		/// Gets the <see cref="UserState"/> object for the conversation.
		/// </summary>
		/// <value>The <see cref="UserState"/> object.</value>
		public UserState UserState { get; }
	}

	public class EchoBot1Bot : IBot
	{
		// Messages sent to the user.
		private const string WelcomeMessage = @"Hey there! I'm the ASH Music Festival Bot. I'm here to guide you around the festival!";
		private const string PatternMessage = @"How would you like to explore the event?";

		private readonly EchoBot1Accessors _accessors;
		private readonly ILogger _logger;

		/// <summary>
		/// Initializes a new instance of the class.
		/// </summary>
		/// <param name="conversationState">The managed conversation state.</param>
		/// <param name="loggerFactory">A <see cref="ILoggerFactory"/> that is hooked to the Azure App Service provider.</param>
		/// <seealso cref="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-2.1#windows-eventlog-provider"/>
		public EchoBot1Bot(ConversationState conversationState, ILoggerFactory loggerFactory)
		{
			if (conversationState == null)
			{
				throw new System.ArgumentNullException(nameof(conversationState));
			}

			if (loggerFactory == null)
			{
				throw new System.ArgumentNullException(nameof(loggerFactory));
			}

			_accessors = new EchoBot1Accessors(conversationState)
			{
				CounterState = conversationState.CreateProperty<CounterState>(EchoBot1Accessors.CounterStateName),
				WelcomeUserState = conversationState.CreateProperty<WelcomeUserState>( EchoBot1Accessors.WelcomeUserName ),
			};

			_logger = loggerFactory.CreateLogger<EchoBot1Bot>();
			_logger.LogTrace("Turn start.");
		}

		

		/// <summary>
		/// Every conversation turn for our Echo Bot will call this method.
		/// There are no dialogs used, since it's "single turn" processing, meaning a single
		/// request and response.
		/// </summary>
		/// <param name="turnContext">A <see cref="ITurnContext"/> containing all the data needed
		/// for processing this conversation turn. </param>
		/// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects
		/// or threads to receive notice of cancellation.</param>
		/// <returns>A <see cref="Task"/> that represents the work queued to execute.</returns>
		/// <seealso cref="BotStateSet"/>
		/// <seealso cref="ConversationState"/>
		/// <seealso cref="IMiddleware"/>
		public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
		{
			// use state accessor to extract the didBotWelcomeUser flag
			var didBotWelcomeUser = await _accessors.WelcomeUserState.GetAsync( turnContext, () => new WelcomeUserState() );

			// Handle Message activity type, which is the main activity type for shown within a conversational interface
			// Message activities may contain text, speech, interactive cards, and binary or unknown attachments.
			// see https://aka.ms/about-bot-activity-message to learn more about the message and other activity types
			if( turnContext.Activity.Type == ActivityTypes.ConversationUpdate )
			{
				if( turnContext.Activity.MembersAdded != null )
				{
					// Iterate over all new members added to the conversation
					foreach( var member in turnContext.Activity.MembersAdded )
					{
						// Greet anyone that was not the target (recipient) of this message
						// the 'bot' is the recipient for events from the channel,
						// turnContext.Activity.MembersAdded == turnContext.Activity.Recipient.Id indicates the
						// bot was added to the conversation.
						if( member.Id != turnContext.Activity.Recipient.Id )
						{
							await turnContext.SendActivityAsync( WelcomeMessage, cancellationToken: cancellationToken );
							await DisplayOptionsAsync( turnContext, cancellationToken );
						}
					}
				}
			}
			else if (turnContext.Activity.Type == ActivityTypes.Message)
			{
				// Take the input from the user and create the appropriate response.
				var reply = ProcessInput( turnContext );

				// Respond to the user.
				await turnContext.SendActivityAsync( reply, cancellationToken );

				await DisplayOptionsAsync( turnContext, cancellationToken );
			}
			else
			{
				await turnContext.SendActivityAsync($"{turnContext.Activity.Type} event detected");
			}

			// save any state changes made to your state objects.
			await _accessors.ConversationState.SaveChangesAsync( turnContext );
		}

		/// <summary>
		///  Displays a <see cref="HeroCard"/> with options for the user to select.
		/// </summary>
		/// <param name="turnContext">Provides the <see cref="ITurnContext"/> for the turn of the bot.</param>
		/// <param name="cancellationToken" >(Optional) A <see cref="CancellationToken"/> that can be used by other objects
		/// or threads to receive notice of cancellation.</param>
		/// <returns>A <see cref="Task"/> representing the operation result of the operation.</returns>
		private static async Task DisplayOptionsAsync( ITurnContext turnContext, CancellationToken cancellationToken )
		{
			var reply = turnContext.Activity.CreateReply();

			// Create a HeroCard with options for the user to interact with the bot.
			var card = new HeroCard
			{
				Text = PatternMessage,
				Buttons = new List<CardAction>
				{
                    // Note that some channels require different values to be used in order to get buttons to display text.
                    // In this code the emulator is accounted for with the 'title' parameter, but in other channels you may
                    // need to provide a value for other parameters like 'text' or 'displayText'.
                    new CardAction(ActionTypes.ImBack, title: "FAQs", value: "FAQs"),
					new CardAction(ActionTypes.ImBack, title: "Band Search", value: "Band Search"),
					new CardAction(ActionTypes.ImBack, title: "Navigate", value: "Navigate"),
				},
			};

			// Add the card to our reply.
			reply.Attachments = new List<Attachment>() { card.ToAttachment() };

			await turnContext.SendActivityAsync( reply, cancellationToken );
		}

		/// <summary>
		/// Given the input from the message <see cref="Activity"/>, create the response.
		/// </summary>
		/// <param name="turnContext">Provides the <see cref="ITurnContext"/> for the turn of the bot.</param>
		/// <returns>An <see cref="Activity"/> to send as a response.</returns>
		private static Activity ProcessInput( ITurnContext turnContext )
		{
			var activity = turnContext.Activity;
			var reply = activity.CreateReply();

			string buttonText;

			if( activity.Text.ToLower().StartsWith( "faq" ) )
			{
				buttonText = "FAQ";
			}
			else if( activity.Text.ToLower().StartsWith( "band" ) )
			{
				buttonText = "Band Search";
			}
			else if( activity.Text.ToLower().StartsWith( "nav" ) )
			{
				buttonText = "Navigate";
			}
			else
			{
				// The user did not enter input that this bot was built to handle.
				reply.Text = "Your input was not recognized.";
				return reply;
			}

			reply.Text = String.Format( "You clicked the {0} button!", buttonText );
			return reply;
		}		
	}


}
