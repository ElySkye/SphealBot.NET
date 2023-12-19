using System;
using System.Collections.Generic;
using Discord;

namespace SysBot.Base
{
    public static class EchoUtil
    {
        public static readonly List<Action<string>> Forwarders = new();
        public static readonly List<Action<Embed>> EmbedForwarders = new();

        public static void Echo(string message)
        {
            foreach (var fwd in Forwarders)
            {
                try
                {
                    fwd(message);
                }
                catch (Exception ex)
                {
                    LogUtil.LogInfo($"Exception: {ex} occurred while trying to echo: {message} to the forwarder: {fwd}", "Echo");
                    LogUtil.LogSafe(ex, "Echo");
                }
            }
            LogUtil.LogInfo(message, "Echo");
        }
        public static void EchoEmbed(Embed embedObj)
        {
            foreach (var fwd in EmbedForwarders)
            {
                try
                {
                    fwd(embedObj);
                }
                catch (Exception ex)
                {
                    LogUtil.LogInfo($"Exception: {ex} occurred while trying to echo: to the forwarder: {fwd}", "EchoEmbed");
                    LogUtil.LogSafe(ex, "EchoEmbed");
                }
            }
            LogUtil.LogInfo("Echoing an Embed to the forwarder", "EchoEmbed");
        }
    }
}