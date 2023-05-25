using System;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PKHeX.Core;
using System.Linq;
using System.Threading.Tasks;
using SysBot.Base;

namespace SysBot.Pokemon.Discord
{
    [Summary("Queues new Link Code trades")]
    public class TradeModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
    {
        private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;

        [Command("tradeList")]
        [Alias("tl")]
        [Summary("Prints the users in the trade queues.")]
        [RequireSudo]
        public async Task GetTradeListAsync()
        {
            string msg = Info.GetTradeList(PokeRoutineType.LinkTrade);
            var embed = new EmbedBuilder();
            embed.AddField(x =>
            {
                x.Name = "Pending Trades";
                x.Value = msg;
                x.IsInline = false;
            });
            await ReplyAsync("These are the users who are currently waiting:", embed: embed.Build()).ConfigureAwait(false);
        }

        [Command("trade")]
        [Alias("t")]
        [Summary("Makes the bot trade you the provided Pokémon file.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task TradeAsyncAttach([Summary("Trade Code")] int code)
        {
            var sig = Context.User.GetFavor();
            await TradeAsyncAttach(code, sig, Context.User).ConfigureAwait(false);
        }

        [Command("trade")]
        [Alias("t")]
        [Summary("Makes the bot trade you a Pokémon converted from the provided Showdown Set.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task TradeAsync([Summary("Trade Code")] int code, [Summary("Showdown Set")][Remainder] string content)
        {
            content = ReusableActions.StripCodeBlock(content);
            var set = new ShowdownSet(content);
            var template = AutoLegalityWrapper.GetTemplate(set);
            if (set.InvalidLines.Count != 0)
            {
                var msg = $"Unable to parse Showdown Set:\n{string.Join("\n", set.InvalidLines)}";
                await ReplyAsync(msg).ConfigureAwait(false);
                return;
            }

            try
            {
                var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
                var pkm = sav.GetLegal(template, out var result);
                var la = new LegalityAnalysis(pkm);
                var spec = GameInfo.Strings.Species[template.Species];
                pkm = EntityConverter.ConvertToType(pkm, typeof(T), out _) ?? pkm;
                if (pkm is not T pk || !la.Valid)
                {
                    var reason = result == "Timeout" ? $"That {spec} set took too long to generate." : result == "VersionMismatch" ? "Request refused: PKHeX and Auto-Legality Mod version mismatch." : $"I wasn't able to create a {spec} from that set.";
                    var imsg = $"Oops! {reason}";
                    if (result == "Failed")
                        imsg += $"\n{AutoLegalityWrapper.GetLegalizationHint(template, sav, pkm)}";
                    await ReplyAsync(imsg).ConfigureAwait(false);
                    return;
                }
                pk.ResetPartyStats();

                var sig = Context.User.GetFavor();
                await AddTradeToQueueAsync(code, Context.User.Username, pk, sig, Context.User).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogUtil.LogSafe(ex, nameof(TradeModule<T>));
                var msg = $"Oops! An unexpected problem happened with this Showdown Set:\n```{string.Join("\n", set.GetSetLines())}```";
                await ReplyAsync(msg).ConfigureAwait(false);
            }
        }

        [Command("trade")]
        [Alias("t")]
        [Summary("Makes the bot trade you a Pokémon converted from the provided Showdown Set.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task TradeAsync([Summary("Showdown Set")][Remainder] string content)
        {
            var code = Info.GetRandomTradeCode();
            await TradeAsync(code, content).ConfigureAwait(false);
        }

        [Command("trade")]
        [Alias("t")]
        [Summary("Makes the bot trade you the attached file.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task TradeAsyncAttach()
        {
            var code = Info.GetRandomTradeCode();
            await TradeAsyncAttach(code).ConfigureAwait(false);
        }

        // Item Trade - Extracted from [https://github.com/Koi-3088/ForkBot.NET]
        [Command("itemTrade")]
        [Alias("it", "item")]
        [Summary("Makes the bot trade you a Pokémon holding the requested item")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task ItemTrade([Remainder] string item)
        {
            var code = Info.GetRandomTradeCode();
            await ItemTrade(code, item).ConfigureAwait(false);
        }

        [Command("itemTrade")]
        [Alias("it", "item")]
        [Summary("Makes the bot trade you a Pokémon holding the requested item.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task ItemTrade([Summary("Trade Code")] int code, [Remainder] string item)
        {
            Species species = Info.Hub.Config.Distribution.ItemTradeSpecies is Species.None ? Species.Finizen : Info.Hub.Config.Distribution.ItemTradeSpecies;
            var set = new ShowdownSet($"{SpeciesName.GetSpeciesNameGeneration((ushort)species, 2, 8)} @ {item.Trim()}");
            var template = AutoLegalityWrapper.GetTemplate(set);
            var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
            var pkm = sav.GetLegal(template, out var result);
            pkm = EntityConverter.ConvertToType(pkm, typeof(T), out _) ?? pkm;
            if (pkm.HeldItem == 0)
            {
                await ReplyAsync($"{Context.User.Username}, the item you entered wasn't recognized.").ConfigureAwait(false);
                return;
            }

            var la = new LegalityAnalysis(pkm);
            if (await ItemTrade(Context, pkm is not T || !la.Valid, pkm, true).ConfigureAwait(false))
                return;

            if (pkm is not T pk || !la.Valid)
            {
                var reason = result == "Timeout" ? "That set took too long to generate." : "I wasn't able to create something from that.";
                var imsg = $"Oops! {reason} Here's my best attempt for that {species}!";
                await Context.Channel.SendPKMAsync(pkm, imsg).ConfigureAwait(false);
                return;
            }
            pk.ResetPartyStats();

            var sig = Context.User.GetFavor();
            await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, pk, PokeRoutineType.LinkTrade, PokeTradeType.Specific).ConfigureAwait(false);
        }
        public static Task<bool> ItemTrade(SocketCommandContext context, bool invalid, PKM pkm, bool itemTrade = false)
        {
            bool noItem = pkm.HeldItem == 0 && itemTrade;
            if (invalid || !ItemRestrictions.IsHeldItemAllowed(pkm) || noItem || (pkm.Nickname.ToLower() == "egg" && !Breeding.CanHatchAsEgg(pkm.Species)))
            {
                var msg = $"{(noItem ? $"{context.User.Username}, the item you entered wasn't recognized." : $"Oops! I wasn't able to create that {GameInfo.Strings.Species[pkm.Species]}.")}";
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        [Command("banTrade")]
        [Alias("bt")]
        [RequireSudo]
        public async Task BanTradeAsync([Summary("Online ID")] ulong nnid, string comment)
        {
            SysCordSettings.HubConfig.TradeAbuse.BannedIDs.AddIfNew(new[] { GetReference(nnid, comment) });
            await ReplyAsync("https://tenor.com/view/bane-no-banned-and-you-are-explode-gif-16047504").ConfigureAwait(false);
        }

        private RemoteControlAccess GetReference(ulong id, string comment) => new()
        {
            ID = id,
            Name = id.ToString(),
            Comment = $"Added by {Context.User.Username} on {DateTime.Now:yyyy.MM.dd-hh:mm:ss} ({comment})",
        };

        [Command("tradeUser")]
        [Alias("tu", "tradeOther")]
        [Summary("Makes the bot trade the mentioned user the attached file.")]
        [RequireSudo]
        public async Task TradeAsyncAttachUser([Summary("Trade Code")] int code, [Remainder]string _)
        {
            if (Context.Message.MentionedUsers.Count > 1)
            {
                await ReplyAsync("Too many mentions. Queue one user at a time.").ConfigureAwait(false);
                return;
            }

            if (Context.Message.MentionedUsers.Count == 0)
            {
                await ReplyAsync("A user must be mentioned in order to do this.").ConfigureAwait(false);
                return;
            }

            var usr = Context.Message.MentionedUsers.ElementAt(0);
            var sig = usr.GetFavor();
            await TradeAsyncAttach(code, sig, usr).ConfigureAwait(false);
        }

        [Command("tradeUser")]
        [Alias("tu", "tradeOther")]
        [Summary("Makes the bot trade the mentioned user the attached file.")]
        [RequireSudo]
        public async Task TradeAsyncAttachUser([Remainder] string _)
        {
            var code = Info.GetRandomTradeCode();
            await TradeAsyncAttachUser(code, _).ConfigureAwait(false);
        }

        private async Task TradeAsyncAttach(int code, RequestSignificance sig, SocketUser usr)
        {
            var attachment = Context.Message.Attachments.FirstOrDefault();
            if (attachment == default)
            {
                await ReplyAsync("No attachment provided!").ConfigureAwait(false);
                return;
            }

            var att = await NetUtil.DownloadPKMAsync(attachment).ConfigureAwait(false);
            var pk = GetRequest(att);
            if (pk == null)
            {
                await ReplyAsync("Attachment provided is not compatible with this module!").ConfigureAwait(false);
                return;
            }

            await AddTradeToQueueAsync(code, usr.Username, pk, sig, usr).ConfigureAwait(false);
        }

        private static T? GetRequest(Download<PKM> dl)
        {
            if (!dl.Success)
                return null;
            return dl.Data switch
            {
                null => null,
                T pk => pk,
                _ => EntityConverter.ConvertToType(dl.Data, typeof(T), out _) as T,
            };
        }

        private async Task AddTradeToQueueAsync(int code, string trainerName, T pk, RequestSignificance sig, SocketUser usr)
        {
            if (!pk.CanBeTraded())
            {
                await ReplyAsync("Provided Pokémon content is blocked from trading!").ConfigureAwait(false);
                return;
            }

            var la = new LegalityAnalysis(pk);
            if (!la.Valid)
            {
                await ReplyAsync($"{typeof(T).Name} attachment is not legal, and cannot be traded!").ConfigureAwait(false);
                return;
            }

            await QueueHelper<T>.AddToQueueAsync(Context, code, trainerName, sig, pk, PokeRoutineType.LinkTrade, PokeTradeType.Specific, usr).ConfigureAwait(false);
        }
    }
}
