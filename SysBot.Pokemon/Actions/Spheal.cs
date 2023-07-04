using PKHeX.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Pokemon.PokeDataOffsetsSWSH;
using Discord;
using System.Net.Http;
using SysBot.Base;
using System.Net;

namespace SysBot.Pokemon
{
    public partial class PokeTradeBotSWSH : PokeRoutineExecutor8SWSH, ICountBot
    {
        private async Task<(PK8, bool)> SetTradePartnerDetailsSWSH(PK8 toSend, PK8 offered, string trainerName, SAV8SWSH sav, CancellationToken token)
        {
            var data = await Connection.ReadBytesAsync(LinkTradePartnerNameOffset - 0x8, 8, token).ConfigureAwait(false);
            var tidsid = BitConverter.ToUInt32(data, 0);
            var cln = (PK8)toSend.Clone();
            var changeallowed = OTChangeAllowed(toSend, data);
            var config = Hub.Config.Distribution;
            var counts = TradeSettings;

            if (changeallowed)
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

                Log($"OT_Name: {cln.OT_Name}");
                Log($"TID: {cln.TrainerTID7}");
                Log($"SID: {cln.TrainerSID7}");
                Log($"Gender: {(Gender)cln.OT_Gender}");
                Log($"Language: {(LanguageID)(cln.Language)}");
                Log($"Game: {(GameVersion)(cln.Version)}");

            }
            if (BallSwap(offered.HeldItem) != 0 && cln.HeldItem != (int)config.OTSwapItem) //Distro Ball Selector
            {
                cln.Ball = BallSwap(offered.HeldItem);
                Log($"Ball swapped to: {(Ball)cln.Ball}");
            }
            //OT for Overworld8 (Galar Birds/Swords of Justice/Marked mons)
            if (toSend.RibbonMarkFishing == true || toSend.Species == (ushort)Species.Keldeo || toSend.Species == (ushort)Species.Cobalion || toSend.Species == (ushort)Species.Terrakion || toSend.Species == (ushort)Species.Virizion
                || toSend.Species == (ushort)Species.Zapdos && toSend.Form == 1 || toSend.Species == (ushort)Species.Moltres && toSend.Form == 1 || toSend.Species == (ushort)Species.Articuno && toSend.Form == 1)
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
            cln.RefreshChecksum();

            var tradeswsh = new LegalityAnalysis(cln); //Legality check, if fail, sends original PK8 instead
            if (tradeswsh.Valid)
            {
                if (toSend.HeldItem == (int)config.OTSwapItem)
                {
                    DumpPokemon(DumpSetting.DumpFolder, "OTSwaps", cln);
                    counts.AddCompletedOTSwaps();
                }
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
            {
                if (cln.HeldItem >= 0 && cln.Species != (ushort)Species.Spheal)
                    cln.SetDefaultNickname();
            }
            else //Set eggs received in Picnic, instead of received in Link Trade
            {
                cln.HeightScalar = (byte)rnd.Next(0, 255);
                cln.WeightScalar = (byte)rnd.Next(0, 255);
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

            if (BallSwap(offered.HeldItem) != 0) //Distro Ball Selector
            {
                cln.Ball = BallSwap(offered.HeldItem);
                Log($"Ball swapped to: {(Ball)cln.Ball}");
            }

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
    public class Sphealcl
    {
        static readonly HttpClient client = new();
        public async Task EmbedPokemonMessage(PKM toSend, bool CanGMAX, uint formArg, string msg, string msgTitle)
        {
            EmbedAuthorBuilder embedAuthor = new()
            {
                IconUrl = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/Ballimg/50x50/" + ((Ball)toSend.Ball).ToString().ToLower() + "ball.png",
                Name = msgTitle,
            };

            string embedThumbUrl = await EmbedImgUrlBuilder(toSend, CanGMAX, formArg.ToString("00000000")).ConfigureAwait(false);

            Color embedMsgColor = new Color((uint)Enum.Parse(typeof(EmbedColor), Enum.GetName(typeof(Ball), toSend.Ball)));

            EmbedBuilder embedBuilder = new()
            {
                Color = embedMsgColor,
                ThumbnailUrl = embedThumbUrl,
                Description = "" + msg + "",
                Author = embedAuthor
            };
            Embed embedMsg = embedBuilder.Build();
            EchoUtil.EchoEmbed(embedMsg);
        }
        public async Task EmbedAlertMessage(PKM toSend, bool CanGMAX, uint formArg, string msg, string msgTitle)
        {
            string embedThumbUrl = await EmbedImgUrlBuilder(toSend, CanGMAX, formArg.ToString("00000000")).ConfigureAwait(false);

            EmbedAuthorBuilder embedAuthor = new()
            {
                IconUrl = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/alert.png",
                Name = msgTitle,
            };

            EmbedBuilder embedBuilder = new()
            {
                Color = Color.Red,
                ThumbnailUrl = embedThumbUrl,
                Description = "" + msg + "",
                Author = embedAuthor
            };

            Embed embedMsg = embedBuilder.Build();

            EchoUtil.EchoEmbed(embedMsg);
        }
        public static Embed EmbedCDMessage(TimeSpan cdAbuse, double cd, int attempts, int repeatConnections, string msg, string msgTitle)
        {
            string embedThumbUrl = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/Sprites/200x200/poke_capture_0363_000_mf_n_00000000_f_n.png";

            EmbedAuthorBuilder embedAuthor = new()
            {
                IconUrl = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/alert.png",
                Name = msgTitle,
            };
            EmbedFooterBuilder embedFtr = new()
            {
                Text = $"Last encountered {cdAbuse.TotalMinutes:F1} minutes ago.\nIgnoring the the {cd} minute trade cooldown.\nStrike {attempts} out of {repeatConnections}",
                IconUrl = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/approvalspheal.png"
            };
            EmbedBuilder embedBuilder = new()
            {
                Color = Color.Blue,
                ThumbnailUrl = embedThumbUrl,
                Description = "" + msg + "",
                Author = embedAuthor,
                Footer = embedFtr
            };
            Embed embedMsg = embedBuilder.Build();
            return embedMsg;
        }
        public static EmbedBuilder EmbedCDMessage2(double cd, string msg, string msgTitle)
        {
            string embedThumbUrl = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/Sprites/200x200/poke_capture_0363_000_mf_n_00000000_f_n.png";

            EmbedAuthorBuilder embedAuthor = new()
            {
                IconUrl = "https://www.serebii.net/games/ribbons/twinklingstarribbon.png",
                Name = msgTitle,
            };
            EmbedFooterBuilder embedFtr = new()
            {
                Text = $"Current trade cooldown of the bot is {cd} mins",
                IconUrl = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/approvalspheal.png"
            };
            EmbedBuilder embedBuilder = new()
            {
                Color = Color.DarkTeal,
                ThumbnailUrl = embedThumbUrl,
                Description = "" + msg + "",
                Author = embedAuthor,
                Footer = embedFtr
            };
            return embedBuilder;
        }
        public static Embed EmbedBanMessage(string msg, string msgTitle)
        {
            string embedThumbUrl = "https://www.serebii.net/scarletviolet/ribbons/alphamark.png";

            EmbedAuthorBuilder embedAuthor = new()
            {
                IconUrl = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/alert.png",
                Name = msgTitle,
            };
            EmbedFooterBuilder embedFtr = new()
            {
                Text = $"They may want to consider buying a new clock",
                IconUrl = "https://archives.bulbagarden.net/media/upload/9/9b/Pok%C3%A9_Mart_FRLG.png"
            };
            EmbedBuilder embedBuilder = new()
            {
                Color = Color.Red,
                ThumbnailUrl = embedThumbUrl,
                Description = "" + msg + "",
                Author = embedAuthor,
                Footer = embedFtr
            };
            Embed embedMsg = embedBuilder.Build();
            return embedMsg;
        }
        public async Task<string> EmbedImgUrlBuilder(PKM mon, bool canGMax, string URLFormArg)
        {
            string URLStart = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/Sprites/200x200/poke_capture";
            string URLString, URLGender;
            string URLGMax = canGMax ? "g" : "n";
            string URLShiny = mon.IsShiny ? "r.png" : "n.png";

            if (mon.Gender < 2)
                URLGender = "mf";
            else
                URLGender = "uk";

            URLString = URLStart + "_" + mon.Species.ToString("0000") + "_" + mon.Form.ToString("000") + "_" + URLGender + "_" + URLGMax + "_" + URLFormArg + "_f_" + URLShiny;

            if (mon.IsEgg)
                URLString = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/Sprites/512x512/egg.png";

            try
            {
                using HttpResponseMessage response = await client.GetAsync(URLString);
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                if (mon.Gender == 0)
                    URLGender = "md";
                else
                    URLGender = "fd";

                URLString = URLStart + "_" + mon.Species.ToString("0000") + "_" + mon.Form.ToString("000") + "_" + URLGender + "_" + URLGMax + "_" + URLFormArg + "_f_" + URLShiny;

                try
                {
                    using HttpResponseMessage response = await client.GetAsync(URLString);
                    response.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException ex1) when (ex1.StatusCode == HttpStatusCode.NotFound)
                {
                    if (mon.Gender == 0)
                        URLGender = "mo";
                    else
                        URLGender = "fo";

                    URLString = URLStart + "_" + mon.Species.ToString("0000") + "_" + mon.Form.ToString("000") + "_" + URLGender + "_" + URLGMax + "_" + URLFormArg + "_f_" + URLShiny;
                }
            }
            return URLString;
        }
        public static string FixHeldItemName(string name)
        {
            name = name.Replace("____", " ");
            name = name.Replace("___", ".");
            name = name.Replace("__", "'");
            name = name.Replace("_", "-");
            return name;
        }
    }
}