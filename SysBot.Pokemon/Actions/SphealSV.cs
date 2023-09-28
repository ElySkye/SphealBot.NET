using PKHeX.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using SysBot.Base;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SysBot.Pokemon
{
    public partial class PokeTradeBotSV : PokeRoutineExecutor9SV, ICountBot
    {
        private async Task<(PK9 toSend, PokeTradeResult check)> HandleMysteryEggs(SAV9SV sav, PokeTradeDetail<PK9> poke, PK9 offered, PK9 toSend, PartnerDataHolder partner, CancellationToken token)
        {
            var counts = TradeSettings;
            var user = partner.TrainerName;

            string? myst;
            PK9? rnd;
            do
            {
                rnd = Hub.Ledy.Pool.GetRandomTrade();
            } while (!rnd.IsEgg);
            toSend = rnd;

            var Size = toSend.Scale switch
            {
                255 => "Jumbo",
                0 => "Tiny",
                _ => "Average",
            };
            var Shiny = toSend.IsShiny switch
            {
                true => "Shiny",
                false => "Non-Shiny",
            };

            Log($"Sending Surprise Egg: {Shiny} {Size} {(Gender)toSend.Gender} {GameInfo.GetStrings(1).Species[toSend.Species]}");
            await SetTradePartnerDetailsSV(poke, toSend, offered, sav, token).ConfigureAwait(false);
            poke.TradeData = toSend;

            myst = $"**{user}** has received a Mystery Egg !\n";
            myst += $"**Don't reveal if you want the surprise**\n\n";
            myst += $"||**Pokémon**: **{GameInfo.GetStrings(1).Species[toSend.Species]}**\n";
            myst += $"**Gender**: **{(Gender)toSend.Gender}**\n";
            myst += $"**Shiny**: **{Shiny}**\n";
            myst += $"**Size**: **{Size}**\n";
            myst += $"**Nature**: **{(Nature)toSend.Nature}**\n";
            myst += $"**Ability**: **{(Ability)toSend.Ability}**\n";
            myst += $"**IVs**: **{toSend.IV_HP}/{toSend.IV_ATK}/{toSend.IV_DEF}/{toSend.IV_SPA}/{toSend.IV_SPD}/{toSend.IV_SPE}**\n";
            myst += $"**Language**: **{(LanguageID)toSend.Language}**||";

            EchoUtil.EchoEmbed(Sphealcl.EmbedEggMystery(toSend, myst, $"{user}'s Mystery Egg"));
            counts.AddCompletedMystery();
            return (toSend, PokeTradeResult.Success);
        }

        private async Task<(PK9 toSend, PokeTradeResult)> HandleCustomSwaps(SAV9SV sav, PokeTradeDetail<PK9> poke, PK9 offered, PK9 toSend, PartnerDataHolder partner, CancellationToken token)
        {
            var custom = Hub.Config.CustomSwaps;
            var counts = TradeSettings;
            var swap = offered.HeldItem;
            var user = partner.TrainerName;
            var offer = offered.Species;
            var nick = offered.Nickname;
            var loc = toSend.Met_Location;
            var botot = Hub.Config.Legality.GenerateOT;
            var la = new LegalityAnalysis(offered);

            var evSwap = new List<int>
            {
                (int)custom.EVResetItem,
                (int)custom.EVRaidAtkItem,
                (int)custom.EVCompAtkItem,
                (int)custom.EVRaidSPAItem,
                (int)custom.EVCompSPAItem,
                (int)custom.EVGenDEFItem,
                (int)custom.EVGenSPDItem,
            };
            var PLAevo = new List<ushort>
            {
                (ushort)Species.Ursaluna,
                (ushort)Species.Wyrdeer,
                (ushort)Species.Overqwil,
                (ushort)Species.Kleavor,
            };
            var Formevo = new List<ushort>
            {
                (ushort)Species.Typhlosion,
                (ushort)Species.Samurott,
                (ushort)Species.Decidueye,
                (ushort)Species.Sliggoo,
                (ushort)Species.Goodra,
                (ushort)Species.Slowbro,
                (ushort)Species.Slowking,
                (ushort)Species.Avalugg,
                (ushort)Species.Braviary,
                (ushort)Species.Lilligant,
                (ushort)Species.Weezing,
            };
            var notball = new List<int>
            {
                228, //Smoke Ball
                236, //Light Ball
                278, //Iron Ball
                541, //Air Balloon
            };

            string[] teraItem = GameInfo.GetStrings(1).Item[swap].Split(' ');
            string[] ballItem = GameInfo.GetStrings(1).Item[swap].Split(' ');
            string? msg;
            toSend = offered.Clone();

            if (swap == (int)custom.OTSwapItem || ballItem.Length > 1 && ballItem[1] == "Ball" || swap == (int)custom.GenderSwapItem || Enum.TryParse(nick, true, out Ball _))
            {
                //Allow Ursaluna Bloodmoon & 7 star Hisuian Decidueye
                if (PLAevo.Contains(offer) && offered.Form == 0 || Formevo.Contains(offer) && offered.Form != 0 && offered.RibbonMarkMightiest != true) //Check for species that require to be moved out of SV to evolve
                {
                    if (poke.Type == PokeTradeType.LinkSV)
                        poke.SendNotification(this, $"```Request Denied - Bot will not swap Home Tracker Pokémon for OT/Ball/Gender```");
                    msg = $"{user}, **{(Species)offer}** cannot be swapped due to Home Tracker\n";
                    msg += $"Features cannot be used for OT/Ball/Gender Swap";
                    await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Invalid Request").ConfigureAwait(false);
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);
                    return (toSend, PokeTradeResult.TrainerRequestBad);
                }
            }
            if (!la.Valid)
            {
                if (poke.Type == PokeTradeType.LinkSV)
                    poke.SendNotification(this, $"__**Legality Analysis**__\n```{la.Report()}```");
                msg = $"{user}, **{(Species)offer}** is not legal\n";
                msg += $"Features cannot be used\n\n";
                msg += $"__**Legality Analysis**__\n";
                msg += la.Report();
                await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Illegal Request").ConfigureAwait(false);
                DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);
                return (toSend, PokeTradeResult.IllegalTrade);
            }
            else if (swap == (int)custom.OTSwapItem) //OT Swap for existing mons
            {
                Log($"{user} is requesting OT swap for: {GameInfo.GetStrings(1).Species[offer]} with OT Name: {offered.OT_Name}");

                toSend.Tracker = 0; //We clean the tracker since we only do the Origin Game

                if (!await SetTradePartnerDetailsSV(poke, toSend, offered, sav, token).ConfigureAwait(false))
                {
                    //Non SV should get rejected
                    if (poke.Type == PokeTradeType.LinkSV)
                        poke.SendNotification(this, $"```{user}, {(Species)offer} cannot be OT swap\n\nPokémon is either:\n1) Not SV native\n2) SV Event/In-game trade with FIXED OT```");
                    msg = $"{user}, **{(Species)offer}** cannot be OT swap\n\n";
                    msg += "Pokémon is either:\n1) Not SV native\n2) SV Event/In-game trade with FIXED OT\n\n";
                    msg += $"Original OT: **{offered.OT_Name}**";
                    await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Bad OT Swap").ConfigureAwait(false);
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", toSend);
                    return (toSend, PokeTradeResult.TrainerRequestBad);
                }
                poke.TradeData = toSend;
                return (toSend, PokeTradeResult.Success);
            }
            //Ball Swapper
            else if (ballItem.Length > 1 && ballItem[1] == "Ball" && !notball.Contains(swap))
            {
                if (toSend.Generation != 9)
                {
                    if (poke.Type == PokeTradeType.LinkSV)
                        poke.SendNotification(this, $"```{user}, {(Species)offer} cannot be Ball Swap\nReason: Not from SV```");
                    msg = $"{user}, **{(Species)offer}** is not SV native & cannot be swapped due to Home Tracker";
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);
                    await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Bad Ball Swap").ConfigureAwait(false);
                    return (offered, PokeTradeResult.TrainerRequestBad);
                }
                else
                {
                    if (Enum.TryParse(nick, true, out MoveType _)) //Double Swap for Tera
                    {
                        Log($"{user} is requesting Double swap for: {GameInfo.GetStrings(1).Species[offer]}");
                        toSend.TeraTypeOverride = (MoveType)Enum.Parse(typeof(MoveType), nick, true);
                        toSend.ClearNickname();
                        counts.AddCompletedDoubleSwaps();
                        Log($"Tera swapped to {toSend.TeraTypeOverride}");
                    }
                    else
                        Log($"{user} is requesting Ball swap for: {GameInfo.GetStrings(1).Species[offer]}");

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
                        await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
                        await Task.Delay(2_500, token).ConfigureAwait(false);
                        return (toSend, PokeTradeResult.Success);
                    }
                    else
                    {
                        if (poke.Type == PokeTradeType.LinkSV)
                        {
                            if (toSend.WasEgg && toSend.Ball == 1)
                                poke.SendNotification(this, $"```{user}, {(Species)offer} is from an egg & cannot be in {(Ball)toSend.Ball}```");
                            else
                                poke.SendNotification(this, $"```{user}, {(Species)offer} cannot be in {(Ball)toSend.Ball}```");
                        }
                        msg = $"{user}, **{(Species)offer}** cannot be in **{(Ball)toSend.Ball}**\n";
                        if (toSend.WasEgg && toSend.Ball == 1)
                            msg += "Egg hatches cannot be in **Master Ball**\n";
                        msg += "The ball cannot be swapped";
                        await SphealEmbed.EmbedAlertMessage(toSend, false, toSend.FormArgument, msg, "Bad Ball Swap").ConfigureAwait(false);
                        DumpPokemon(DumpSetting.DumpFolder, "hacked", toSend);
                        return (toSend, PokeTradeResult.TrainerRequestBad);
                    }
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
                            if (offered.Form == 0)
                                toSend.Form = 0;
                            else if (offered.Form == 1)
                                toSend.Form = 1;
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
                        case (ushort)Species.Poliwhirl:
                            toSend.Species = (ushort)Species.Politoed;
                            break;
                        case (ushort)Species.Slowpoke:
                            toSend.Species = (ushort)Species.Slowking;
                            break;
                    }
                    toSend.HeldItem = 1882;
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
                        case (ushort)Species.Finizen:
                            toSend.Species = (ushort)Species.Palafin;
                            break;
                        case (ushort)Species.Rellor:
                            toSend.Species = (ushort)Species.Rabsca;
                            break;
                        case (ushort)Species.Pawmo:
                            toSend.Species = (ushort)Species.Pawmot;
                            break;
                        case (ushort)Species.Bramblin:
                            toSend.Species = (ushort)Species.Brambleghast;
                            break;
                        case (ushort)Species.Sliggoo:
                            if (offered.Form == 0) //Kalos
                                toSend.Species = (ushort)Species.Goodra;
                            break;
                        case (ushort)Species.Gimmighoul:
                            toSend.Form = 0; //Account for POGO Gimmighoul
                            toSend.Species = (ushort)Species.Gholdengo;
                            toSend.FormArgument = 999;
                            break;
                        case (ushort)Species.Primeape:
                            toSend.Species = (ushort)Species.Annihilape;
                            toSend.FormArgument = 20;
                            break;
                        case (ushort)Species.Bisharp:
                            toSend.Species = (ushort)Species.Kingambit;
                            toSend.FormArgument = 3;
                            break;
                        case (ushort)Species.Basculin:
                            if (offered.Form == 2) //White
                            {
                                toSend.Species = (ushort)Species.Basculegion;
                                if (offered.Gender == 0) //Male
                                {
                                    toSend.Form = 0;
                                    toSend.Gender = 0;
                                }
                                else if (offered.Gender == 1) //Female
                                {
                                    toSend.Form = 1;
                                    toSend.Gender = 1;
                                }
                                toSend.FormArgument = 300;
                            }
                            break;
                        //Item Trade Evos
                        case (ushort)Species.Feebas:
                            toSend.Species = (ushort)Species.Milotic;
                            break;
                        case (ushort)Species.Scyther:
                            toSend.Species = (ushort)Species.Scizor;
                            break;
                        case (ushort)Species.Dusclops:
                            toSend.Species = (ushort)Species.Dusknoir;
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
                if (!toSend.FatefulEncounter || loc != 30001)
                    toSend.ClearNickname();
                toSend.RefreshChecksum();

                var la2 = new LegalityAnalysis(toSend);
                if (la2.Valid)
                {
                    Log($"Swap Success. Sending back: {GameInfo.GetStrings(1).Species[toSend.Species]}.");
                    poke.TradeData = toSend;
                    counts.AddCompletedTrilogySwaps();
                    DumpPokemon(DumpSetting.DumpFolder, "trilogy", toSend);
                    await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
                    await Task.Delay(2_500, token).ConfigureAwait(false);
                    return (toSend, PokeTradeResult.Success);
                }
                else //Safety Net
                {
                    msg = $"{user}, {(Species)toSend.Species} has a problem\n\n";
                    msg += $"__**Legality Analysis**__\n";
                    msg += la2.Report();
                    await SphealEmbed.EmbedAlertMessage(toSend, false, toSend.FormArgument, msg, "Bad Trilogy Swap").ConfigureAwait(false);
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", toSend);
                    return (toSend, PokeTradeResult.IllegalTrade);
                }
            }
            else if (swap > 0 && evSwap.Contains(swap)) //EV Presets Swap
            {
                //EVs, Either reset or apply chosen preset, totalling to 504, User can apply final 6 themself
                Log($"{user} is requesting EV swap for: {GameInfo.GetStrings(1).Species[offer]}");
                //Format => HP/ATK/DEF/SPE/SPA/SPD (Read Carefully)
                int[] evReset = new int[] { 0, 0, 0, 0, 0, 0 };
                int[] RaidAtk = new int[] { 252, 252, 0, 0, 0, 0 };
                int[] CompAtk = new int[] { 0, 252, 0, 252, 0, 0 };
                int[] RaidSPA = new int[] { 252, 0, 0, 0, 252, 0 };
                int[] CompSPA = new int[] { 0, 0, 0, 252, 252, 0 };
                int[] GenDef = new int[] { 252, 0, 252, 0, 0, 0 };
                int[] GenSPD = new int[] { 252, 0, 0, 0, 0, 252 };

                Log($"Resetting EVs...");
                toSend.SetEVs(evReset);

                if (swap == (int)custom.EVRaidAtkItem)
                {
                    Log($"Type of EV Swap: Raid Atk");
                    toSend.SetEVs(RaidAtk);
                    toSend.StatNature = 3; //Adamant
                }
                else if (swap == (int)custom.EVCompAtkItem)
                {
                    Log($"Type of EV Swap: Comp Atk");
                    toSend.SetEVs(CompAtk);
                }
                else if (swap == (int)custom.EVRaidSPAItem)
                {
                    Log($"Type of EV Swap: Raid SPAtk");
                    toSend.SetEVs(RaidSPA);
                    toSend.StatNature = 15; //Modest
                }
                else if (swap == (int)custom.EVCompSPAItem)
                {
                    Log($"Type of EV Swap: Comp SPAtk");
                    toSend.SetEVs(CompSPA);
                }
                else if (swap == (int)custom.EVGenDEFItem)
                {
                    Log($"Type of EV Swap: Generic Def");
                    toSend.SetEVs(GenDef);
                }
                else if (swap == (int)custom.EVGenSPDItem)
                {
                    Log($"Type of EV Swap: Generic SPDef");
                    toSend.SetEVs(GenSPD);
                }
                toSend.RefreshChecksum();

                var la2 = new LegalityAnalysis(toSend);
                if (la2.Valid)
                {
                    Log($"EV Swap Success");
                    poke.TradeData = toSend;
                    counts.AddCompletedEVSwaps();
                    await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
                    await Task.Delay(2_500, token).ConfigureAwait(false);
                    return (toSend, PokeTradeResult.Success);
                }
                else //Safety Net
                {
                    msg = $"{user}, {(Species)toSend.Species} has a problem\n\n";
                    msg += $"__**Legality Analysis**__\n";
                    msg += la2.Report();
                    await SphealEmbed.EmbedAlertMessage(toSend, false, toSend.FormArgument, msg, "Bad EV Swap").ConfigureAwait(false);
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", toSend);
                    return (toSend, PokeTradeResult.IllegalTrade);
                }
            }
            //Gender Swap - Only SV Natives & not Tera9
            else if (swap == (int)custom.GenderSwapItem)
            {
                Log($"{user} is requesting Gender Swap for: {(Gender)offered.Gender} {GameInfo.GetStrings(1).Species[offer]}");

                if (offered.Gender == 2)
                {
                    msg = $"{user},\n";
                    msg += $"Why are you trying to Swap a *{(Gender)offered.Gender}* **{(Species)offer}**?";
                    await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Bad Gender Swap").ConfigureAwait(false);
                    return (toSend, PokeTradeResult.IllegalTrade);
                }
                if (toSend.Generation != 9 || loc == 30024)
                {
                    if (poke.Type == PokeTradeType.LinkSV)
                        poke.SendNotification(this, $"```{user}, {(Species)offer} cannot be Gender Swap\nReason: Not from SV or from a Raid```");
                    msg = $"{user}, **{(Species)offer}** is not SV native & cannot be swapped due to Home Tracker / Raidmon";
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);
                    await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Bad Gender Swap").ConfigureAwait(false);
                    return (offered, PokeTradeResult.TrainerRequestBad);
                }
                else
                {
                    toSend.Tracker = 0;
                    if (toSend.Gender == 0) //Male to Female
                        toSend.Gender = 1;
                    else if (toSend.Gender == 1) //Female to Male
                        toSend.Gender = 0;

                    if (offered.IsShiny)
                        toSend.SetShiny();
                    else
                    {
                        toSend.SetShiny();
                        toSend.SetUnshiny();
                    }
                    if (toSend.Species == (ushort)Species.Dunsparce || toSend.Species == (ushort)Species.Tandemaus) //Keep EC to maintain form
                    {
                        if (offered.EncryptionConstant % 100 == 0)
                            toSend = KeepECModable(toSend);
                    }
                    else
                        toSend.SetRandomEC();
                    toSend.RefreshChecksum();

                    var la2 = new LegalityAnalysis(toSend);
                    if (la2.Valid)
                    {
                        Log($"Gender swapped to: {(Gender)toSend.Gender}");
                        poke.TradeData = toSend;
                        counts.AddCompletedGenderSwaps();
                        await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
                        await Task.Delay(2_500, token).ConfigureAwait(false);
                        return (toSend, PokeTradeResult.Success);
                    }
                    else
                    {
                        if (poke.Type == PokeTradeType.LinkSV)
                        {
                            poke.SendNotification(this, $"```{user}, {(Species)toSend.Species} cannot be that Gender```");
                            if (toSend.FatefulEncounter)
                                poke.SendNotification(this, $"```{user}, {(Species)toSend.Species} gender is locked by the Event it's from```");
                        }
                        msg = $"{user}, **{(Species)toSend.Species}** cannot be that Gender";
                        if (toSend.FatefulEncounter)
                            msg += $"Gender is locked by the Event it's from";
                        await SphealEmbed.EmbedAlertMessage(toSend, false, offered.FormArgument, msg, "Bad Gender Swap").ConfigureAwait(false);
                        DumpPokemon(DumpSetting.DumpFolder, "hacked", toSend);
                        return (toSend, PokeTradeResult.IllegalTrade);
                    }
                }
            }
            //Power Swap - Max all moves PP & Gives relearn TMs
            else if (swap == (int)custom.PowerSwapItem)
            {
                Log($"{user} is requesting Power Swap for: {GameInfo.GetStrings(1).Species[offer]}");

                toSend.SetMaximumPPUps(); //Max PP Ups
                toSend.HealPP();
                toSend.SetRecordFlagsAll(); //TM Relearns (Only learnable ones)
                toSend.RefreshChecksum();

                var la2 = new LegalityAnalysis(toSend);
                if (la2.Valid)
                {
                    Log($"Power Swap Success.");
                    poke.TradeData = toSend;
                    counts.AddCompletedPowerSwaps();
                    await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
                    await Task.Delay(2_500, token).ConfigureAwait(false);
                    return (toSend, PokeTradeResult.Success);
                }
                else //Safety Net
                {
                    msg = $"{user}, {(Species)toSend.Species} has a problem\n\n";
                    msg += $"__**Legality Analysis**__\n";
                    msg += la2.Report();
                    await SphealEmbed.EmbedAlertMessage(toSend, false, toSend.FormArgument, msg, "Bad Power Swap").ConfigureAwait(false);
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", toSend);
                    return (toSend, PokeTradeResult.IllegalTrade);
                }
            }
            else if (swap == (int)custom.SizeSwapItem) //Size Swap, only SV Natives & not Tera9
            {
                Log($"{user} is requesting Size Swap for: {GameInfo.GetStrings(1).Species[offer]}");

                if (toSend.Generation != 9 || loc == 30024 || loc == 30001 || toSend.FatefulEncounter)
                {
                    if (poke.Type == PokeTradeType.LinkSV)
                        poke.SendNotification(this, $"```{user}, {(Species)offer} cannot be Size Swap\nReason: Not from SV or from a Raid```");
                    msg = $"{user}, **{(Species)offer}** is not SV native & cannot be swapped due to Home Tracker / Raidmon";
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);
                    await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Bad Size Swap").ConfigureAwait(false);
                    return (offered, PokeTradeResult.TrainerRequestBad);
                }
                else
                {
                    toSend.Tracker = 0;
                    if (nick == "255") //Jumbo
                    {
                        toSend.Scale = 255;
                        toSend.RibbonMarkMini = false;
                        toSend.RibbonMarkJumbo = true;
                        toSend.AffixedRibbon = 101;
                    }
                    else if (nick == "0") //Mini
                    {
                        toSend.Scale = 0;
                        toSend.RibbonMarkMini = true;
                        toSend.RibbonMarkJumbo = false;
                        toSend.AffixedRibbon = 102;
                    }
                    else //Assign custom value or random
                    {
                        if (int.TryParse(nick, out int pscale) && pscale < 256)
                            toSend.Scale = (byte)pscale;
                        if (!offered.IsNicknamed)
                            toSend.Scale = (byte)rnd.Next(1, 255); //Never trigger 0 or 255 Scale
                        toSend.RibbonMarkJumbo = false;
                        toSend.RibbonMarkMini = false;
                        toSend.AffixedRibbon = -1;
                    }
                    Log($"Size changed to {toSend.Scale}");
                    toSend.HeightScalar = toSend.Scale;
                    //Refresh PID/EC jus incase (Prevent Clones too)
                    if (offered.IsShiny)
                        toSend.SetShiny();
                    else
                    {
                        toSend.SetShiny();
                        toSend.SetUnshiny();
                    }
                    if (toSend.Species == (ushort)Species.Dunsparce || toSend.Species == (ushort)Species.Tandemaus) //Keep EC to maintain form
                    {
                        if (offered.EncryptionConstant % 100 == 0)
                            toSend = KeepECModable(toSend);
                    }
                    else
                        toSend.SetRandomEC();
                    toSend.ClearNickname();
                    toSend.RefreshChecksum();

                    var la2 = new LegalityAnalysis(toSend);
                    if (la2.Valid)
                    {
                        Log($"Size Swap Success.");
                        poke.TradeData = toSend;
                        counts.AddCompletedSizeSwaps();
                        await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
                        await Task.Delay(2_500, token).ConfigureAwait(false);
                        return (toSend, PokeTradeResult.Success);
                    }
                    else //Safety Net
                    {
                        msg = $"{user}, {(Species)toSend.Species} has a problem\n\n";
                        msg += $"__**Legality Analysis**__\n";
                        msg += la2.Report();
                        await SphealEmbed.EmbedAlertMessage(toSend, false, toSend.FormArgument, msg, "Bad Size Swap").ConfigureAwait(false);
                        DumpPokemon(DumpSetting.DumpFolder, "hacked", toSend);
                        return (toSend, PokeTradeResult.IllegalTrade);
                    }
                }
            }
            else if (swap == (int)custom.FriendshipSwapItem) //Friendship Swap
            {
                Log($"{user} is requesting Friendship swap for: {GameInfo.GetStrings(1).Species[offer]}");
                var tradepartner = await GetTradePartnerInfo(token).ConfigureAwait(false);

                toSend.CurrentLevel = 100; //Mirror Trilogy
                if (toSend.OT_Name == user && toSend.Version == tradepartner.Game) //They are the OT
                    toSend.OT_Friendship = 255;
                else
                    toSend.HT_Friendship = 255;

                if (!Regex.IsMatch("null", nick, RegexOptions.IgnoreCase))
                {
                    toSend.RibbonBestFriends = true;
                    toSend.RibbonMarkPartner = true;
                    toSend.AffixedRibbon = 3;
                }

                switch (toSend.Species) //Evolve Friendship-Evos, only SV allowed added
                {
                    case (ushort)Species.Pichu:
                        toSend.Species = (ushort)Species.Pikachu;
                        break;
                    case (ushort)Species.Cleffa:
                        toSend.Species = (ushort)Species.Clefairy;
                        break;
                    case (ushort)Species.Igglybuff:
                        toSend.Species = (ushort)Species.Jigglypuff;
                        break;
                    case (ushort)Species.Meowth:
                        if (offered.Form == 1)
                        {
                            toSend.Species = (ushort)Species.Persian;
                            toSend.Form = 1;
                        }
                        break;
                    case (ushort)Species.Chansey:
                        toSend.Species = (ushort)Species.Blissey;
                        break;
                    case (ushort)Species.Eevee:
                        toSend.HeldItem = 50; //Free candy to evolve it to whatever after
                        break;
                    case (ushort)Species.Munchlax:
                        toSend.Species = (ushort)Species.Snorlax;
                        break;
                    case (ushort)Species.Azurill:
                        toSend.Species = (ushort)Species.Marill;
                        break;
                    case (ushort)Species.Chingling:
                        toSend.Species = (ushort)Species.Chimecho;
                        break;
                    case (ushort)Species.Riolu:
                        toSend.Species = (ushort)Species.Lucario;
                        break;
                    case (ushort)Species.Swadloon:
                        toSend.Species = (ushort)Species.Leavanny;
                        break;
                    case (ushort)Species.Snom:
                        toSend.Species = (ushort)Species.Frosmoth;
                        break;
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

                if (!toSend.FatefulEncounter || loc != 30001)
                    toSend.ClearNickname();
                if (toSend.Species != (ushort)Species.Eevee)
                    toSend.HeldItem = 497;
                toSend.RefreshChecksum();

                var la2 = new LegalityAnalysis(toSend);
                if (la2.Valid)
                {
                    Log($"Swap Success. Sending back: {GameInfo.GetStrings(1).Species[toSend.Species]}.");
                    poke.TradeData = toSend;
                    counts.AddCompletedFriendshipSwaps();
                    DumpPokemon(DumpSetting.DumpFolder, "trilogy", toSend);
                    await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
                    await Task.Delay(2_500, token).ConfigureAwait(false);
                    return (toSend, PokeTradeResult.Success);
                }
                else //Safety Net
                {
                    msg = $"{user}, {(Species)toSend.Species} has a problem\n\n";
                    msg += $"__**Legality Analysis**__\n";
                    msg += la2.Report();
                    await SphealEmbed.EmbedAlertMessage(toSend, false, toSend.FormArgument, msg, "Bad Friendship Swap").ConfigureAwait(false);
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", toSend);
                    return (toSend, PokeTradeResult.IllegalTrade);
                }
            }
            
            //Tera Swapper + Ball (if applicable)
            else if (teraItem.Length > 1 && teraItem[1] == "Tera")
            {
                if (nick == "Poké") //Account for
                    nick = "Poke";
                if (Enum.TryParse(nick, true, out Ball _) && toSend.Generation == 9) //Double Swap for Ball
                {
                    Log($"{user} is requesting Double swap for: {GameInfo.GetStrings(1).Species[offer]}");
                    toSend.Tracker = 0;
                    toSend.Ball = (int)(Ball)Enum.Parse(typeof(Ball), nick, true);
                    toSend.ClearNickname();
                    counts.AddCompletedDoubleSwaps();
                    Log($"Ball swapped to {(Ball)toSend.Ball}");
                }
                else
                    Log($"{user} is requesting Tera swap for: {GameInfo.GetStrings(1).Species[offer]}");

                toSend.TeraTypeOverride = (MoveType)Enum.Parse(typeof(MoveType), teraItem[0]);
                toSend.RefreshChecksum();

                var la2 = new LegalityAnalysis(toSend);
                if (la2.Valid)
                {
                    Log($"Tera swapped to: {toSend.TeraTypeOverride}");
                    poke.TradeData = toSend;
                    counts.AddCompletedTeraSwaps();
                    await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
                    await Task.Delay(2_500, token).ConfigureAwait(false);
                    return (toSend, PokeTradeResult.Success);
                }
                else //Safety Net as theres double swap for Ball now
                {
                    if (Enum.TryParse(nick, true, out Ball _) && toSend.Generation == 9)
                    {
                        if (poke.Type == PokeTradeType.LinkSV)
                        {
                            if (toSend.WasEgg && toSend.Ball == 1)
                                poke.SendNotification(this, $"```{user}, {(Species)offer} is from an egg & cannot be in {(Ball)toSend.Ball}```");
                            else
                                poke.SendNotification(this, $"```{user}, {(Species)offer} cannot be in {(Ball)toSend.Ball}```");
                        }
                        msg = $"{user}, **{(Species)toSend.Species}** cannot be in **{(Ball)toSend.Ball}**\n";
                        if (toSend.WasEgg && toSend.Ball == 1)
                            msg += "Egg hatches cannot be in **Master Ball**";
                    }
                    else
                    {
                        msg = $"{user}, {(Species)toSend.Species} has a problem\n\n";
                        msg += $"__**Legality Analysis**__\n";
                        msg += la2.Report();
                    }
                    if (Enum.TryParse(nick, true, out Ball _) && toSend.Generation == 9)
                        await SphealEmbed.EmbedAlertMessage(toSend, false, toSend.FormArgument, msg, "Bad Double Swap [Ball]").ConfigureAwait(false);
                    else
                        await SphealEmbed.EmbedAlertMessage(toSend, false, toSend.FormArgument, msg, "Bad Tera Swap").ConfigureAwait(false);
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", toSend);
                    return (toSend, PokeTradeResult.IllegalTrade);
                }
            }
            else
            {
                Log($"{user} is requesting Basic Clone for: {GameInfo.GetStrings(1).Species[offer]}");
                if (poke.Type == PokeTradeType.LinkSV)
                    poke.SendNotification(this, $"Cloned your {GameInfo.GetStrings(1).Species[offer]}");
                toSend.RefreshChecksum();
                counts.AddCompletedClones();
                await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
                await Task.Delay(2_500, token).ConfigureAwait(false);
                DumpPokemon(DumpSetting.DumpFolder, "clone", toSend);
            }
            return (toSend, PokeTradeResult.Success);
        }
        private async Task<bool> SetTradePartnerDetailsSV(PokeTradeDetail<PK9> poke, PK9 toSend, PK9 offered, SAV9SV sav, CancellationToken token)
        {
            var cln = (PK9)toSend.Clone();
            var tradepartner = await GetTradePartnerInfo(token).ConfigureAwait(false);
            var changeallowed = OTChangeAllowed(toSend, poke);
            var custom = Hub.Config.CustomSwaps;
            var counts = TradeSettings;
            var Scarlet = (ushort)Species.Koraidon;
            var Violet = (ushort)Species.Miraidon;
            var version = tradepartner.Game;
            var swap = offered.HeldItem;
            string[] teraItem = GameInfo.GetStrings(1).Item[swap].Split(' ');
            string[] ballItem = GameInfo.GetStrings(1).Item[swap].Split(' ');

            if (changeallowed)
            {
                cln.OT_Name = tradepartner.TrainerName;
                cln.OT_Gender = tradepartner.Gender;
                cln.Language = tradepartner.Language;

                if ((cln.Species == Scarlet && version == 51) || (cln.Species == Violet && version == 50)) //Box Legends OT
                {
                    cln.TrainerTID7 = (ushort)rnd.Next(1, 999999);
                    cln.TrainerSID7 = (ushort)rnd.Next(1, 4294);
                    cln.ClearNickname();
                }
                else
                {
                    cln.TrainerTID7 = Convert.ToUInt32(tradepartner.TID7);
                    cln.TrainerSID7 = Convert.ToUInt32(tradepartner.SID7);
                    if (toSend.IsEgg == false)
                    {
                        cln.Version = version; //Eggs should not have Origin Game on SV
                        if (cln.HeldItem > 0)
                        {
                            if (cln.Species != (ushort)custom.ItemTradeSpecies)
                                cln.ClearNickname();
                            if (!cln.IsNicknamed && cln.Species != (ushort)custom.ItemTradeSpecies)
                                cln.ClearNickname();
                            else if (offered.HeldItem == (int)custom.OTSwapItem)
                                cln.ClearNickname();
                        }
                        else
                            cln.ClearNickname();
                        if (toSend.WasEgg && toSend.Egg_Location == 30002) //Hatched Eggs from Link Trade fixed via OTSwap
                            cln.Egg_Location = 30023; //Picnic
                        if (teraItem.Length > 1 && (teraItem[1] == "Tera")) //Distro Tera Selector
                        {
                            cln.TeraTypeOverride = (MoveType)Enum.Parse(typeof(MoveType), teraItem[0]);
                            Log($"Tera swapped to: {cln.TeraTypeOverride}");
                        }
                    }
                    else //Set eggs received in Picnic, instead of received in Link Trade
                    {
                        if (teraItem.Length > 1 && (teraItem[1] == "Tera")) //Eggs can only change original
                        {
                            //Will only work if Tera is one of it's other typing
                            cln.TeraTypeOriginal = (MoveType)Enum.Parse(typeof(MoveType), teraItem[0]);
                            Log($"Tera swapped to: {cln.TeraTypeOverride}");
                        }
                        if (toSend.Egg_Location == 30002)
                            cln.Egg_Location = 30023; //For people who gen on blank PK so it fixes met in Link Trade
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
                }
                //This is for eggs hatched in the Academy/Academy Meowth gift
                if (version == (int)GameVersion.SL && toSend.Met_Location == 131) //Scarlet
                    cln.Met_Location = 130;//Naranja Academy
                else if (version == (int)GameVersion.VL && toSend.Met_Location == 130) //Violet
                    cln.Met_Location = 131;//Uva Academy

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

                if (toSend.IsShiny)
                {
                    if (cln.Met_Location == 30024) //Allow Shiny Raidmons to OT
                    {
                        if (toSend.ShinyXor != 0) //Altho we can't see square shiny, its still in the code...
                            cln.PID = (((uint)(cln.TID16 ^ cln.SID16) ^ (cln.PID & 0xFFFF) ^ 1u) << 16) | (cln.PID & 0xFFFF); //Star
                        else
                            cln.PID = (((uint)(cln.TID16 ^ cln.SID16) ^ (cln.PID & 0xFFFF) ^ 0) << 16) | (cln.PID & 0xFFFF); //Square
                    }
                    else
                        cln.SetShiny();
                }
                else
                    if (cln.Met_Location != 30024) //Allow Raidmons to OT, Reroll PID of Non-Shinies 
                {
                    cln.SetShiny();
                    cln.SetUnshiny();
                }
                if (cln.Species == (ushort)Species.Dunsparce || cln.Species == (ushort)Species.Tandemaus) //Keep EC to maintain form
                {
                    if (cln.EncryptionConstant % 100 == 0)
                        cln = KeepECModable(cln);
                }
                else
                    if (cln.Met_Location != 30024) cln.SetRandomEC(); //Allow raidmon to OT
                cln.RefreshChecksum();
            }
            var tradesv = new LegalityAnalysis(cln); //Legality check, if fail, sends original PK9 instead
            if (tradesv.Valid)
            {
                if (changeallowed && !custom.LogTrainerDetails) //So it does not log twice
                {
                    Log($"OT info swapped to:");
                    Log($"OT_Name: {cln.OT_Name}");
                    Log($"TID: {cln.TrainerTID7:000000}");
                    Log($"SID: {cln.TrainerSID7:0000}");
                    Log($"Gender: {(Gender)cln.OT_Gender}");
                    Log($"Language: {(LanguageID)cln.Language}");
                    Log($"Game: {(GameVersion)cln.Version}");
                }
                else if (!changeallowed)
                    Log($"Sending original Pokémon as it can't be OT swapped");
                if (changeallowed)
                    Log($"OT swap success.");
                if (offered.HeldItem == (int)custom.OTSwapItem)
                {
                    DumpPokemon(DumpSetting.DumpFolder, "OTSwaps", cln);
                    counts.AddCompletedOTSwaps();
                }
                await SetBoxPokemonAbsolute(BoxStartOffset, cln, token, sav).ConfigureAwait(false);
            }
            else
            {
                Log($"Sending original Pokémon as it can't be OT swapped");
                if (toSend.FatefulEncounter)
                    Log($"Reason: Fateful Encounter");
            }
            return tradesv.Valid;
        }
        private bool OTChangeAllowed(PK9 mon, PokeTradeDetail<PK9> poke)
        {
            var changeallowed = true;
            var custom = Hub.Config.CustomSwaps;
            // Check if OT change is allowed for different situations
            switch (mon.Species)
            {
                //Ditto will not OT change unless it has Destiny Mark
                case (ushort)Species.Ditto:
                    if (mon.RibbonMarkDestiny == true)
                        changeallowed = true;
                    else
                        changeallowed = false;
                    break;
                //Block SV natives that required to be moved out of SV to evolve
                case (ushort)Species.Ursaluna:
                case (ushort)Species.Wyrdeer:
                case (ushort)Species.Overqwil:
                case (ushort)Species.Kleavor:
                    if (poke.Type == PokeTradeType.Specific || mon.Species == (ushort)Species.Ursaluna && mon.Form == 1) //Allow Bloodmoon
                        changeallowed = true;
                    else
                        changeallowed = false;
                    break;
                //Block SV natives but only if other forms (Hisui/Galar)
                case (ushort)Species.Typhlosion:
                case (ushort)Species.Samurott:
                case (ushort)Species.Decidueye:
                case (ushort)Species.Sliggoo:
                case (ushort)Species.Goodra:
                case (ushort)Species.Slowbro:
                case (ushort)Species.Slowking:
                case (ushort)Species.Avalugg:
                case (ushort)Species.Braviary:
                case (ushort)Species.Lilligant:
                case (ushort)Species.Weezing:
                    if (mon.Form != 0)
                        changeallowed = false;
                    else
                        changeallowed = true;
                    if (poke.Type == PokeTradeType.Specific || mon.RibbonMarkMightiest == true)
                        changeallowed = true;
                    break;
            }
            switch (mon.OT_Name) //Stops mons with Specific OT from changing to User's OT
            {
                case "Blaines":
                case "New Year 23":
                case "Valentine":
                case "4July":
                    if (mon.HeldItem == (int)custom.OTSwapItem) //Allow OT Swap if function triggered by held item only
                        changeallowed = true;
                    else
                        changeallowed = false;
                    break;
            }
            return changeallowed;
        }
        private static PK9 KeepECModable(PK9 eckeep) //Maintain form for Dunsparce/Tandemaus
        {
            eckeep.SetRandomEC();

            uint ecDelta = eckeep.EncryptionConstant % 100;
            eckeep.EncryptionConstant -= ecDelta;

            return eckeep;
        }
    }
}