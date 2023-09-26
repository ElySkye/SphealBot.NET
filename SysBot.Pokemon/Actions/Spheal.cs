using PKHeX.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using System.Net.Http;
using SysBot.Base;
using System.Net;

namespace SysBot.Pokemon
{
    public partial class PokeTradeBotBS : PokeRoutineExecutor8BS, ICountBot
    {
        private async Task<bool> SetTradePartnerDetailsBDSP(PB8 toSend, PB8 offered, SAV8BS sav, CancellationToken token)
        {
            var cln = (PB8)toSend.Clone();
            var custom = Hub.Config.CustomSwaps;
            var tradepartner = await GetTradePartnerInfo(token).ConfigureAwait(false);
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

            cln.TrainerTID7 = offered.TrainerTID7;
            cln.TrainerSID7 = offered.TrainerSID7;
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
                Log($"OT Swap Success");
                await SetBoxPokemonAbsolute(BoxStartOffset, cln, token, sav).ConfigureAwait(false);
            }   
            else
                Log($"Sending original Pokémon as it can't be OT swapped");
            return tradebdsp.Valid;
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
            string embedImageURL = "https://media.tenor.com/6Wu-MMdSdu8AAAAC/officer-dogdog-capoo.gif";

            EmbedAuthorBuilder embedAuthor = new()
            {
                IconUrl = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/alert.png",
                Name = msgTitle,
            };
            EmbedFooterBuilder embedFtr = new()
            {
                Text = $"Last Seen: {cdAbuse.TotalMinutes:F1} mins ago.\nCurrent Cooldown: {cd} mins\nStrike {attempts} out of {repeatConnections}",
                IconUrl = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/approvalspheal.png"
            };
            EmbedBuilder embedBuilder = new()
            {
                Color = Color.Blue,
                ThumbnailUrl = embedThumbUrl,
                ImageUrl = embedImageURL,
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
        public static Embed EmbedBanMessage(string msg, string msgTitle, bool banned = false)
        {
            string embedThumbUrl = "https://www.serebii.net/scarletviolet/ribbons/alphamark.png";
            string embedImageURL;
            if (banned)
                embedImageURL = "https://media.tenor.com/9zCgefg___cAAAAC/bane-no.gif";
            else
                embedImageURL = "https://media.tenor.com/EOPjsoaIVOYAAAAC/sirius-sirius-black.gif0";

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
                ImageUrl = embedImageURL,
                Description = "" + msg + "",
                Author = embedAuthor,
                Footer = embedFtr
            };
            Embed embedMsg = embedBuilder.Build();
            return embedMsg;
        }
        public static Embed EmbedSFList(string msg, string msgTitle)
        {
            string embedThumbUrl = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/Sprites/200x200/poke_capture_0363_000_mf_n_00000000_f_n.png";

            EmbedAuthorBuilder embedAuthor = new()
            {
                IconUrl = "https://archives.bulbagarden.net/media/upload/1/1e/ShinyLAStar.png",
                Name = msgTitle,
            };
            EmbedFooterBuilder embedFtr = new()
            {
                Text = $"Special Features - Spheal Bot",
                IconUrl = "https://www.serebii.net/games/ribbons/conteststarribbon.png"
            };
            EmbedBuilder embedBuilder = new()
            {
                Color = Color.Teal,
                ThumbnailUrl = embedThumbUrl,
                Description = "" + msg + "",
                Author = embedAuthor,
                Footer = embedFtr
            };
            Embed embedMsg = embedBuilder.Build();
            return embedMsg;
        }
        public static Embed EmbedEggMystery(PKM toSend, string msg, string msgTitle)
        {
            string embedThumbUrl = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/Sprites/512x512/MysteryEgg.png";

            EmbedAuthorBuilder embedAuthor = new()
            {
                IconUrl = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/Ballimg/50x50/" + ((Ball)toSend.Ball).ToString().ToLower() + "ball.png",
                Name = msgTitle,
            };
            EmbedFooterBuilder embedFtr = new()
            {
                Text = $"What could be inside that Egg? - Enjoy!",
                IconUrl = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/Sprites/512x512/egg.png"
            };
            EmbedBuilder embedBuilder = new()
            {
                Color = Color.Teal,
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