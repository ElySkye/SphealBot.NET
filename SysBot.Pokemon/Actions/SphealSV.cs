using PKHeX.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using SysBot.Base;
using System.Collections.Generic;

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
            await SetTradePartnerDetailsSV(toSend, offered, sav, token).ConfigureAwait(false);
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
            if (swap == 4)
                toSend.Ball = 4;

            if (swap == (int)custom.OTSwapItem || ballItem.Length > 1 && ballItem[1] == "Ball" || swap == (int)custom.GenderSwapItem || Enum.TryParse(nick, true, out Ball _))
            {
                if (PLAevo.Contains(offer) || Formevo.Contains(offer) && offered.Form != 0) //Check for species that require to be moved out of SV to evolve
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

                if (!await SetTradePartnerDetailsSV(toSend, offered, sav, token).ConfigureAwait(false))
                {
                    //Non SV should get rejected
                    if (poke.Type == PokeTradeType.LinkSV)
                        poke.SendNotification(this, $"```{user}, {(Species)offer} cannot be OT swap\nPokémon is either:\n\n1) Not SV native\n2) SV Event/In-game trade with FIXED OT```");
                    msg = $"{user}, **{(Species)offer}** cannot be OT swap\n";
                    msg += "Pokémon is either:\n1) Not SV native\n\n2) SV Event/In-game trade with FIXED OT\n";
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
                    if (toSend.Ball != 4)
                        toSend.Ball = (int)(Ball)Enum.Parse(typeof(Ball), ballItem[0]);
                    toSend.RefreshChecksum();
                    Log($"Ball swapped to: {(Ball)toSend.Ball}");

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
                    }
                    toSend.HeldItem = 1882;
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
                            if (toSend.Form == 0) //Kalos
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
                            if (toSend.Form == 2) //White
                            {
                                if (toSend.Gender == 0) //Male
                                {
                                    toSend.Species = (ushort)Species.Basculegion;
                                    toSend.Form = 0;
                                    toSend.Gender = 0;
                                }
                                else if (toSend.Gender == 1) //Female
                                {
                                    toSend.Species = (ushort)Species.Basculegion;
                                    toSend.Form = 1;
                                    toSend.Gender = 1;
                                }
                                toSend.FormArgument = 300;
                            }
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
            //Gender Swap - Only SV Natives
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
                else if (toSend.Generation != 9)
                {
                    if (poke.Type == PokeTradeType.LinkSV)
                        poke.SendNotification(this, $"```{user}, {(Species)offer} cannot be Gender Swap\nReason: Not from SV```");
                    msg = $"{user}, **{(Species)offer}** is not SV native & cannot be swapped due to Home Tracker";
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

                    if (toSend.IsShiny)
                    {
                        if (toSend.Met_Location == 30024)
                        {
                            if (toSend.ShinyXor != 0)
                                toSend.PID = (((uint)(toSend.TID16 ^ toSend.SID16) ^ (toSend.PID & 0xFFFF) ^ 1u) << 16) | (toSend.PID & 0xFFFF); //Star
                            else
                                toSend.PID = (((uint)(toSend.TID16 ^ toSend.SID16) ^ (toSend.PID & 0xFFFF) ^ 0) << 16) | (toSend.PID & 0xFFFF); //Square
                        }
                        else
                            toSend.SetShiny();
                    }
                    else
                    if (toSend.Met_Location != 30024)
                    {
                        toSend.SetShiny();
                        toSend.SetUnshiny();
                    }
                    if (toSend.Species == (ushort)Species.Dunsparce || toSend.Species == (ushort)Species.Tandemaus) //Keep EC to maintain form
                    {
                        if (toSend.EncryptionConstant % 100 == 0)
                            toSend = KeepECModable(toSend);
                    }
                    else
                        if (toSend.Met_Location != 30024) toSend.SetRandomEC();
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
                            poke.SendNotification(this, $"```{user}, {(Species)toSend.Species} cannot be that Gender```");
                        msg = $"{user}, **{(Species)toSend.Species}** cannot be that Gender";
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
            //Tera Swapper + Ball (if applicable)
            else if (teraItem.Length > 1 && teraItem[1] == "Tera")
            {
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
                        msg = $"{user}, {(Species)toSend.Species} has a problem\n\n";
                    msg += $"__**Legality Analysis**__\n";
                    msg += la2.Report();
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

        private async Task<bool> SetTradePartnerDetailsSV(PK9 toSend, PK9 offered, SAV9SV sav, CancellationToken token)
        {
            var cln = (PK9)toSend.Clone();
            var tradepartner = await GetTradePartnerInfo(token).ConfigureAwait(false);
            var changeallowed = OTChangeAllowed(toSend);
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
                            else if (cln.HeldItem == (int)custom.OTSwapItem)
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
                }
                //This is for eggs hatched in the Academy/Academy Meowth gift
                if (version == (int)GameVersion.SL && toSend.Met_Location == 131) //Scarlet
                    cln.Met_Location = 130;//Naranja Academy
                else if (version == (int)GameVersion.VL && toSend.Met_Location == 130) //Violet
                    cln.Met_Location = 131;//Uva Academy

                if (ballItem.Length > 1 && ballItem[1] == "Ball") //Distro Ball Selector
                {
                    if (swap == 4)
                        cln.Ball = 4;
                    else
                        cln.Ball = (int)(Ball)Enum.Parse(typeof(Ball), ballItem[0]);
                    Log($"Ball swapped to: {(Ball)cln.Ball}");
                }

                if (toSend.IsShiny)
                {
                    if (cln.Met_Location == 30024) //Allow Shiny Raidmons to OT
                    {
                        if (toSend.ShinyXor != 0)
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
                    Log($"TID: {cln.TrainerTID7}");
                    Log($"SID: {cln.TrainerSID7}");
                    Log($"Gender: {(Gender)cln.OT_Gender}");
                    Log($"Language: {(LanguageID)cln.Language}");
                    Log($"Game: {(GameVersion)cln.Version}");
                }
                else if (!changeallowed)
                    Log($"Sending original Pokémon as it can't be OT swapped");
                if (changeallowed)
                    Log($"OT swap success.");
                if (toSend.HeldItem == (int)custom.OTSwapItem)
                {
                    DumpPokemon(DumpSetting.DumpFolder, "OTSwaps", cln);
                    counts.AddCompletedOTSwaps();
                }
                await SetBoxPokemonAbsolute(BoxStartOffset, cln, token, sav).ConfigureAwait(false);
            }
            else
                Log($"Sending original Pokémon as it can't be OT swapped");
            return tradesv.Valid;
        }
        private bool OTChangeAllowed(PK9 mon)
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
                    if (mon.Form != 0)
                        changeallowed = false;
                    else
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