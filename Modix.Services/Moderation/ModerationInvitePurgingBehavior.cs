using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Discord;
using Discord.WebSocket;

using Modix.Data.Models.Core;
using Modix.Services.Core;
using Serilog;

namespace Modix.Services.Moderation
{
    /// <summary>
    /// Implements a behavior that automatically deletes invite links posted by select users.
    /// </summary>
    public class ModerationInvitePurgingBehavior : BehaviorBase
    {
        // TODO: Abstract DiscordSocketClient to IDiscordSocketClient, or something, to make this testable
        /// <summary>
        /// Constructs a new <see cref="ModerationInvitePurgingBehavior"/> object, with the given injected dependencies.
        /// See <see cref="BehaviorBase"/> for more details.
        /// </summary>
        /// <param name="discordClient">The value to use for <see cref="DiscordClient"/>.</param>
        /// <param name="serviceProvider">See <see cref="BehaviorBase"/>.</param>
        public ModerationInvitePurgingBehavior(DiscordSocketClient discordClient, IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
            DiscordClient = discordClient;
        }

        /// <inheritdoc />
        internal protected override Task OnStartingAsync()
        {
            DiscordClient.MessageReceived += OnDiscordClientMessageReceived;
            DiscordClient.MessageUpdated += OnDiscordClientMessageUpdated;

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        internal protected override Task OnStoppedAsync()
        {
            DiscordClient.MessageReceived -= OnDiscordClientMessageReceived;
            DiscordClient.MessageUpdated -= OnDiscordClientMessageUpdated;

            return Task.CompletedTask;
        }

        // TODO: Abstract DiscordSocketClient to IDiscordSocketClient, or something, to make this testable
        /// <summary>
        /// A <see cref="DiscordSocketClient"/> for interacting with, and receiving events from, the Discord API.
        /// </summary>
        internal protected DiscordSocketClient DiscordClient { get; }

        private Task OnDiscordClientMessageReceived(IMessage message)
            => TryPurgeInviteLink(message);

        private Task OnDiscordClientMessageUpdated(Cacheable<IMessage, ulong> oldMessage, IMessage newMessage, ISocketMessageChannel channel)
            => TryPurgeInviteLink(newMessage);

        /// <summary>
        /// Determines whether or not to skip a message event, based on unmoderated channel designations
        /// </summary>
        /// <param name="guild">The guild designations should be looked up for</param>
        /// <param name="channel">The channel designations should be looked up for</param>
        /// <returns>True if the channel is designated as Unmoderated, false if not</returns>
        private async Task<bool> ChannelIsUnmoderated(IGuild guild, IMessageChannel channel)
        {
            var result = false;

            await SelfExecuteRequest<IDesignatedChannelService>(async designatedChannelService =>
            {
                result = await designatedChannelService.ChannelHasDesignationAsync(guild, channel, DesignatedChannelType.Unmoderated);
            });

            return result;
        }

        private async Task TryPurgeInviteLink(IMessage message)
        {
            if
            (
                !(message.Author is IGuildUser author) ||
                !(message.Channel is IGuildChannel guildChannel) ||
                !(message.Channel is IMessageChannel msgChannel)
            )
            {
                Log.Debug("Message {MessageId} was not in an IGuildChannel & IMessageChannel, or Author {Author} was not an IGuildUser", 
                    message.Id, message.Author.Id);
                return;
            }

            if (author.Id == DiscordClient.CurrentUser.Id)
            {
                Log.Debug("Message {MessageId} was skipped because the author was Modix", message.Id);
                return;
            }

            if (await ChannelIsUnmoderated(guildChannel.Guild, msgChannel))
            {
                Log.Debug("Message {MessageId} was skipped because the channel {Channel} was designated as Unmoderated", 
                    message.Id, msgChannel.Id);
                return;
            }

            var matches = _inviteLinkMatcher.Matches(message.Content);
            if (!matches.Any())
            {
                Log.Debug("Message {MessageId} was skipped because the content did not contain an invite link: \"{Content}\"",
                    message.Id, message.Content);
                return;
            }

            // TODO: Booooooo for non-abstractable dependencies
            if (author.Guild is SocketGuild socketGuild)
            {
                // Allow invites to the guild in which the message was posted
                var newInvites = matches
                    .Select(x => x.Value)
                    .Except((await socketGuild
                        .GetInvitesAsync())
                        .Select(x => x.Url));

                if (!newInvites.Any())
                {
                    Log.Debug("Message {MessageId} was skipped because the invite was to this server", message.Id);
                    return;
                }
            }

            await SelfExecuteRequest<IAuthorizationService, IModerationService>(async (authorizationService, moderationService) =>
            {
                if (await authorizationService.HasClaimsAsync(author, AuthorizationClaim.PostInviteLink))
                {
                    Log.Debug("Message {MessageId} was skipped because the author {Author} has the PostInviteLink claim",
                        message.Id, message.Author.Id);
                    return;
                }

                await moderationService.DeleteMessageAsync(message, "Unauthorized Invite Link");

                await msgChannel.SendMessageAsync($"Sorry {author.Mention} your invite link has been removed - please don't post links to other guilds");
            });
        }

        private static readonly Regex _inviteLinkMatcher
            = new Regex(
                pattern: @"(https?:\/\/)?(www\.)?(discord\.(gg|io|me|li)|discordapp\.com\/invite)\/.+[a-z]",
                options: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                matchTimeout: TimeSpan.FromSeconds(2));
    }
}
