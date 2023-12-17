using PKHeX.Core;
using System;
using System.Threading.Tasks;
using Discord;
using System.Net.Http;
using SysBot.Base;
using System.Net;

namespace SysBot.Pokemon
{
    public class Sphealcl
    {
        static readonly HttpClient client = new();
        private readonly PokeTradeHubConfig Hub;
        public Sphealcl(PokeTradeHubConfig hub)
        {
            Hub = hub;
        }
        public Sphealcl()
        {
            Hub = new PokeTradeHubConfig();
        }

        public async Task EmbedPokemonMessage(PKM toSend, bool CanGMAX, uint formArg, string msg, string msgTitle)
        {
            EmbedAuthorBuilder embedAuthor = new()
            {
                IconUrl = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/Ballimg/50x50/" + ((Ball)toSend.Ball).ToString().ToLower() + "ball.png",
                Name = msgTitle,
            };

            string embedThumbUrl = await EmbedImgUrlBuilder(toSend, CanGMAX, formArg.ToString("00000000")).ConfigureAwait(false);

#pragma warning disable CS8604 // Possible null reference argument.
            Color embedMsgColor = new Color((uint)Enum.Parse(typeof(EmbedColor), Enum.GetName(typeof(Ball), toSend.Ball)));
#pragma warning restore CS8604 // Possible null reference argument.

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
        public async Task EmbedAlertMessage(PKM offered, bool CanGMAX, uint formArg, string msg, string msgTitle)
        {
            string embedThumbUrl = await EmbedImgUrlBuilder(offered, CanGMAX, formArg.ToString("00000000")).ConfigureAwait(false);

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
        public async Task EmbedMarkMessage(PKM toSend, bool CanGMAX, uint formArg, string msg, int counts, string msgTitle, string markimg)
        {
            string embedThumbUrl = await EmbedImgUrlBuilder(toSend, CanGMAX, formArg.ToString("00000000")).ConfigureAwait(false);

            EmbedAuthorBuilder embedAuthor = new()
            {
                IconUrl = "https://www.serebii.net/swordshield/ribbons/worldchampionribbon.png",
                Name = msgTitle,
            };
            EmbedFooterBuilder embedFtr = new()
            {
                Text = $"Total Mark Swaps: {counts}",
                IconUrl = "https://archives.bulbagarden.net/media/upload/2/26/Bag_Mark_Charm_SV_Sprite.png"
            };

            EmbedBuilder embedBuilder = new()
            {
                Color = Color.Gold,
                ThumbnailUrl = embedThumbUrl,
                ImageUrl = markimg,
                Description = "" + msg + "",
                Author = embedAuthor,
                Footer = embedFtr
            };

            Embed embedMsg = embedBuilder.Build();

            EchoUtil.EchoEmbed(embedMsg);
        }
        public async Task EmbedTradeEvoMsg(PKM offered, bool CanGMAX, uint formArg, string msg, string msgTitle, int attempts, int repeatConnections, bool banned = false)
        {
            string embedImageURL = "";
            var custom = Hub.CustomEmbed;
            
            if (banned)
            {
                if (custom.CustomGIFs && custom.BanEmbedGIF != null)
                    embedImageURL = $"{custom.BanEmbedGIF}";
                else
                    embedImageURL = "https://media.tenor.com/9zCgefg___cAAAAC/bane-no.gif";
            }

            string embedThumbUrl = await EmbedImgUrlBuilder(offered, CanGMAX, formArg.ToString("00000000")).ConfigureAwait(false);

            EmbedAuthorBuilder embedAuthor = new()
            {
                IconUrl = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/alert.png",
                Name = msgTitle,
            };
            EmbedFooterBuilder embedFtr = new()
            {
                Text = $"Strike {attempts} out of {repeatConnections}",
                IconUrl = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/approvalspheal.png"
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

            EchoUtil.EchoEmbed(embedMsg);
        }
        public Embed EmbedCDMessage(TimeSpan cdAbuse, double cd, int attempts, int repeatConnections, string msg, string msgTitle)
        {
            var custom = Hub.CustomEmbed;
            string embedThumbUrl = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/Sprites/200x200/poke_capture_0363_000_mf_n_00000000_f_n.png";
            string embedImageURL;

            if (custom.CustomGIFs && custom.CooldownGIF != null)
                embedImageURL = $"{custom.CooldownGIF}";
            else
                embedImageURL = "https://media.tenor.com/6Wu-MMdSdu8AAAAC/officer-dogdog-capoo.gif";

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
                Text = $"Cooldown: {cd} mins",
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
        public static EmbedBuilder EmbedGeneric(string msg, string msgTitle)
        {
            EmbedAuthorBuilder embedAuthor = new()
            {
                IconUrl = "https://archives.bulbagarden.net/media/upload/b/bb/Tretta_Mega_Evolution_icon.png",
                Name = msgTitle,
            };
            EmbedFooterBuilder embedFtr = new()
            {
                Text = $"Spheal Bot",
                IconUrl = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/Sprites/200x200/poke_capture_0363_000_mf_n_00000000_f_n.png"
            };
            EmbedBuilder embedBuilder = new()
            {
                Color = Color.DarkOrange,
                Description = "" + msg + "",
                Author = embedAuthor,
                Footer = embedFtr
            };
            return embedBuilder;
        }
        public static EmbedBuilder EmbedSpheal(string msg, string msgTitle, string gif, int counts)
        {
            EmbedAuthorBuilder embedAuthor = new()
            {
                IconUrl = "https://cdn.discordapp.com/emojis/1115571174949265428.gif?size=128&quality=lossless",
                Name = msgTitle,
            };
            EmbedFooterBuilder embedFtr = new()
            {
                Text = $"Spheal Facts Read: {counts}",
                IconUrl = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/Sprites/200x200/poke_capture_0363_000_mf_n_00000000_f_n.png"
            };
            EmbedBuilder embedBuilder = new()
            {
                Color = Color.Teal,
                ImageUrl = gif,
                Description = "" + msg + "",
                Author = embedAuthor,
                Footer = embedFtr
            };
            return embedBuilder;
        }
        public Embed EmbedBanMessage(string msg, string msgTitle, bool banned = false)
        {
            string embedThumbUrl = "https://www.serebii.net/scarletviolet/ribbons/alphamark.png";
            string embedImageURL;
            var custom = Hub.CustomEmbed;

            if (custom.CustomGIFs && custom.BanEmbedGIF != null && custom.BanUEmbedGIF != null)
            {
                if (banned)
                    embedImageURL = $"{custom.BanEmbedGIF}";
                else
                    embedImageURL = $"{custom.BanUEmbedGIF}";
            }
            else
            {
                if (banned)
                    embedImageURL = "https://media.tenor.com/mjsMmSBtmzIAAAAd/honkai-star-rail-kafka.gif";
                else
                    embedImageURL = "https://media.tenor.com/oRmZ2_3oHZQAAAAd/topaz-star-rail.gif";
            }

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
        public static Embed EmbedSFList(string msg, string msgTitle, bool marks = false)
        {
            string embedThumbUrl = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/Sprites/200x200/poke_capture_0363_000_mf_n_00000000_f_n.png";
            string embedImageURL;

            if (marks)
                embedImageURL = "https://media.discordapp.net/attachments/1092662051777826856/1159371227165642784/image.png?ex=6530c798&is=651e5298&hm=f1a1f7441b303d401d39804e47dc6ceb8f747644289104ca0cc5ffc2ba3656bb&=&width=972&height=662";
            else
                embedImageURL = "";

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
                ImageUrl = embedImageURL,
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
        public static string MoveTypeImage(ushort move, CustomEmbedSettings custom, EntityContext context)
        {
            string url;
            url = (MoveType)MoveInfo.GetType(move, context) switch
            {
                MoveType.Bug => custom.TeraIMGBug,
                MoveType.Dark => custom.TeraIMGDark,
                MoveType.Dragon => custom.TeraIMGDragon,
                MoveType.Electric => custom.TeraIMGElectric,
                MoveType.Fairy => custom.TeraIMGFairy,
                MoveType.Fighting => custom.TeraIMGFighting,
                MoveType.Fire => custom.TeraIMGFire,
                MoveType.Flying => custom.TeraIMGFlying,
                MoveType.Ghost => custom.TeraIMGGhost,
                MoveType.Grass => custom.TeraIMGGrass,
                MoveType.Ground => custom.TeraIMGGround,
                MoveType.Ice => custom.TeraIMGIce,
                MoveType.Normal => custom.TeraIMGNormal,
                MoveType.Poison => custom.TeraIMGPoison,
                MoveType.Psychic => custom.TeraIMGPsychic,
                MoveType.Rock => custom.TeraIMGRock,
                MoveType.Steel => custom.TeraIMGSteel,
                MoveType.Water => custom.TeraIMGWater,
                _ => custom.TeraIMGStellar, //unreleased
            };
            return url;
        }
    }
}