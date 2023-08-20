﻿using Discord;
using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;

namespace SysBot.Pokemon
{
    public abstract class PokeRoutineExecutor<T> : PokeRoutineExecutorBase where T : PKM, new()
    {
        protected PokeRoutineExecutor(IConsoleBotManaged<IConsoleConnection, IConsoleConnectionAsync> cfg) : base(cfg)
        {
        }

        public abstract Task<T> ReadPokemon(ulong offset, CancellationToken token);
        public abstract Task<T> ReadPokemon(ulong offset, int size, CancellationToken token);
        public abstract Task<T> ReadPokemonPointer(IEnumerable<long> jumps, int size, CancellationToken token);
        public abstract Task<T> ReadBoxPokemon(int box, int slot, CancellationToken token);
        readonly Sphealcl SphealEmbed = new();

        public async Task<T?> ReadUntilPresent(ulong offset, int waitms, int waitInterval, int size, CancellationToken token)
        {
            int msWaited = 0;
            while (msWaited < waitms)
            {
                var pk = await ReadPokemon(offset, size, token).ConfigureAwait(false);
                if (pk.Species != 0 && pk.ChecksumValid)
                    return pk;
                await Task.Delay(waitInterval, token).ConfigureAwait(false);
                msWaited += waitInterval;
            }
            return null;
        }

        public async Task<T?> ReadUntilPresentPointer(IReadOnlyList<long> jumps, int waitms, int waitInterval, int size, CancellationToken token)
        {
            int msWaited = 0;
            while (msWaited < waitms)
            {
                var pk = await ReadPokemonPointer(jumps, size, token).ConfigureAwait(false);
                if (pk.Species != 0 && pk.ChecksumValid)
                    return pk;
                await Task.Delay(waitInterval, token).ConfigureAwait(false);
                msWaited += waitInterval;
            }
            return null;
        }

        protected async Task<(bool, ulong)> ValidatePointerAll(IEnumerable<long> jumps, CancellationToken token)
        {
            var solved = await SwitchConnection.PointerAll(jumps, token).ConfigureAwait(false);
            return (solved != 0, solved);
        }

        public static void DumpPokemon(string folder, string subfolder, T pk)
        {
            if (!Directory.Exists(folder))
                return;
            var dir = Path.Combine(folder, subfolder);
            Directory.CreateDirectory(dir);
            var fn = Path.Combine(dir, Util.CleanFileName(pk.FileName));
            File.WriteAllBytes(fn, pk.DecryptedPartyData);
            LogUtil.LogInfo($"Saved file: {fn}", "Dump");
        }

        public async Task<bool> TryReconnect(int attempts, int extraDelay, SwitchProtocol protocol, CancellationToken token)
        {
            // USB can have several reasons for connection loss, some of which is not recoverable (power loss, sleep). Only deal with WiFi for now.
            if (protocol is SwitchProtocol.WiFi)
            {
                // If ReconnectAttempts is set to -1, this should allow it to reconnect (essentially) indefinitely.
                for (int i = 0; i < (uint)attempts; i++)
                {
                    LogUtil.LogInfo($"Trying to reconnect... ({i + 1})", Connection.Label);
                    Connection.Reset();
                    if (Connection.Connected)
                        break;

                    await Task.Delay(30_000 + extraDelay, token).ConfigureAwait(false);
                }
            }
            return Connection.Connected;
        }

        public async Task VerifyBotbaseVersion(CancellationToken token)
        {
            var data = await SwitchConnection.GetBotbaseVersion(token).ConfigureAwait(false);
            var version = decimal.TryParse(data, CultureInfo.InvariantCulture, out var v) ? v : 0;
            if (version < BotbaseVersion)
            {
                var protocol = Config.Connection.Protocol;
                var msg = protocol is SwitchProtocol.WiFi ? "sys-botbase" : "usb-botbase";
                msg += $" version is not supported. Expected version {BotbaseVersion} or greater, and current version is {version}. Please download the latest version from: ";
                if (protocol is SwitchProtocol.WiFi)
                    msg += "https://github.com/olliz0r/sys-botbase/releases/latest";
                else
                    msg += "https://github.com/Koi-3088/usb-botbase/releases/latest";
                throw new Exception(msg);
            }
        }

        // Check if either Tesla or dmnt are active if the sanity check for Trainer Data fails, as these are common culprits.
        private const ulong ovlloaderID = 0x420000000007e51a; // Tesla Menu
        private const ulong dmntID = 0x010000000000000d;      // dmnt used for cheats

        public async Task CheckForRAMShiftingApps(CancellationToken token)
        {
            Log("Trainer data is not valid.");

            bool found = false;
            var msg = "";
            if (await SwitchConnection.IsProgramRunning(ovlloaderID, token).ConfigureAwait(false))
            {
                msg += "Found Tesla Menu";
                found = true;
            }

            if (await SwitchConnection.IsProgramRunning(dmntID, token).ConfigureAwait(false))
            {
                if (found)
                    msg += " and ";
                msg += "dmnt (cheat codes?)";
                found = true;
            }
            if (found)
            {
                msg += ".";
                Log(msg);
                Log("Please remove interfering applications and reboot the Switch.");
            }
        }

        protected async Task<PokeTradeResult> CheckPartnerReputation(PokeRoutineExecutor<T> bot, PokeTradeDetail<T> poke, ulong TrainerNID, string TrainerName,
            TradeAbuseSettings AbuseSettings, CooldownTracker UserCooldowns, CancellationToken token)
        {
            bool quit = false;
            var user = poke.Trainer;
            bool isDistribution = false;
            if (poke.Type == PokeTradeType.Random || poke.Type == PokeTradeType.Clone)
                isDistribution = true;
            var useridmsg = isDistribution ? "" : $" ({user.ID})";
            var list = isDistribution ? PreviousUsersDistribution : PreviousUsers;
            int attempts;
            var listCool = UserCooldowns;

            int wlIndex = AbuseSettings.WhiteListedIDs.List.FindIndex(z => z.ID == TrainerNID);
            int banIndex = AbuseSettings.BannedIDs.List.FindIndex(z => z.ID == TrainerNID);
            DateTime wlCheck = DateTime.Now;

            bool wlAllow = false;
            if (banIndex > -1)
            {
                var banExpires = AbuseSettings.BannedIDs.List[banIndex].Expiration;
                ulong banID = AbuseSettings.BannedIDs.List[banIndex].ID;

                if (banID != 0 && banExpires <= wlCheck)
                {
                    var msg = $"Removed {TrainerName} from the Bannedlist due to an expired duration.";
                    AbuseSettings.BannedIDs.RemoveAll(z => z.ID == TrainerNID);
                    EchoUtil.Echo(Format.Code(msg, "cs"));
                }
            }
                // Matches to a list of banned NIDs, in case the user ever manages to enter a trade.
                var entry = AbuseSettings.BannedIDs.List.Find(z => z.ID == TrainerNID);
            if (entry != null)
            {
                var banexpire = AbuseSettings.BannedIDs.List[banIndex].Expiration;
                if (AbuseSettings.BlockDetectedBannedUser && bot is PokeRoutineExecutor8SWSH)
                    await BlockUser(token).ConfigureAwait(false);
                var bmsg = $"🚨Alert🚨\n";
                if (!string.IsNullOrWhiteSpace(entry.Comment))
                {
                    bmsg += $"Banned NPC named **{TrainerName}** is attempting to Prison Break\n";
                    bmsg += $"Spheal Guards are now sending them back to their cell\n\n";
                    bmsg += $"They were banned for: {entry.Comment}\n\n";
                    bmsg += $"Release Date: {banexpire}";
                    EchoUtil.EchoEmbed(Sphealcl.EmbedBanMessage(bmsg, "[Warning] Banned NPC Detection"));
                }
                if (!string.IsNullOrWhiteSpace(AbuseSettings.BannedIDMatchEchoMention))
                {
                    bmsg = $"{AbuseSettings.BannedIDMatchEchoMention} {bmsg}";
                    EchoUtil.EchoEmbed(Sphealcl.EmbedBanMessage(bmsg, "[Warning] Banned NPC Detection"));
                }
                return PokeTradeResult.SuspiciousActivity;
            }
            if (wlIndex > -1)
            {
                ulong wlID = AbuseSettings.WhiteListedIDs.List[wlIndex].ID;
                var wlExpires = AbuseSettings.WhiteListedIDs.List[wlIndex].Expiration;
                    if (wlID != 0 && wlExpires <= wlCheck)
                    {
                        var msg = $"Removed {TrainerName} from Whitelist due to an expired duration.";
                        AbuseSettings.WhiteListedIDs.RemoveAll(z => z.ID == TrainerNID);
                        EchoUtil.Echo(Format.Code(msg, "cs"));
                        wlAllow = false;
                    }
                    else if (wlID != 0)
                        wlAllow = true;
            }
            // Check within the trade type (distribution or non-Distribution).
            var previous = list.TryGetPreviousNID(TrainerNID);
            if (previous != null)
            {
                var delta = DateTime.Now - previous.Time; // Time that has passed since last trade.
                var coolDelta = DateTime.Now - DateTime.ParseExact(AbuseSettings.CooldownUpdate, "yyyy.MM.dd - HH:mm:ss", CultureInfo.InvariantCulture);
                Log($"Last saw {user.TrainerName} {delta.TotalMinutes:F1} minutes ago (OT: {TrainerName}).");

                var cd = AbuseSettings.TradeCooldown;
                attempts = listCool.TryInsert(TrainerNID, TrainerName);
                if (cd != 0 && TimeSpan.FromMinutes(cd) > delta && !wlAllow)
                {
                    list.TryRegister(TrainerNID, TrainerName);
                    var msg = $"Added to the NPC Registry\n\n";
                    var wait = TimeSpan.FromMinutes(cd) - delta;

                    poke.Notifier.SendNotification(bot, poke, $"Still on trade cooldown, CD missed by **{wait.TotalMinutes:F1}** minute(s).");
                    if (AbuseSettings.EchoNintendoOnlineIDCooldown)
                        msg += $"**{TrainerName}** was caught by the NPC Police\nNPC ID: {TrainerNID}";
                    EchoUtil.EchoEmbed(Sphealcl.EmbedCDMessage(delta, cd, attempts, AbuseSettings.RepeatConnections, msg, "[Warning] NPC Detection"));

                    if (!string.IsNullOrWhiteSpace(AbuseSettings.CooldownAbuseEchoMention))
                    {
                        msg = $"{AbuseSettings.CooldownAbuseEchoMention} {msg}";
                        EchoUtil.EchoEmbed(Sphealcl.EmbedCDMessage(delta, cd, attempts, AbuseSettings.RepeatConnections, msg, "[Warning] NPC Detection"));
                    }
                    if (AbuseSettings.AutoBanCooldown && TimeSpan.FromMinutes(60) < coolDelta)
                    {
                        if (attempts >= AbuseSettings.RepeatConnections)
                        {
                            DateTime expires = DateTime.Now.AddDays(2);
                            string expiration = $"{expires:yyyy.MM.dd hh:mm:ss}";
                            AbuseSettings.BannedIDs.AddIfNew(new[] { GetReference(TrainerName, TrainerNID, "Cooldown Abuse Ban", expiration) });
                            var bmsg = $"Unfortunately...\n";
                            bmsg += $"{TrainerName}-{TrainerNID} has been **BANNED** for cooldown abuse\n";
                            EchoUtil.EchoEmbed(Sphealcl.EmbedBanMessage(bmsg, "Cooldown Abuse Ban"));
                            EchoUtil.Echo($"https://tenor.com/view/bane-no-banned-and-you-are-explode-gif-16047504");
                        }
                    }
                    return PokeTradeResult.SuspiciousActivity;
                }
                // For non-Distribution trades, flag users using multiple Discord/Twitch accounts to send to the same in-game player within a time limit.
                // This is usually to evade a ban or a trade cooldown.
                if (previous != null && previous.NetworkID != TrainerNID && !isDistribution)
                    if (!isDistribution && previous.NetworkID == TrainerNID && previous.RemoteID != user.ID && !wlAllow)
                    {
                        if (delta < TimeSpan.FromMinutes(AbuseSettings.TradeAbuseExpiration) && AbuseSettings.TradeAbuseAction != TradeAbuseAction.Ignore)
                        {
                            if (AbuseSettings.TradeAbuseAction == TradeAbuseAction.BlockAndQuit)
                            {
                                await BlockUser(token).ConfigureAwait(false);
                                if (AbuseSettings.BanIDWhenBlockingUser || bot is not PokeRoutineExecutor8SWSH) // Only ban ID if blocking in SWSH, always in other games.
                                {
                                    AbuseSettings.BannedIDs.AddIfNew(new[] { GetReference(TrainerName, TrainerNID, "in-game block for multiple accounts") });
                                    Log($"Added {TrainerNID} to the BannedIDs list.");
                                }
                            }
                            quit = true;
                        }

                        var msg = $"Found {user.TrainerName}{useridmsg} using multiple accounts.\nPreviously traded with {previous.Name} ({previous.RemoteID}) {delta.TotalMinutes:F1} minutes ago on OT: {TrainerName}.";
                        if (AbuseSettings.EchoNintendoOnlineIDMulti)
                            msg += $"\nID: {TrainerNID}";
                        if (!string.IsNullOrWhiteSpace(AbuseSettings.MultiAbuseEchoMention))
                            msg = $"{AbuseSettings.MultiAbuseEchoMention} {msg}";
                        EchoUtil.Echo(Format.Code(msg, "cs"));
                    }
            }

            // For non-Distribution trades, we can optionally flag users sending to multiple in-game players.
            // Can trigger if the user gets sniped, but can also catch abusers sending to many people.
            if (!isDistribution && !wlAllow)
            {
                var previous_remote = PreviousUsers.TryGetPreviousRemoteID(poke.Trainer.ID);
                if (previous_remote != null && previous_remote.Name != TrainerName)
                {
                    if (AbuseSettings.TradeAbuseAction != TradeAbuseAction.Ignore)
                    {
                        if (AbuseSettings.TradeAbuseAction == TradeAbuseAction.BlockAndQuit)
                        {
                            await BlockUser(token).ConfigureAwait(false);
                            if (AbuseSettings.BanIDWhenBlockingUser || bot is not PokeRoutineExecutor8SWSH) // Only ban ID if blocking in SWSH, always in other games.
                            {
                                AbuseSettings.BannedIDs.AddIfNew(new[] { GetReference(TrainerName, TrainerNID, "in-game block for sending to multiple in-game players") });
                                Log($"Added {TrainerNID} to the BannedIDs list.");
                            }
                        }
                        quit = true;
                    }

                    var msg = $"Found {user.TrainerName}{useridmsg} sending to multiple in-game players. Previous OT: {previous_remote.Name}, Current OT: {TrainerName}";
                    if (AbuseSettings.EchoNintendoOnlineIDMultiRecipients)
                        msg += $"\nID: {TrainerNID}";
                    if (!string.IsNullOrWhiteSpace(AbuseSettings.MultiRecipientEchoMention))
                        msg = $"{AbuseSettings.MultiRecipientEchoMention} {msg}";
                    EchoUtil.Echo(msg);
                }
            }

            if (quit)
                return PokeTradeResult.SuspiciousActivity;

            return PokeTradeResult.Success;
        }

        public static void LogSuccessfulTrades(PokeTradeDetail<T> poke, ulong TrainerNID, string TrainerName)
        {
            // All users who traded, tracked by whether it was a targeted trade or distribution.
            if (poke.Type == PokeTradeType.Random)
                PreviousUsersDistribution.TryRegister(TrainerNID, TrainerName);
            else
                PreviousUsers.TryRegister(TrainerNID, TrainerName, poke.Trainer.ID);
        }

        private static RemoteControlAccess GetReference(string name, ulong id, string comment, string expiration = "yyyy.MM.dd hh:mm:ss") => new()
        {
            ID = id,
            Name = name,
            Expiration = DateTime.Parse(expiration),
            Comment = $"Added automatically on {DateTime.Now:yyyy.MM.dd-hh:mm:ss} ({comment})",
        };

        // Blocks a user from the box during in-game trades (SWSH).
        private async Task BlockUser(CancellationToken token)
        {
            Log("Blocking user in-game...");
            await PressAndHold(RSTICK, 0_750, 0, token).ConfigureAwait(false);
            await Click(DUP, 0_300, token).ConfigureAwait(false);
            await Click(A, 1_300, token).ConfigureAwait(false);
            await Click(A, 1_300, token).ConfigureAwait(false);
            await Click(DUP, 0_300, token).ConfigureAwait(false);
            await Click(A, 1_100, token).ConfigureAwait(false);
            await Click(A, 1_100, token).ConfigureAwait(false);
        }
    }
}
