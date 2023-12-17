using PKHeX.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using SysBot.Base;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using static SysBot.Pokemon.PokeDataOffsetsSV;

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
            myst += $"**Ability**: **{GameInfo.GetStrings(1).Ability[toSend.Ability]}**\n";
            myst += $"**IVs**: **{toSend.IV_HP}/{toSend.IV_ATK}/{toSend.IV_DEF}/{toSend.IV_SPA}/{toSend.IV_SPD}/{toSend.IV_SPE}**\n";
            myst += $"**Language**: **{(LanguageID)toSend.Language}**||";

            EchoUtil.EchoEmbed(Sphealcl.EmbedEggMystery(toSend, myst, $"{user}'s Mystery Egg"));
            counts.AddCompletedMystery();
            return (toSend, PokeTradeResult.Success);
        }

        private async Task<(PK9 toSend, PokeTradeResult)> HandleCustomSwaps(SAV9SV sav, PokeTradeDetail<PK9> poke, PK9 offered, PK9 toSend, byte[] oldEC, PartnerDataHolder partner, CancellationToken token)
        {
            toSend = offered.Clone();
            var custom = Hub.Config.CustomSwaps;
            var config = Hub.Config.CustomEmbed;
            var counts = TradeSettings;
            var swap = offered.HeldItem;
            var user = partner.TrainerName;
            var offer = offered.Species;
            var offers = GameInfo.GetStrings(1).Species[offered.Species];
            var offerts = GameInfo.GetStrings(1).Species[toSend.Species];
            var nick = offered.Nickname;
            var loc = offered.Met_Location;
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
                649, //Snowball
            };

            var staticscale = new List<ushort>
            {
                (ushort)Species.TingLu,
                (ushort)Species.ChienPao,
                (ushort)Species.WoChien,
                (ushort)Species.ChiYu,
                (ushort)Species.Koraidon,
                (ushort)Species.Miraidon,
                (ushort)Species.Ogerpon,
                (ushort)Species.Fezandipiti,
                (ushort)Species.Munkidori,
                (ushort)Species.Okidogi,
                (ushort)Species.WalkingWake,
                (ushort)Species.IronLeaves,
                //(ushort)Species.GougingFire,
                //(ushort)Species.RagingBolt,
                //(ushort)Species.IronCrown,
                //(ushort)Species.IronBoulder,
                //(ushort)Species.Terapagos,
                //(ushort)Species.Pecharunt,
            };

            string[] teraItem = GameInfo.GetStrings(1).Item[swap].Split(' ');
            string[] ballItem = GameInfo.GetStrings(1).Item[swap].Split(' ');
            string? msg;

            //Blocked due to tracker: OTSwap, Gender, Size, Mark, Ball
            if (swap == (int)custom.OTSwapItem || swap == (int)custom.GenderSwapItem || swap == (int)custom.SizeSwapItem || swap == (int)custom.MarkSwapItem || Enum.TryParse(nick, true, out Ball _) || ballItem.Length > 1 && ballItem[1] == "Ball")
            {
                //Allow Ursaluna Bloodmoon & 7 star Raids
                if (PLAevo.Contains(offer) && offered.Form == 0 || Formevo.Contains(offer) && offered.Form != 0 && offered.Met_Location != Locations.TeraCavern9) //Check for species that require to be moved out of SV to evolve
                {
                    if (poke.Type == PokeTradeType.LinkSV)
                        poke.SendNotification(this, $"```Request Denied - Bot will not swap Home Tracker Pokémon for OT/Ball/Gender/Size/Mark```");
                    msg = $"{user}, **{offers}** cannot be swapped due to Home Tracker\n";
                    msg += $"Features cannot be used for OT/Ball/Gender/Size/Mark Swap";
                    await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Invalid Request").ConfigureAwait(false);
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);
                    return (toSend, PokeTradeResult.TrainerRequestBad);
                }
            }
            if (!la.Valid)
            {
                if (poke.Type == PokeTradeType.LinkSV)
                    poke.SendNotification(this, $"**{offers}** is not legal\n\n__**Legality Analysis**__\n```{la.Report()}```");
                msg = $"{user}, **{offers}** is not legal, Features cannot be used\n";
                msg += $"**OT**: {offered.OT_Name}-{offered.TID16}\n**HT**: {offered.HT_Name}\n";
                msg += $"**Game**: {GameInfo.GetStrings(1).gamelist[offered.Version]}\n";
                msg += $"**Ball**: {GameInfo.GetStrings(1).balllist[offered.Ball]}\n\n";
                msg += $"__**Legality Analysis**__\n";
                msg += la.Report();
                if (staticscale.Contains(offer) && offered.Scale != 128)
                    msg += "\nScale must be 128\n";
                if (la.Info.PIDIV.Type != PIDType.None)
                    msg += $"\n**PIDType**: {la.Info.PIDIV.Type}";
                await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Illegal Request").ConfigureAwait(false);
                DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);
                return (toSend, PokeTradeResult.IllegalTrade);
            }
            else if (swap == (int)custom.OTSwapItem) //OT Swap for existing mons
            {
                Log($"{user} is requesting OT swap for: {offers} with OT Name: {offered.OT_Name}");

                toSend.Tracker = 0; //We clean the tracker since we only do the Origin Game

                if (!await SetTradePartnerDetailsSV(poke, toSend, offered, sav, token).ConfigureAwait(false))
                {
                    //Non SV should get rejected
                    if (poke.Type == PokeTradeType.LinkSV)
                        poke.SendNotification(this, $"```{user}, {offers} cannot be OT swap\n\nPokémon is either:\n1) Not SV native\n2) SV Event/In-game trade with FIXED OT```");
                    msg = $"{user}, **{offers}** cannot be OT swap\n\n";
                    msg += "Pokémon is either:\n1) Not SV native\n2) SV Event/In-game trade with FIXED OT\n\n";
                    msg += $"Original OT: **{offered.OT_Name}**\nGame: {GameInfo.GetStrings(1).gamelist[toSend.Version]}\n";
                    if (offered.FatefulEncounter)
                        msg += $"Pokémon is a Fateful Encounter (Event)\n";
                    else if (loc == 30001)
                        msg += $"Pokémon is an in-game trade\n";
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
                if (toSend.Generation != 9 || toSend.FatefulEncounter)
                {
                    if (poke.Type == PokeTradeType.LinkSV)
                        poke.SendNotification(this, $"```{user}, {offers} cannot be Ball Swap\nReason: Not from SV/Is an Event mon```");
                    msg = $"{user}, **{offers}** is not SV native & cannot be swapped due to Home Tracker / An event mon";
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);
                    await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Bad Ball Swap").ConfigureAwait(false);
                    return (offered, PokeTradeResult.TrainerRequestBad);
                }
                else
                {
                    if (Enum.TryParse(nick, true, out MoveType _) && offer != (ushort)Species.Ogerpon) //Double Swap for Tera
                    {
                        Log($"{user} is requesting Double swap for: {offers}");
                        toSend.TeraTypeOverride = (MoveType)Enum.Parse(typeof(MoveType), nick, true);
                        toSend.ClearNickname();
                        counts.AddCompletedDoubleSwaps();
                        Log($"Tera swapped to {toSend.TeraTypeOverride}");
                    }
                    else
                        Log($"{user} is requesting Ball swap for: {offers}");

                    toSend.Tracker = 0;
                    if (ballItem[0] == "Poké") //Account for Pokeball having an apostrophe
                        ballItem[0] = "Poke";
                    toSend.Ball = (int)(Ball)Enum.Parse(typeof(Ball), ballItem[0]);
                    toSend.RefreshChecksum();
                    Log($"Ball swapped to: {GameInfo.GetStrings(1).balllist[toSend.Ball]}");

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
                                poke.SendNotification(this, $"```{user}, {offers} is from an egg & cannot be in {(Ball)toSend.Ball}```");
                            else
                                poke.SendNotification(this, $"```{user}, {offers} cannot be in {(Ball)toSend.Ball}```");
                        }
                        if (Enum.TryParse(nick, true, out MoveType _))
                        {
                            msg = $"Invalid Tera Type: **{toSend.TeraTypeOverride}**\n";
                            msg += "Unable to swap Tera\n";
                            msg += $"Requested Ball: **{(Ball)toSend.Ball}**\n";
                        }
                        else
                        {
                            msg = $"{user}, **{offers}** cannot be in **{(Ball)toSend.Ball}**\n";
                            msg += "The ball cannot be swapped\n";
                        }
                        if (toSend.WasEgg && toSend.Ball == 1)
                            msg += "Egg hatches cannot be in **Master Ball**\n";
                        await SphealEmbed.EmbedAlertMessage(toSend, false, toSend.FormArgument, msg, "Bad Ball Swap").ConfigureAwait(false);
                        DumpPokemon(DumpSetting.DumpFolder, "hacked", toSend);
                        return (toSend, PokeTradeResult.TrainerRequestBad);
                    }
                }
            }
            //Trilogy Swap for existing mons (Level/Nickname/Evolve)
            else if (swap == (int)custom.TrilogySwapItem || swap == 229)
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
                        case (ushort)Species.Poliwhirl:
                            toSend.Species = (ushort)Species.Politoed;
                            break;
                        case (ushort)Species.Slowpoke:
                            toSend.Species = (ushort)Species.Slowking;
                            break;
                        case (ushort)Species.Rhydon:
                            toSend.Species = (ushort)Species.Rhyperior;
                            break;
                        case (ushort)Species.Magmar:
                            toSend.Species = (ushort)Species.Magmortar;
                            break;
                        case (ushort)Species.Electabuzz:
                            toSend.Species = (ushort)Species.Electivire;
                            break;
                        case (ushort)Species.Porygon:
                            toSend.Species = (ushort)Species.Porygon2;
                            break;
                        case (ushort)Species.Porygon2:
                            toSend.Species = (ushort)Species.PorygonZ;
                            break;
                        case (ushort)Species.Onix:
                            toSend.Species = (ushort)Species.Steelix;
                            break;
                        case (ushort)Species.Seadra:
                            toSend.Species = (ushort)Species.Kingdra;
                            break;
                        case (ushort)Species.Clamperl:
                            if (nick == "Hunt")
                                toSend.Species = (ushort)Species.Huntail;
                            else if (nick == "Gore")
                                toSend.Species = (ushort)Species.Gorebyss;
                            else
                                poke.SendNotification(this, "You must specify either **Hunt** or **Gore** to choose which to evolve to");
                            break;
                            //Missing: Karrablast, Shelmet, Spritzee, Swirlix (todo)
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
                        case (ushort)Species.Inkay:
                            toSend.Species = (ushort)Species.Malamar;
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
                if (!toSend.FatefulEncounter && loc != 30001 || toSend.Tracker == 0)
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
                    msg = $"{user}, {offerts} has a problem\n";
                    msg += $"Pls contact the Bot Owner for a review\n\n";
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
                Log($"{user} is requesting EV swap for: {offers}");
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
                    msg = $"{user}, {offerts} has a problem\n";
                    msg += $"Pls contact the Bot Owner for a review\n\n";
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
                Log($"{user} is requesting Gender Swap for: {(Gender)offered.Gender} {offers}");

                if (offered.Gender == 2)
                {
                    msg = $"{user},\n";
                    msg += $"Why are you trying to Swap a *{(Gender)offered.Gender}* **{offers}**?";
                    await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Bad Gender Swap").ConfigureAwait(false);
                    return (toSend, PokeTradeResult.IllegalTrade);
                }
                if (toSend.Generation != 9 || loc == 30024)
                {
                    if (poke.Type == PokeTradeType.LinkSV)
                        poke.SendNotification(this, $"```{user}, {offers} cannot be Gender Swap\nReason: Not from SV or from a Raid```");
                    msg = $"{user}, **{offers}** is a Raidmon / not SV native & cannot be swapped due to Home Tracker";
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
                    //todo: Nidoran if it comes to SV
                    switch (offered.Species)
                    {
                        case (ushort)Species.Meowstic:
                        case (ushort)Species.Indeedee:
                        case (ushort)Species.Oinkologne:
                        case (ushort)Species.Basculegion:
                            if (toSend.Gender == 0)
                                toSend.Form = 0;
                            else if (toSend.Gender == 1)
                                toSend.Form = 1;
                            break;
                    }
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
                            poke.SendNotification(this, $"```{user}, {offerts} cannot be that Gender```");
                            if (toSend.FatefulEncounter)
                                poke.SendNotification(this, $"```{user}, {offerts} gender is locked by the Event it's from```");
                        }
                        msg = $"{user}, **{offerts}** cannot be that Gender";
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
                Log($"{user} is requesting Power Swap for: {offers}");

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
                    msg = $"{user}, {offerts} has a problem\n";
                    msg += $"Pls contact the Bot Owner for a review\n\n";
                    msg += $"__**Legality Analysis**__\n";
                    msg += la2.Report();
                    await SphealEmbed.EmbedAlertMessage(toSend, false, toSend.FormArgument, msg, "Bad Power Swap").ConfigureAwait(false);
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", toSend);
                    return (toSend, PokeTradeResult.IllegalTrade);
                }
            }
            //Size Swap, only SV Natives & not Tera9
            else if (swap == (int)custom.SizeSwapItem)
            {
                Log($"{user} is requesting Size Swap for: {offers}");

                if (toSend.Generation != 9 || loc == 30024 || loc == 30001 || toSend.FatefulEncounter || staticscale.Contains(offer))
                {
                    if (poke.Type == PokeTradeType.LinkSV)
                    {
                        if (staticscale.Contains(offer))
                            poke.SendNotification(this, $"```{user}, {offers} has a static scale & cannot be Size Swapped```");
                        else
                            poke.SendNotification(this, $"```{user}, {offers} cannot be Size Swap\nReason: Not from SV or from a Raid```");
                    }
                    if (staticscale.Contains(offer))
                        msg = $"{user}, **{offers}** has a static scale & cannot be Size Swapped";
                    else
                        msg = $"{user}, **{offers}** is a Raidmon / not SV native & cannot be swapped due to Home Tracker";
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
                    var sscale = toSend.Scale switch
                    {
                        255 => "Jumbo",
                        0 => "Mini",
                        _ => "Average"
                    };
                    Log($"Size changed to {sscale} ({toSend.Scale})");
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
                        string sizeimg = string.Empty;
                        if (config.CustomEmoji && config.TESizeJumbo != null && config.TESizeMini != null)
                        {
                            if (offered.Scale == 255 || toSend.Scale == 255)
                                sizeimg = config.TESizeJumbo;
                            else if (offered.Scale == 0 || toSend.Scale == 0)
                                sizeimg = config.TESizeMini;
                        }
                        msg = $"{user}, {offerts} has a problem\n";
                        msg += $"Original Size: {sizeimg} {offered.Scale}\n";
                        msg += $"Requested Size: {sizeimg} {toSend.Scale}\n\n";
                        msg += $"__**Legality Analysis**__\n";
                        msg += la2.Report();
                        await SphealEmbed.EmbedAlertMessage(toSend, false, toSend.FormArgument, msg, "Bad Size Swap").ConfigureAwait(false);
                        DumpPokemon(DumpSetting.DumpFolder, "hacked", toSend);
                        return (toSend, PokeTradeResult.IllegalTrade);
                    }
                }
            }
            //Friendship Swap
            else if (swap == (int)custom.FriendshipSwapItem)
            {
                Log($"{user} is requesting Friendship swap for: {offers}");
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

                switch (toSend.Species) //Evolve Friendship-Evos
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
                    case (ushort)Species.Golbat:
                        toSend.Species = (ushort)Species.Crobat;
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
                    case (ushort)Species.Togepi:
                        toSend.Species = (ushort)Species.Togetic;
                        break;
                    case (ushort)Species.Azurill:
                        toSend.Species = (ushort)Species.Marill;
                        break;
                    case (ushort)Species.Budew:
                        toSend.Species = (ushort)Species.Roselia;
                        break;
                    case (ushort)Species.Chingling:
                        toSend.Species = (ushort)Species.Chimecho;
                        break;
                    case (ushort)Species.Buneary:
                        toSend.Species = (ushort)Species.Lopunny;
                        break;
                    case (ushort)Species.Riolu:
                        toSend.Species = (ushort)Species.Lucario;
                        break;
                    case (ushort)Species.Woobat:
                        toSend.Species = (ushort)Species.Swoobat;
                        break;
                    case (ushort)Species.Swadloon:
                        toSend.Species = (ushort)Species.Leavanny;
                        break;
                    case (ushort)Species.TypeNull:
                        toSend.Species = (ushort)Species.Silvally;
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
                if (offered.FatefulEncounter && !offered.IsNicknamed) //Allow Fateful Encounters without Original Nickname
                    toSend.ClearNickname();
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
                    msg = $"{user}, {offerts} has a problem\n\n";
                    msg += $"__**Legality Analysis**__\n";
                    msg += la2.Report();
                    await SphealEmbed.EmbedAlertMessage(toSend, false, toSend.FormArgument, msg, "Bad Friendship Swap").ConfigureAwait(false);
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", toSend);
                    return (toSend, PokeTradeResult.IllegalTrade);
                }
            }
            //Mark Swap
            else if (swap == (int)custom.MarkSwapItem)
            {
                var itemf = rnd.Next(0, 21); //ItemFinder
                var tier = rnd.Next(0, 11); //Tier - Common, Uncommon, Unique, Legend
                var common = rnd.Next(0, 12); //T1
                var epic = rnd.Next(0, 11); //T2
                var unique = rnd.Next(0, 10); //T3
                var legend = rnd.Next(0, 4); //T4
                int selected = 0;
                var smarks = new List<string> //Label situational marks
                {
                    "Bliz",
                    "Snow",
                    "Sand",
                    "Rain",
                    "Storm",
                    "Cloud",
                };
                var markbl = new List<ushort>
                {
                    (ushort)Species.Gimmighoul,
                    (ushort)Species.Gholdengo,
                    (ushort)Species.Spiritomb,
                };

                if (toSend.Species == (ushort)Species.Mew)
                {
                    toSend.RibbonMarkMightiest = true;
                    toSend.AffixedRibbon = 108;
                    Log($"{user}'s Mew received the Mightiest Mark");
                }
                //Allow only Wild Encounters in SV, disallow trackered mons to avoid complications
                else if (toSend.Tracker != 0 || toSend.Generation != 9 || loc == 30024 || loc == 30001 || toSend.WasEgg || toSend.FatefulEncounter || toSend.RibbonMarkTitan == true || toSend.RibbonMarkMightiest == true || markbl.Contains(offered.Species) || (offered.Species == (ushort)Species.Ursaluna && offered.Form == 1))
                {
                    if (poke.Type == PokeTradeType.LinkSV)
                        poke.SendNotification(this, $"```{user}, {offers} cannot receive marks\nOnly SV wild encounters allowed```");
                    msg = $"{user}, **{offers}** is not a SV wild encounter and cannot receive marks";
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);
                    await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Bad Mark Swap").ConfigureAwait(false);
                    return (offered, PokeTradeResult.TrainerRequestBad);
                }
                else
                {
                    ///Clear all marks as we only want 1 mark per pokemon
                    toSend.AffixedRibbon = -1; //Clear the equipped mark/ribbon so it's a surprise
                    RibbonApplicator.RemoveAllValidRibbons(toSend);
                    //Replace original ribbons/itemfinder mark
                    if (offered.RibbonMarkItemfinder == true)
                        toSend.RibbonMarkItemfinder = true;
                    if (offered.RibbonChampionPaldea == true)
                        toSend.RibbonChampionPaldea = true;
                    if (offered.RibbonEffort == true)
                        toSend.RibbonEffort = true;
                    if (offered.RibbonBestFriends == true)
                        toSend.RibbonBestFriends = true;
                    if (offered.RibbonMasterRank == true)
                        toSend.RibbonMasterRank = true;
                    if (offered.RibbonMarkPartner == true)
                        toSend.RibbonMarkPartner = true;
                    if (offered.RibbonMarkGourmand == true)
                        toSend.RibbonMarkGourmand = true;

                    if (smarks.Contains(nick)) //Guaranteed Situational marks to avoid complications
                    {
                        var blsn = new List<int>
                        {
                            69, //Dalizapa Passage
                            38, //Glaseado (1)
                            42, //Glaseado (2)
                            68, //Glaseado (3)
                        };
                        var indoors = new List<int> //Not Eligible for Rain/Storm/Cloud Mark
                        {
                            64, //Inlet Grotto
                            67, //Alfornada Cavern
                            124, //Area Zero (5)
                        };
                        switch (nick)
                        {
                            case "Bliz":
                                if (blsn.Contains(loc))
                                    toSend.RibbonMarkBlizzard = true;
                                selected = 1;
                                break;
                            case "Snow":
                                if (blsn.Contains(loc))
                                    toSend.RibbonMarkSnowy = true;
                                selected = 2;
                                break;
                            case "Sand":
                                if (loc == 24) //Asado Desert
                                    toSend.RibbonMarkSandstorm = true;
                                selected = 3;
                                break;
                            case "Rain":
                                if (!indoors.Contains(loc))
                                    toSend.RibbonMarkRainy = true;
                                selected = 4;
                                break;
                            case "Storm":
                                if (!indoors.Contains(loc))
                                    toSend.RibbonMarkStormy = true;
                                selected = 5;
                                break;
                            case "Cloud":
                                if (!indoors.Contains(loc))
                                    toSend.RibbonMarkCloudy = true;
                                selected = 6;
                                break;
                        }
                        toSend.ClearNickname();
                    }
                    else //We don't want multiple marks, so either situational or random
                    {
                        if (tier == 10) //Legend
                        {
                            switch (legend)
                            {
                                case 0:
                                    toSend.RibbonMarkRare = true;
                                    selected = 7;
                                    break;
                                case 1:
                                case 2:
                                case 3:
                                case 4:
                                    toSend.RibbonMarkDestiny = true;
                                    selected = 8;
                                    break;
                            }
                        }
                        else if (tier == 8 || tier == 9) //Unique
                        {
                            switch (unique)
                            {
                                case 0:
                                case 10:
                                    toSend.RibbonMarkAbsentMinded = true;
                                    selected = 9;
                                    break;
                                case 1:
                                    toSend.RibbonMarkCharismatic = true;
                                    selected = 10;
                                    break;
                                case 2:
                                    toSend.RibbonMarkCalmness = true;
                                    selected = 11;
                                    break;
                                case 3:
                                    toSend.RibbonMarkIntense = true;
                                    selected = 12;
                                    break;
                                case 4:
                                    toSend.RibbonMarkUpbeat = true;
                                    selected = 13;
                                    break;
                                case 5:
                                    toSend.RibbonMarkIntellectual = true;
                                    selected = 14;
                                    break;
                                case 6:
                                    toSend.RibbonMarkFerocious = true;
                                    selected = 15;
                                    break;
                                case 7:
                                    toSend.RibbonMarkCrafty = true;
                                    selected = 16;
                                    break;
                                case 8:
                                    toSend.RibbonMarkThorny = true;
                                    selected = 17;
                                    break;
                                case 9:
                                    toSend.RibbonMarkSlump = true;
                                    selected = 18;
                                    break;
                            }
                        }
                        else if (tier == 5 || tier == 6 || tier == 7) //Epic
                        {
                            switch (epic)
                            {
                                case 0:
                                case 11:
                                    toSend.RibbonMarkRowdy = true;
                                    selected = 19;
                                    break;
                                case 1:
                                    toSend.RibbonMarkJittery = true;
                                    selected = 20;
                                    break;
                                case 2:
                                    toSend.RibbonMarkExcited = true;
                                    selected = 21;
                                    break;
                                case 3:
                                    toSend.RibbonMarkZonedOut = true;
                                    selected = 22;
                                    break;
                                case 4:
                                    toSend.RibbonMarkJoyful = true;
                                    selected = 23;
                                    break;
                                case 5:
                                    toSend.RibbonMarkSmiley = true;
                                    selected = 24;
                                    break;
                                case 6:
                                    toSend.RibbonMarkKindly = true;
                                    selected = 25;
                                    break;
                                case 7:
                                    toSend.RibbonMarkPumpedUp = true;
                                    selected = 26;
                                    break;
                                case 8:
                                    toSend.RibbonMarkZeroEnergy = true;
                                    selected = 27;
                                    break;
                                case 9:
                                    toSend.RibbonMarkUnsure = true;
                                    selected = 28;
                                    break;
                                case 10:
                                    toSend.RibbonMarkVigor = true;
                                    selected = 29;
                                    break;
                            }
                        }
                        else //Commons (0 - 4 + 11)
                        {
                            switch (common)
                            {
                                case 0:
                                case 12:
                                    toSend.RibbonMarkDusk = true;
                                    selected = 30;
                                    break;
                                case 1:
                                    toSend.RibbonMarkLunchtime = true;
                                    selected = 31;
                                    break;
                                case 2:
                                    toSend.RibbonMarkSleepyTime = true;
                                    selected = 32;
                                    break;
                                case 3:
                                    toSend.RibbonMarkDawn = true;
                                    selected = 33;
                                    break;
                                case 4:
                                    toSend.RibbonMarkUncommon = true;
                                    selected = 34;
                                    break;
                                case 5:
                                    toSend.RibbonMarkAngry = true;
                                    selected = 35;
                                    break;
                                case 6:
                                    toSend.RibbonMarkTeary = true;
                                    selected = 36;
                                    break;
                                case 7:
                                    toSend.RibbonMarkPeeved = true;
                                    selected = 37;
                                    break;
                                case 8:
                                    toSend.RibbonMarkScowling = true;
                                    selected = 38;
                                    break;
                                case 9:
                                    toSend.RibbonMarkFlustered = true;
                                    selected = 39;
                                    break;
                                case 10:
                                    toSend.RibbonMarkPrideful = true;
                                    selected = 40;
                                    break;
                                case 11:
                                    toSend.RibbonMarkHumble = true;
                                    selected = 41;
                                    break;
                            }
                        }
                    }

                    if (itemf == 0) // 1/20
                    {
                        toSend.RibbonMarkItemfinder = true;
                        Log($"{user}'s {offers} received the ItemFinder Mark");
                    }
                    toSend.ClearNickname();
                }
                toSend.RefreshChecksum();

                var la2 = new LegalityAnalysis(toSend);
                if (la2.Valid)
                {
                    var received = selected switch
                    {
                        1 => "Blizzard",
                        2 => "Snowy",
                        3 => "Sandstorm",
                        4 => "Rainy",
                        5 => "Stormy",
                        6 => "Cloudy",
                        7 => "Rare",
                        8 => "Destiny",
                        9 => "Absent Minded",
                        10 => "Charismatic",
                        11 => "Calmness",
                        12 => "Intense",
                        13 => "Upbeat",
                        14 => "Intellectual",
                        15 => "Ferocious",
                        16 => "Crafty",
                        17 => "Thorny",
                        18 => "Slump",
                        19 => "Rowdy",
                        20 => "Jittery",
                        21 => "Excited",
                        22 => "Zoned Out",
                        23 => "Joyful",
                        24 => "Smiley",
                        25 => "Kindly",
                        26 => "Pumped Up",
                        27 => "Zero Energy",
                        28 => "Unsure",
                        29 => "Vigor",
                        30 => "Dusk",
                        31 => "Lunchtime",
                        32 => "Sleepytime",
                        33 => "Dawn",
                        34 => "Uncommon",
                        35 => "Angry",
                        36 => "Teary",
                        37 => "Peeved",
                        38 => "Scowling",
                        39 => "Flustered",
                        40 => "Prideful",
                        41 => "Humble",
                        _ => "Mightiest",
                    };
                    var imgmark = received.Replace(" ", "");
                    string markimg = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/Markimg/" + imgmark.ToLower() + ".png";

                    msg = $"**{user}** used the __**Mark Swap**__ on **{offers}** and received:\n";
                    if (toSend.RibbonMarkRare == true)
                        msg += ":star2: **MEGA RARE** :star2:";
                    if (offer != (ushort)Species.Mew)
                        msg += $"**{received}** Mark\n";
                    else
                        msg += ":fleur_de_lis: **Mightiest Mark** :fleur_de_lis:\n";
                    if (itemf == 0)
                        msg += "> Special Loot: :dizzy: **ItemFinder Mark** :dizzy: ";
                    if (poke.Type == PokeTradeType.LinkSV)
                        poke.SendNotification(this, $"> Received the ||{received} Mark|| on {offers}");
                    Log($"{user} used the Mark Swap on {offers} and received: {received} Mark");

                    poke.TradeData = toSend;
                    DumpPokemon(DumpSetting.DumpFolder, "marks", toSend);
                    counts.AddCompletedMarkSwaps();
                    await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
                    await SphealEmbed.EmbedMarkMessage(toSend, false, toSend.FormArgument, msg, counts.CompletedMarkSwaps, "Swap Results", markimg).ConfigureAwait(false);//change this
                    await Task.Delay(2_500, token).ConfigureAwait(false);
                    return (toSend, PokeTradeResult.Success);
                }
                else //Safety Net
                {
                    msg = $"{user}, {offerts} has a problem\n\n";
                    if (selected == 31 || selected == 33)
                        msg += $"{offerts} cannot be found in the middle of the day, better luck next roll\n";
                    else if (selected == 32 || selected == 30)
                        msg += $"{offerts} cannot be found at night, better luck next roll\n";
                    msg += $"__**Legality Analysis**__\n";
                    msg += la2.Report();
                    await SphealEmbed.EmbedAlertMessage(toSend, false, toSend.FormArgument, msg, "Bad Mark Swap").ConfigureAwait(false);
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", toSend);
                    return (toSend, PokeTradeResult.IllegalTrade);
                }
            }
            //Date Swapper
            else if (offered.HeldItem == (int)custom.DateSwapItem)
            {
                Log($"{user} is requesting Date swap for {offers} with Date: {toSend.Met_Day}/{toSend.Met_Month}/{toSend.Met_Year} (DD/MM/YYYY)");
                //SV natives only, disallow trackered mons to avoid complications
                if (toSend.Tracker != 0 || toSend.Generation != 9 || toSend.FatefulEncounter)
                {
                    if (poke.Type == PokeTradeType.LinkSV)
                        poke.SendNotification(this, $"```{user}, {offerts} cannot change dates\nOnly SV natives without HOME Tracker```");
                    msg = $"{user}, **{offers}** is not a SV native or has a HOME tracker";
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);
                    await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Bad Date Swap").ConfigureAwait(false);
                    return (offered, PokeTradeResult.TrainerRequestBad);
                }
                else
                {
                    toSend.Nickname = "2023/" + nick; //Number limit
                    toSend.MetDate = DateOnly.Parse(toSend.Nickname);
                    if (toSend.WasEgg)
                        toSend.EggMetDate = toSend.MetDate;
                    toSend.ClearNickname();
                    toSend.RefreshChecksum();
                }

                var la2 = new LegalityAnalysis(toSend);
                if (la2.Valid)
                {
                    Log($"Date of {offerts} changed to {toSend.Met_Day}/{toSend.Met_Month}/{toSend.Met_Year} (DD/MM/YYYY)");
                    poke.TradeData = toSend;
                    counts.AddCompletedDateSwaps();
                    await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
                    await Task.Delay(2_500, token).ConfigureAwait(false);
                    return (toSend, PokeTradeResult.Success);
                }
                else //Safety Net
                {
                    if (poke.Type == PokeTradeType.LinkSV)
                        poke.SendNotification(this, $"```{user}, {offers} cannot Date Swapped to {toSend.Met_Day}/{toSend.Met_Month}/{toSend.Met_Year} (DD/MM/YYYY)```");
                    msg = $"{user}, **{offers}** cannot Date Swapped to **{toSend.Met_Day}/{toSend.Met_Month}/{toSend.Met_Year}** (DD/MM/YYYY)\n";
                    msg += $"__**Legality Analysis**__\n";
                    msg += la2.Report();
                    await SphealEmbed.EmbedAlertMessage(toSend, false, toSend.FormArgument, msg, "Bad Date Swap").ConfigureAwait(false);
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", toSend);
                    return (toSend, PokeTradeResult.IllegalTrade);
                }
            }
            //Ultimate Swap
            else if (offered.HeldItem == (int)custom.UltimateSwapItem)
            {
                int[] evReset = new int[] { 0, 0, 0, 0, 0, 0 };
                int[] RaidAtk = new int[] { 252, 252, 0, 0, 0, 0 };
                int[] CompAtk = new int[] { 0, 252, 0, 252, 0, 0 };
                int[] RaidSPA = new int[] { 252, 0, 0, 0, 252, 0 };
                int[] CompSPA = new int[] { 0, 0, 0, 252, 252, 0 };
                int[] GenDef = new int[] { 252, 0, 252, 0, 0, 0 };
                int[] GenSPD = new int[] { 252, 0, 0, 0, 0, 252 };

                var tradepartner = await GetTradePartnerInfo(token).ConfigureAwait(false);
                var otresult = "";

                Log($"{user} is requesting the Magical Eraser of Swapperinos for {offers}");

                toSend.StatNature = toSend.Nature; //Reset Mints
                toSend.TeraTypeOverride = toSend.TeraTypeOriginal; //Reset Tera to default
                toSend.SetEVs(evReset); //Reset all EVs
                toSend.CurrentLevel = 100; //Trilogy Swap (Partial)
                //Power Swap
                toSend.SetMaximumPPUps();
                toSend.HealPP();
                toSend.SetRecordFlagsAll();
                //Friendship
                if (toSend.OT_Name == user && toSend.Version == tradepartner.Game) //They are the OT
                    toSend.OT_Friendship = 255;
                else
                    toSend.HT_Friendship = 255;
                var changed = await ReadUntilChanged(TradePartnerOfferedOffset, oldEC, 10_000, 0_200, false, true, token).ConfigureAwait(false);
                var trash = await ReadUntilPresent(TradePartnerOfferedOffset, 25_000, 1_000, BoxFormatSlotSize, token).ConfigureAwait(false);
                var tnick = trash.Nickname;
                var tswap = trash.HeldItem;
                string[] teraT = GameInfo.GetStrings(1).Item[tswap].Split(' ');
                string[] ballT = GameInfo.GetStrings(1).Item[tswap].Split(' ');
                string[] mintT = GameInfo.GetStrings(1).Item[tswap].Split(' ');

                if (changed)
                    Log($"Trashmon found, applying extra swaps");
                if (offered.IsNicknamed || changed)
                {
                    //Tera Swap
                    if (offer != (ushort)Species.Ogerpon)
                    {
                        if (Enum.TryParse(nick, true, out MoveType _))
                            toSend.TeraTypeOverride = (MoveType)Enum.Parse(typeof(MoveType), nick, true);
                        else if (Enum.TryParse(tnick, true, out MoveType _))
                            toSend.TeraTypeOverride = (MoveType)Enum.Parse(typeof(MoveType), tnick, true);
                        else if (teraT.Length > 1 && teraT[1] == "Tera")
                            toSend.TeraTypeOverride = (MoveType)Enum.Parse(typeof(MoveType), teraT[0]);
                        Log($"Tera Swapped to {toSend.TeraTypeOverride}");
                    }
                    //Friendship Ribbons
                    if (!Regex.IsMatch("null", nick, RegexOptions.IgnoreCase) || !Regex.IsMatch("null", tnick, RegexOptions.IgnoreCase))
                    {
                        toSend.RibbonBestFriends = true;
                        toSend.RibbonMarkPartner = true;
                    }
                    //EV Swap
                    if (tswap == (int)custom.EVRaidAtkItem)
                    {
                        Log($"Type of EV Swap: Raid Atk");
                        toSend.SetEVs(RaidAtk);
                        toSend.StatNature = 3; //Adamant
                    }
                    else if (tswap == (int)custom.EVCompAtkItem)
                    {
                        Log($"Type of EV Swap: Comp Atk");
                        toSend.SetEVs(CompAtk);
                    }
                    else if (tswap == (int)custom.EVRaidSPAItem)
                    {
                        Log($"Type of EV Swap: Raid SPAtk");
                        toSend.SetEVs(RaidSPA);
                        toSend.StatNature = 15; //Modest
                    }
                    else if (tswap == (int)custom.EVCompSPAItem)
                    {
                        Log($"Type of EV Swap: Comp SPAtk");
                        toSend.SetEVs(CompSPA);
                    }
                    else if (tswap == (int)custom.EVGenDEFItem)
                    {
                        Log($"Type of EV Swap: Generic Def");
                        toSend.SetEVs(GenDef);
                    }
                    else if (tswap == (int)custom.EVGenSPDItem)
                    {
                        Log($"Type of EV Swap: Generic SPDef");
                        toSend.SetEVs(GenSPD);
                    }
                }

                if (offered.Generation == 9) //SV only Swaps
                {
                    if (toSend.WasEgg)
                        toSend.EggMetDate = toSend.MetDate;
                    //Reset to Met Level
                    if (Regex.IsMatch(nick, "reset", RegexOptions.IgnoreCase) || Regex.IsMatch(tnick, "reset", RegexOptions.IgnoreCase))
                    {
                        toSend.CurrentLevel = toSend.Met_Level;
                        toSend.Obedience_Level = (byte)toSend.CurrentLevel;
                    }
                    //Removes all Ribbons & Marks
                    if (Regex.IsMatch(nick, "null", RegexOptions.IgnoreCase) || Regex.IsMatch(tnick, "null", RegexOptions.IgnoreCase))
                    {
                        if (!toSend.FatefulEncounter)
                        {
                            RibbonApplicator.RemoveAllValidRibbons(toSend);
                            Log($"Removal of ALL Ribbons selected");
                        }
                        else
                            Log($"Ribbons intact as it's a fateful encounter");
                    }
                    //Remove PP UP
                    if (Regex.IsMatch(nick, "NoPPUP", RegexOptions.IgnoreCase) || Regex.IsMatch(tnick, "NoPP", RegexOptions.IgnoreCase))
                    {
                        toSend.Move1_PPUps = toSend.Move2_PPUps = toSend.Move3_PPUps = toSend.Move4_PPUps = 0;
                        toSend.HealPP();
                        Log($"Removal of PP Ups selected");
                    }
                    //Date Swap
                    if (tswap == (int)custom.DateSwapItem && tnick.Contains('/'))
                    {
                        toSend.Nickname = "2023/" + tnick; //Number limit
                        toSend.MetDate = DateOnly.Parse(toSend.Nickname);
                        Log($"Date changed to {toSend.Met_Day}/{toSend.Met_Month}/{toSend.Met_Year} (DD/MM/YYYY)");
                    }
                    //No Event/FatefulEncounter/Ingame-trades
                    if (!toSend.FatefulEncounter && loc != 30001)
                    {
                        toSend.Tracker = 0;
                        //Ball Swap
                        if (!int.TryParse(nick, out int _) && !int.TryParse(tnick, out int _))
                        {
                            if (Enum.TryParse(nick, true, out Ball _))
                            {
                                if (nick == "Poké")
                                    nick = "Poke";
                                toSend.Ball = (int)(Ball)Enum.Parse(typeof(Ball), nick, true);
                            }
                            else if (Enum.TryParse(tnick, true, out Ball _))
                            {
                                if (tnick == "Poké")
                                    tnick = "Poke";
                                toSend.Ball = (int)(Ball)Enum.Parse(typeof(Ball), tnick, true);
                            }
                        }
                        if (ballT.Length > 1 && ballT[1] == "Ball" && !notball.Contains(tswap))
                        {
                            if (ballT[0] == "Poké") //Account for Pokeball having an apostrophe
                                ballT[0] = "Poke";
                            toSend.Ball = (int)(Ball)Enum.Parse(typeof(Ball), ballT[0]);
                        }
                        if (offered.Ball != toSend.Ball)
                            Log($"Ball Swapped to: {GameInfo.GetStrings(1).balllist[toSend.Ball]}");
                        //Base Nature Swap
                        if (mintT.Length > 1 && mintT[1] == "Mint")
                        {
                            toSend.Nature = (int)(Nature)Enum.Parse(typeof(Nature), mintT[0]);
                            toSend.StatNature = toSend.Nature;
                            Log($"Base Nature Swapped to: {(Nature)toSend.Nature}");
                        }
                        //Randomize IVs
                        if (Regex.IsMatch(nick, "rndiv", RegexOptions.IgnoreCase) || Regex.IsMatch(tnick, "rndiv", RegexOptions.IgnoreCase))
                            toSend.SetRandomIVs();
                        //Gender Swap
                        if (offered.Gender != 2 && tswap == (int)custom.GenderSwapItem)
                        {
                            if (toSend.Gender == 0) //Male to Female
                                toSend.Gender = 1;
                            else if (toSend.Gender == 1) //Female to Male
                                toSend.Gender = 0;
                            //todo: Nidoran if it comes to SV
                            switch (offered.Species)
                            {
                                case (ushort)Species.Meowstic:
                                case (ushort)Species.Indeedee:
                                case (ushort)Species.Oinkologne:
                                case (ushort)Species.Basculegion:
                                    if (toSend.Gender == 0)
                                        toSend.Form = 0;
                                    else if (toSend.Gender == 1)
                                        toSend.Form = 1;
                                    break;
                            }
                            Log($"");
                        }
                        //Size Swap
                        if (loc != Locations.TeraCavern9 && !staticscale.Contains(offer))
                        {
                            if (nick == "255" || tnick == "255") //Jumbo
                            {
                                toSend.Scale = 255;
                                toSend.RibbonMarkMini = false;
                                toSend.RibbonMarkJumbo = true;
                            }
                            else if (nick == "0" || tnick == "0") //Mini
                            {
                                toSend.Scale = 0;
                                toSend.RibbonMarkMini = true;
                                toSend.RibbonMarkJumbo = false;
                            }
                            var sscale = toSend.Scale switch
                            {
                                255 => "Jumbo",
                                0 => "Mini",
                                _ => "Invalid"
                            };
                            toSend.HeightScalar = toSend.Scale;
                            if (offered.Scale != toSend.Scale)
                                Log($"Size swapped to: {sscale}");
                        }
                        toSend.ClearNickname(); //Nicknamed Clear
                    }
                    //OT Swap
                    var OTBefore = toSend.Clone();
                    var otswap = await SetTradePartnerDetailsSV(poke, toSend, offered, sav, token).ConfigureAwait(false);
                    if (!otswap) //Failed OTSwap
                    {
                        toSend = OTBefore.Clone();
                        otresult = "Failed";
                    }
                    else
                        otresult = "Passed";
                }
                if (trash.HeldItem != 0) //Incase they forget to swap to a 3rd trashmon
                    toSend.HeldItem = trash.HeldItem;
                toSend.RefreshChecksum();
                var la2 = new LegalityAnalysis(toSend);
                if (la2.Valid)
                {
                    Log($"Successfully reset {offers} to default and applied possible swaps");
                    poke.TradeData = toSend;
                    counts.AddCompletedUltimateSwaps();
                    DumpPokemon(DumpSetting.DumpFolder, "Ultimate", toSend);
                    await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
                    await Task.Delay(2_500, token).ConfigureAwait(false);
                    return (toSend, PokeTradeResult.Success);
                }
                else //Safety Net, includes detailed summary
                {
                    if (poke.Type == PokeTradeType.LinkSV)
                        poke.SendNotification(this, $"```{user}, {offers} has failed to perform any legal swaps```");
                    msg = $"{user}, **{offers}** has failed to perform any legal swaps\n";
                    msg += $"Please contact the Bot Owner for further assistance\n\n";
                    msg += $"**Nick**: {nick}\n**Trash Nick**: {tnick}\n";
                    msg += $"**B**: {(Ball)toSend.Ball}, **T**: {toSend.TeraTypeOverride}, **S**: {toSend.Scale}, **OT**: {otresult}\n";
                    msg += $"**MetDate**: {toSend.Met_Day}/{toSend.Met_Month}/{toSend.Met_Year}\n\n";
                    msg += $"__**Legality Analysis**__\n";
                    msg += la2.Report();
                    await SphealEmbed.EmbedAlertMessage(toSend, false, toSend.FormArgument, msg, "Bad Ultimate Swap").ConfigureAwait(false);
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", toSend);
                    return (toSend, PokeTradeResult.IllegalTrade);
                }
            }
            //Tera Swapper + Ball (if applicable)
            else if (teraItem.Length > 1 && teraItem[1] == "Tera" && offer != (ushort)Species.Ogerpon)
            {
                if (nick == "Poké") //Account for
                    nick = "Poke";
                if (Enum.TryParse(nick, true, out Ball _) && toSend.Generation == 9 && !toSend.FatefulEncounter) //Double Swap for Ball
                {
                    Log($"{user} is requesting Double swap for: {offers}");
                    toSend.Tracker = 0;
                    toSend.Ball = (int)(Ball)Enum.Parse(typeof(Ball), nick, true);
                    toSend.ClearNickname();
                    counts.AddCompletedDoubleSwaps();
                    Log($"Ball swapped to {(Ball)toSend.Ball}");
                }
                else
                    Log($"{user} is requesting Tera swap for: {offers}");

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
                                poke.SendNotification(this, $"```{user}, {offers} is from an egg & cannot be in {(Ball)toSend.Ball}```");
                            else
                                poke.SendNotification(this, $"```{user}, {offers} cannot be in {(Ball)toSend.Ball}```");
                        }
                        msg = $"{user}, **{offerts}** cannot be in **{(Ball)toSend.Ball}**\n";
                        if (toSend.WasEgg && toSend.Ball == 1)
                            msg += "Egg hatches cannot be in **Master Ball**";
                    }
                    else
                    {
                        msg = $"{user}, {offerts} has a problem\n\n";
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
            else //No held item match, so we clone
            {
                Log($"{user} is requesting Basic Clone for: {offers}");
                if (poke.Type == PokeTradeType.LinkSV)
                    poke.SendNotification(this, $"Cloned your {offers}");
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
            var partner = tradepartner.TrainerName;
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
                string pattern = "(yt\\.? | youtube\\.? | ttv\\.? | tv\\.?)";
                if (partner.Contains('.') || Regex.IsMatch(partner, pattern, RegexOptions.IgnoreCase))
                {
                    cln.OT_Name = Regex.Replace(partner, pattern, string.Empty, RegexOptions.IgnoreCase); //Gives their OT without the Ads in the name
                    cln.OT_Name = partner.Replace(".", string.Empty); //Allow users who accidentally have a fullstop in their IGN
                }
                else
                    cln.OT_Name = partner;
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

                    if (toSend.Egg_Location == 30002) //Link Trade
                        cln.Egg_Location = 30023; //Picnic
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
                        if (toSend.WasEgg) 
                            cln.EggMetDate = cln.MetDate; //Ensure no date mismatch for users who want specifc hatch date
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
                    else if (toSend.Generation == 9 || toSend.IsEgg)
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
                else if (cln.Met_Location != 30024) //Allow Raidmons to OT, Reroll PID of Non-Shinies 
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
                if (changeallowed && !custom.LogTrainerDetails && !custom.FilterLogging) //So it does not log twice
                {
                    Log($"OT info swapped to:");
                    Log($"OT_Name: {cln.OT_Name}");
                    Log($"TID: {cln.TrainerTID7:000000}");
                    Log($"SID: {cln.TrainerSID7:0000}");
                    Log($"Gender: {(Gender)cln.OT_Gender}");
                    Log($"Language: {(LanguageID)cln.Language}");
                    if (!cln.IsEgg)
                        Log($"Game: {GameInfo.GetStrings(1).gamelist[cln.Version]}");
                }
                else if (!changeallowed)
                    Log($"Sending original Pokémon as it can't be OT swapped");
                if (changeallowed && cln.OT_Name == tradepartner.TrainerName)
                    Log($"OT swap success.");
                if (offered.HeldItem == (int)custom.OTSwapItem)
                {
                    DumpPokemon(DumpSetting.DumpFolder, "OTSwaps", cln);
                    counts.AddCompletedOTSwaps();
                }
                poke.TradeData = cln;
                await SetBoxPokemonAbsolute(BoxStartOffset, cln, token, sav).ConfigureAwait(false);
            }
            else
            {
                Log($"Sending original Pokémon as it can't be OT swapped");
                if (!custom.FilterLogging)
                {
                    if (toSend.FatefulEncounter)
                        Log($"Reason: Fateful Encounter");
                    else if (!changeallowed)
                        Log($"Reason: Transfer Only Evolution/Ditto/Blocked OT filter");
                }
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
                    if (mon.RibbonMarkDestiny == true || mon.HeldItem == (int)custom.OTSwapItem)
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
                    if (poke.Type == PokeTradeType.Specific || mon.Met_Location == Locations.TeraCavern9)
                        changeallowed = true;
                    break;
            }
            switch (mon.OT_Name) //Stops mons with Specific OT from changing to User's OT
            {
                case "Blaines":
                case "Blainette":
                case "Blanes":
                case "Suarez":
                case "New Year 23":
                case "Valentine":
                case "4July":
                case "Moon2023":
                case "BDAY2023":
                case "Halloween23":
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
        private async Task<(PK9 offered, PokeTradeResult check)> HandleTradeEvo(PokeTradeDetail<PK9> poke, PK9 offered, PK9 toSend, PartnerDataHolder partner, CancellationToken token)
        {
            bool isDistribution = true;
            var list = isDistribution ? PreviousUsersDistribution : PreviousUsers;
            var listCool = UserCooldowns;
            var listEvo = EvoTracker;
            var trainerNID = await GetTradePartnerNID(TradePartnerNIDOffset, token).ConfigureAwait(false);
            var cd = AbuseSettings.TradeCooldown;
            var banduration = AbuseSettings.AutoBanEvoDuration;
            var user = partner.TrainerName;
            var maxAttempts = AbuseSettings.RepeatConnections;
            var offers = GameInfo.GetStrings(1).Species[offered.Species];
            int wlIndex = AbuseSettings.WhiteListedIDs.List.FindIndex(z => z.ID == trainerNID);
            int attempts;
            attempts = listEvo.TryInsert(trainerNID, user);
            bool wlAllow = false;

            list.TryRegister(trainerNID, user);

            Log($"{user} is trying to give a trade evolution ({offered.Species})");
            if (poke.Type == PokeTradeType.LinkSV)
                poke.SendNotification(this, $"```No Trade Evolutions\nAttach an everstone to allow trading```");
            var msg = $"\n{user} is trying to give a trade evolution\n";
            msg += $"\nEquip an Everstone on **{offers}** to allow trade";
            await SphealEmbed.EmbedTradeEvoMsg(offered, false, offered.FormArgument, msg, "Illegal Activity", attempts, AbuseSettings.RepeatConnections).ConfigureAwait(false);

            if (wlIndex > -1)
            {
                ulong wlID = AbuseSettings.WhiteListedIDs.List[wlIndex].ID;
                if (wlID != 0)
                    wlAllow = true;
            }

            if (AbuseSettings.AutoBanCooldown && cd == 0 && !wlAllow)
            {
                if (attempts >= maxAttempts)
                {
                    if (poke.Type == PokeTradeType.LinkSV)
                        poke.SendNotification(this, $"```No Trade Evolutions\nYou are now banned for {banduration} days```");
                    DateTime expires = DateTime.Now.AddDays(banduration);
                    string expiration = $"{expires:yyyy.MM.dd hh:mm:ss}";
                    AbuseSettings.BannedIDs.AddIfNew(new[] { GetReference(user, trainerNID, "Autobanned for TradeEvo", expiration) });
                    msg = $"{user} tried to give a trade evolution too many times\n";
                    msg += $"No punishment evasion, They are now banned for **{banduration}** days\n";
                    if (attempts > maxAttempts)
                    {
                        msg += $"**{user}** is a repeat offender";
                        Log($"Found a Trade Evo repeat offender: {user}, re-banning for {banduration} days");
                    }
                    await SphealEmbed.EmbedTradeEvoMsg(offered, false, offered.FormArgument, msg, "Trade Evo Ban", attempts, AbuseSettings.RepeatConnections, true).ConfigureAwait(false);
                }
            }
            return (toSend, PokeTradeResult.TradeEvo);
        }
    }
}