using Discord.Commands;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public class PingModule : ModuleBase<SocketCommandContext>
    {
        [Command("ping")]
        [Summary("Makes the bot respond, indicating that it is running.")]
        public async Task PingAsync()
        {
            await ReplyAsync("https://tenor.com/view/who-ping-seal-ping-gif-24789222").ConfigureAwait(false);
        }
    }
}