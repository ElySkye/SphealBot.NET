using Discord;
using Discord.Commands;
using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public class HubModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
    {
        [Command("status")]
        [Alias("stats")]
        [Summary("Gets the status of the bot environment.")]
        public async Task GetStatusAsync()
        {
            var me = SysCord<T>.Runner;
            var hub = me.Hub;
            var bots = me.Bots.Select(z => z.Bot).OfType<PokeRoutineExecutorBase>().ToArray();

            EmbedAuthorBuilder embedAuthor = new()
            {
                IconUrl = "https://cdn.discordapp.com/emojis/1116237525665718352.webp?size=128&quality=lossless",
                Name = "Bot Summary & Status",
            };
            EmbedFooterBuilder embedFtr = new()
            {
                Text = $"SphealBot",
                IconUrl = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/Sprites/200x200/poke_capture_0363_000_mf_n_00000000_f_n.png"
            };

            var builder = new EmbedBuilder
            {
                Author = embedAuthor,
                Color = Color.Gold,
                Footer = embedFtr
            };

            var runner = SysCord<T>.Runner;
            var allBots = runner.Bots.ConvertAll(z => z.Bot);
            var botCount = allBots.Count;
            builder.AddField(x =>
            {
                x.Name = "Summary";
                x.Value =
                    $"Bot Count: {botCount}\n" +
                    $"Bot State: {SummarizeBots(allBots)}\n" +
                    $"Pool Count: {hub.Ledy.Pool.Count}\n";
                x.IsInline = false;
            });

            builder.AddField(x =>
            {
                var bots = allBots.OfType<ICountBot>();
                var lines = bots.SelectMany(z => z.Counts.GetNonZeroCounts()).Distinct();
                var msg = string.Join("\n", lines);
                if (string.IsNullOrWhiteSpace(msg))
                    msg = "Nothing counted yet!";
                x.Name = "Trade Stats";
                x.Value = msg;
                x.IsInline = false;
            });

            var queues = hub.Queues.AllQueues;
            int count = 0;
            foreach (var q in queues)
            {
                var c = q.Count;
                if (c == 0)
                    continue;

                var nextMsg = GetNextName(q);
                builder.AddField(x =>
                {
                    x.Name = $"{q.Type} Queue";
                    x.Value =
                        $"Next: {nextMsg}\n" +
                        $"Count: {c}\n";
                    x.IsInline = false;
                });
                count += c;
            }

            if (count == 0)
            {
                builder.AddField(x =>
                {
                    x.Name = "Queues are empty.";
                    x.Value = "Nobody in line!";
                    x.IsInline = false;
                });
            }

            builder.AddField(x =>
            {
                var bot0 = me.Bots.Select(z => z.Bot).OfType<PokeRoutineExecutorBase>().ToArray();
                var summaries = bot0.Select(GetDetailedSummary);
                var lines = string.Join("\n", summaries);
                if (bot0.Length == 0)
                {
                    lines = "No bots configured.";
                }

                var msgS = string.Join("\n", lines);
                x.Name = "Bot Status";
                x.Value = msgS;
                x.IsInline = false;
            });

            await ReplyAsync("", false, builder.Build()).ConfigureAwait(false);
        }
        private static string GetDetailedSummary(PokeRoutineExecutorBase z)
        {
            return $"```{z.Connection.Label} - {z.Config.CurrentRoutineType} ~ {z.LastTime:hh:mm:ss} | {z.LastLogged}```";
        }

        private static string GetNextName(PokeTradeQueue<T> q)
        {
            var next = q.TryPeek(out var detail, out _);
            if (!next)
                return "None!";

            var name = detail.Trainer.TrainerName;

            // show detail of trade if possible
            var nick = detail.TradeData.Nickname;
            if (!string.IsNullOrEmpty(nick))
                name += $" - {nick}";
            return name;
        }

        private static string SummarizeBots(IReadOnlyCollection<RoutineExecutor<PokeBotState>> bots)
        {
            if (bots.Count == 0)
                return "No bots configured.";
            var summaries = bots.Select(z => $"- {z.GetSummary()}");
            return Environment.NewLine + string.Join(Environment.NewLine, summaries);
        }
    }
}
