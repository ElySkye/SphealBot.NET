using PKHeX.Core;
using SysBot.Base;
using System;
using System.Diagnostics.Metrics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SysBot.Pokemon
{
    public partial class PokeTradeBotBS : PokeRoutineExecutor8BS, ICountBot
    {
        private async Task<(PB8 toSend, PokeTradeResult check)> HandleMysteryEggs(SAV8BS sav, PokeTradeDetail<PB8> poke, PB8 offered, PB8 toSend, PartnerDataHolder partner, CancellationToken token)
        {
            var counts = TradeSettings;
            var user = partner.TrainerName;

            string? myst;
            PB8? rnd;
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
            await SetTradePartnerDetailsBDSP(poke, toSend, offered, sav, token).ConfigureAwait(false);
            poke.TradeData = toSend;

            myst = $"**{user}** has received a Mystery Egg !\n";
            myst += $"**Don't reveal if you want the surprise**\n\n";
            myst += $"||**Pokémon**: **{GameInfo.GetStrings(1).Species[toSend.Species]}**\n";
            myst += $"**Gender**: **{(Gender)toSend.Gender}**\n";
            myst += $"**Shiny**: **{Shiny}**\n";
            myst += $"**Nature**: **{(Nature)toSend.Nature}**\n";
            myst += $"**Ability**: **{GameInfo.GetStrings(1).Ability[toSend.Ability]}**\n";
            myst += $"**IVs**: **{toSend.IV_HP}/{toSend.IV_ATK}/{toSend.IV_DEF}/{toSend.IV_SPA}/{toSend.IV_SPD}/{toSend.IV_SPE}**\n";
            myst += $"**Language**: **{(LanguageID)toSend.Language}**||";

            EchoUtil.EchoEmbed(Sphealcl.EmbedEggMystery(toSend, myst, $"{user}'s Mystery Egg"));
            counts.AddCompletedMystery();
            return (toSend, PokeTradeResult.Success);
        }

        private async Task<(PB8 toSend, PokeTradeResult)> HandleCustomSwaps(SAV8BS sav, PokeTradeDetail<PB8> poke, PB8 offered, PB8 toSend, PartnerDataHolder partner, CancellationToken token)
        {
            toSend = offered.Clone();
            var custom = Hub.Config.CustomSwaps;
            var counts = TradeSettings;
            var swap = offered.HeldItem;
            var user = partner.TrainerName;
            var nick = offered.Nickname;
            var offers = GameInfo.GetStrings(1).Species[offered.Species];
            var offerts = GameInfo.GetStrings(1).Species[toSend.Species];
            var loc = offered.Met_Location;
            var botot = Hub.Config.Legality.GenerateOT;
            var la = new LegalityAnalysis(offered);

            string msg;

            if ((GameVersion)offered.Version != GameVersion.BD || (GameVersion)offered.Version != GameVersion.SP)
            {
                if (poke.Type == PokeTradeType.LinkBDSP)
                    poke.SendNotification(this, $"```Request Denied - Bot will not swap Home Tracker Pokémon for OT/Ball/Gender```");
                msg = $"{user}, **{offers}** cannot be swapped due to Home Tracker\n";
                msg += $"Features cannot be used";
                await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Invalid Request").ConfigureAwait(false);
                DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);
                return (toSend, PokeTradeResult.TrainerRequestBad);
            }
            if (!la.Valid)
            {
                if (poke.Type == PokeTradeType.LinkSV)
                    poke.SendNotification(this, $"__**Legality Analysis**__\n```{la.Report()}```");
                msg = $"{user}, **{offers}** is not legal\n";
                msg += $"Features cannot be used\n\n";
                msg += $"__**Legality Analysis**__\n";
                msg += la.Report();
                await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Illegal Request").ConfigureAwait(false);
                DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);
                return (toSend, PokeTradeResult.IllegalTrade);
            }
            else if (swap == (int)custom.OTSwapItem) //OT Swap for existing mons
            {
                Log($"{user} is requesting OT swap for: {offers} with OT Name: {offered.OT_Name}");

                toSend.Tracker = 0; //We clean the tracker since we only do the Origin Game

                if (!await SetTradePartnerDetailsBDSP(poke, toSend, offered, sav, token).ConfigureAwait(false))
                {
                    //Non SV should get rejected
                    if (poke.Type == PokeTradeType.LinkBDSP)
                        poke.SendNotification(this, $"```{user}, {offers} cannot be OT swap\n\nPokémon is either:\n1) Not BDSP native\n2) BDSP Event/In-game trade with FIXED OT```");
                    msg = $"{user}, **{offers}** cannot be OT swap\n\n";
                    msg += "Pokémon is either:\n1) Not BDSP native\n2) BDSP Event/In-game trade with FIXED OT\n\n";
                    msg += $"Original OT: **{offered.OT_Name}**";
                    await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Bad OT Swap").ConfigureAwait(false);
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", toSend);
                    return (toSend, PokeTradeResult.TrainerRequestBad);
                }
                poke.TradeData = toSend;
                return (toSend, PokeTradeResult.Success);
            }
            //Trilogy Swap for existing mons (Level/Nickname/Evolve)
            else if (swap == (int)custom.TrilogySwapItem || swap == 229)
            {
                Log($"{user} is requesting Trilogy swap for: {offers}");

                toSend.CurrentLevel = 100;//#1 Set level to 100 (Level Swap)
                if (swap == 229) //#2 Evolve Trade Evos
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
                        //Item Trade Evos
                        case (ushort)Species.Clamperl:
                            if (Regex.IsMatch(nick, "Hunt", RegexOptions.IgnoreCase))
                                toSend.Species = (ushort)Species.Huntail;
                            else if (Regex.IsMatch(nick, "Gore", RegexOptions.IgnoreCase))
                                toSend.Species = (ushort)Species.Gorebyss;
                            break;
                        case (ushort)Species.Magmar:
                            toSend.Species = (ushort)Species.Magmortar;
                            break;
                        case (ushort)Species.Electabuzz:
                            toSend.Species = (ushort)Species.Electivire;
                            break;
                        case (ushort)Species.Onix:
                            toSend.Species = (ushort)Species.Steelix;
                            break;
                        case (ushort)Species.Porygon:
                            toSend.Species = (ushort)Species.Porygon2;
                            break;
                        case (ushort)Species.Porygon2:
                            toSend.Species = (ushort)Species.PorygonZ;
                            break;
                        case (ushort)Species.Rhydon:
                            toSend.Species = (ushort)Species.Rhyperior;
                            break;
                        case (ushort)Species.Seadra:
                            toSend.Species = (ushort)Species.Kingdra;
                            break;
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
                    }
                    if (botot != "SysBot")
                        toSend.HT_Name = botot;
                    else toSend.HT_Name = user;
                    toSend.HT_Language = 2;
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
                    Log($"Swap Success. Sending back: {offerts}.");
                    poke.TradeData = toSend;
                    await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
                    var change = await ReadPokemon(LinkTradePokemonOffset, token).ConfigureAwait(false); //To prevent clone issues, disallow B press to swap to a trashmon
                    var change2 = await SwitchConnection.ReadBytesAbsoluteAsync(LinkTradePokemonOffset, 8, token).ConfigureAwait(false);
                    if (change == null || change2 != lastOffered || toSend.Species != change.Species)
                        return (toSend, PokeTradeResult.TrainerRequestBad);
                    else
                    {
                        DumpPokemon(DumpSetting.DumpFolder, "trilogy", toSend);
                        counts.AddCompletedTrilogySwaps();
                        await Task.Delay(2_500, token).ConfigureAwait(false);
                        return (toSend, PokeTradeResult.Success);
                    }
                }
                else //Safety Net
                {
                    msg = $"{user}, {offerts} has a problem\n\n";
                    msg += $"__**Legality Analysis**__\n";
                    msg += la2.Report();
                    await SphealEmbed.EmbedAlertMessage(toSend, false, toSend.FormArgument, msg, "Bad Trilogy Swap").ConfigureAwait(false);
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", toSend);
                    return (toSend, PokeTradeResult.IllegalTrade);
                }
            }
            return (toSend, PokeTradeResult.Success);
        }

        private async Task<bool> SetTradePartnerDetailsBDSP(PokeTradeDetail<PB8> poke, PB8 toSend, PB8 offered, SAV8BS sav, CancellationToken token)
        {
            var cln = (PB8)toSend.Clone();
            var counts = TradeSettings;
            var custom = Hub.Config.CustomSwaps;
            var tradepartner = await GetTradePartnerInfo(token).ConfigureAwait(false);
            var jumbo = (byte)rnd.Next(0, 6);
            var tiny = (byte)rnd.Next(0, 6);
            string[] ballItem = GameInfo.GetStrings(1).Item[offered.HeldItem].Split(' ');

            switch (cln.Species) //OT for Arceus on the other version
            {
                case (ushort)Species.Arceus:
                    {
                        if (tradepartner.Game == (int)GameVersion.BD) //Brilliant Diamond
                        {
                            cln.Met_Location = 218;
                            cln.Version = (int)GameVersion.BD;
                        }
                        else if (tradepartner.Game == (int)GameVersion.SP) //Shining Pearl
                        {
                            cln.Met_Location = 618;
                            cln.Version = (int)GameVersion.SP;
                        }
                        break;
                    }
            }

            cln.TrainerTID7 = Convert.ToUInt32(tradepartner.TID7);
            cln.TrainerSID7 = Convert.ToUInt32(tradepartner.SID7);
            cln.OT_Name = tradepartner.TrainerName;
            cln.Version = tradepartner.Game;
            cln.Language = offered.Language;
            cln.OT_Gender = offered.OT_Gender;

            if (toSend.IsEgg == false)
            {
                if (cln.HeldItem >= 0 && cln.Species != (ushort)Species.Spheal)
                    cln.ClearNickname();
            }
            else //Set eggs received in Picnic, instead of received in Link Trade
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
                else
                    cln.HeightScalar = (byte)rnd.Next(0, 256);
                cln.WeightScalar = (byte)rnd.Next(0, 256);
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

            //OT for Shiny Roamers, else set shiny as normal
            if (toSend.Species == (ushort)Species.Mesprit || toSend.Species == (ushort)Species.Cresselia)
            {
                if (toSend.IsShiny)
                    cln.PID = (((uint)(cln.TID16 ^ cln.SID16) ^ (cln.PID & 0xFFFF) ^ 1u) << 16) | (cln.PID & 0xFFFF);
                else
                    cln.PID = cln.PID; //Do nothing since not shiny
            }
            else
            {
                if (toSend.IsShiny)
                    cln.SetShiny();
                else //reroll PID for non-shiny
                {
                    cln.SetShiny();
                    cln.SetUnshiny();
                }
                cln.SetRandomEC();
            }
            cln.RefreshChecksum();

            var tradebdsp = new LegalityAnalysis(cln);
            if (tradebdsp.Valid)
            {
                if (!custom.LogTrainerDetails && !custom.FilterLogging) //So it does not log twice
                {
                    Log($"OT info swapped to:");
                    Log($"OT_Name: {cln.OT_Name}");
                    Log($"TID: {cln.TrainerTID7}");
                    Log($"SID: {cln.TrainerSID7}");
                    Log($"Gender: {(Gender)cln.OT_Gender}");
                    Log($"Language: {(LanguageID)cln.Language}");
                    Log($"Game: {(GameVersion)cln.Version}");
                }
                if (offered.HeldItem == (int)custom.OTSwapItem)
                {
                    DumpPokemon(DumpSetting.DumpFolder, "OTSwaps", cln);
                    counts.AddCompletedOTSwaps();
                }
                poke.TradeData = cln;
                Log($"OT Swap Success");
                await SetBoxPokemonAbsolute(BoxStartOffset, cln, token, sav).ConfigureAwait(false);
            }   
            else
                Log($"Sending original Pokémon as it can't be OT swapped");
            return tradebdsp.Valid;
        }
    }
}