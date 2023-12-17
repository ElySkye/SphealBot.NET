using Discord.Commands;
using System;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public class PingModule : ModuleBase<SocketCommandContext>
    {
        [Command("ping")]
        [Summary("Makes the bot respond, indicating that it is running.")]
        public async Task PingAsync()
        {
            var custom = SysCordSettings.HubConfig.CustomEmbed;
            var msg = "";
            Random rndmsg = new();
            int num = rndmsg.Next(1, 12);
            switch (num)
            {
                case 1:
                    msg = "https://tenor.com/view/who-ping-seal-ping-gif-24789222";
                    break;
                case 2:
                    msg = "https://media.tenor.com/aT_NiRIu7x4AAAAM/adalfarus-okbb.gif";
                    break;
                case 3:
                    msg = "https://i.pinimg.com/originals/b1/f1/53/b1f15382caa471a94eed6e5587f6b2b7.gif";
                    break;
                case 4:
                    msg = "https://68.media.tumblr.com/b2a0199a8d8898cc3b308722aa7ad32a/tumblr_os6whlXFce1vbdodoo1_1280.gif";
                    break;
                case 5:
                    msg = "https://media.tenor.com/6Wu-MMdSdu8AAAAC/officer-dogdog-capoo.gif";
                    break;
                case 6:
                    msg = "https://tenor.com/view/cat-ping-pong-funny-animals-cats-gif-8766860";
                    break;
                case 7:
                case 8:
                case 9:
                case 10:
                case 11:
                case 12:
                    if (custom.CustomGIFs && custom.CustomPingMsg != null)
                        msg = $"{custom.CustomPingMsg}";
                    else
                        msg = "https://media.tenor.com/WHOwHxdVSQIAAAAC/capoo-capoo-type.gif";
                    break;
            }
            await ReplyAsync(msg).ConfigureAwait(false);
        }
    }
}