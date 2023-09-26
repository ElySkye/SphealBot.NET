using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SysBot.Pokemon
{
    public partial class PokeTradeBotLA : PokeRoutineExecutor8LA, ICountBot
    {
        private async Task<(PA8 toSend, PokeTradeResult check)> HandleCustomSwaps(SAV8LA sav, PokeTradeDetail<PA8> poke, PA8 offered, PA8 toSend, PartnerDataHolder partner, CancellationToken token)
        {
            var counts = TradeSettings;
            var sf = offered.Nickname;
            var user = partner.TrainerName;
            var evolve = "evo";
            var ballSwap = new List<string>
            {
                "Poke",
                "Great",
                "Ultra",
                "Feat",
                "Wing",
                "Jet",
                "Heavy",
                "Lead",
                "Giga",
            };

            toSend = offered.Clone();
            string? msg;

            var la = new LegalityAnalysis(offered);
            if (!la.Valid)
            {
                if (poke.Type == PokeTradeType.LinkLA)
                    poke.SendNotification(this, $"__**Legality Analysis**__\n{la.Report()}");
                msg = $"{user}, **{(Species)offered.Species}** is not legal\n";
                msg += $"Features cannot be used\n\n";
                msg += $"__**Legality Report**__\n";
                msg += la.Report();
                await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Illegal Request").ConfigureAwait(false);
                DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);
                return (toSend, PokeTradeResult.IllegalTrade);
            }
            else if (Regex.IsMatch(sf, evolve, RegexOptions.IgnoreCase))
            {
                Log($"{user} is requesting Trilogy swap for: {GameInfo.GetStrings(1).Species[offered.Species]}");

                toSend.CurrentLevel = 100;//#1 Set level to 100 (Level Swap)
                
                //#2 Evolve difficult to evolve Species (Evo Swap)
                switch (toSend.Species)
                {
                    case (ushort)Species.Ursaring:
                        toSend.Species = (ushort)Species.Ursaluna;
                        break;
                    case (ushort)Species.Qwilfish:
                        if (toSend.Form == 1) //Hisui
                        {
                            toSend.FormArgument = 20;
                            toSend.Species = (ushort)Species.Overqwil;
                            toSend.Form = 0;
                        }
                        break;
                    case (ushort)Species.Scyther:
                        toSend.Species = (ushort)Species.Kleavor;
                        break;
                    case (ushort)Species.Stantler:
                        {
                            toSend.FormArgument = 20;
                            toSend.Species = (ushort)Species.Wyrdeer;
                        }
                        break;
                    case (ushort)Species.Sliggoo:
                        if (toSend.Form == 1) //Hisui
                        {
                            toSend.Species = (ushort)Species.Goodra;
                            toSend.Form = 1;
                        }
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
                var RA = toSend.AbilityNumber switch
                {
                    1 => 0,
                    2 => 1,
                    3 => 2,
                    4 => 2,
                    _ => 2,
                };

                toSend.RefreshAbility(RA);
                toSend.HeightAbsolute = toSend.CalcHeightAbsolute;
                toSend.WeightAbsolute = toSend.CalcWeightAbsolute;

                //#3 Clear Nicknames
                if (!toSend.FatefulEncounter)
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
            else if (ballSwap.Contains(sf) || Enum.TryParse(sf, true, out Ball _))
            {
                Log($"{user} is requesting Ball Swap for: {GameInfo.GetStrings(1).Species[offered.Species]}");

                if (toSend.Tracker != 0 && toSend.Version == 47)
                    toSend.Tracker = 0;
                else if (toSend.Version != 47)
                {
                    msg = $"Pokémon: **{(Species)offered.Species}**\n";
                    msg += $"{user} is attempting to Ballswap non PLA origin Pokémon\n";
                    msg += $"You can only ballswap PLA origin Pokémon";
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);
                    await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Bad Ball Swap:").ConfigureAwait(false);
                    return (offered, PokeTradeResult.TrainerRequestBad);
                }
                else
                {
                    toSend.Nickname = sf switch
                    {
                        "Poke" => "LAPoke",
                        "Great" => "LAGreat",
                        "Ultra" => "LAUltra",
                        "Feat" => "LAFeather",
                        "Wing" => "LAWing",
                        "Jet" => "LAJet",
                        "Heavy" => "LAHeavy",
                        "Lead" => "LALeaden",
                        "Giga" => "LAGigaton",
                        _ => "LAPoke"
                    };
                    toSend.Ball = (int)(Ball)Enum.Parse(typeof(Ball), toSend.Nickname, true);
                    toSend.ClearNickname();
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
                        msg = $"{user}, **{(Species)offered.Species}** cannot be in {(Ball)toSend.Ball}";
                        msg += $"\nThe ball cannot be swapped";
                        await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Bad Ball Swap:").ConfigureAwait(false);
                        DumpPokemon(DumpSetting.DumpFolder, "hacked", toSend);
                        return (toSend, PokeTradeResult.IllegalTrade);
                    }
                }
            }
            return (toSend, PokeTradeResult.Success);
        }
        private async Task<bool> SetTradePartnerDetailsLA(PA8 toSend, SAV8LA sav, CancellationToken token)
        {
            var cln = (PA8)toSend.Clone();
            var custom = Hub.Config.CustomSwaps;
            var tradepartner = await GetTradePartnerInfo(token).ConfigureAwait(false);

            cln.TrainerTID7 = Convert.ToUInt32(tradepartner.TID7);
            cln.TrainerSID7 = Convert.ToUInt32(tradepartner.SID7);
            cln.Language = tradepartner.Language;
            cln.OT_Name = tradepartner.TrainerName;
            cln.OT_Gender = tradepartner.Gender;
            cln.Version = tradepartner.Game;
            cln.ClearNickname();

            if (toSend.IsShiny)
                cln.SetShiny();
            else
            {
                cln.SetShiny();
                cln.SetUnshiny();
            }

            cln.SetRandomEC();
            cln.RefreshChecksum();

            var tradela = new LegalityAnalysis(cln);

            if (tradela.Valid)
            {
                if (custom.LogTrainerDetails == false) //So it does not log twice
                {
                    Log($"OT info swapped to:");
                    Log($"OT_Name: {cln.OT_Name}");
                    Log($"TID: {cln.TrainerTID7}");
                    Log($"SID: {cln.TrainerSID7}");
                    Log($"Gender: {(Gender)cln.OT_Gender}");
                    Log($"Language: {(LanguageID)(cln.Language)}");
                }
                Log($"OT Swap Success");
                await SetBoxPokemonAbsolute(BoxStartOffset, cln, token, sav).ConfigureAwait(false);
            }
            else Log($"Sending original Pokémon as it can't be OT swapped");
            return tradela.Valid;
        }
    }
}