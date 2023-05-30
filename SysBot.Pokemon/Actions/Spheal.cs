using PKHeX.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Pokemon.PokeDataOffsetsSWSH;

namespace SysBot.Pokemon
{
    public partial class PokeTradeBotSWSH : PokeRoutineExecutor8SWSH, ICountBot
    {
        private async Task<(PK8, bool)> SetTradePartnerDetailsSWSH(PK8 toSend, string trainerName, SAV8SWSH sav, CancellationToken token)
        {
            var data = await Connection.ReadBytesAsync(LinkTradePartnerNameOffset - 0x8, 8, token).ConfigureAwait(false);
            var tidsid = BitConverter.ToUInt32(data, 0);
            var cln = (PK8)toSend.Clone();
            var changeallowed = OTChangeAllowed(toSend, data);

            if (changeallowed && toSend.OT_Name != "Crown")
            {
                Log($"Changing OT info to:");
                cln.TrainerTID7 = tidsid % 1_000_000;
                cln.TrainerSID7 = tidsid / 1_000_000;
                cln.OT_Name = trainerName;
                cln.Version = data[4];
                cln.Language = data[5];
                cln.OT_Gender = data[6];

                if (toSend.IsEgg == false)
                {
                    if (cln.HeldItem >= 0 && cln.Species != (ushort)Species.Yamper || cln.Species != (ushort)Species.Spheal)
                        cln.SetDefaultNickname(); //Block nickname clear for item distro, Change Species as needed.
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

                Log($"OT_Name: {cln.OT_Name}");
                Log($"TID: {cln.TrainerTID7}");
                Log($"SID: {cln.TrainerSID7}");
                Log($"Gender: {(Gender)cln.OT_Gender}");
                Log($"Language: {(LanguageID)(cln.Language)}");
                Log($"Game: {(GameVersion)(cln.Version)}");

            }
            //OT for Overworld8 (Galar Birds/Gen 5 Trio/Marked mons)
            if (toSend.RibbonMarkFishing == true || toSend.Species == (ushort)Species.Cobalion || toSend.Species == (ushort)Species.Terrakion || toSend.Species == (ushort)Species.Virizion
                || toSend.Species == (ushort)Species.Zapdos && toSend.Form == 1 || toSend.Species == (ushort)Species.Moltres && toSend.Form == 1 || toSend.Species == (ushort)Species.Articuno && toSend.Form == 1)
            {
                if (toSend.Species == (ushort)Species.Zapdos || toSend.Species == (ushort)Species.Moltres || toSend.Species == (ushort)Species.Articuno)
                    Log($"Non-Shiny OW8, Do nothing to PID");
                else
                    cln.PID = (((uint)(cln.TID16 ^ cln.SID16) ^ (cln.PID & 0xFFFF) ^ 0) << 16) | (cln.PID & 0xFFFF);
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
                else //reroll PID for non-shiny
                {
                    cln.SetShiny();
                    cln.SetUnshiny();
                }
                if (toSend.PID != toSend.EncryptionConstant) //Filter old mons who are PID = EC
                    cln.SetRandomEC();
                else
                    cln.EncryptionConstant = cln.PID;
            }
            if (toSend.OT_Name == "Crown" && toSend.Ball == 16) //Galar Articuno Event, use ENG file as base
            {
                cln.Language = data[5];
                cln.OT_Name = cln.Language switch
                {
                    1 => "カンムリ",
                    3 => "Couronneige",
                    4 => "L. Corona",
                    5 => "Krone",
                    7 => "Corona",
                    8 => "왕관설원",
                    9 or 10 => "王冠",
                    _ => "Crown",
                };
                cln.SetDefaultNickname();
            }
            cln.RefreshChecksum();

            var tradeswsh = new LegalityAnalysis(cln); //Legality check, if fail, sends original PK8 instead
            if (tradeswsh.Valid)
            {
                Log($"OT Swap success");
                return (cln, true);
            }
            else
            {
                Log($"Pokemon was analyzed as not legal");
                return (toSend, false);
            }
        }
        private static bool OTChangeAllowed(PK8 mon, byte[] trader1)
        {
            var changeallowed = true;

            // Check if OT change is allowed for different situations
            switch (mon.Species)
            {
                //Zacian on Shield
                case (ushort)Species.Zacian:
                    if (trader1[4] == (int)GameVersion.SH && mon.Ball != 16)
                        changeallowed = false;
                    break;
                //Zamazenta on Sword
                case (ushort)Species.Zamazenta:
                    if (trader1[4] == (int)GameVersion.SW && mon.Ball != 16)
                        changeallowed = false;
                    break;
            }
            return changeallowed;
        }
    }
    public partial class PokeTradeBotBS : PokeRoutineExecutor8BS, ICountBot
    {
        private async Task<bool> SetTradePartnerDetailsBDSP(PB8 toSend, PB8 offered, SAV8BS sav, CancellationToken token)
        {
            var cln = (PB8)toSend.Clone();
            var tradepartner = await GetTradePartnerInfo(token).ConfigureAwait(false);

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
            Log($"Preparing to change OT");//offered - todo future
            cln.TrainerTID7 = offered.TrainerTID7;
            cln.TrainerSID7 = offered.TrainerSID7;
            cln.OT_Name = tradepartner.TrainerName;
            cln.Version = tradepartner.Game;
            cln.Language = offered.Language;
            cln.OT_Gender = offered.OT_Gender;

            if (toSend.IsEgg == false)
                cln.SetDefaultNickname();
            else //Set eggs received in Picnic, instead of received in Link Trade
            {
                cln.HeightScalar = (byte)rnd.Next(1, 254);
                cln.WeightScalar = (byte)rnd.Next(1, 254);
                cln.HT_Name = "";
                cln.HT_Language = 0;
                cln.HT_Gender = 0;
                cln.CurrentHandler = 0;
                cln.Met_Location = 65535;
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

            Log($"OT_Name: {cln.OT_Name}");
            Log($"TID: {cln.TrainerTID7}");
            Log($"SID: {cln.TrainerSID7}");
            Log($"Gender: {(Gender)cln.OT_Gender}");
            Log($"Language: {(LanguageID)(cln.Language)}");
            Log($"Game: {(GameVersion)(cln.Version)}");
            Log($"OT Swapped");

            //OT for Shiny Roamers, else set shiny as normal
            if (toSend.Species == (ushort)Species.Mesprit || toSend.Species == (ushort)Species.Cresselia)
                cln.PID = (((uint)(cln.TID16 ^ cln.SID16) ^ (cln.PID & 0xFFFF) ^ 1u) << 16) | (cln.PID & 0xFFFF);
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
                await SetBoxPokemonAbsolute(BoxStartOffset, cln, token, sav).ConfigureAwait(false);
            else Log($"Pokemon was analyzed as not legal");
            return tradebdsp.Valid;
        }
    }
    public partial class PokeTradeBotLA : PokeRoutineExecutor8LA, ICountBot
    {
        private async Task<bool> SetTradePartnerDetailsLA(PA8 toSend, SAV8LA sav, CancellationToken token)
        {
            var cln = (PA8)toSend.Clone();
            var tradepartner = await GetTradePartnerInfo(token).ConfigureAwait(false);

            cln.TrainerTID7 = Convert.ToUInt32(tradepartner.TID7);
            cln.TrainerSID7 = Convert.ToUInt32(tradepartner.SID7);
            cln.Language = tradepartner.Language;
            cln.OT_Name = tradepartner.TrainerName;
            cln.OT_Gender = tradepartner.Gender;
            cln.Version = tradepartner.Game;
            cln.SetDefaultNickname();

            Log($"OT_Name: {cln.OT_Name}");
            Log($"TID: {cln.TrainerTID7}");
            Log($"SID: {cln.TrainerSID7}");
            Log($"Gender: {(Gender)cln.OT_Gender}");
            Log($"Language: {(LanguageID)(cln.Language)}");
            Log($"OT Swap Success");

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
                await SetBoxPokemonAbsolute(BoxStartOffset, cln, token, sav).ConfigureAwait(false);
            else Log($"Pokemon was analyzed as not legal");
            return tradela.Valid;
        }
    }
}