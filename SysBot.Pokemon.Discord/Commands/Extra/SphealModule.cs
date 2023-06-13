using PKHeX.Core;
using Discord.Commands;
using System.Threading.Tasks;
using Discord;
using System;
using System.IO;
using System.Threading;

namespace SysBot.Pokemon.Discord
{
    [Summary("Custom Spheal Commands")]
    public class SphealModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
    {
        private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;

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
                return Task.FromResult(true);
            return Task.FromResult(false);
        }

        [Command("requestSV")]
        [Alias("reqsv", "rsv")]
        [Summary("Starts a Distribution Trade through Discord")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task ReqSV()
        {
            var code = Info.GetRandomTradeCode();
            var sig = Context.User.GetFavor();
            await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.DirectTrade, PokeTradeType.LinkSV).ConfigureAwait(false);
        }

        [Command("requestSV")]
        [Alias("reqsv", "rsv")]
        [Summary("Starts a Distribution Trade through Discord")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task ReqSV([Summary("Trade Code")] int code)
        {
            var sig = Context.User.GetFavor();
            await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.DirectTrade, PokeTradeType.LinkSV).ConfigureAwait(false);
        }

        [Command("DTList")]
        [Alias("dtl")]
        [Summary("List the users in the DirectTrade queue.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task GetDTListAsync()
        {
            string msg = Info.GetTradeList(PokeRoutineType.DirectTrade);
            var embed = new EmbedBuilder();
            embed.AddField(s =>
            {
                s.Name = "Pending Trades";
                s.Value = msg;
                s.IsInline = false;
            });
            await ReplyAsync("These are the users who are currently waiting:", embed: embed.Build()).ConfigureAwait(false);
        }

        [Command("requestSWSH")]
        [Alias("reqswsh", "rswsh")]
        [Summary("Starts a Distribution Trade through Discord")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task ReqSWSH()
        {
            var code = Info.GetRandomTradeCode();
            var sig = Context.User.GetFavor();
            await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.DirectTrade, PokeTradeType.LinkSWSH).ConfigureAwait(false);
        }

        [Command("requestSWSH")]
        [Alias("reqswsh", "rswsh")]
        [Summary("Starts a Distribution Trade through Discord")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task ReqSWSH([Summary("Trade Code")] int code)
        {
            var sig = Context.User.GetFavor();
            await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.DirectTrade, PokeTradeType.LinkSWSH).ConfigureAwait(false);
        }

        [Command("requestLA")]
        [Alias("reqla", "rla")]
        [Summary("Starts a Distribution Trade through Discord")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task ReqLA()
        {
            var code = Info.GetRandomTradeCode();
            var sig = Context.User.GetFavor();
            await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.DirectTrade, PokeTradeType.LinkLA).ConfigureAwait(false);
        }

        [Command("requestLA")]
        [Alias("reqla", "rla")]
        [Summary("Starts a Distribution Trade through Discord")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task ReqLA([Summary("Trade Code")] int code)
        {
            var sig = Context.User.GetFavor();
            await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.DirectTrade, PokeTradeType.LinkLA).ConfigureAwait(false);
        }

        [Command("requestBDSP")]
        [Alias("reqbdsp", "rbdsp")]
        [Summary("Starts a Distribution Trade through Discord")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task ReqBDSP()
        {
            var code = Info.GetRandomTradeCode();
            var sig = Context.User.GetFavor();
            await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.DirectTrade, PokeTradeType.LinkBDSP).ConfigureAwait(false);
        }

        [Command("requestBDSP")]
        [Alias("reqbdsp", "rbdsp")]
        [Summary("Starts a Distribution Trade through Discord")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task ReqBDSP([Summary("Trade Code")] int code)
        {
            var sig = Context.User.GetFavor();
            await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.DirectTrade, PokeTradeType.LinkBDSP).ConfigureAwait(false);
        }

        [Command("cooldown")]
        [Summary("Changes cooldown in minutes.")]
        [RequireSudo]
        public async Task UpdateCooldown([Remainder] string input)
        {
            bool res = uint.TryParse(input, out var cooldown);
            if (res)
            {
                SysCordSettings.HubConfig.TradeAbuse.TradeCooldown = cooldown;
                SysCordSettings.HubConfig.TradeAbuse.CooldownUpdate = $"{DateTime.Now:yyyy.MM.dd - HH:mm:ss}";
                await ReplyAsync($"Cooldown has been updated to {cooldown} minutes.").ConfigureAwait(false);
            }
            else
            {
                await ReplyAsync("Please enter a valid number of minutes.").ConfigureAwait(false);
            }
        }

        [Command("checkcd")]
        [Summary("Allows users to check their current cooldown using NID")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task CooldownLeft([Remainder] string input)
        {
            bool isDistribution = true;
            var list = isDistribution ? PokeRoutineExecutorBase.PreviousUsersDistribution : PokeRoutineExecutorBase.PreviousUsers;
            var cooldown = list.TryGetPrevious(ulong.Parse(input));
            if (cooldown != null)
            {
                string trainerName = cooldown.ToString().Substring(21, cooldown.ToString().IndexOf('=', cooldown.ToString().IndexOf('=') + 1) - 31);
                var delta = DateTime.Now - cooldown.Time;
                double ddelta = delta.TotalMinutes;
                if (ddelta.CompareTo((double)SysCordSettings.HubConfig.TradeAbuse.TradeCooldown) < 1)
                    await ReplyAsync($"{trainerName} your last encounter with the bot was {delta.TotalMinutes:F1} mins ago. The trade cooldown is {SysCordSettings.HubConfig.TradeAbuse.TradeCooldown} mins.").ConfigureAwait(false);
                else
                    await ReplyAsync($"{trainerName} your cooldown is up. You may trade again.").ConfigureAwait(false);
            }
            else
                await ReplyAsync($"User has not traded with the bot recently.").ConfigureAwait(false);
            if (!Context.IsPrivate)
                await Context.Message.DeleteAsync(RequestOptions.Default).ConfigureAwait(false);
        }

        [Command("addwl")]
        [Summary("Adds NID to whitelist for cooldown skipping. Format: <prefix>addwl [NID] [IGN] [Duration in hours](optional)")]
        [RequireSudo]
        // Adds a <NID> to cooldown whitelist.  Syntax: <prefix>addwl <NID>, <IGN>, <Duration in hours>
        // Do not provide last parameter for non-expiring whitelist.
        public async Task AddWhiteList([Summary("Whitelist user from cooldowns. Format: <NID>, <OT Name>, <Duration>:<Day/Hour>, <Reason for whitelisting>")][Remainder] string input)
        {
            string msg = "";
            var wlParams = input.Split(", ", 4);
            DateTime wlExpires = DateTime.Now;
            RemoteControlAccess wlRef = new ();

            if (wlParams.Length <= 2)
            {
                await ReplyAsync(Format.Code($"Please enter the command with the correct syntax. Format: <NID>, <OT Name>, <Duration>:<Day/Hour>, <Reason for whitelisting> (Last two are optional but BOTH must be given if one is)")).ConfigureAwait(false);
                return;
            }
            else if (wlParams.Length == 4)
            {
                var durParams = wlParams[2].Split(":", 2);
                durParams[1] = durParams[1].ToLower();
                bool isValidDur = int.TryParse(durParams[0], out int duration);
                if (!isValidDur)
                {
                    msg += $"{durParams[0]} is an invalid number. Defaulting to no expiration\r\n";
                    wlExpires = DateTime.MaxValue;
                }
                else
                {
                    wlExpires = durParams[1] switch
                    {
                        "days" or "day" => wlExpires.AddDays(duration),
                        "hours" or "hour" => wlExpires.AddHours(duration),
                        _ => wlExpires.AddHours(duration),
                    };
                }
                wlRef = GetReference(wlParams[1], Convert.ToUInt64(wlParams[0]), wlExpires, wlParams[3]);
            }
            else
            {
                wlExpires = DateTime.MaxValue;
                wlRef = GetReference(wlParams[1], Convert.ToUInt64(wlParams[0]), wlExpires, wlParams[2]);
            }
            SysCordSettings.HubConfig.TradeAbuse.WhiteListedIDs.AddIfNew(new[] { wlRef });
            msg += $"{wlParams[1]}({wlParams[0]}) added to the whitelist";
            await ReplyAsync(Format.Code(msg)).ConfigureAwait(false);
        }
        private RemoteControlAccess GetReference(string name, ulong id, DateTime expiration, string comment) => new()
        {
            ID = id,
            Name = name,
            Expiration = expiration,
            Comment = $"{comment} - Added by {Context.User.Username} on {DateTime.Now:yyyy.MM.dd-hh:mm:ss}",
        };

        // Peek Command - Extracted from [https://github.com/Koi-3088/ForkBot.NET]
        [Command("peek")]
        [Summary("Take and send a screenshot from the specified Switch.")]
        [RequireSudo]
        public async Task Peek(string address)
        {
            var source = new CancellationTokenSource();
            var token = source.Token;

            var bot = SysCord<T>.Runner.GetBot(address);
            if (bot == null)
            {
                await ReplyAsync($"No bot found with the specified address ({address}).").ConfigureAwait(false);
                return;
            }

            var c = bot.Bot.Connection;
            var bytes = await c.PixelPeek(token).ConfigureAwait(false);
            if (bytes.Length == 1)
            {
                await ReplyAsync($"Failed to take a screenshot for bot at {address}. Is the bot connected?").ConfigureAwait(false);
                return;
            }
            MemoryStream ms = new(bytes);

            var img = "SphealCheck.jpg";
            var embed = new EmbedBuilder { ImageUrl = $"attachment://{img}", Color = Color.Blue }.WithFooter(new EmbedFooterBuilder { Text = $"Captured image from bot at address {address}." });
            await Context.Channel.SendFileAsync(ms, img, "", false, embed: embed.Build());
        }
        [Command("spheal")]
        [Summary("Sends random Spheals")]
        public async Task SphealAsync()
        {
            var msg = "Placeholder";
            Random rndmsg = new();
            int num = rndmsg.Next(1, 5);
            switch (num)
            {
                case 1:
                    msg = $"https://tenor.com/view/tess-spheal-gif-22641311";
                    break;
                case 2:
                    msg = $"https://tenor.com/view/spheal-pokemon-clapping-gif-25991342";
                    break;
                case 3:
                    msg = $"https://tenor.com/view/swoshi-swsh-spheal-dlc-pokemon-gif-18917062";
                    break;
                case 4:
                    msg = $"https://tenor.com/view/spheal-pokemon-soupokailloux-seal-gif-24173644";
                    break;
                case 5:
                    msg = $"https://tenor.com/view/pokemon-spheal-gif-26674053";
                    break;
                case 6:
                    msg = $"https://tenor.com/view/spheal-wake-up-gif-19887467";
                    break;
                case 7:
                    msg = $"https://tenor.com/view/on-my-way-pokemon-spheal-sphere-seal-gif-15438411";
                    break;
                case 8:
                    msg = $"https://tenor.com/view/pokemon-spheal-rolling-yolo-gif-24805701";
                    break;
            }
            await ReplyAsync(msg).ConfigureAwait(false);
        }
    }
}