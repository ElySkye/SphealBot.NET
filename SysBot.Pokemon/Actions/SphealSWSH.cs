using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Pokemon.PokeDataOffsetsSWSH;

namespace SysBot.Pokemon
{
    public partial class PokeTradeBotSWSH : PokeRoutineExecutor8SWSH, ICountBot
    {
        private async Task<(PK8 toSend, PokeTradeResult check)> HandleMysteryEggs(SAV8SWSH sav, PokeTradeDetail<PK8> poke, PK8 offered, PK8 toSend, PartnerDataHolder partner, CancellationToken token)
        {
            var counts = TradeSettings;
            var user = partner.TrainerName;

            string? myst;
            PK8? rnd;
            do
            {
                rnd = Hub.Ledy.Pool.GetRandomTrade();
            } while (!rnd.IsEgg);
            toSend = rnd;

            var Shiny = toSend.IsShiny switch
            {
                true => "Shiny",
                false => "Non-Shiny",
            };

            Log($"Sending Surprise Egg: {Shiny} {(Gender)toSend.Gender} {GameInfo.GetStrings(1).Species[toSend.Species]}");
            await SetTradePartnerDetailsSWSH(poke, toSend, offered, partner.TrainerName, sav, token).ConfigureAwait(false);
            await SetBoxPokemon(toSend, 0, 0, token, sav).ConfigureAwait(false);
            await Task.Delay(2_500, token).ConfigureAwait(false);
            poke.TradeData = toSend;

            myst = $"**{user}** has received a Mystery Egg !\n";
            myst += $"**Don't reveal if you want the surprise**\n\n";
            myst += $"**Pokémon**: ||**{GameInfo.GetStrings(1).Species[toSend.Species]}**||\n";
            myst += $"**Gender**: ||**{(Gender)toSend.Gender}**||\n";
            myst += $"**Shiny**: ||**{Shiny}**||\n";
            myst += $"**Nature**: ||**{(Nature)toSend.Nature}**||\n";
            myst += $"**Ability**: ||**{GameInfo.GetStrings(1).Ability[toSend.Ability]}**||\n";
            myst += $"**IVs**: ||**{toSend.IV_HP}/{toSend.IV_ATK}/{toSend.IV_DEF}/{toSend.IV_SPA}/{toSend.IV_SPD}/{toSend.IV_SPE}**||\n";
            myst += $"**Language**: ||**{(LanguageID)toSend.Language}**||";

            EchoUtil.EchoEmbed(Sphealcl.EmbedEggMystery(toSend, myst, $"{user}'s Mystery Egg"));
            counts.AddCompletedMystery();
            return (toSend, PokeTradeResult.Success);
        }
        private async Task<(PK8 toSend, PokeTradeResult check)> HandleCustomSwaps(SAV8SWSH sav, PokeTradeDetail<PK8> poke, PK8 offered, PK8 toSend, PartnerDataHolder partner, CancellationToken token)
        {
            toSend = offered.Clone();
            var custom = Hub.Config.CustomSwaps;
            var counts = TradeSettings;
            var swap = offered.HeldItem;
            var user = partner.TrainerName;
            var offers = GameInfo.GetStrings(1).Species[offered.Species];
            var offerts = GameInfo.GetStrings(1).Species[toSend.Species];
            var la = new LegalityAnalysis(offered);
            var botot = Hub.Config.Legality.GenerateOT;
            var notball = new List<int>
            {
                228, //Smoke Ball
                236, //Light Ball
                278, //Iron Ball
                541, //Air Balloon
                649, //Snowball
            };

            string? msg;
            string[] ballItem = GameInfo.GetStrings(1).Item[swap].Split(' ');

            if (!la.Valid)
            {
                if (poke.Type == PokeTradeType.LinkSWSH)
                    poke.SendNotification(this, $"**{offers}** is not legal\n\n__**Legality Analysis**__\n```{la.Report()}```");
                msg = $"{user}, **{offers}** is not legal, Features cannot be used\n";
                msg += $"**OT**: {offered.OT_Name}-{offered.TID16}\n**HT**: {offered.HT_Name}\n";
                msg += $"**Game**: {(GameVersion)offered.Version}\n\n";
                if (la.Info.PIDIV.Type != PIDType.None)
                    msg += $"**PIDType**: {la.Info.PIDIV.Type}\n\n";
                msg += $"__**Legality Report**__\n";
                msg += la.Report();
                await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Illegal Request").ConfigureAwait(false);
                DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);
                return (toSend, PokeTradeResult.IllegalTrade);
            }
            else if (swap == (int)custom.OTSwapItem)
            {
                var result = await SetTradePartnerDetailsSWSH(poke, toSend, offered, partner.TrainerName, sav, token).ConfigureAwait(false);
                Log($"{user} is requesting OT swap for: {offers} with OT Name: {offered.OT_Name}");
                toSend.Tracker = 0; //We clean the tracker since we only do the Origin Game

                if (result.Item2 == false)
                {
                    //Non SWSH should get rejected
                    if (poke.Type == PokeTradeType.LinkSWSH)
                        poke.SendNotification(this, $"```{user}, {offers} cannot be OT swap\n\nPokémon is either:\n1) Not SWSH native\n2) SWSH Event/In-game trade with FIXED OT```");
                    msg = $"{user}, **{offers}** cannot be OT swap\n\n";
                    msg += "Pokémon is either:\n1) Not SWSH native\n2) SWSH Event / In - game trade with FIXED OT\n\n";
                    msg += $"Original OT: **{offered.OT_Name}**\nGame: {(GameVersion)toSend.Version}\n";
                    if (offered.FatefulEncounter)
                        msg += $"Pokémon is a Fateful Encounter (Event)\n";
                    else if (offered.Met_Location == 30001)
                        msg += $"Pokémon is an in-game trade\n";
                    await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Bad OT Swap").ConfigureAwait(false);
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", toSend);
                    return (toSend, PokeTradeResult.TrainerRequestBad);
                }
                else
                    toSend = result.Item1;

                poke.TradeData = toSend;
                await SetBoxPokemon(toSend, 0, 0, token, sav).ConfigureAwait(false);
                await Task.Delay(2_500, token).ConfigureAwait(false);
                return (toSend, PokeTradeResult.Success);
            }
            else if (ballItem.Length > 1 && ballItem[1] == "Ball" && !notball.Contains(swap))
            {
                Log($"{user} is requesting Ball swap for: {offers}");

                if ((GameVersion)toSend.Version == GameVersion.SW || (GameVersion)toSend.Version == GameVersion.SH)
                {
                    toSend.Tracker = 0;
                    if (ballItem[0] == "Poké") //Account for Pokeball having an apostrophe
                        ballItem[0] = "Poke";
                    toSend.Ball = (int)(Ball)Enum.Parse(typeof(Ball), ballItem[0]);
                    toSend.RefreshChecksum();
                    Log($"Ball swapped to: {(Ball)toSend.Ball} Ball");

                    var la2 = new LegalityAnalysis(toSend);
                    if (la2.Valid)
                    {
                        poke.TradeData = toSend;
                        counts.AddCompletedBallSwaps();
                        await SetBoxPokemon(toSend, 0, 0, token, sav).ConfigureAwait(false);
                        await Task.Delay(2_500, token).ConfigureAwait(false);
                        return (toSend, PokeTradeResult.Success);
                    }
                    else
                    {
                        if (poke.Type == PokeTradeType.LinkSWSH)
                        {
                            if (toSend.WasEgg && toSend.Ball == 1)
                                poke.SendNotification(this, $"```{user}, {offers} is from an egg & cannot be in {(Ball)toSend.Ball}```");
                            else
                                poke.SendNotification(this, $"```{user}, {offers} cannot be in {(Ball)toSend.Ball}```");
                        }
                        msg = $"{user}, **{offers}** cannot be in **{(Ball)toSend.Ball}**\n";
                        if (toSend.WasEgg && toSend.Ball == 1)
                            msg += "Egg hatches cannot be in **Master Ball**\n";
                        msg += "The ball cannot be swapped";
                        await SphealEmbed.EmbedAlertMessage(toSend, false, toSend.FormArgument, msg, "Bad Ball Swap").ConfigureAwait(false);
                        DumpPokemon(DumpSetting.DumpFolder, "hacked", toSend);
                        return (toSend, PokeTradeResult.TrainerRequestBad);
                    }
                }
                else
                {
                    if (poke.Type == PokeTradeType.LinkSWSH)
                        poke.SendNotification(this, $"```{user}, {offers} cannot be Ball Swap\nReason: Not from SWSH```");
                    msg = $"{user}, **{offers}** is not SWSH native & cannot be swapped due to Home Tracker";
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);
                    await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Bad Ball Swap").ConfigureAwait(false);
                    return (offered, PokeTradeResult.TrainerRequestBad);
                }
            }
            else if (swap == (int)custom.TrilogySwapItem || swap == 229) //Trilogy Swap for existing mons (Level/Nickname/Evolve)
            {
                Log($"{user} is requesting Trilogy swap for: {offers}");

                toSend.CurrentLevel = 100;//#1 Set level to 100 (Level Swap)
                if (swap == 229)
                {
                    Log($"Evo Species is holding an Everstone");

                    switch (toSend.Species)
                    {
                        case (ushort)Species.Kadabra:
                            toSend.Species = (ushort)Species.Alakazam;
                            break;
                        case (ushort)Species.Machoke:
                            toSend.Species = (ushort)Species.Machamp;
                            break;
                        case (ushort)Species.Gurdurr:
                            toSend.Species = (ushort)Species.Conkeldurr;
                            break;
                        case (ushort)Species.Haunter:
                            toSend.Species = (ushort)Species.Gengar;
                            break;
                        case (ushort)Species.Graveler:
                            toSend.Species = (ushort)Species.Golem;
                            break;
                        case (ushort)Species.Phantump:
                            toSend.Species = (ushort)Species.Trevenant;
                            break;
                        case (ushort)Species.Pumpkaboo:
                            toSend.Species = (ushort)Species.Gourgeist;
                            toSend.Form = offered.Form switch
                            {
                                0 => 0, //Average
                                1 => 1, //Small
                                2 => 2, //Large
                                _ => 3, //Super
                            };
                            break;
                        case (ushort)Species.Boldore:
                            toSend.Species = (ushort)Species.Gigalith;
                            break;
                        case (ushort)Species.Feebas:
                            toSend.Species = (ushort)Species.Milotic;
                            break;
                        case (ushort)Species.Shelmet:
                            toSend.Species = (ushort)Species.Accelgor;
                            break;
                        case (ushort)Species.Karrablast:
                            toSend.Species = (ushort)Species.Escavalier;
                            break;
                    }
                    toSend.HeldItem = 224;
                    if (botot != "SysBot")
                        toSend.HT_Name = botot;
                    else toSend.HT_Name = user;
                    toSend.HT_Language = 2;
                }
                else
                {
                    //#2 Evolve difficult to evolve Species (Evo Swap)
                    switch (toSend.Species)
                    {
                        case (ushort)Species.Farfetchd:
                            if (toSend.Form == 1)
                            {
                                toSend.Species = (ushort)Species.Sirfetchd;
                                toSend.Form = 0;
                            }
                            break;
                        case (ushort)Species.Yamask:
                            if (toSend.Form == 1)
                            {
                                toSend.Species = (ushort)Species.Runerigus;
                                toSend.Form = 0;
                                toSend.FormArgument = 50;
                            }
                            break;
                        case (ushort)Species.Sliggoo:
                            if (toSend.Form == 0) //Kalos
                                toSend.Species = (ushort)Species.Goodra;
                            break;
                    }
                }
                var RA = toSend.AbilityNumber switch
                {
                    1 => 0,
                    2 => 1,
                    3 => 2,
                    4 => 2,
                    _ => 2,
                };
                toSend.RefreshAbility(RA);

                //#3 Clear Nicknames
                if (!toSend.FatefulEncounter || toSend.Met_Location != 30001 || toSend.Tracker == 0)
                    toSend.ClearNickname();
                toSend.RefreshChecksum();

                var la2 = new LegalityAnalysis(toSend);
                if (la2.Valid)
                {
                    Log($"Swap Success. Sending back: {GameInfo.GetStrings(1).Species[toSend.Species]}.");
                    poke.TradeData = toSend;
                    counts.AddCompletedTrilogySwaps();
                    DumpPokemon(DumpSetting.DumpFolder, "trilogy", toSend);
                    await SetBoxPokemon(toSend, 0, 0, token, sav).ConfigureAwait(false);
                    await Task.Delay(2_500, token).ConfigureAwait(false);
                    return (toSend, PokeTradeResult.Success);
                }
                else //Safety Net incase something slips through
                {
                    msg = $"{user}, **{GameInfo.GetStrings(1).Species[toSend.Species]}** has a problem\n\n";
                    msg += $"__**Legality Analysis**__\n";
                    msg += la2.Report();
                    await SphealEmbed.EmbedAlertMessage(toSend, false, toSend.FormArgument, msg, "Bad Trilogy Swap").ConfigureAwait(false);
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", toSend);
                    return (toSend, PokeTradeResult.IllegalTrade);
                }
            }
            else
            {
                Log($"{user} is requesting Basic Clone for: {offers}");
                if (poke.Type == PokeTradeType.LinkSWSH)
                    poke.SendNotification(this, $"Cloned your {offers}");
                toSend.RefreshChecksum();
                counts.AddCompletedClones();
                await SetBoxPokemon(toSend, 0, 0, token, sav).ConfigureAwait(false);
                await Task.Delay(2_500, token).ConfigureAwait(false);
                DumpPokemon(DumpSetting.DumpFolder, "clone", toSend);
            }
            return (toSend, PokeTradeResult.Success);
        }
        private async Task<(PK8, bool)> SetTradePartnerDetailsSWSH(PokeTradeDetail<PK8> poke, PK8 toSend, PK8 offered, string trainerName, SAV8SWSH sav, CancellationToken token)
        {
            var data = await Connection.ReadBytesAsync(LinkTradePartnerNameOffset - 0x8, 8, token).ConfigureAwait(false);
            var tidsid = BitConverter.ToUInt32(data, 0);
            var cln = (PK8)toSend.Clone();
            var custom = Hub.Config.CustomSwaps;
            var counts = TradeSettings;
            var jumbo = (byte)rnd.Next(0, 6);
            var tiny = (byte)rnd.Next(0, 6);
            var Sword = (ushort)Species.Zacian;
            var Shield = (ushort)Species.Zamazenta;
            var version = data[4];
            var PIDla = new LegalityAnalysis(toSend);
            string[] ballItem = GameInfo.GetStrings(1).Item[offered.HeldItem].Split(' ');

            if (toSend.Species != (ushort)Species.Ditto || offered.HeldItem == (int)custom.OTSwapItem)
            {
                string pattern = "(yt\\.? | youtube\\.? | ttv\\.? | tv\\.?)";
                if (trainerName.Contains('.') || Regex.IsMatch(trainerName, pattern, RegexOptions.IgnoreCase))
                {
                    cln.OT_Name = Regex.Replace(trainerName, pattern, string.Empty, RegexOptions.IgnoreCase); //Gives their OT without the Ads in the name
                    cln.OT_Name = trainerName.Replace(".", string.Empty); //Allow users who accidentally have a fullstop in their IGN
                }
                else
                    cln.OT_Name = trainerName;
                cln.Version = data[4];
                cln.Language = data[5];
                cln.OT_Gender = data[6];

                if ((cln.Species == Sword && version == 45) || (cln.Species == Shield && version == 44)) //Box Legends OT
                {
                    cln.TrainerTID7 = (ushort)rnd.Next(1, 999999);
                    cln.TrainerSID7 = (ushort)rnd.Next(1, 4294);
                }
                else
                {
                    cln.TrainerTID7 = tidsid % 1_000_000;
                    cln.TrainerSID7 = tidsid / 1_000_000;
                }

                if (toSend.Egg_Location == 30002) //Eggs from Link Trade fixed via OTSwap
                    cln.Egg_Location = 60002; //Nursery (SWSH)
                if (toSend.IsEgg == false)
                {
                    if (cln.HeldItem > 0 && (cln.Species != (ushort)Species.Yamper || cln.Species != (ushort)Species.Spheal))
                        cln.ClearNickname();
                    else if (offered.HeldItem == (int)custom.OTSwapItem)
                        cln.ClearNickname();
                    else
                        cln.ClearNickname();
                    if (toSend.WasEgg)
                        cln.EggMetDate = cln.MetDate; //Ensure no date mismatch for users who want specifc hatch date
                }
                else //Set eggs received in Daycare, instead of received in Link Trade
                {
                    if (jumbo == 0)
                    {
                        cln.HeightScalar = 255;
                        Log($"Jumbo Size was given");
                    }
                    else if (tiny == 0)
                    {
                        cln.HeightScalar = 0;
                        Log($"Tiny Size was given");
                    }
                    cln.IsNicknamed = true;
                    cln.Nickname = cln.Language switch
                    {
                        1 => "タマゴ",
                        3 => "Œuf",
                        4 => "Uovo",
                        5 => "Ei",
                        7 => "Huevo",
                        8 => "알",
                        9 or 10 => "蛋",
                        _ => "Egg",
                    };
                }

                if (!toSend.FatefulEncounter && ballItem.Length > 1 && ballItem[1] == "Ball") //Distro Ball Selector, account for OT-able event natives
                {
                    if (ballItem[0] == "Master")
                    {
                        if (toSend.IsEgg || toSend.WasEgg)
                            Log($"Eggs (hatched or not) cannot be in Master Ball");
                    }
                    else
                    {
                        if (ballItem[0] == "Poké") //Account for Pokeball having an apostrophe
                            ballItem[0] = "Poke";
                        cln.Ball = (int)(Ball)Enum.Parse(typeof(Ball), ballItem[0]);
                        Log($"Ball swapped to: {(Ball)cln.Ball} Ball");
                    }
                }
                //OT for Overworld8 (Galar Birds/Swords of Justice/Marked mons/Wild Grass)
                if (PIDla.Info.PIDIV.Type == PIDType.Overworld8)
                {
                    if (toSend.IsShiny)
                        cln.PID = (((uint)(cln.TID16 ^ cln.SID16) ^ (cln.PID & 0xFFFF) ^ 0) << 16) | (cln.PID & 0xFFFF);
                    else
                        cln.PID = cln.PID; //Do nothing as non shiny
                }
                else
                {
                    if (toSend.IsShiny)
                    {
                        if (toSend.ShinyXor == 0) //Ensure proper shiny type is rerolled
                        {
                            do
                            {
                                cln.SetShiny();
                            } while (cln.ShinyXor != 0);
                        }
                        else
                        {
                            do
                            {
                                cln.SetShiny();
                            } while (cln.ShinyXor != 1);
                        }
                        if (toSend.Met_Location == 244)  //Dynamax Adventures
                        {
                            do
                            {
                                cln.SetShiny();
                            } while (cln.ShinyXor != 1);
                        }
                    }
                    else if (toSend.Met_Location != 162 && toSend.Met_Location != 244) //If not Max Raid, reroll PID for non shiny 
                    {
                        cln.SetShiny();
                        cln.SetUnshiny();
                    }
                    if (toSend.Met_Location != 162 && toSend.Met_Location != 244) //Leave Max Raid EC alone
                        cln.SetRandomEC();
                }
                cln.RefreshChecksum();
            }
            var tradeswsh = new LegalityAnalysis(cln); //Legality check, if fail, sends original PK8 instead
            if (tradeswsh.Valid)
            {
                if (cln.Species != (ushort)Species.Ditto && !custom.LogTrainerDetails && !custom.FilterLogging) //So it does not log twice
                {
                    Log($"OT info swapped to:");
                    Log($"OT_Name: {cln.OT_Name}");
                    Log($"TID: {cln.TrainerTID7:000000}");
                    Log($"SID: {cln.TrainerSID7:0000}");
                    Log($"Gender: {(Gender)cln.OT_Gender}");
                    Log($"Language: {(LanguageID)(cln.Language)}");
                    if (!cln.IsEgg)
                        Log($"Game: {GameInfo.GetStrings(1).gamelist[cln.Version]}");
                }
                Log($"OT Swap success");
                if (offered.HeldItem == (int)custom.OTSwapItem)
                {
                    DumpPokemon(DumpSetting.DumpFolder, "OTSwaps", cln);
                    counts.AddCompletedOTSwaps();
                }
                poke.TradeData = cln;
                return (cln, true);
            }
            else
            {
                Log($"Sending original Pokémon as it can't be OT swapped");
                if (toSend.FatefulEncounter && !custom.FilterLogging)
                    Log($"Reason: Fateful Encounter");
            return (toSend, false);
            }
        }
        private async Task<(PK8 offered, PokeTradeResult check)> HandleTradeEvo(PokeTradeDetail<PK8> poke, PK8 offered, PK8 toSend, PartnerDataHolder partner, CancellationToken token)
        {
            bool isDistribution = true;
            var list = isDistribution ? PreviousUsersDistribution : PreviousUsers;
            var listCool = UserCooldowns;
            var listEvo = EvoTracker;
            var trainerNID = await GetTradePartnerNID(token).ConfigureAwait(false);
            var cd = AbuseSettings.TradeCooldown;
            var user = partner.TrainerName;
            var offers = GameInfo.GetStrings(1).Species[offered.Species];
            int attempts;
            attempts = listEvo.TryInsert(trainerNID, user);

            list.TryRegister(trainerNID, partner.TrainerName);

            Log($"{user} is trying to give a trade evolution ({offered.Species})");
            if (poke.Type == PokeTradeType.LinkSWSH)
                poke.SendNotification(this, $"```No Trade Evolutions\nAttach an everstone to allow trading```");
            var msg = $"\n{user} is trying to give a trade evolution\n";
            msg += $"\nEquip an Everstone on **{offers}** to allow trade";
            await SphealEmbed.EmbedTradeEvoMsg(offered, false, offered.FormArgument, msg, "Illegal Activity", attempts, AbuseSettings.RepeatConnections).ConfigureAwait(false);

            if (AbuseSettings.AutoBanCooldown && cd == 0)
            {
                if (attempts >= AbuseSettings.RepeatConnections)
                {
                    if (poke.Type == PokeTradeType.LinkSWSH)
                        poke.SendNotification(this, $"```No Trade Evolutions\nYou are now banned for 3 days```");
                    DateTime expires = DateTime.Now.AddDays(3);
                    string expiration = $"{expires:yyyy.MM.dd hh:mm:ss}";
                    AbuseSettings.BannedIDs.AddIfNew(new[] { GetReference(user, trainerNID, "Autobanned for tradeEvo", expiration) });
                    msg = $"\n{user} tried to give a trade evolution too many times\n";
                    msg += $"\nNo punishment evasion, They are now banned for 3 days";
                    await SphealEmbed.EmbedTradeEvoMsg(offered, false, offered.FormArgument, msg, "Trade Evo Ban", attempts, AbuseSettings.RepeatConnections, true).ConfigureAwait(false);
                }
            }
            return (toSend, PokeTradeResult.TradeEvo);
        }
    }
}