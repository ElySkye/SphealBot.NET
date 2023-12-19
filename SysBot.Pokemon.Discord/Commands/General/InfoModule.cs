using Discord;
using Discord.Commands;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    // src: https://github.com/foxbot/patek/blob/master/src/Patek/Modules/InfoModule.cs
    // ISC License (ISC)
    // Copyright 2017, Christopher F. <foxbot@protonmail.com>
    public class InfoModule : ModuleBase<SocketCommandContext>
    {
        private const string detail = "An open-source Discord bot powered by PKHeX.Core and other open-source software.";
        private const string repo = "https://github.com/kwsch/SysBot.NET";
        private const string repo2 = "https://github.com/ElySkye/SphealBot.NET/tree/SphealMain";

        [Command("info")]
        [Alias("about", "whoami", "owner")]
        public async Task InfoAsync()
        {
            var app = await Context.Client.GetApplicationInfoAsync().ConfigureAwait(false);
            string icon = "https://cdn.discordapp.com/emojis/1116237525665718352.webp?size=128&quality=lossless";
            string msgTitle = "SphealBot Info";
            EmbedAuthorBuilder embedAuthor = new()
            {
                IconUrl = icon,
                Name = msgTitle,
            };
            EmbedFooterBuilder embedFtr = new()
            {
                Text = $"SphealBot",
                IconUrl = "https://raw.githubusercontent.com/PhantomL98/HomeImages/main/Sprites/200x200/poke_capture_0363_000_mf_n_00000000_f_n.png"
            };
            var builder = new EmbedBuilder
            {
                Author = embedAuthor,
                Color = new Color(114, 137, 218),
                Description = detail,
                Footer = embedFtr
            };

            builder.AddField("Info",
                $"- [Original Source Code]({repo})\n" +
                $"- [This Fork's Source Code]({repo2})\n" +
                $"- {Format.Bold("Owner")}: {app.Owner.GlobalName} ({app.Owner.Id})\n" +
                $"- {Format.Bold("Library")}: Discord.Net ({DiscordConfig.Version})\n" +
                $"- {Format.Bold("Uptime")}: {GetUptime()}\n" +
                $"- {Format.Bold("Runtime")}: {RuntimeInformation.FrameworkDescription} {RuntimeInformation.ProcessArchitecture} " +
                $"({RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture})\n" +
                $"- {Format.Bold("Buildtime")}: {GetVersionInfo("SysBot.Base", false)}\n" +
                $"- {Format.Bold("Core Version")}: {GetVersionInfo("PKHeX.Core")}\n" +
                $"- {Format.Bold("AutoLegality Version")}: {GetVersionInfo("PKHeX.Core.AutoMod")}\n"
                );

            builder.AddField("Stats",
                $"- {Format.Bold("Heap Size")}: {GetHeapSize()}MiB\n" +
                $"- {Format.Bold("Guilds")}: {Context.Client.Guilds.Count}\n" +
                $"- {Format.Bold("Channels")}: {Context.Client.Guilds.Sum(g => g.Channels.Count)}\n" +
                $"- {Format.Bold("Users")}: {Context.Client.Guilds.Sum(g => g.MemberCount)}\n"
                );

            await ReplyAsync("Here's a bit about me!", embed: builder.Build()).ConfigureAwait(false);
        }

        private static string GetUptime() => (DateTime.Now - Process.GetCurrentProcess().StartTime).ToString(@"dd\.hh\:mm\:ss");
        private static string GetHeapSize() => Math.Round(GC.GetTotalMemory(true) / (1024.0 * 1024.0), 2).ToString(CultureInfo.CurrentCulture);

        private static string GetVersionInfo(string assemblyName, bool inclVersion = true)
        {
            const string _default = "Unknown";
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var assembly = assemblies.FirstOrDefault(x => x.GetName().Name == assemblyName);
            if (assembly is null)
                return _default;

            var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (attribute is null)
                return _default;

            var info = attribute.InformationalVersion;
            var split = info.Split('+');
            if (split.Length >= 2)
            {
                var version = split[0];
                var revision = split[1];
                if (DateTime.TryParseExact(revision, "yyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var buildTime))
                    return inclVersion ? $"{version} ({buildTime:yy-MM-dd\\.hh\\:mm})" : buildTime.ToString(@"yy-MM-dd\.hh\:mm");
                return inclVersion ? version : _default;
            }
            return _default;
        }
    }
}
