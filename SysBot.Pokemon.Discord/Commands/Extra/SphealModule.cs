using PKHeX.Core;
using Discord.Commands;
using System.Threading.Tasks;
using Discord;
using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;

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
            Species species = Info.Hub.Config.CustomSwaps.ItemTradeSpecies;
            var set = new ShowdownSet($"{SpeciesName.GetSpeciesNameGeneration((ushort)species, 2, 8)} @ {item.Trim()}");
            var template = AutoLegalityWrapper.GetTemplate(set);
            var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
            var pkm = sav.GetLegal(template, out var result);
            pkm = EntityConverter.ConvertToType(pkm, typeof(T), out _) ?? pkm;
            if (pkm.HeldItem == 0)
            {
                await ReplyAsync($"**{Context.User.Username}**, {item} is not a valid item").ConfigureAwait(false);
                return;
            }

            var la = new LegalityAnalysis(pkm);
            if (await ItemTrade(pkm is not T || !la.Valid, pkm, true).ConfigureAwait(false))
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
        public static Task<bool> ItemTrade(bool invalid, PKM pkm, bool itemTrade = false)
        {
            bool noItem = pkm.HeldItem == 0 && itemTrade;
            if (invalid || !ItemRestrictions.IsHeldItemAllowed(pkm) || noItem || (pkm.Nickname.ToLower() == "egg" && !Breeding.CanHatchAsEgg(pkm.Species)))
                return Task.FromResult(true);
            return Task.FromResult(false);
        }

        [Command("directTrade")]
        [Alias("drt", "rsv")]
        [Summary("Starts a Distribution Trade through Discord")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task DRT()
        {
            var code = Info.GetRandomTradeCode();
            var sig = Context.User.GetFavor();
            var config = SysCordSettings.HubConfig.CustomSwaps;
            var me = SysCord<T>.Runner;
            string botversion;
            if (me is not null)
            {
                botversion = me.ToString()!.Substring(46, 3);
                switch (botversion)
                {
                    case "PK9":
                        await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.DirectTrade, PokeTradeType.LinkSV).ConfigureAwait(false);
                        break;
                    case "PK8":
                        await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.DirectTrade, PokeTradeType.LinkSWSH).ConfigureAwait(false);
                        break;
                    case "PA8":
                        await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.DirectTrade, PokeTradeType.LinkLA).ConfigureAwait(false);
                        break;
                    case "PB8":
                        if (config.EnableBDSPTrades)
                            await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.DirectTrade, PokeTradeType.LinkBDSP).ConfigureAwait(false);
                        else
                        {
                            var msg = "# This command is disabled for BDSP";
                            EmbedBuilder? embed = Sphealcl.EmbedGeneric(msg, "Disabled Command");
                            await ReplyAsync("", false, embed: embed.Build()).ConfigureAwait(false);
                        }
                        break;
                }
            }
        }

        [Command("directTrade")]
        [Alias("drt", "rsv")]
        [Summary("Starts a Distribution Trade through Discord")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task DRT([Summary("Trade Code")] int code)
        {
            var sig = Context.User.GetFavor();
            var me = SysCord<T>.Runner;
            var config = SysCordSettings.HubConfig.CustomSwaps;
            string botversion;
            if (me is not null)
            {
                botversion = me.ToString()!.Substring(46, 3);
                switch (botversion)
                {
                    case "PK9":
                        await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.DirectTrade, PokeTradeType.LinkSV).ConfigureAwait(false);
                        break;
                    case "PK8":
                        await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.DirectTrade, PokeTradeType.LinkSWSH).ConfigureAwait(false);
                        break;
                    case "PA8":
                        await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.DirectTrade, PokeTradeType.LinkLA).ConfigureAwait(false);
                        break;
                    case "PB8":
                        if (config.EnableBDSPTrades)
                            await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.DirectTrade, PokeTradeType.LinkBDSP).ConfigureAwait(false);
                        else
                        {
                            var msg = "# This command is disabled for BDSP";
                            EmbedBuilder? embed = Sphealcl.EmbedGeneric(msg, "Disabled Command");
                            await ReplyAsync("", false, embed: embed.Build()).ConfigureAwait(false);
                        }
                        break;
                }
            }
        }

        [Command("DTList")]
        [Alias("dtl")]
        [Summary("List the users in the DirectTrade queue.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task GetDTListAsync()
        {
            string msg = Info.GetTradeList(PokeRoutineType.DirectTrade);

            EmbedBuilder? embed = Sphealcl.EmbedGeneric(msg, "Queue List");
            await ReplyAsync("", false, embed: embed.Build()).ConfigureAwait(false);
        }

        [Command("mysteryEgg")]
        [Alias("myst", "rme")]
        [Summary("Request a Mystery Egg through Discord - Pool from distribution folder")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task Myst()
        {
            var code = Info.GetRandomTradeCode();
            var sig = Context.User.GetFavor();
            var me = SysCord<T>.Runner;
            var config = SysCordSettings.HubConfig.CustomSwaps;
            string botversion;
            if (me is not null)
            {
                botversion = me.ToString()!.Substring(46, 3);
                switch (botversion)
                {
                    case "PK9":
                        await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.DirectTrade, PokeTradeType.EggSV).ConfigureAwait(false);
                        break;
                    case "PK8":
                        await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.DirectTrade, PokeTradeType.EggSWSH).ConfigureAwait(false);
                        break;
                    case "PA8":
                        await ReplyAsync($"PLA has no eggs. Nice try.").ConfigureAwait(false);
                        break;
                    case "PB8":
                        if (config.EnableBDSPTrades)
                            await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.DirectTrade, PokeTradeType.LinkBDSP).ConfigureAwait(false);
                        else
                        {
                            var msg = "# This command is disabled for BDSP";
                            EmbedBuilder? embed = Sphealcl.EmbedGeneric(msg, "Disabled Command");
                            await ReplyAsync("", false, embed: embed.Build()).ConfigureAwait(false);
                        }
                        break;
                }
            }
        }

        [Command("mysteryEgg")]
        [Alias("myst", "rme")]
        [Summary("Request a Mystery Egg through Discord - Pool from distribution folder")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task MRE([Summary("Trade Code")] int code)
        {
            var sig = Context.User.GetFavor();
            var me = SysCord<T>.Runner;
            var config = SysCordSettings.HubConfig.CustomSwaps;
            string botversion;
            if (me is not null)
            {
                botversion = me.ToString()!.Substring(46, 3);
                switch (botversion)
                {
                    case "PK9":
                        await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.DirectTrade, PokeTradeType.EggSV).ConfigureAwait(false);
                        break;
                    case "PK8":
                        await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.DirectTrade, PokeTradeType.EggSWSH).ConfigureAwait(false);
                        break;
                    case "PA8":
                        await ReplyAsync($"PLA has no eggs. Nice try.").ConfigureAwait(false);
                        break;
                    case "PB8":
                        if (config.EnableBDSPTrades)
                            await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.DirectTrade, PokeTradeType.LinkBDSP).ConfigureAwait(false);
                        else
                        {
                            var msg = "# This command is disabled for BDSP";
                            EmbedBuilder? embed = Sphealcl.EmbedGeneric(msg, "Disabled Command");
                            await ReplyAsync("", false, embed: embed.Build()).ConfigureAwait(false);
                        }
                        break;
                }
            }
        }

        [Command("checkgame")]
        [Alias("game", "cg", "currentgame", "whatgame")]
        [Summary("What game is currently running?")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task CheckGame()
        {
            var me = SysCord<T>.Runner;
            var custom = SysCordSettings.HubConfig.CustomEmbed;
            var cd = SysCordSettings.HubConfig.TradeAbuse.TradeCooldown;
            var p = SysCordSettings.Settings.CommandPrefix;
            var cooldown = $"Cooldown: **{cd}** mins";
            if (cd == 0)
                cooldown = "There is no Cooldown";
            
            string botversion = "";
            string gameicon = "";

            if (me is not null)
                botversion = me.ToString()!.Substring(46, 3);
            var gamever = botversion switch
            {
                "PK9" => "Scarlet & Violet",
                "PK8" => "Sword & Shield",
                "PA8" => "Legends Arceus",
                "PB8" => "Brilliant Diamond & Shining Pearl",
                _ => "Let's Go Pikachu & Eevee",
            };
            if (custom.CustomEmoji)
                gameicon = botversion switch
                {
                    "PK9" => custom.TEGameIconSV,
                    "PK8" => custom.TEGameIconSWSH,
                    "PA8" => custom.TEGameIconPLA,
                    "PB8" => custom.TEGameIconBDSP,
                    _ => ""
                };

            var msg = $"### Current Game: {gameicon}**{gamever}**\n";
            msg += $"{cooldown}\n\n";
            msg += $"**Commands**:\r\n{p}**rsv**, {p}**rme**, {p}**t**, {p}**it**, {p}**tc**, {p}**dump**,\r\n{p}**clone**, {p}**checkcd**, {p}**dt**l, {p}**spf**";

            EmbedBuilder? embed = Sphealcl.EmbedGeneric(msg, "Game Status");
            await ReplyAsync("", false, embed: embed.Build()).ConfigureAwait(false);
        }

        [Command("cooldown")]
        [Alias("cd")]
        [Summary("Changes cooldown in minutes.")]
        [RequireSudo]
        public async Task UpdateCooldown([Remainder] string input)
        {
            bool res = uint.TryParse(input, out var cooldown);
            if (res)
            {
                SysCordSettings.HubConfig.TradeAbuse.TradeCooldown = cooldown;
                var msg = $"Cooldown has been updated to **{cooldown}** minutes";

                EmbedBuilder? embed = Sphealcl.EmbedGeneric(msg, "Cooldown Update");
                await ReplyAsync("", false, embed: embed.Build()).ConfigureAwait(false);
            }
            else
                await ReplyAsync("`Please enter a valid number of minutes.`").ConfigureAwait(false);
        }

        [Command("checkcd")]
        [Alias("ccd")]
        [Summary("Allows users to check their current cooldown using NID")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task CooldownLeft([Remainder] string input)
        {
            var distrocd = PokeRoutineExecutorBase.PreviousUsersDistribution.TryGetPreviousNID(ulong.Parse(input));
            var othercd = PokeRoutineExecutorBase.PreviousUsers.TryGetPreviousNID(ulong.Parse(input));
            var cd = SysCordSettings.HubConfig.TradeAbuse.TradeCooldown;
            string trainerName;
            TimeSpan delta;
            if (distrocd != null || othercd != null)
            {
                if (distrocd != null)
                {
                    trainerName = distrocd.ToString().Substring(21, distrocd.ToString().IndexOf('=', distrocd.ToString().IndexOf('=') + 1) - 31);
                    delta = DateTime.Now - distrocd.Time;
                }
                else
                {
                    trainerName = othercd.ToString().Substring(21, othercd.ToString().IndexOf('=', othercd.ToString().IndexOf('=') + 1) - 31);
                    delta = DateTime.Now - othercd.Time;
                }
                var wait = TimeSpan.FromMinutes(cd) - delta;
                double ddelta = delta.TotalMinutes;
                if (ddelta.CompareTo((double)cd) < 1)
                {
                    EmbedBuilder? embed = Sphealcl.EmbedCDMessage2(cd, $"{trainerName} your last encounter with the bot was {delta.TotalMinutes:F1} mins ago.\nTime left: {wait.TotalMinutes:F1} mins.", "Cooldown Checker");
                    await ReplyAsync("", false, embed: embed.Build()).ConfigureAwait(false);
                }
                else
                {
                    EmbedBuilder? embed = Sphealcl.EmbedCDMessage2(cd, $"{trainerName} your cooldown is up. You may trade again.", "Cooldown Checker");
                    await ReplyAsync("", false, embed: embed.Build()).ConfigureAwait(false);
                }
            }
            else
            {
                EmbedBuilder? embed = Sphealcl.EmbedCDMessage2(cd, $"User has not traded with the bot recently.", "Cooldown Checker");
                await ReplyAsync("", false, embed: embed.Build()).ConfigureAwait(false);
            }
            if (!Context.IsPrivate)
                await Context.Message.DeleteAsync(RequestOptions.Default).ConfigureAwait(false);
        }

        [Command("addwl")]
        [Alias("wladd", "whitelist")]
        [Summary("Adds NID to whitelist for cooldown skipping. Format: <prefix>addwl [NID] [IGN] [Duration in hours](optional)")]
        [RequireSudo]
        // Adds a <NID> to cooldown whitelist.  Syntax: <prefix>addwl <NID>, <IGN>, <Duration in hours>
        // Do not provide last parameter for non-expiring whitelist.
        public async Task AddWhiteList([Summary("Whitelist user from cooldowns. Format: <NID>, <OT Name>, <Duration>:<Day/Hour>, <Reason for whitelisting>")][Remainder] string input)
        {
            string msg = "";
            var wlParams = input.Split(", ", 4);
            DateTime wlExpires = DateTime.Now;
            RemoteControlAccess wlRef = new();

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
            msg += $"{wlParams[1]} has been added to the whitelist\nExpires: {wlExpires}";
            await ReplyAsync(Format.Code(msg, "cs")).ConfigureAwait(false);
            if (!Context.IsPrivate)
                await Context.Message.DeleteAsync(RequestOptions.Default).ConfigureAwait(false);
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
                await ReplyAsync($"```No bot found with the specified address ({address}).```").ConfigureAwait(false);
                return;
            }

            var c = bot.Bot.Connection;
            var bytes = await c.PixelPeek(token).ConfigureAwait(false);
            if (bytes.Length == 1)
            {
                await ReplyAsync($"```Failed to take a screenshot for bot at {address}. Is the bot connected?```").ConfigureAwait(false);
                return;
            }
            MemoryStream ms = new(bytes);

            var imgicon = Context.User.GetAvatarUrl() switch
            {
                null => Context.User.GetDefaultAvatarUrl(),
                _ => Context.User.GetAvatarUrl(),
            };

            EmbedAuthorBuilder embedAuthor = new()
            {
                IconUrl = imgicon,
                Name = "Switch Status",
            };

            var icon = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/Sprites/200x200/poke_capture_0363_000_mf_n_00000000_f_n.png";
            var img = "SphealCheck.jpg";
            var embed = new EmbedBuilder { Author = embedAuthor, ImageUrl = $"attachment://{img}", Color = Color.Teal }.WithFooter(new EmbedFooterBuilder { IconUrl = icon, Text = $"Captured image from bot at address {address}." });
            await Context.Channel.SendFileAsync(ms, img, "", false, embed: embed.Build());
            if (!Context.IsPrivate)
                await Context.Message.DeleteAsync(RequestOptions.Default).ConfigureAwait(false);
        }

        [Command("facts")]
        [Alias("fact", "fax", "spheal", "qotd")]
        [Summary("Sends random Spheal facts")]
        public async Task FaxSpeaker()
        {
            string gif = "";
            string title = "Invalid Command";
            string msg = "This command only works in Whitelisted Servers, not intended for public use";

            var custom = SysCordSettings.HubConfig.CustomEmbed;
            var app = await Context.Client.GetApplicationInfoAsync().ConfigureAwait(false);
            var whitelisted = new List<ulong>
            {
                1078487890318860378,
            };

            if (!whitelisted.Contains(Context.Guild.Id) && Context.User.Username != app.Owner.Username)
            {
                EmbedBuilder? embed = Sphealcl.EmbedGeneric(msg, title);
                await ReplyAsync("", false, embed: embed.Build()).ConfigureAwait(false);
            }
            else
            {
                Random rndmsg = new();
                //add toggle to hide sphealfacts
                /*if (custom.DisableOldFacts)*/
                int num = rndmsg.Next(1, 25);
                switch (num)
                {
                    case 1:
                    case 25:
                        gif = $"https://media.tenor.com/b6poqCTHCXkAAAAd/tess-spheal.gif";
                        title = "Sphealtopian Fact #1";
                        msg = "Spheals are carefree creatures";
                        break;
                    case 2:
                        gif = $"https://media.tenor.com/UfSl83Kq2ZkAAAAC/spheal-pokemon.gif";
                        title = "Sphealtopian Fact #2";
                        msg = "Spheals are mostly always happy, thats why they clap their fins";
                        break;
                    case 3:
                        gif = $"https://media.tenor.com/LeY7aqDgKrAAAAAC/swoshi-swsh.gif";
                        title = "Sphealtopian Fact #3";
                        msg = "Spheals love the company of other adventurers";
                        break;
                    case 4:
                        gif = $"https://media.tenor.com/SBZlFs2nvJEAAAAC/spheal-pokemon.gif";
                        title = "Sphealtopian Fact #4";
                        msg = "Spheals are playful and love to hide";
                        break;
                    case 5:
                        gif = $"https://media.tenor.com/q8w8kujQeyYAAAAd/pokemon-spheal.gif";
                        title = "Sphealtopian Fact #5";
                        msg = "As a natural snow inhabitant, Spheals love the snow";
                        break;
                    case 6:
                        gif = $"https://media.tenor.com/IXLsyG9QYxcAAAAd/spheal-wake.gif";
                        title = "Sphealtopian Fact #6";
                        msg = "Spheal just wants to sleep, but it appears something is preventing it";
                        break;
                    case 7:
                        gif = $"https://media.tenor.com/wH0l_PaFRskAAAAC/on-my-way-pokemon.gif";
                        title = "Sphealtopian Fact #7";
                        msg = "Spheals don't walk, we roll";
                        break;
                    case 8:
                        gif = $"https://media.tenor.com/fY3-eIP4RfwAAAAd/pokemon-spheal.gif";
                        title = "Sphealtopian Fact #8";
                        msg = "Spheals get exercise by rolling down mountains and cliffs";
                        break;
                    case 9:
                        gif = $"https://media.tenor.com/RaeurldBMa0AAAAd/kafka.gif";
                        title = "Sphealtopian Fact #9";
                        msg = "Di$co likes Kafka but is too shy to download Star Rail to meet his destiny with her";
                        break;
                    case 10:
                        gif = $"https://media.tenor.com/uYd9QIUs_U0AAAAC/pokemon-fennekin.gif";
                        title = "Sphealtopian Fact #10";
                        msg = "Roshi has a secret weakness for Fox-like creatures (Who would have thought tho?)";
                        break;
                    case 11:
                        gif = $"https://media.tenor.com/JFZnLq-pEXUAAAAC/pokemon-gardevoir.gif";
                        title = "Sphealtopian Fact #11";
                        msg = "Fraudious's favourite Pokémon is Gardevoir";
                        break;
                    case 12:
                        gif = $"https://media.tenor.com/-peVj0fSHl4AAAAC/mew-shiny.gif";
                        title = "Sphealtopian Fact #12";
                        msg = "Mew Mew does not have a Gender";
                        break;
                    case 13:
                        gif = $"https://media.tenor.com/8go55uv3EWkAAAAC/riverdale-strike.gif";
                        title = "Sphealtopian Fact #13";
                        msg = "The CEO of FraudCorp does not pay his employees";
                        break;
                    case 14:
                        gif = $"";
                        title = "Sphealtopian Fact #14";
                        msg = "The master of Spheals is just a Spheal holding a beer";
                        break;
                    case 15:
                        gif = $"";
                        title = "Sphealtopian Fact #15";
                        msg = "Mother of Spheals is the most relentless Spheal-Member, protector of SphealKind";
                        break;
                    case 16:
                        gif = $"https://media.tenor.com/Msp-rktu24YAAAAC/greys-anatomy-meredith-grey.gif";
                        title = "Sphealtopian Fact #16";
                        msg = "During BDSP rushhour, we had 2 security guards. Nobody got past them.";
                        break;
                    case 17:
                        gif = $"https://media.tenor.com/SfV3v9XeSZcAAAAC/jueee-tortuga.gif";
                        title = "Sphealtopian Fact #17";
                        msg = "Step Spheal is actually a cosplay turtle";
                        break;
                    case 18:
                        gif = $"https://media.tenor.com/F3j4rPPTMSkAAAAd/npc.gif";
                        title = "Sphealtopian Fact #18";
                        msg = "NPCs are users that require immediate help, and clocks of course";
                        break;
                    case 19:
                        gif = $"https://media.tenor.com/Mq6Jvu9RuEwAAAAd/boltund-yamper.gif";
                        title = "Sphealtopian Fact #19";
                        msg = "The true boss of FraudCorp is the CEO's pet dog IRL, he is the true mastermind";
                        break;
                    case 20:
                        gif = $"https://media.tenor.com/pRIi24X3tAQAAAAC/tkthao219-capoo.gif";
                        title = "Sphealtopian Fact #20";
                        msg = "All Spheals are actually bugcats in disguise";
                        break;
                    case 21:
                        gif = $"https://media.tenor.com/H3ngcuMGMXoAAAAC/blue-cat.gif";
                        title = "Sphealtopian Fact #21";
                        msg = "Bugcats are Spheals in cosplay";
                        break;
                    case 22:
                        gif = $"https://media.tenor.com/qYuKuP6d_hMAAAAC/he-had-such-a-knowledge-of-the-dark-side-starwars.gif";
                        title = "Sphealtopian Fact #22";
                        msg = "The CEO & Vader live in the same star Galaxy";
                        break;
                    case 23:
                        gif = $"https://media.tenor.com/rr2m8vD0vPsAAAAC/skitty-happy.gif";
                        title = "Sphealtopian Fact #23";
                        msg = "Katora is a secret skitty lover";
                        break;
                    case 24:
                        gif = $"https://media.tenor.com/qVyPGfLoA54AAAAC/pikachu-sad.gif";
                        title = "Sphealtopian Fact #24";
                        msg = "Mayo is actually a bottle of sauce";
                        break;
                }
                custom.AddSphealCount();
                EmbedBuilder? embed = Sphealcl.EmbedSpheal(msg, title, gif, custom.SphealFactsCounter);
                await ReplyAsync("", false, embed: embed.Build()).ConfigureAwait(false);
                if (!Context.IsPrivate)
                    await Context.Message.DeleteAsync(RequestOptions.Default).ConfigureAwait(false);
            }
        }

        [Command("specialfeatures")]
        [Alias("spf")]
        [Summary("Displays Special Features")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task SpecialFeatures()
        {
            var p = SysCordSettings.Settings.CommandPrefix;
            var swap = SysCordSettings.HubConfig.CustomSwaps;
            var swaps = GameInfo.GetStrings(1).Item;
            var me = SysCord<T>.Runner;
            string botversion = "";
            if (me is not null)
                botversion = me.ToString()!.Substring(46, 3);
            var gamever = botversion switch
            {
                "PK9" => "SV",
                "PK8" => "SWSH",
                "PA8" => "PLA",
                "PB8" => "BDSP",
                _ => "LGPE",
            };
            if (gamever == "SV")
            {
                var sv = $"__**Instructions**__\n> Have the Pokémon hold the **swap item**\n> Show it to the bot via **{p}rsv**\n> Your choice if you want to press **B** (*optional*)\n\n";
                sv += "**__OT Swap__**\n";
                sv += $"Function: Changes existing Pokémon OT to yours\nSwap Item: **{swaps[(int)swap.OTSwapItem]}**\n";
                sv += $"> Only **{gamever}** natives allowed with exceptions\n\n";

                sv += "**__Double Swap__**\n";
                sv += $"Function: Ball + Tera can be done in a single trade\n";
                sv += $"> Eg. Nickname Level, Hold Grass Tera Shard OR\r\nNickname Grass, Hold Level Ball\n\n";

                sv += "**__Pokéball Select__**\n";
                sv += $"Function: Allows Ball selection on nicknamed mons holding the respective Pokéball\nSwap Item: **Pokéball of choice**\n";
                sv += "> If it cannot legally be in that ball, it comes in original ball without your OT\n> If no held ball, it will come in the original ball with your OT\n\n";

                sv += "**__Pokéball Swap__**\n";
                sv += $"Function: Allows Ball swap for existing Pokémon\nSwap Item: **Pokéball of choice**\n";
                sv += $"> Pokémon gets in the ball it was holding\n> Any non {gamever} / Event mons cannot be ball swapped\n\n";

                sv += "**__Mystery Eggs__**\n";
                sv += $"Function: Receive a **Mystery Egg** [Command: **{p}rme**]\n";
                sv += $"> Alt Method:\n> Trade Pokémon nicknamed **{swap.MysteryEgg}** via {p}rsv\n> Receive a random egg\n> Eggs will be your OT and met in picnic\n\n";

                sv += "**__Tera Select__**\n";
                sv += $"Function: Allows Tera selection on nicknamed mons holding the respective Tera Shard\nSwap Item: **Tera Shard of choice**\n";
                sv += "> Eggs can only be in their base or secondary types\n> Can only be done on SV native as it's sent via OTSwap\n\n";

                sv += "**__Tera Swap__**\n";
                sv += "Function: Allows Tera type swap for existing Pokémon\nSwap Item: **Tera Shard of choice**\n";
                sv += "> Pokémon gets the New Tera type according to the shard held\n\n";

                sv += "**__EV Swap__**\n";
                sv += "Function: Perform either depending on held item ➜ \nReset EVs | EV Raid Preset | EV Comp Preset | EV Tank Preset\n";
                sv += $"> Pokémon gets resetted EVs or **252** EVs in **2** stats, last **6** EVs are done yourself\n> Raid Presets are *minted* to **Adamant/Modest** respectively\n\n";
                sv += $"__**Swap Item(s)**__\nEV Reset ➜ **{swaps[(int)swap.EVResetItem]}** [Resets ALL EVs]\n\nEV Raid Atk ➜ **{swaps[(int)swap.EVRaidAtkItem]}** [Reset ALL EVs, Apply ATK/HP]\nEV Raid SP Atk ➜ **{swaps[(int)swap.EVRaidSPAItem]}** [Reset, Apply SPAtk/HP]\n\n";
                sv += $"EV Comp Atk ➜ **{swaps[(int)swap.EVCompAtkItem]}** [Reset, Atk/Speed]\nEV Comp SP Atk ➜ **{swaps[(int)swap.EVCompSPAItem]}** [Reset, SPAtk/Speed]\n\nEV Def Tank ➜ **{swaps[(int)swap.EVGenDEFItem]}** [Reset, HP/Def]\nEV Sp Def Tank ➜ **{swaps[(int)swap.EVGenSPDItem]}** [Reset, HP/SPDef]\n\n";

                sv += $"Do {p}spf2 to view Page 2";

                Embed? embed = Sphealcl.EmbedSFList(sv, "Special Features - SV");
                await ReplyAsync("", false, embed: embed).ConfigureAwait(false);
            }
            else if (gamever == "SWSH")
            {
                var swsh = $"__**Instructions**__\n> Have the Pokémon hold the **swap item**\n> Show it to the bot via **{p}rsv**\n> Your choice if you want to press **B** (*optional*)\n\n";
                swsh += "**__OT Swap__**\n";
                swsh += $"Function: Changes existing Pokémon OT to yours\nSwap Item: **{swaps[(int)swap.OTSwapItem]}**\n";
                swsh += $"> • Only **{gamever}** natives allowed with exceptions\n\n";

                swsh += "**__Pokéball Select__**\n";
                swsh += $"Function: Allows Ball selection on nicknamed mons holding the respective Pokéball\nSwap Item: **Pokéball of choice**\n";
                swsh += "> • If it cannot legally be in that ball, it comes in whatever is on the sheet and without your OT\n> • If the Pokémon does not hold any ball, it will come in the ball specified on the sheet with your OT\n\n";

                swsh += "**__Pokéball Swap__**\n";
                swsh += $"Function: Allows Ball swap for existing Pokémon\nSwap Item: **Pokéball of choice**\n";
                swsh += $"> • Receive the offered Pokémon in the ball it was holding\n> • Any non {gamever} / Event mons cannot be ball swapped\n\n";

                swsh += "**__Mystery Eggs__**\n";
                swsh += $"Function: Trade a Pokémon with the nickname **{swap.MysteryEgg}** to get a random egg\n";
                swsh += "> • Receive a random egg\n> • Eggs will be in your OT and met in daycare\n\n";

                swsh += "**__Trilogy Swap__**\n";
                swsh += $"Function: Performs a trio of actions ➜ \nClear Nickname | Set Level to 1OO | Evolve Species\nSwap Item: **{swaps[(int)swap.TrilogySwapItem]}**\n";
                swsh += "> • First two functions can be done on any legal mon\n\n**Clear Nickname** ➜ Clears the Nickname\n**Level to 1OO** ➜ Sets the Pokémon's level to 1OO\n**Evolve** ➜ Evolves the Species, all of its stats/details will be cloned\n\n__**Species List**__\n```Farfetch'd (Galar) | Yamask (Galar) | Sliggoo (Kalos)```\n";

                swsh += "**__Trade Evo <Purifier>__**\n";
                swsh += "Function: Evolve Basic Trade Evolutions\nSwap Item: **Everstone**\n";
                swsh += "__**Species List**__\n```Kadabra | Machoke | Gurdurr\nHaunter | Phantump | Pumpkaboo\nBoldore | Feebas\nShelmet | Karrablast```\n";

                Embed? embed = Sphealcl.EmbedSFList(swsh, "Special Features - SWSH");
                await ReplyAsync("", false, embed: embed).ConfigureAwait(false);
            }
            else if (gamever == "PLA")
            {
                var pla = $"__**Instructions**__\n> Have the Pokémon be nicknamed as specified\nShow it to the bot via **{p}rsv**\n> Your choice if you want to press **B** (*optional*)\n\n";
                pla += "**__Trilogy Swap__**\n";
                pla += "Function: Performs a trio of actions ➜ \nClear Nickname | Set Level to 1OO | Evolve Species\n";
                pla += $"> • Have the Pokémon be named evo\n> • First two functions can be done on any legal mon\n\n**Clear Nickname** ➜ Clears the Nickname\n**Level to 1OO** ➜ Sets the Pokémon's level to 1OO\n**Evolve** ➜ Evolves the Species, all of its stats/details will be cloned\n\n__**Species List**__\n```Ursaring | Hisui Qwilfish | Scyther\nStantler | Hisui Sliggoo | White Basculin```\n";

                pla += "**__Pokéball Swap__**\n";
                pla += "Function: Allows Ball swap for existing Pokémon\n";
                pla += $"> • Have the Pokémon be nicknamed either of these:\n> Poke ➜ LAPoke | Great ➜ LAGreat | Ultra ➜ LAUltra\n> Feat ➜ Feather | Wing ➜ Wing | Jet ➜ Jet\n> Heavy ➜ LAHeavy | Lead ➜ Leaden | Giga ➜ Gigaton\n> • Receive the offered Pokémon in the ball you chose\nAny non {gamever} / Event mons cannot be ball swapped\n\n";

                Embed? embed = Sphealcl.EmbedSFList(pla, "Special Features - PLA");
                await ReplyAsync("", false, embed: embed).ConfigureAwait(false);
            }
            else if (gamever == "BDSP")
            {
                var bdsp = $"__**Instructions**__\n> Have the Pokémon hold the **swap item**\n> Show it to the bot via **{p}rsv**\n> Pressing B is disallowed for non **Select** Swaps\n\n";
                bdsp += "**__Pokéball Select__**\n";
                bdsp += "Function: Allows Ball selection on nicknamed mons holding the respective Pokéball\nSwap Item: **Pokéball of choice**\n";
                bdsp += "> • If it cannot legally be in that ball, it comes in whatever is on the sheet and without your OT\n> • If the Pokémon does not hold any ball, it will come in the ball specified on the sheet with your OT\n";

                Embed? embed = Sphealcl.EmbedSFList(bdsp, "Special Features - BDSP");
                await ReplyAsync("", false, embed: embed).ConfigureAwait(false);
            }
        }

        [Command("specialfeatures2")]
        [Alias("spf2")]
        [Summary("Displays Special Features Page 2")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task SpecialFeatures2()
        {
            var p = SysCordSettings.Settings.CommandPrefix;
            var swap = SysCordSettings.HubConfig.CustomSwaps;
            var swaps = GameInfo.GetStrings(1).Item;
            var me = SysCord<T>.Runner;
            string botversion = "";
            if (me is not null)
                botversion = me.ToString()!.Substring(46, 3);
            var gamever = botversion switch
            {
                "PK9" => "SV",
                "PK8" => "SWSH",
                "PA8" => "PLA",
                "PB8" => "BDSP",
                _ => "LGPE",
            };

            if (gamever == "SV")
            {
                var sv = "**__Trilogy Swap__**\n";
                sv += $"Function: Performs a trio of actions ➜ \nClear Nickname | Level to 1OO | Evolve Species\nSwap Item: **{swaps[(int)swap.TrilogySwapItem]}**\nFirst two functions can be done on any legal mon\n";
                sv += "> **Evolve** ➜ Evolves the Species, all of its stats/details will be cloned\n\n__**Species**__\n```Finizen | Inkay\r\nRellor | Pawmo | Bramblin\nKalos Sliggoo | White Basculin\nGimmighoul | Primeape | Bisharp```\n";

                sv += "**__Friendship Swap__**\n";
                sv += $"Function: Like Trilogy, but focuses on Friendship\nSets Friendship to MAX & Gives Best Friends + Partner Ribbons (Nickname: 'null' to disable ribbons)\nSwap Item: **{swaps[(int)swap.FriendshipSwapItem]}\n**";
                sv += "> Eevee only gets friendship, but you get a candy to evolve it\n[**Species**](<https://pokemondb.net/evolution/friendship>)\n\n";

                sv += "**__Trade Evo <Purifier>__**\n";
                sv += "Function: Evolve Basic Trade Evolutions\nSwap Item: **Everstone**\n";
                sv += "__**Species**__\n```Haunter | Graveler | Phantump\nGurdurr | Poliwhirl | Slowpoke\nFeebas | Scyther | Dusclops\nRhydon | Magmar```\n";

                sv += "**__Gender Swap__**\n";
                sv += $"Function: Allows Gender swap for existing Pokémon\nSwap Item: **{swaps[(int)swap.GenderSwapItem]}**\n";
                sv += $"> Pokémon becomes opposite Gender\n> ONLY works for {gamever} natives\n\n";

                sv += "**__Power Swap__**\n";
                sv += $"Function: Maxes out all PP for moves & gives relearn TMs for existing Pokémon\nSwap Item: **{swaps[(int)swap.PowerSwapItem]}**\n";
                sv += $"> Pokémon gets maxed out PPs & relearn moves\n\n";

                sv += "**__Size Swap__**\n";
                sv += $"Function: Allows Scale customization [**ONLY SV NATIVES**]\nSwap Item: **{swaps[(int)swap.SizeSwapItem]}**\n";
                sv += $"> Nickname a number from 0 - 255\n> Pokémon gets the scale you nicknamed\n> If none defined, you get random\n> If Jumbo or Mini, mark gets applied\n\n";

                sv += "**__Mark Swap__**\n";
                sv += $"Function: Allows Mark customization [**Must not have entered HOME**]\nSwap Item: **{swaps[(int)swap.MarkSwapItem]}**\n";
                sv += $"> {p}mk command to view more\n\n";

                sv += "**__Date Swap__**\n";
                sv += $"Function: Allows Date customization [**Must not have entered HOME**]\nSwap Item: **{swaps[(int)swap.DateSwapItem]}**\n";
                sv += $"> Main purpose to fix correct date for Destiny Mark as the Bot cannot read your birthday\n> Fixed Year of 2023\n> Nickname a Pokémon in this format:\n> MM/DD\n> Make sure to not forget the / between Month & Day\n\n";

                sv += "**__Ultimate Swapperino__**\n";
                sv += $"Function: Performs many functions\nSwap Item: **{swaps[(int)swap.UltimateSwapItem]}**\n";
                sv += $"> {p}us for more information\n\n";

                Embed? embed = Sphealcl.EmbedSFList(sv, "Special Features - SV");
                await ReplyAsync("", false, embed: embed).ConfigureAwait(false);
            }
        }

        [Command("mark")]
        [Alias("mk")]
        [Summary("Displays Mark Swap Info")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task MarkInfo()
        {
            var mk = "__**Instructions**__\nOnly SV Wild encounters that have not entered HOME can use this function\nAny Mew can also use this function to get the Mightiest Mark\n\n";
            mk += "`Bonus Feature` ➜ 1/20 chance for your swap to obtain the **ItemFinder** Mark\n";
            mk += "Two ways to get Marks: Choose via Nickname or Random\n\n";
            mk += "__**Nicknames**__\n```Bliz ➜ Blizzard Mark | Snow ➜ Snowy Mark\nSand ➜ Sandstorm Mark | Rain ➜ Rainy Mark\nStorm ➜ Stormy Mark | Cloud ➜ Cloudy Mark\n<These are chosen as they are situational>```\n";
            mk += "For Random, there are **4** tiers: Common➜Epic➜Unique➜Legend (Rarest)\n";
            mk += "View the table below for categories:";

            Embed? embed = Sphealcl.EmbedSFList(mk, "Mark Info", true);
            await ReplyAsync("", false, embed: embed).ConfigureAwait(false);
        }

        [Command("ultimate")]
        [Alias("ulti", "us")]
        [Summary("Displays Ultimate Swap Info")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task UltimateInfo()
        {
            var p = SysCordSettings.Settings.CommandPrefix;
            var ut = "## __Instructions__\nCan be used on **ANY** Pokémon, However, will only apply possible swaps to it\n\n";
            ut += "So what does this do? This is the ultimate swapperino of all swaps. This does *nearly* all possible swaps from the bot in a single trade & more\n";
            ut += "In order for this to work, introducing an old method: TrashMon (You hit **B** and offer this trashmon to apply more options)\n\n";
            ut += "Example: Lechonk nicknamed '**Steel**' & trashmon Fidough nicknamed '**Beast**' gets you Steel Tera & Beast Ball alongside the default swap options\n\n";

            ut += "## __Any Pokémon (Default)__\n";
            ut += "- Reset EVs | Revert to Original Tera | Set Lv to 1OO\n";
            ut += "- Max PPups | Set all possible TMs | Max Friendship\n";
            ut += "- Reset Mints\n\n";

            ut += "## __Any Pokémon (Read brackets)__\n";
            ut += "- Tera Swap (Nickname on either or Held Item) | EV Presets (Held Item on the **TRASHMON** only)\n";
            ut += "- Best Friends & Partner Ribbons (Disable with nickname: **null**)\n\n";

            ut += "## __SV Only (**Read brackets**)__\n";
            ut += "- Date Swap (Held Item on the **TRASHMON** only) | Fix Egg Date to match Hatch Date (**Default**) | Remove all Marks & Ribbons (Req: **null** nickname)\n";
            ut += "- Remove all PP ups (Nickname: **NoPPUP**) | Ball Swap (Nick on either or Held on **TRASHmon**) | Size Swap (Only **255** & **0**)\n";
            ut += "- Randomize IVs (Nickname: **rndiv**) | Clear Nickname & Set to **YOUR** OT (**Default**) || Gender Swap (Held Item on the **TRASHMON** only)\n";
            ut += "- Reset Level to Met Level (Nickname: **reset**) | Base Nature Swap (Held Item <Mint> on the **TRASHMON** only)\n\n";
            ut += $"For details how to nickname for existing swap format check {p}spf or {p}spf2\n";

            Embed? embed = Sphealcl.EmbedSFList(ut, "Ultimate Swap Info");
            await ReplyAsync("", false, embed: embed).ConfigureAwait(false);
        }

        [Command("nohome")]
        [Alias("10015", "homeerror")]
        [Summary("Display FAQ for Home error")]
        public async Task HomeFAQ()
        {
            var msg = "## Depositing Pokémon into HOME\n\n";
            msg += "Starting with HOME Ver. 3.1.0, the servers will now validate Pokémon based on their HOME data (or lack thereof).\n\n";
            msg += "This can include the following:\r\n- Having no HOME Tracker.\r\n- The existing HOME data is invalid.\r\n- Immutable stats (such as IVs) were modified after being assigned a valid HOME Tracker.\n\n";
            msg += "To avoid this error code, you should only be generating Pokémon in their respective origin games (e.g. BDSP Darkrai in BDSP, not SV), and transfer them to other games using HOME, not PKHeX.\n\n";
            msg += "Note that this is not an issue that can (ever) be solved by PKHeX or the bot owner\n";

            EmbedBuilder? embed = Sphealcl.EmbedGeneric(msg, "Error Code 10015");
            await ReplyAsync("", false, embed: embed.Build()).ConfigureAwait(false);
        }
    }
}