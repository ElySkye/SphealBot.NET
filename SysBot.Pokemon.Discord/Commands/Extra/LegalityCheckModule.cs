using Discord;
using Discord.Commands;
using PKHeX.Core;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public class LegalityCheckModule : ModuleBase<SocketCommandContext>
    {
        [Command("lc"), Alias("check", "validate", "verify")]
        [Summary("Verifies the attachment for legality.")]
        public async Task LegalityCheck()
        {
            var attachments = Context.Message.Attachments;
            foreach (var att in attachments)
                await LegalityCheck(att, false).ConfigureAwait(false);
        }

        [Command("lcv"), Alias("verbose")]
        [Summary("Verifies the attachment for legality with a verbose output.")]
        public async Task LegalityCheckVerbose()
        {
            var attachments = Context.Message.Attachments;
            foreach (var att in attachments)
                await LegalityCheck(att, true).ConfigureAwait(false);
        }

        private async Task LegalityCheck(IAttachment att, bool verbose)
        {
            var download = await NetUtil.DownloadPKMAsync(att).ConfigureAwait(false);
            if (!download.Success)
            {
                await ReplyAsync(download.ErrorMessage).ConfigureAwait(false);
                return;
            }

            Sphealcl spheal = new();
            bool CanGMax = false;
            var pkm = download.Data!;
            uint FormArgument = 0;
            switch (pkm.Version)
            {
                case (int)GameVersion.X or (int)GameVersion.Y:
                    PK6 mon6 = (PK6)pkm.Clone();
                    FormArgument = mon6.FormArgument;
                    break;
                case (int)GameVersion.SN or (int)GameVersion.MN or (int)GameVersion.US or (int)GameVersion.UM:
                    PK7 mon7 = (PK7)pkm.Clone();
                    FormArgument = mon7.FormArgument;
                    break;
                case (int)GameVersion.GP or (int)GameVersion.GE:
                    PB7 monLGPE = (PB7)pkm.Clone();
                    FormArgument = monLGPE.FormArgument;
                    break;
                case (int)GameVersion.SW or (int)GameVersion.SH:
                    PK8 mon8 = (PK8)pkm.Clone();
                    CanGMax = mon8.CanGigantamax;
                    FormArgument = mon8.FormArgument;
                    break;
                case (int)GameVersion.BD or (int)GameVersion.SP:
                    PB8 monBDSP = (PB8)pkm.Clone();
                    FormArgument = monBDSP.FormArgument;
                    break;
                case (int)GameVersion.PLA:
                    PA8 monLA = (PA8)pkm.Clone();
                    FormArgument = monLA.FormArgument;
                    break;
                case (int)GameVersion.SL or (int)GameVersion.VL:
                    PK9 mon9 = (PK9)pkm.Clone();
                    FormArgument = mon9.FormArgument;
                    break;
            }

            var la = new LegalityAnalysis(pkm);
            string embedThumbUrl = await spheal.EmbedImgUrlBuilder(pkm, false, FormArgument.ToString("00000000")).ConfigureAwait(false);

            EmbedAuthorBuilder embedAuthor = new()
            {
                IconUrl = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/Ballimg/50x50/" + ((Ball)pkm.Ball).ToString().ToLower() + "ball.png",
                Name = $"Legality Report Request",
            };
            EmbedFooterBuilder embedFtr = new()
            {
                Text = $"SphealBot",
                IconUrl = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/Sprites/200x200/poke_capture_0363_000_mf_n_00000000_f_n.png"
            };

            var builder = new EmbedBuilder
            {
                Author = embedAuthor,
                Color = la.Valid ? Color.Green : Color.Red,
                ThumbnailUrl = embedThumbUrl,
                Description = $"**File**: {download.SanitizedFileName}\n**Requester**: {Context.User.Mention}",
                Footer = embedFtr
            };

            builder.AddField(x =>
            {
                x.Name = la.Valid ? "Valid" : "Invalid";
                x.Value = la.Report(verbose);
                x.IsInline = false;
            });

            await ReplyAsync("", false, builder.Build()).ConfigureAwait(false);
        }
    }
}