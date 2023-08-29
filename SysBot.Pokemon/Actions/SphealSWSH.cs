using Discord;
using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
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
            await SetTradePartnerDetailsSWSH(toSend, offered, partner.TrainerName, sav, token).ConfigureAwait(false);
            await SetBoxPokemon(toSend, 0, 0, token, sav).ConfigureAwait(false);
            await Task.Delay(2_500, token).ConfigureAwait(false);
            poke.TradeData = toSend;

            myst = $"**{user}** has received a Mystery Egg !\n";
            myst += $"**Don't reveal if you want the surprise**\n\n";
            myst += $"**Pokémon**: ||**{GameInfo.GetStrings(1).Species[toSend.Species]}**||\n";
            myst += $"**Gender**: ||**{(Gender)toSend.Gender}**||\n";
            myst += $"**Shiny**: ||**{Shiny}**||\n";
            myst += $"**Nature**: ||**{(Nature)toSend.Nature}**||\n";
            myst += $"**Ability**: ||**{(Ability)toSend.Ability}**||\n";
            myst += $"**IVs**: ||**{toSend.IV_HP}/{toSend.IV_ATK}/{toSend.IV_DEF}/{toSend.IV_SPA}/{toSend.IV_SPD}/{toSend.IV_SPE}**||\n";
            myst += $"**Language**: ||**{(LanguageID)toSend.Language}**||";

            EchoUtil.EchoEmbed(Sphealcl.EmbedEggMystery(toSend, myst, $"{user}'s Mystery Egg"));
            counts.AddCompletedMystery();
            return (toSend, PokeTradeResult.Success);
        }
        private async Task<(PK8 toSend, PokeTradeResult check)> HandleCustomSwaps(SAV8SWSH sav, PokeTradeDetail<PK8> poke, PK8 offered, PK8 toSend, PartnerDataHolder partner, CancellationToken token)
        {
            var custom = Hub.Config.CustomSwaps;
            var counts = TradeSettings;
            var swap = offered.HeldItem;
            var user = partner.TrainerName;
            var offer = offered.Species;
            var la = new LegalityAnalysis(offered);
            var notball = new List<int>
            {
                228, //Smoke Ball
                236, //Light Ball
                278, //Iron Ball
                541, //Air Balloon
            };

            toSend = offered.Clone();
            string? msg;
            string[] ballItem = GameInfo.GetStrings(1).Item[swap].Split(' ');

            if (!la.Valid)
            {
                if (poke.Type == PokeTradeType.LinkSWSH)
                    poke.SendNotification(this, $"__**Legality Analysis**__\n{la.Report()}");
                msg = $"{user}, **{(Species)offer}** is not legal\n";
                msg += $"Features cannot be used\n\n";
                msg += $"__**Legality Report**__\n";
                msg += la.Report();
                await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Illegal Request").ConfigureAwait(false);
                DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);
                return (toSend, PokeTradeResult.IllegalTrade);
            }
            else if (swap == (int)custom.OTSwapItem)
            {
                var result = await SetTradePartnerDetailsSWSH(toSend, offered, partner.TrainerName, sav, token).ConfigureAwait(false);
                Log($"{user} is requesting OT swap for: {GameInfo.GetStrings(1).Species[offer]} with OT Name: {offered.OT_Name}");
                toSend.Tracker = 0; //We clean the tracker since we only do the Origin Game

                if (result.Item2 == false)
                {
                    //Non SWSH should get rejected
                    if (poke.Type == PokeTradeType.LinkSWSH)
                        poke.SendNotification(this, $"```{user}, {(Species)offer} cannot be OT swap\nPokémon is either:\n1) Not SWSH native\n2) SWSH Event/In-game trade with FIXED OT```");
                    msg = $"{user}, **{(Species)offer}** cannot be OT swap";
                    msg += $"\nOriginal OT: {offered.OT_Name}";
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
                Log($"{user} is requesting Ball swap for: {GameInfo.GetStrings(1).Species[offer]}");

                if ((GameVersion)toSend.Version == GameVersion.SW || (GameVersion)toSend.Version == GameVersion.SH)
                {
                    toSend.Tracker = 0;
                    toSend.Ball = (int)(Ball)Enum.Parse(typeof(Ball), ballItem[0]);
                    toSend.RefreshChecksum();
                    Log($"Ball swapped to: {(Ball)toSend.Ball}");

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
                            poke.SendNotification(this, $"```{user}, {(Species)offer} cannot be in {(Ball)toSend.Ball}```");
                        msg = $"{user}, **{(Species)offer}** cannot be in **{(Ball)toSend.Ball}**\n";
                        msg += $"The ball cannot be swapped";
                        await SphealEmbed.EmbedAlertMessage(toSend, false, toSend.FormArgument, msg, "Bad Ball Swap").ConfigureAwait(false);
                        DumpPokemon(DumpSetting.DumpFolder, "hacked", toSend);
                        return (toSend, PokeTradeResult.TrainerRequestBad);
                    }
                }
                else
                {
                    if (poke.Type == PokeTradeType.LinkSWSH)
                        poke.SendNotification(this, $"```{user}, {(Species)offer} cannot be Ball Swap\nReason: Not from SWSH```");
                    msg = $"{user}, **{(Species)offer}** is not SWSH native & cannot be swapped due to Home Tracker";
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);
                    await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Bad Ball Swap").ConfigureAwait(false);
                    return (offered, PokeTradeResult.TrainerRequestBad);
                }
            }
            else if (swap == (int)custom.TrilogySwapItem || swap == 229) //Trilogy Swap for existing mons (Level/Nickname/Evolve)
            {
                Log($"{user} is requesting Trilogy swap for: {GameInfo.GetStrings(1).Species[offer]}");

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
                }
                else
                {
                    //#2 Evolve difficult to evolve Species (Evo Swap)
                    switch (toSend.Species)
                    {
                        case (ushort)Species.Farfetchd:
                            if (toSend.Form == 1)
                                toSend.Species = (ushort)Species.Sirfetchd;
                            break;
                        case (ushort)Species.Yamask:
                            if (toSend.Form == 1)
                            {
                                toSend.Species = (ushort)Species.Runerigus;
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
                if (!toSend.FatefulEncounter || toSend.Met_Location != 30001)
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
                    msg = $"{user}, **{(Species)toSend.Species}** has a problem\n\n";
                    msg += $"__**Legality Analysis**__\n";
                    msg += la2.Report();
                    await SphealEmbed.EmbedAlertMessage(toSend, false, toSend.FormArgument, msg, "Bad Trilogy Swap").ConfigureAwait(false);
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", toSend);
                    return (toSend, PokeTradeResult.IllegalTrade);
                }
            }
            else
            {
                Log($"{user} is requesting Basic Clone for: {GameInfo.GetStrings(1).Species[offer]}");
                if (poke.Type == PokeTradeType.LinkSWSH)
                    poke.SendNotification(this, $"Cloned your {GameInfo.GetStrings(1).Species[offer]}");
                toSend.RefreshChecksum();
                counts.AddCompletedClones();
                await SetBoxPokemon(toSend, 0, 0, token, sav).ConfigureAwait(false);
                await Task.Delay(2_500, token).ConfigureAwait(false);
                DumpPokemon(DumpSetting.DumpFolder, "clone", toSend);
            }
            return (toSend, PokeTradeResult.Success);
        }
        private async Task<(PK8, bool)> SetTradePartnerDetailsSWSH(PK8 toSend, PK8 offered, string trainerName, SAV8SWSH sav, CancellationToken token)
        {
            var data = await Connection.ReadBytesAsync(LinkTradePartnerNameOffset - 0x8, 8, token).ConfigureAwait(false);
            var tidsid = BitConverter.ToUInt32(data, 0);
            var cln = (PK8)toSend.Clone();
            var custom = Hub.Config.CustomSwaps;
            var counts = TradeSettings;
            string[] ballItem = GameInfo.GetStrings(1).Item[offered.HeldItem].Split(' ');

            cln.TrainerTID7 = tidsid % 1_000_000;
            cln.TrainerSID7 = tidsid / 1_000_000;
            cln.OT_Name = trainerName;
            cln.Version = data[4];
            cln.Language = data[5];
            cln.OT_Gender = data[6];

            if (toSend.IsEgg == false)
            {
                if (cln.HeldItem > 0 && cln.Species != (ushort)Species.Yamper || cln.Species != (ushort)Species.Spheal)
                    cln.ClearNickname();
                else if (cln.HeldItem == (int)custom.OTSwapItem)
                    cln.ClearNickname();
                else
                    cln.ClearNickname();
                if (toSend.WasEgg && toSend.Egg_Location == 30002) //Hatched Eggs from Link Trade fixed via OTSwap
                    cln.Egg_Location = 60002; //Nursery (SWSH)
            }
            else //Set eggs received in Daycare, instead of received in Link Trade
            {
                cln.HT_Name = "";
                cln.HT_Language = 0;
                cln.HT_Gender = 0;
                cln.CurrentHandler = 0;
                cln.Met_Location = 0;
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

            if (ballItem.Length > 1 && ballItem[1] == "Ball") //Distro Ball Selector
            {
                cln.Ball = (int)(Ball)Enum.Parse(typeof(Ball), ballItem[0]);
                Log($"Ball swapped to: {(Ball)cln.Ball}");
            }
            //OT for Overworld8 (Galar Birds/Swords of Justice/Marked mons)
            if (toSend.HasMarkEncounter8 || toSend.Species == (ushort)Species.Keldeo || toSend.Species == (ushort)Species.Cobalion || toSend.Species == (ushort)Species.Terrakion || toSend.Species == (ushort)Species.Virizion || toSend.Species == (ushort)Species.Zapdos && toSend.Form == 1 || toSend.Species == (ushort)Species.Moltres && toSend.Form == 1 || toSend.Species == (ushort)Species.Articuno && toSend.Form == 1)
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
                else if (cln.Met_Location != 162 || cln.Met_Location != 244) //If not Max Raid, reroll PID for non shiny 
                {
                    cln.SetShiny();
                    cln.SetUnshiny();
                }
                if (cln.Met_Location != 162 || cln.Met_Location != 244) //Leave Max Raid EC alone
                    cln.SetRandomEC();
            }
            cln.RefreshChecksum();

            var tradeswsh = new LegalityAnalysis(cln); //Legality check, if fail, sends original PK8 instead
            if (tradeswsh.Valid)
            {
                if (custom.LogTrainerDetails == false) //So it does not log twice
                {
                    Log($"OT info swapped to:");
                    Log($"OT_Name: {cln.OT_Name}");
                    Log($"TID: {cln.TrainerTID7}");
                    Log($"SID: {cln.TrainerSID7}");
                    Log($"Gender: {(Gender)cln.OT_Gender}");
                    Log($"Language: {(LanguageID)(cln.Language)}");
                    Log($"Game: {(GameVersion)(cln.Version)}");
                }
                Log($"OT Swap success");
                if (toSend.HeldItem == (int)custom.OTSwapItem)
                {
                    DumpPokemon(DumpSetting.DumpFolder, "OTSwaps", cln);
                    counts.AddCompletedOTSwaps();
                }
                return (cln, true);
            }
            else
            {
                Log($"Sending original Pokémon as it can't be OT swapped");
                return (toSend, false);
            }
        }
    }
}