using PKHeX.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Pokemon.PokeDataOffsetsSWSH;

namespace SysBot.Pokemon
{
    public partial class PokeTradeBotSWSH : PokeRoutineExecutor8SWSH, ICountBot
    {
        private async Task<(PK8, bool)> SetTradePartnerDetailsSWSH(PK8 toSend, PK8 offered, string trainerName, SAV8SWSH sav, CancellationToken token)
        {
            var data = await Connection.ReadBytesAsync(LinkTradePartnerNameOffset - 0x8, 8, token).ConfigureAwait(false);
            var tidsid = BitConverter.ToUInt32(data, 0);
            var cln = (PK8)toSend.Clone();
            var custom = Hub.Config.CustomSwaps;
            var counts = TradeSettings;

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

            if (BallSwap(offered.HeldItem) != 0 && cln.HeldItem != (int)custom.OTSwapItem) //Distro Ball Selector
            {
                cln.Ball = BallSwap(offered.HeldItem);
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
                Log($"OT info swapped to:");
                Log($"OT_Name: {cln.OT_Name}");
                Log($"TID: {cln.TrainerTID7}");
                Log($"SID: {cln.TrainerSID7}");
                Log($"Gender: {(Gender)cln.OT_Gender}");
                Log($"Language: {(LanguageID)(cln.Language)}");
                Log($"Game: {(GameVersion)(cln.Version)}");
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
        private static int BallSwap(int ballItem) => ballItem switch
        {
            1 => 1,
            2 => 2,
            3 => 3,
            4 => 4,
            5 => 5,
            6 => 6,
            7 => 7,
            8 => 8,
            9 => 9,
            10 => 10,
            11 => 11,
            12 => 12,
            13 => 13,
            14 => 14,
            15 => 15,
            492 => 17,
            493 => 18,
            494 => 19,
            495 => 20,
            496 => 21,
            497 => 22,
            498 => 23,
            499 => 24,
            576 => 25,
            851 => 26,
            _ => 0,
        };
    }
}