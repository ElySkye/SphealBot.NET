using Discord.Commands;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public class HelloModule : ModuleBase<SocketCommandContext>
    {
        [Command("NPC")]
        [Alias("NPC Status")]
        [Summary("What does Spheal think of this NPC")]
        public async Task PingAsync()
        {
            var str = SysCordSettings.Settings.HelloResponse;
            var msg = string.Format(str, Context.User.Mention);
            await ReplyAsync(msg).ConfigureAwait(false);
        }
    }
}