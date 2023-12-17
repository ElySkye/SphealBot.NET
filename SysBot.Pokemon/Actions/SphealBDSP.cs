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
        private async Task<bool> SetTradePartnerDetailsBDSP(PokeTradeDetail<PB8> poke, PB8 toSend, PB8 offered, SAV8BS sav, CancellationToken token)
        {
            var cln = (PB8)toSend.Clone();
            var counts = TradeSettings;
            var custom = Hub.Config.CustomSwaps;
            var tradepartner = await GetTradePartnerInfo(token).ConfigureAwait(false);
            var partner = tradepartner.TrainerName;
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
            string pattern = "(yt\\.? | youtube\\.? | ttv\\.? | tv\\.?)";
            if (partner.Contains('.') || Regex.IsMatch(partner, pattern, RegexOptions.IgnoreCase))
            {
                cln.OT_Name = Regex.Replace(partner, pattern, string.Empty, RegexOptions.IgnoreCase); //Gives their OT without the Ads in the name
                cln.OT_Name = partner.Replace(".", string.Empty); //Allow users who accidentally have a fullstop in their IGN
            }
            else
                cln.OT_Name = partner;
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
                    Log($"TID: {cln.TrainerTID7:000000}");
                    Log($"SID: {cln.TrainerSID7:0000}");
                    Log($"Gender: {(Gender)cln.OT_Gender}");
                    Log($"Language: {(LanguageID)cln.Language}");
                    if (!cln.IsEgg)
                        Log($"Game: {GameInfo.GetStrings(1).gamelist[cln.Version]}");
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
        private async Task<(PB8 offered, PokeTradeResult check)> HandleTradeEvo(PokeTradeDetail<PB8> poke, PB8 offered, PB8 toSend, PartnerDataHolder partner, CancellationToken token)
        {
            bool isDistribution = true;
            var list = isDistribution ? PreviousUsersDistribution : PreviousUsers;
            var listCool = UserCooldowns;
            var listEvo = EvoTracker;
            var cd = AbuseSettings.TradeCooldown;
            var user = partner.TrainerName;
            var tradePartner = await GetTradePartnerInfo(token).ConfigureAwait(false);
            var trainerNID = GetFakeNID(tradePartner.TrainerName, tradePartner.TrainerID);
            var offers = GameInfo.GetStrings(1).Species[offered.Species];
            int attempts;
            attempts = listEvo.TryInsert(trainerNID, user);

            list.TryRegister(trainerNID, partner.TrainerName);

            Log($"{user} is trying to give a trade evolution ({offered.Species})");
            if (poke.Type == PokeTradeType.LinkSV)
                poke.SendNotification(this, $"```No Trade Evolutions\nAttach an everstone to allow trading```");
            var msg = $"\n{user} is trying to give a trade evolution\n";
            msg += $"\nEquip an Everstone on **{offers}** to allow trade";
            await SphealEmbed.EmbedTradeEvoMsg(offered, false, offered.FormArgument, msg, "Illegal Activity", attempts, AbuseSettings.RepeatConnections).ConfigureAwait(false);

            if (AbuseSettings.AutoBanCooldown && cd == 0)
            {
                if (attempts >= 2)
                {
                    if (poke.Type == PokeTradeType.LinkSV)
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