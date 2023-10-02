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
            Species species = Info.Hub.Config.CustomSwaps.ItemTradeSpecies is Species.None ? Species.Finizen : Info.Hub.Config.CustomSwaps.ItemTradeSpecies;
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
                            await ReplyAsync($"This command is disabled for BDSP").ConfigureAwait(false);
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
                        await ReplyAsync($"This command is disabled for BDSP").ConfigureAwait(false);
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
            var embed = new EmbedBuilder();
            embed.AddField(s =>
            {
                s.Name = "Queue List";
                s.Value = msg;
                s.IsInline = false;
            });
            await ReplyAsync("These are the users who are currently waiting:", embed: embed.Build()).ConfigureAwait(false);
        }

        [Command("mysteryEgg")]
        [Alias("myst", "rme")]
        [Summary("Request a Mystery Egg through Discord - Pool from distribution folder")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task myst()
        {
            var code = Info.GetRandomTradeCode();
            var sig = Context.User.GetFavor();
            var me = SysCord<T>.Runner;
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
                        await ReplyAsync($"This command is disabled for BDSP").ConfigureAwait(false);
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
                        await ReplyAsync($"This command is disabled for BDSP").ConfigureAwait(false);
                        break;
                }
            }
        }

        [Command("checkgame")]
        [Alias("game", "cg")]
        [Summary("What game is currently running?")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task CheckGame()
        {
            var me = SysCord<T>.Runner;
            string botversion = "";
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
            await ReplyAsync($"# Current Game running is **{gamever}**").ConfigureAwait(false);
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
                SysCordSettings.HubConfig.TradeAbuse.CooldownUpdate = $"{DateTime.Now:yyyy.MM.dd - HH:mm:ss}";
                await ReplyAsync($"Cooldown has been updated to {cooldown} minutes.").ConfigureAwait(false);
            }
            else
                await ReplyAsync("Please enter a valid number of minutes.").ConfigureAwait(false);
        }

        [Command("checkcd")]
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
                    EmbedBuilder? embed = Sphealcl.EmbedCDMessage2(cd, $"{trainerName} your last encounter with the bot was {delta.TotalMinutes:F1} mins ago.\nTime left: {wait.TotalMinutes:F1} mins.", "[Cooldown Checker]");
                    await ReplyAsync("", false, embed: embed.Build()).ConfigureAwait(false);
                }
                else
                {
                    EmbedBuilder? embed = Sphealcl.EmbedCDMessage2(cd, $"{trainerName} your cooldown is up. You may trade again.", "[Cooldown Checker]");
                    await ReplyAsync("", false, embed: embed.Build()).ConfigureAwait(false);
                }
            }
            else
            {
                EmbedBuilder? embed = Sphealcl.EmbedCDMessage2(cd, $"User has not traded with the bot recently.", "[Cooldown Checker]");
                await ReplyAsync("", false, embed: embed.Build()).ConfigureAwait(false);
            }
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
            if (!Context.IsPrivate)
                await Context.Message.DeleteAsync(RequestOptions.Default).ConfigureAwait(false);
        }
        [Command("spheal")]
        [Summary("Sends random Spheals")]
        public async Task SphealAsync()
        {
            var msg = "Placeholder";
            Random rndmsg = new();
            int num = rndmsg.Next(1, 9);
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
            if (!Context.IsPrivate)
                await Context.Message.DeleteAsync(RequestOptions.Default).ConfigureAwait(false);
        }

        [Command("specialfeatures")]
        [Alias("spf")]
        [Summary("Displays Special Features")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task SpecialFeatures()
        {
            var p = SysCordSettings.Settings.CommandPrefix;
            var swap = SysCordSettings.HubConfig.CustomSwaps;
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
                sv += $"Function: Changes existing Pokémon OT to yours\nSwap Item: **{swap.OTSwapItem}**\n";
                sv += $"> • Only **{gamever}** natives allowed with exceptions\n\n";

                sv += "**__Double Swap__**\n";
                sv += $"Function: Ball + Tera can be done in a single trade\n";
                sv += $"> • Eg. Nickname Level, Hold Grass Tera Shard OR\r\nNickname Grass, Hold Level Ball\n\n";

                sv += "**__Pokéball Select__**\n";
                sv += $"Function: Allows Ball selection on nicknamed mons holding the respective Pokéball\nSwap Item: **Pokéball of choice**\n";
                sv += "> • If it cannot legally be in that ball, it comes in whatever is on the sheet and without your OT\n> • If the Pokémon does not hold any ball, it will come in the ball specified on the sheet with your OT\n\n";
                
                sv += "**__Pokéball Swap__**\n";
                sv += $"Function: Allows Ball swap for existing Pokémon\nSwap Item: **Pokéball of choice**\n";
                sv += $"> • Receive the offered Pokémon in the ball it was holding\n> • Any non {gamever} / Event mons cannot be ball swapped\n\n";

                sv += "**__Mystery Eggs__**\n";
                sv += $"Function: Receive a **Mystery Egg** [Command: **{p}rme**]\n";
                sv += $"> Alternate Method:\n> • Trade a Pokémon nicknamed **{swap.MysteryEgg}** via {p}rsv\n> • Receive a random egg\n> • Eggs will be in your OT and met in picnic\n\n";

                sv += "**__Tera Select__**\n";
                sv += $"Function: Allows Tera selection on nicknamed mons holding the respective Tera Shard\nSwap Item: **Tera Shard of choice**\n";
                sv += "> • Eggs can only be in their base or secondary types\n> • Can only be done on SV native as it's sent via OTSwap\n\n";

                sv += "**__Tera Swap__**\n";
                sv += "Function: Allows Tera type swap for existing Pokémon\nSwap Item: **Tera Shard of choice**\n";
                sv += "> • Receive the offered Pokémon in the New Tera type according to what shard it was holding\n\n";

                sv += "**__Trilogy Swap__**\n";
                sv += $"Function: Performs a trio of actions ➜ \nClear Nickname | Set Level to 1OO | Evolve Species\nSwap Item: **{swap.TrilogySwapItem}**\nFirst two functions can be done on any legal mon\n";
                sv += "> **Clear Nickname** ➜ Clears the Nickname\n> **Level 1OO** ➜ Sets the Pokémon's level to 1OO\n> **Evolve** ➜ Evolves the Species, all of its stats/details will be cloned\n\n__**Species List**__\n```Finizen\r\nRellor | Pawmo | Bramblin\nKalos Sliggoo | White Basculin\nGimmighoul | Primeape | Bisharp```\n";

                sv += "**__Friendship Swap__**\n";
                sv += $"Function: Like Trilogy, but focuses on Friendship\nSets Friendship to MAX & Gives Best Friends + Partner Ribbons (Nickname: 'null' to disable ribbons)\nSwap Item: **{swap.FriendshipSwapItem}\n**";
                sv += "> Eevee only gets friendship, but you get a candy to evolve it\n[**Species List**](<https://pokemondb.net/evolution/friendship>)\n\n";

                sv += "**__Trade Evo <Purifier>__**\n";
                sv += "Function: Evolve Basic Trade Evolutions\nSwap Item: **Everstone**\n";
                sv += "__**Species List**__\n```Haunter | Graveler | Phantump\nGurdurr | Poliwhirl | Slowpoke\nFeebas | Scyther | Dusclops```\n";

                sv += "**__EV Swap__**\n";
                sv += "Function: Perform either depending on held item ➜ \nReset EVs | EV Raid Preset | EV Comp Preset | EV Tank Preset\n";
                sv += $"> • Bot will reset or apply **252** EVs in **2** stats, last **6** EVs are done yourself\n> • Raid Presets are *minted* to **Adamant/Modest** respectively\n\n__**Swap Item(s)**__\nEV Reset ➜ **{swap.EVResetItem}** [Resets ALL EVs]\n\nEV Raid Atk ➜ **{swap.EVRaidAtkItem}** [Reset ALL EVs, Apply ATK/HP]\nEV Raid SP Atk ➜ **{swap.EVRaidSPAItem}** [Reset, Apply SPAtk/HP]\n\nEV Comp Atk ➜ **{swap.EVCompAtkItem}** [Reset, Atk/Speed]\nEV Comp SP Atk ➜ **{swap.EVCompSPAItem}** [Reset, SPAtk/Speed]\n\nEV Def Tank ➜ **{swap.EVGenDEFItem}** [Reset, HP/Def]\nEV Sp Def Tank ➜ **{swap.EVGenSPDItem}** [Reset, HP/SPDef]\n\n";

                sv += "**__Gender Swap__**\n";
                sv += $"Function: Allows Gender swap for existing Pokémon\nSwap Item: **{swap.GenderSwapItem}**\n";
                sv += $"> • Receive the offered Pokémon in the opposite Gender\n> • ONLY works for {gamever} natives\n\n";

                sv += "**__Power Swap__**\n";
                sv += $"Function: Maxes out all PP for moves & gives relearn TMs for existing Pokémon\nSwap Item: **{swap.PowerSwapItem}**\n";
                sv += $"> • Receive the offered Pokémon with maxed out PPs & relearn moves\n\n";

                sv += "**__Size Swap__**\n";
                sv += $"Function: Allows Scale customization [**ONLY SV NATIVES**]\nSwap Item: **{swap.SizeSwapItem}**\n";
                sv += $"\n> Nickname it a number between 0 - 255 to choose Size\n> • Receive the offered Pokémon with the chosen scale you nicknamed\n> • If none defined, you get random\n> • If Jumbo or Mini, mark gets applied\n\n";

                Embed? embed = Sphealcl.EmbedSFList(sv, "Special Features - SV");
                await ReplyAsync("", false, embed: embed).ConfigureAwait(false);
            }
            else if (gamever == "SWSH")
            {
                var swsh = $"__**Instructions**__\n> Have the Pokémon hold the **swap item**\n> Show it to the bot via **{p}rsv**\n> Your choice if you want to press **B** (*optional*)\n\n";
                swsh += "**__OT Swap__**\n";
                swsh += $"Function: Changes existing Pokémon OT to yours\nSwap Item: **{swap.OTSwapItem}**\n";
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
                swsh += $"Function: Performs a trio of actions ➜ \nClear Nickname | Set Level to 1OO | Evolve Species\nSwap Item: **{swap.TrilogySwapItem}**\n";
                swsh += "> • First two functions can be done on any legal mon\n\n**Clear Nickname** ➜ Clears the Nickname\n**Level to 1OO** ➜ Sets the Pokémon's level to 1OO\n**Evolve** ➜ Evolves the Species, all of its stats/details will be cloned\n\n__**Species List**__\n```Farfetch'd (Galar) | Yamask (Galar) | Sliggoo (Kalos)```\n";

                swsh += "**__Trade Evo <Purifier>__**\n";
                swsh += "Function: Evolve Basic Trade Evolutions\nSwap Item: **Everstone**\n";
                swsh += "__**Species List**__\n```Kadabra | Machoke | Gurdurr\nHaunter | Phantump| Pumpkaboo\nBoldore | Feebas\nShelmet | Karrablast```\n";

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
                var bdsp = $"__**Instructions**__\n> Have the Pokémon hold the **swap item**\n> Show it to the bot via **{p}rsv**\n> Your choice if you want to press **B** (*optional*)\n\n";
                bdsp += "**__Pokéball Select__**\n";
                bdsp += "Function: Allows Ball selection on nicknamed mons holding the respective Pokéball\nSwap Item: **Pokéball of choice**\n";
                bdsp += "> • If it cannot legally be in that ball, it comes in whatever is on the sheet and without your OT\n> • If the Pokémon does not hold any ball, it will come in the ball specified on the sheet with your OT\n";

                Embed? embed = Sphealcl.EmbedSFList(bdsp, "Special Features - BDSP");
                await ReplyAsync("", false, embed: embed).ConfigureAwait(false);
            }
        }
    }
}