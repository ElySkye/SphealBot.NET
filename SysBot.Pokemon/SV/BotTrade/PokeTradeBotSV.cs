using PKHeX.Core;
using PKHeX.Core.Searching;
using SysBot.Base;
using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsetsSV;
using Discord;
using System.Collections.Generic;

namespace SysBot.Pokemon
{
    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
    public partial class PokeTradeBotSV : PokeRoutineExecutor9SV, ICountBot
    {
        private readonly PokeTradeHub<PK9> Hub;
        private readonly TradeSettings TradeSettings;
        public readonly TradeAbuseSettings AbuseSettings;
        readonly Sphealcl SphealEmbed = new();
        private static readonly Random rnd = new();

        public ICountSettings Counts => TradeSettings;

        /// <summary>
        /// Folder to dump received trade data to.
        /// </summary>
        /// <remarks>If null, will skip dumping.</remarks>
        private readonly IDumper DumpSetting;

        /// <summary>
        /// Synchronized start for multiple bots.
        /// </summary>
        public bool ShouldWaitAtBarrier { get; private set; }

        /// <summary>
        /// Tracks failed synchronized starts to attempt to re-sync.
        /// </summary>
        public int FailedBarrier { get; private set; }

        public PokeTradeBotSV(PokeTradeHub<PK9> hub, PokeBotState cfg) : base(cfg)
        {
            Hub = hub;
            TradeSettings = hub.Config.Trade;
            AbuseSettings = hub.Config.TradeAbuse;
            DumpSetting = hub.Config.Folder;
            lastOffered = new byte[8];
        }

        // Cached offsets that stay the same per session.
        private ulong BoxStartOffset;
        private ulong OverworldOffset;
        private ulong PortalOffset;
        private ulong ConnectedOffset;
        private ulong TradePartnerNIDOffset;
        private ulong TradePartnerOfferedOffset;

        // Store the current save's OT and TID/SID for comparison.
        private string OT = string.Empty;
        private uint DisplaySID;
        private uint DisplayTID;

        // Stores whether we returned all the way to the overworld, which repositions the cursor.
        private bool StartFromOverworld = true;
        // Stores whether the last trade was Distribution with fixed code, in which case we don't need to re-enter the code.
        private bool LastTradeDistributionFixed;

        // Track the last Pokémon we were offered since it persists between trades.
        private byte[] lastOffered;

        public override async Task MainLoop(CancellationToken token)
        {
            try
            {
                await InitializeHardware(Hub.Config.Trade, token).ConfigureAwait(false);

                Log("Identifying trainer data of the host console.");
                var sav = await IdentifyTrainer(token).ConfigureAwait(false);
                OT = sav.OT;
                DisplaySID = sav.DisplaySID;
                DisplayTID = sav.DisplayTID;
                RecentTrainerCache.SetRecentTrainer(sav);
                await InitializeSessionOffsets(token).ConfigureAwait(false);

                // Force the bot to go through all the motions again on its first pass.
                StartFromOverworld = true;
                LastTradeDistributionFixed = false;

                Log($"Starting main {nameof(PokeTradeBotSV)} loop.");
                await InnerLoop(sav, token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log(e.Message);
            }

            Log($"Ending {nameof(PokeTradeBotSV)} loop.");
            await HardStop().ConfigureAwait(false);
        }

        public override async Task HardStop()
        {
            UpdateBarrier(false);
            await CleanExit(CancellationToken.None).ConfigureAwait(false);
        }

        private async Task InnerLoop(SAV9SV sav, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                Config.IterateNextRoutine();
                var task = Config.CurrentRoutineType switch
                {
                    PokeRoutineType.Idle => DoNothing(token),
                    _ => DoTrades(sav, token),
                };
                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (SocketException e)
                {
                    if (e.StackTrace != null)
                        Connection.LogError(e.StackTrace);
                    var attempts = Hub.Config.Timings.ReconnectAttempts;
                    var delay = Hub.Config.Timings.ExtraReconnectDelay;
                    var protocol = Config.Connection.Protocol;
                    if (!await TryReconnect(attempts, delay, protocol, token).ConfigureAwait(false))
                        return;
                }
            }
        }

        private async Task DoNothing(CancellationToken token)
        {
            Log("No task assigned. Waiting for new task assignment.");
            while (!token.IsCancellationRequested && Config.NextRoutineType == PokeRoutineType.Idle)
                await Task.Delay(1_000, token).ConfigureAwait(false);
        }

        private async Task DoTrades(SAV9SV sav, CancellationToken token)
        {
            var type = Config.CurrentRoutineType;
            int waitCounter = 0;
            await SetCurrentBox(0, token).ConfigureAwait(false);
            while (!token.IsCancellationRequested && Config.NextRoutineType == type)
            {
                var (detail, priority) = GetTradeData(type);
                if (detail is null)
                {
                    await WaitForQueueStep(waitCounter++, token).ConfigureAwait(false);
                    continue;
                }
                waitCounter = 0;

                detail.IsProcessing = true;
                string tradetype = $" ({detail.Type})";
                Log($"Starting next {type}{tradetype} Bot Trade. Getting data...");
                Hub.Config.Stream.StartTrade(this, detail, Hub);
                Hub.Queues.StartTrade(this, detail);

                await PerformTrade(sav, detail, type, priority, token).ConfigureAwait(false);
            }
        }

        private async Task WaitForQueueStep(int waitCounter, CancellationToken token)
        {
            if (waitCounter == 0)
            {
                // Updates the assets.
                Hub.Config.Stream.IdleAssets(this);
                Log("Nothing to check, waiting for new users...");
            }

            await Task.Delay(1_000, token).ConfigureAwait(false);
        }

        protected virtual (PokeTradeDetail<PK9>? detail, uint priority) GetTradeData(PokeRoutineType type)
        {
            if (Hub.Queues.TryDequeue(type, out var detail, out var priority))
                return (detail, priority);
            if (Hub.Queues.TryDequeueLedy(out detail))
                return (detail, PokeTradePriorities.TierFree);
            return (null, PokeTradePriorities.TierFree);
        }

        private async Task PerformTrade(SAV9SV sav, PokeTradeDetail<PK9> detail, PokeRoutineType type, uint priority, CancellationToken token)
        {
            PokeTradeResult result;
            try
            {
                result = await PerformLinkCodeTrade(sav, detail, token).ConfigureAwait(false);
                if (result == PokeTradeResult.Success)
                    return;
            }
            catch (SocketException socket)
            {
                Log(socket.Message);
                result = PokeTradeResult.ExceptionConnection;
                HandleAbortedTrade(detail, type, priority, result);
                throw; // let this interrupt the trade loop. re-entering the trade loop will recheck the connection.
            }
            catch (Exception e)
            {
                Log(e.Message);
                result = PokeTradeResult.ExceptionInternal;
            }

            HandleAbortedTrade(detail, type, priority, result);
        }

        private void HandleAbortedTrade(PokeTradeDetail<PK9> detail, PokeRoutineType type, uint priority, PokeTradeResult result)
        {
            detail.IsProcessing = false;
            if (result.ShouldAttemptRetry() && detail.Type != PokeTradeType.Random && !detail.IsRetry)
            {
                detail.IsRetry = true;
                Hub.Queues.Enqueue(type, detail, Math.Min(priority, PokeTradePriorities.Tier2));
                detail.SendNotification(this, "Oops! Something happened. I'll requeue you for another attempt.");
            }
            else
            {
                detail.SendNotification(this, $"Oops! Something happened. Canceling the trade: {result}.");
                detail.TradeCanceled(this, result);
            }
        }

        private async Task<PokeTradeResult> PerformLinkCodeTrade(SAV9SV sav, PokeTradeDetail<PK9> poke, CancellationToken token)
        {
            // Update Barrier Settings
            UpdateBarrier(poke.IsSynchronized);
            poke.TradeInitialize(this);
            Hub.Config.Stream.EndEnterCode(this);

            // StartFromOverworld can be true on first pass or if something went wrong last trade.
            if (StartFromOverworld && !await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                await RecoverToOverworld(token).ConfigureAwait(false);

            // Handles getting into the portal. Will retry this until successful.
            // if we're not starting from overworld, then ensure we're online before opening link trade -- will break the bot otherwise.
            // If we're starting from overworld, then ensure we're online before opening the portal.
            if (!StartFromOverworld && !await IsConnectedOnline(ConnectedOffset, token).ConfigureAwait(false))
            {
                await RecoverToOverworld(token).ConfigureAwait(false);
                if (!await ConnectAndEnterPortal(token).ConfigureAwait(false))
                {
                    await RecoverToOverworld(token).ConfigureAwait(false);
                    return PokeTradeResult.RecoverStart;
                }
            }
            else if (StartFromOverworld && !await ConnectAndEnterPortal(token).ConfigureAwait(false))
            {
                await RecoverToOverworld(token).ConfigureAwait(false);
                return PokeTradeResult.RecoverStart;
            }

            var toSend = poke.TradeData;
            if (toSend.Species != 0)
                await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);

            // Assumes we're freshly in the Portal and the cursor is over Link Trade.
            Log("Selecting Link Trade.");

            await Click(A, 1_500, token).ConfigureAwait(false);
            // Make sure we clear any Link Codes if we're not in Distribution with fixed code, and it wasn't entered last round.
            if (poke.Type != PokeTradeType.Random || !LastTradeDistributionFixed)
            {
                await Click(X, 1_000, token).ConfigureAwait(false);
                await Click(PLUS, 1_000, token).ConfigureAwait(false);

                // Loading code entry.
                if (poke.Type != PokeTradeType.Random)
                    Hub.Config.Stream.StartEnterCode(this);
                await Task.Delay(Hub.Config.Timings.ExtraTimeOpenCodeEntry, token).ConfigureAwait(false);

                var code = poke.Code;
                Log($"Entering Link Trade code: {code:0000 0000}...");
                await EnterLinkCode(code, Hub.Config, token).ConfigureAwait(false);

                await Click(PLUS, 3_000, token).ConfigureAwait(false);
                StartFromOverworld = false;
            }

            LastTradeDistributionFixed = poke.Type == PokeTradeType.Random && !Hub.Config.Distribution.RandomCode;

            // Search for a trade partner for a Link Trade.
            await Click(A, 1_000, token).ConfigureAwait(false);

            // Clear it so we can detect it loading.
            await ClearTradePartnerNID(TradePartnerNIDOffset, token).ConfigureAwait(false);

            // Wait for Barrier to trigger all bots simultaneously.
            WaitAtBarrierIfApplicable(token);
            await Click(A, 1_000, token).ConfigureAwait(false);

            poke.TradeSearching(this);

            // Wait for a Trainer...
            var partnerFound = await WaitForTradePartner(token).ConfigureAwait(false);

            if (token.IsCancellationRequested)
            {
                StartFromOverworld = true;
                LastTradeDistributionFixed = false;
                await ExitTradeToPortal(false, token).ConfigureAwait(false);
                return PokeTradeResult.RoutineCancel;
            }
            if (!partnerFound)
            {
                if (!await RecoverToPortal(token).ConfigureAwait(false))
                {
                    Log("Failed to recover to portal.");
                    await RecoverToOverworld(token).ConfigureAwait(false);
                }
                return PokeTradeResult.NoTrainerFound;
            }

            Hub.Config.Stream.EndEnterCode(this);

            // Wait until we get into the box.
            var cnt = 0;
            while (!await IsInBox(PortalOffset, token).ConfigureAwait(false))
            {
                await Task.Delay(0_500, token).ConfigureAwait(false);
                if (++cnt > 20) // Didn't make it in after 10 seconds.
                {
                    await Click(A, 1_000, token).ConfigureAwait(false); // Ensures we dismiss a popup.
                    if (!await RecoverToPortal(token).ConfigureAwait(false))
                    {
                        Log("Failed to recover to portal.");
                        await RecoverToOverworld(token).ConfigureAwait(false);
                    }
                    return PokeTradeResult.RecoverOpenBox;
                }
            }
            await Task.Delay(3_000 + Hub.Config.Timings.ExtraTimeOpenBox, token).ConfigureAwait(false);

            var tradePartner = await GetTradePartnerInfo(token).ConfigureAwait(false);
            var trainerNID = await GetTradePartnerNID(TradePartnerNIDOffset, token).ConfigureAwait(false);
            RecordUtil<PokeTradeBotSWSH>.Record($"Initiating\t{trainerNID:X16}\t{tradePartner.TrainerName}\t{poke.Trainer.TrainerName}\t{poke.Trainer.ID}\t{poke.ID}\t{toSend.EncryptionConstant:X8}");
            Log($"Found Link Trade partner: {tradePartner.TrainerName}-{tradePartner.TID7} (ID: {trainerNID})");

            var partnerCheck = await CheckPartnerReputation(this, poke, trainerNID, tradePartner.TrainerName, AbuseSettings, UserCooldowns, token);
            if (partnerCheck != PokeTradeResult.Success)
            {
                await Click(A, 1_000, token).ConfigureAwait(false); // Ensures we dismiss a popup.
                await ExitTradeToPortal(false, token).ConfigureAwait(false);
                return partnerCheck;
            }

            bool isDistribution = false;
            if (poke.Type == PokeTradeType.Random || poke.Type == PokeTradeType.Clone)
                isDistribution = true;
            var list = isDistribution ? PreviousUsersDistribution : PreviousUsers;
            var listCool = UserCooldowns;
            // Hard check to verify that the offset changed from the last thing offered from the previous trade.
            // This is because box opening times can vary per person, the offset persists between trades, and can also change offset between trades.
            var tradeOffered = await ReadUntilChanged(TradePartnerOfferedOffset, lastOffered, 10_000, 0_500, false, true, token).ConfigureAwait(false);
            if (!tradeOffered)
            {
                await ExitTradeToPortal(false, token).ConfigureAwait(false);
                return PokeTradeResult.TrainerTooSlow;
            }

            poke.SendNotification(this, $"Found Trainer: {tradePartner.TrainerName} TID: {tradePartner.TID7} SID: {tradePartner.SID7} (NID: {trainerNID}). Waiting for a Pokémon...");

            if (poke.Type == PokeTradeType.Dump)
            {
                var result = await ProcessDumpTradeAsync(poke, token).ConfigureAwait(false);
                await ExitTradeToPortal(false, token).ConfigureAwait(false);
                return result;
            }

            // Wait for user input...
            var offered = await ReadUntilPresent(TradePartnerOfferedOffset, 25_000, 1_000, BoxFormatSlotSize, token).ConfigureAwait(false);
            var oldEC = await SwitchConnection.ReadBytesAbsoluteAsync(TradePartnerOfferedOffset, 8, token).ConfigureAwait(false);
            if (offered == null || offered.Species < 1 || !offered.ChecksumValid)
            {
                Log("Trade ended because a valid Pokémon was not offered.");
                await ExitTradeToPortal(false, token).ConfigureAwait(false);
                return PokeTradeResult.TrainerTooSlow;
            }
            if (offered.Species == (ushort)Species.Kadabra || offered.Species == (ushort)Species.Machoke || offered.Species == (ushort)Species.Gurdurr || offered.Species == (ushort)Species.Haunter || offered.Species == (ushort)Species.Graveler || offered.Species == (ushort)Species.Phantump || offered.Species == (ushort)Species.Pumpkaboo || offered.Species == (ushort)Species.Boldore)
            {
                if (offered.HeldItem != 229)
                    list.TryRegister(trainerNID, tradePartner.TrainerName);
            }
            PokeTradeResult update;
            var trainer = new PartnerDataHolder(0, tradePartner.TrainerName, tradePartner.TID7);
            (toSend, update) = await GetEntityToSend(sav, poke, offered, oldEC, toSend, trainer, token).ConfigureAwait(false);
            if (update != PokeTradeResult.Success)
            {
                await ExitTradeToPortal(false, token).ConfigureAwait(false);
                return update;
            }

            Log("Confirming trade.");
            var tradeResult = await ConfirmAndStartTrading(poke, token).ConfigureAwait(false);
            if (tradeResult != PokeTradeResult.Success)
            {
                await ExitTradeToPortal(false, token).ConfigureAwait(false);
                return tradeResult;
            }

            if (token.IsCancellationRequested)
            {
                StartFromOverworld = true;
                LastTradeDistributionFixed = false;
                await ExitTradeToPortal(false, token).ConfigureAwait(false);
                return PokeTradeResult.RoutineCancel;
            }

            // Trade was Successful!
            var received = await ReadPokemon(BoxStartOffset, BoxFormatSlotSize, token).ConfigureAwait(false);
            // Pokémon in b1s1 is same as the one they were supposed to receive (was never sent).
            if (SearchUtil.HashByDetails(received) == SearchUtil.HashByDetails(toSend) && received.Checksum == toSend.Checksum)
            {
                Log("User did not complete the trade.");
                await ExitTradeToPortal(false, token).ConfigureAwait(false);
                return PokeTradeResult.TrainerTooSlow;
            }

            // As long as we got rid of our inject in b1s1, assume the trade went through.
            Log("User completed the trade.");
            poke.TradeFinished(this, received);

            // Only log if we completed the trade.
            UpdateCountsAndExport(poke, received, toSend);

            // Log for Trade Abuse tracking.
            LogSuccessfulTrades(poke, trainerNID, tradePartner.TrainerName);

            // Sometimes they offered another mon, so store that immediately upon leaving Union Room.
            lastOffered = await SwitchConnection.ReadBytesAbsoluteAsync(TradePartnerOfferedOffset, 8, token).ConfigureAwait(false);

            list.TryRegister(trainerNID, tradePartner.TrainerName);
            _ = listCool.TryInsert(trainerNID, tradePartner.TrainerName, true);

            await ExitTradeToPortal(false, token).ConfigureAwait(false);
            return PokeTradeResult.Success;
        }

        private void UpdateCountsAndExport(PokeTradeDetail<PK9> poke, PK9 received, PK9 toSend)
        {
            var counts = TradeSettings;
            if (poke.Type == PokeTradeType.Random || poke.Type == PokeTradeType.LinkSV)
                counts.AddCompletedDistribution();
            else if (poke.Type == PokeTradeType.Clone)
                counts.AddCompletedClones();
            else
                counts.AddCompletedTrade();

            if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
            {
                var subfolder = poke.Type.ToString().ToLower();
                DumpPokemon(DumpSetting.DumpFolder, subfolder, received); // received by bot
                if (poke.Type is PokeTradeType.Specific or PokeTradeType.Clone)
                    DumpPokemon(DumpSetting.DumpFolder, "traded", toSend); // sent to partner
            }
        }

        private async Task<PokeTradeResult> ConfirmAndStartTrading(PokeTradeDetail<PK9> detail, CancellationToken token)
        {
            // We'll keep watching B1S1 for a change to indicate a trade started -> should try quitting at that point.
            var oldEC = await SwitchConnection.ReadBytesAbsoluteAsync(BoxStartOffset, 8, token).ConfigureAwait(false);

            await Click(A, 3_000, token).ConfigureAwait(false);
            for (int i = 0; i < Hub.Config.Trade.MaxTradeConfirmTime; i++)
            {
                if (await IsUserBeingShifty(detail, token).ConfigureAwait(false))
                    return PokeTradeResult.SuspiciousActivity;

                // We can fall out of the box if the user offers, then quits.
                if (!await IsInBox(PortalOffset, token).ConfigureAwait(false))
                    return PokeTradeResult.TrainerLeft;

                await Click(A, 1_000, token).ConfigureAwait(false);

                // EC is detectable at the start of the animation.
                var newEC = await SwitchConnection.ReadBytesAbsoluteAsync(BoxStartOffset, 8, token).ConfigureAwait(false);
                if (!newEC.SequenceEqual(oldEC))
                {
                    await Task.Delay(25_000, token).ConfigureAwait(false);
                    return PokeTradeResult.Success;
                }
            }
            // If we don't detect a B1S1 change, the trade didn't go through in that time.
            return PokeTradeResult.TrainerTooSlow;
        }

        // Upon connecting, their Nintendo ID will instantly update.
        protected virtual async Task<bool> WaitForTradePartner(CancellationToken token)
        {
            Log("Waiting for trainer...");
            int ctr = (Hub.Config.Trade.TradeWaitTime * 1_000) - 2_000;
            await Task.Delay(2_000, token).ConfigureAwait(false);
            while (ctr > 0)
            {
                await Task.Delay(1_000, token).ConfigureAwait(false);
                ctr -= 1_000;
                var newNID = await GetTradePartnerNID(TradePartnerNIDOffset, token).ConfigureAwait(false);
                if (newNID != 0)
                {
                    TradePartnerOfferedOffset = await SwitchConnection.PointerAll(Offsets.LinkTradePartnerPokemonPointer, token).ConfigureAwait(false);
                    return true;
                }

                // Fully load into the box.
                await Task.Delay(1_000, token).ConfigureAwait(false);
            }
            return false;
        }

        // If we can't manually recover to overworld, reset the game.
        // Try to avoid pressing A which can put us back in the portal with the long load time.
        private async Task<bool> RecoverToOverworld(CancellationToken token)
        {
            if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                return true;

            Log("Attempting to recover to overworld.");
            var attempts = 0;
            while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            {
                attempts++;
                if (attempts >= 30)
                    break;

                await Click(B, 1_300, token).ConfigureAwait(false);
                if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                    break;

                await Click(B, 2_000, token).ConfigureAwait(false);
                if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                    break;

                await Click(A, 1_300, token).ConfigureAwait(false);
                if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                    break;
            }

            // We didn't make it for some reason.
            if (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            {
                Log("Failed to recover to overworld, rebooting the game.");
                await RestartGameSV(token).ConfigureAwait(false);
            }
            await Task.Delay(1_000, token).ConfigureAwait(false);

            // Force the bot to go through all the motions again on its first pass.
            StartFromOverworld = true;
            LastTradeDistributionFixed = false;
            return true;
        }

        // If we didn't find a trainer, we're still in the portal but there can be 
        // different numbers of pop-ups we have to dismiss to get back to when we can trade.
        // Rather than resetting to overworld, try to reset out of portal and immediately go back in.
        private async Task<bool> RecoverToPortal(CancellationToken token)
        {
            Log("Reorienting to Poké Portal.");
            var attempts = 0;
            while (await IsInPokePortal(PortalOffset, token).ConfigureAwait(false))
            {
                await Click(B, 1_500, token).ConfigureAwait(false);
                if (++attempts >= 30)
                {
                    Log("Failed to recover to Poké Portal.");
                    return false;
                }
            }

            // Should be in the X menu hovered over Poké Portal.
            await Click(A, 1_000, token).ConfigureAwait(false);

            return await SetUpPortalCursor(token).ConfigureAwait(false);
        }

        // Should be used from the overworld. Opens X menu, attempts to connect online, and enters the Portal.
        // The cursor should be positioned over Link Trade.
        private async Task<bool> ConnectAndEnterPortal(CancellationToken token)
        {
            if (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                await RecoverToOverworld(token).ConfigureAwait(false);

            Log("Opening the Poké Portal.");

            // Open the X Menu.
            await Click(X, 1_000, token).ConfigureAwait(false);

            // Handle the news popping up.
            if (await SwitchConnection.IsProgramRunning(LibAppletWeID, token).ConfigureAwait(false))
            {
                Log("News detected, will close once it's loaded!");
                await Task.Delay(5_000, token).ConfigureAwait(false);
                await Click(B, 2_000, token).ConfigureAwait(false);
            }

            // Scroll to the bottom of the Main Menu so we don't need to care if Picnic is unlocked.
            await Click(DRIGHT, 0_300, token).ConfigureAwait(false);
            await PressAndHold(DDOWN, 1_000, 1_000, token).ConfigureAwait(false);
            await Click(DUP, 0_200, token).ConfigureAwait(false);
            await Click(DUP, 0_200, token).ConfigureAwait(false);
            await Click(DUP, 0_200, token).ConfigureAwait(false);
            await Click(A, 1_000, token).ConfigureAwait(false);

            return await SetUpPortalCursor(token).ConfigureAwait(false);
        }

        // Waits for the Portal to load (slow) and then moves the cursor down to Link Trade.
        private async Task<bool> SetUpPortalCursor(CancellationToken token)
        {
            // Wait for the portal to load.
            var attempts = 0;
            while (!await IsInPokePortal(PortalOffset, token).ConfigureAwait(false))
            {
                await Task.Delay(0_500, token).ConfigureAwait(false);
                if (++attempts > 20)
                {
                    Log("Failed to load the Poké Portal.");
                    return false;
                }
            }
            await Task.Delay(2_000 + Hub.Config.Timings.ExtraTimeLoadPortal, token).ConfigureAwait(false);

            // Connect online if not already.
            if (!await ConnectToOnline(Hub.Config, token).ConfigureAwait(false))
            {
                Log("Failed to connect to online.");
                return false; // Failed, either due to connection or softban.
            }

            // Handle the news popping up.
            if (await SwitchConnection.IsProgramRunning(LibAppletWeID, token).ConfigureAwait(false))
            {
                Log("News detected, will close once it's loaded!");
                await Task.Delay(5_000, token).ConfigureAwait(false);
                await Click(B, 2_000 + Hub.Config.Timings.ExtraTimeLoadPortal, token).ConfigureAwait(false);
            }

            Log("Adjusting the cursor in the Portal.");
            // Move down to Link Trade.
            await Click(DDOWN, 0_300, token).ConfigureAwait(false);
            await Click(DDOWN, 0_300, token).ConfigureAwait(false);
            return true;
        }

        // Connects online if not already. Assumes the user to be in the X menu to avoid a news screen.
        private async Task<bool> ConnectToOnline(PokeTradeHubConfig config, CancellationToken token)
        {
            if (await IsConnectedOnline(ConnectedOffset, token).ConfigureAwait(false))
                return true;

            await Click(L, 1_000, token).ConfigureAwait(false);
            await Click(A, 4_000, token).ConfigureAwait(false);

            var wait = 0;
            while (!await IsConnectedOnline(ConnectedOffset, token).ConfigureAwait(false))
            {
                await Task.Delay(0_500, token).ConfigureAwait(false);
                if (++wait > 30) // More than 15 seconds without a connection.
                    return false;
            }

            // There are several seconds after connection is established before we can dismiss the menu.
            await Task.Delay(3_000 + config.Timings.ExtraTimeConnectOnline, token).ConfigureAwait(false);
            await Click(A, 1_000, token).ConfigureAwait(false);
            return true;
        }

        private async Task ExitTradeToPortal(bool unexpected, CancellationToken token)
        {
            if (await IsInPokePortal(PortalOffset, token).ConfigureAwait(false))
                return;

            if (unexpected)
                Log("Unexpected behavior, recovering to Portal.");

            // Ensure we're not in the box first.
            // Takes a long time for the Portal to load up, so once we exit the box, wait 5 seconds.
            Log("Leaving the box...");
            var attempts = 0;
            while (await IsInBox(PortalOffset, token).ConfigureAwait(false))
            {
                await Click(B, 1_000, token).ConfigureAwait(false);
                if (!await IsInBox(PortalOffset, token).ConfigureAwait(false))
                {
                    await Task.Delay(5_000, token).ConfigureAwait(false);
                    break;
                }

                await Click(A, 1_000, token).ConfigureAwait(false);
                if (!await IsInBox(PortalOffset, token).ConfigureAwait(false))
                {
                    await Task.Delay(5_000, token).ConfigureAwait(false);
                    break;
                }

                await Click(B, 1_000, token).ConfigureAwait(false);
                if (!await IsInBox(PortalOffset, token).ConfigureAwait(false))
                {
                    await Task.Delay(5_000, token).ConfigureAwait(false);
                    break;
                }

                // Didn't make it out of the box for some reason.
                if (++attempts > 20)
                {
                    Log("Failed to exit box, rebooting the game.");
                    if (!await RecoverToOverworld(token).ConfigureAwait(false))
                        await RestartGameSV(token).ConfigureAwait(false);
                    await ConnectAndEnterPortal(token).ConfigureAwait(false);
                    return;
                }
            }

            // Wait for the portal to load.
            Log("Waiting on the portal to load...");
            attempts = 0;
            while (!await IsInPokePortal(PortalOffset, token).ConfigureAwait(false))
            {
                await Task.Delay(1_000, token).ConfigureAwait(false);
                if (await IsInPokePortal(PortalOffset, token).ConfigureAwait(false))
                    break;

                // Didn't make it into the portal for some reason.
                if (++attempts > 40)
                {
                    Log("Failed to load the portal, rebooting the game.");
                    if (!await RecoverToOverworld(token).ConfigureAwait(false))
                        await RestartGameSV(token).ConfigureAwait(false);
                    await ConnectAndEnterPortal(token).ConfigureAwait(false);
                    return;
                }
            }
            await Task.Delay(2_000, token).ConfigureAwait(false);
        }

        // These don't change per session and we access them frequently, so set these each time we start.
        private async Task InitializeSessionOffsets(CancellationToken token)
        {
            Log("Caching session offsets...");
            BoxStartOffset = await SwitchConnection.PointerAll(Offsets.BoxStartPokemonPointer, token).ConfigureAwait(false);
            OverworldOffset = await SwitchConnection.PointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
            PortalOffset = await SwitchConnection.PointerAll(Offsets.PortalBoxStatusPointer, token).ConfigureAwait(false);
            ConnectedOffset = await SwitchConnection.PointerAll(Offsets.IsConnectedPointer, token).ConfigureAwait(false);
            TradePartnerNIDOffset = await SwitchConnection.PointerAll(Offsets.LinkTradePartnerNIDPointer, token).ConfigureAwait(false);
        }

        // todo: future
        protected virtual async Task<bool> IsUserBeingShifty(PokeTradeDetail<PK9> detail, CancellationToken token)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return false;
        }

        private async Task RestartGameSV(CancellationToken token)
        {
            await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
            await InitializeSessionOffsets(token).ConfigureAwait(false);
        }

        private async Task<PokeTradeResult> ProcessDumpTradeAsync(PokeTradeDetail<PK9> detail, CancellationToken token)
        {
            int ctr = 0;
            var time = TimeSpan.FromSeconds(Hub.Config.Trade.MaxDumpTradeTime);
            var start = DateTime.Now;

            var pkprev = new PK9();
            var bctr = 0;
            while (ctr < Hub.Config.Trade.MaxDumpsPerTrade && DateTime.Now - start < time)
            {
                if (!await IsInBox(PortalOffset, token).ConfigureAwait(false))
                    break;
                if (bctr++ % 3 == 0)
                    await Click(B, 0_100, token).ConfigureAwait(false);

                // Wait for user input... Needs to be different from the previously offered Pokémon.
                var pk = await ReadUntilPresent(TradePartnerOfferedOffset, 3_000, 0_050, BoxFormatSlotSize, token).ConfigureAwait(false);
                if (pk == null || pk.Species < 1 || !pk.ChecksumValid || SearchUtil.HashByDetails(pk) == SearchUtil.HashByDetails(pkprev))
                    continue;

                // Save the new Pokémon for comparison next round.
                pkprev = pk;

                // Send results from separate thread; the bot doesn't need to wait for things to be calculated.
                if (DumpSetting.Dump)
                {
                    var subfolder = detail.Type.ToString().ToLower();
                    DumpPokemon(DumpSetting.DumpFolder, subfolder, pk); // received
                }

                var la = new LegalityAnalysis(pk);
                var verbose = $"```{la.Report(true)}```";
                Log($"Shown Pokémon is: {(la.Valid ? "Valid" : "Invalid")}.");

                ctr++;
                var msg = Hub.Config.Trade.DumpTradeLegalityCheck ? verbose : $"File {ctr}";

                // Extra information about trainer data for people requesting with their own trainer data.
                var ot = pk.OT_Name;
                var ot_gender = pk.OT_Gender == 0 ? "Male" : "Female";
                var tid = pk.GetDisplayTID().ToString(pk.GetTrainerIDFormat().GetTrainerIDFormatStringTID());
                var sid = pk.GetDisplaySID().ToString(pk.GetTrainerIDFormat().GetTrainerIDFormatStringSID());
                var lang = (LanguageID)pk.Language;
                var game = (GameVersion)pk.Version;
                msg += $"\n**Trainer Data**\n```OT: {ot}\nOTGender: {ot_gender}\nTID: {tid}\nSID: {sid}\nLanguage: {lang}\nGame: {game}```";

                // Extra information for shiny eggs, because of people dumping to skip hatching.
                var eggstring = pk.IsEgg ? "Egg " : string.Empty;
                msg += pk.IsShiny ? $"\n**This Pokémon {eggstring}is shiny!**" : string.Empty;
                detail.SendNotification(this, pk, msg);
            }

            Log($"Ended Dump loop after processing {ctr} Pokémon.");
            if (ctr == 0)
                return PokeTradeResult.TrainerTooSlow;

            TradeSettings.AddCompletedDumps();
            detail.Notifier.SendNotification(this, detail, $"Dumped {ctr} Pokémon.");
            detail.Notifier.TradeFinished(this, detail, detail.TradeData); // blank PK9
            return PokeTradeResult.Success;
        }

        private async Task<TradePartnerSV> GetTradePartnerInfo(CancellationToken token)
        {
            // We're able to see both users' MyStatus, but one of them will be ourselves.
            var trader_info = await GetTradePartnerMyStatus(Offsets.Trader1MyStatusPointer, token).ConfigureAwait(false);
            if (trader_info.OT == OT && trader_info.DisplaySID == DisplaySID && trader_info.DisplayTID == DisplayTID) // This one matches ourselves.
                trader_info = await GetTradePartnerMyStatus(Offsets.Trader2MyStatusPointer, token).ConfigureAwait(false);
            return new TradePartnerSV(trader_info);
        }

        protected virtual async Task<(PK9 toSend, PokeTradeResult check)> GetEntityToSend(SAV9SV sav, PokeTradeDetail<PK9> poke, PK9 offered, byte[] oldEC, PK9 toSend, PartnerDataHolder partnerID, CancellationToken token)
        {
            return poke.Type switch
            {
                PokeTradeType.Random => await HandleRandomLedy(sav, poke, offered, toSend, partnerID, token).ConfigureAwait(false),
                PokeTradeType.Clone => await HandleClone(sav, poke, offered, oldEC, token).ConfigureAwait(false),
                PokeTradeType.LinkSV => await HandleRandomLedy(sav, poke, offered, toSend, partnerID, token).ConfigureAwait(false),
                _ => (toSend, PokeTradeResult.Success),
            };
        }

        private async Task<(PK9 toSend, PokeTradeResult check)> HandleClone(SAV9SV sav, PokeTradeDetail<PK9> poke, PK9 offered, byte[] oldEC, CancellationToken token)
        {
            if (Hub.Config.Discord.ReturnPKMs)
                poke.SendNotification(this, offered, "Here's what you showed me!");

            var la = new LegalityAnalysis(offered);
            if (!la.Valid)
            {
                Log($"Clone request (from {poke.Trainer.TrainerName}) has detected an invalid Pokémon: {GameInfo.GetStrings(1).Species[offered.Species]}.");
                if (DumpSetting.Dump)
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);

                var report = la.Report();
                Log(report);
                poke.SendNotification(this, "This Pokémon is not legal per PKHeX's legality checks. I am forbidden from cloning this. Exiting trade.");
                poke.SendNotification(this, report);

                return (offered, PokeTradeResult.IllegalTrade);
            }

            var clone = offered.Clone();
            if (Hub.Config.Legality.ResetHOMETracker)
                clone.Tracker = 0;

            poke.SendNotification(this, $"**Cloned your {GameInfo.GetStrings(1).Species[clone.Species]}!**\nNow press B to cancel your offer and trade me a Pokémon you don't want.");
            Log($"Cloned a {GameInfo.GetStrings(1).Species[clone.Species]}. Waiting for user to change their Pokémon...");

            // Separate this out from WaitForPokemonChanged since we compare to old EC from original read.
            var partnerFound = await ReadUntilChanged(TradePartnerOfferedOffset, oldEC, 15_000, 0_200, false, true, token).ConfigureAwait(false);
            if (!partnerFound)
            {
                poke.SendNotification(this, "**HEY CHANGE IT NOW OR I AM LEAVING!!!**");
                // They get one more chance.
                partnerFound = await ReadUntilChanged(TradePartnerOfferedOffset, oldEC, 15_000, 0_200, false, true, token).ConfigureAwait(false);
            }

            var pk2 = await ReadUntilPresent(TradePartnerOfferedOffset, 25_000, 1_000, BoxFormatSlotSize, token).ConfigureAwait(false);
            if (!partnerFound || pk2 is null || SearchUtil.HashByDetails(pk2) == SearchUtil.HashByDetails(offered))
            {
                Log("Trade partner did not change their Pokémon.");
                return (offered, PokeTradeResult.TrainerTooSlow);
            }

            await Click(A, 0_800, token).ConfigureAwait(false);
            await SetBoxPokemonAbsolute(BoxStartOffset, clone, token, sav).ConfigureAwait(false);

            return (clone, PokeTradeResult.Success);
        }

        private async Task<(PK9 toSend, PokeTradeResult check)> HandleRandomLedy(SAV9SV sav, PokeTradeDetail<PK9> poke, PK9 offered, PK9 toSend, PartnerDataHolder partner, CancellationToken token)
        {
            // Allow the trade partner to do a Ledy swap.
            var config = Hub.Config.Distribution;
            var custom = Hub.Config.CustomSwaps;
            var trade = Hub.Ledy.GetLedyTrade(offered, partner.TrainerOnlineID, config.LedySpecies, custom.LedySpecies2);
            var counts = TradeSettings;
            var swap = offered.HeldItem;
            var nick = offered.Nickname;
            var user = partner.TrainerName;

            var evSwap = new List<int>
            {
                (int)custom.EVResetItem,
                (int)custom.EVRaidAtkItem,
                (int)custom.EVCompAtkItem,
                (int)custom.EVRaidSPAItem,
                (int)custom.EVCompSPAItem,
                (int)custom.EVGenDEFItem,
                (int)custom.EVGenSPDItem,
            };
            string[] teraItem = GameInfo.GetStrings(1).Item[offered.HeldItem].Split(' ');
            var eventmsg = $"======\r\nSpheal Event Winner:\r\n> OT: {user} <\r\n======";

            if (nick == custom.SphealEvent)
            {
                EchoUtil.Echo(Format.Code(eventmsg, "cs"));
                EchoUtil.Echo("https://tenor.com/view/swoshi-swsh-spheal-dlc-pokemon-gif-18917062");
            }
            //Mystery Eggs
            if (nick == custom.MysteryEgg)
            {
                string? myst;
                PK9? rnd;
                do
                {
                    rnd = Hub.Ledy.Pool.GetRandomTrade();
                } while (!rnd.IsEgg);
                toSend = rnd;

                var Size = toSend.Scale switch
                {
                    255 => "Jumbo",
                    0 => "Tiny",
                    _ => "Average",
                };
                var Shiny = toSend.IsShiny switch
                {
                    true => "Shiny",
                    false => "Non-Shiny",
                };

                Log($"Sending Surprise Egg: {Shiny} {Size} {(Gender)toSend.Gender} {GameInfo.GetStrings(1).Species[toSend.Species]}");
                await SetTradePartnerDetailsSV(toSend, offered, sav, token).ConfigureAwait(false);
                poke.TradeData = toSend;

                myst = $"**{user}** has received a Mystery Egg !\n";
                myst += $"**Don't reveal if you want the surprise**\n\n";
                myst += $"**Pokémon**: ||**{GameInfo.GetStrings(1).Species[toSend.Species]}**||\n";
                myst += $"**Gender**: ||**{(Gender)toSend.Gender}**||\n";
                myst += $"**Shiny**: ||**{Shiny}**||\n";
                myst += $"**Size**: ||**{Size}**||\n";
                myst += $"**Nature**: ||**{(Nature)toSend.Nature}**||\n";
                myst += $"**Ability**: ||**{(Ability)toSend.Ability}**||\n";
                myst += $"**IVs**: ||**{toSend.IV_HP}/{toSend.IV_ATK}/{toSend.IV_DEF}/{toSend.IV_SPA}/{toSend.IV_SPD}/{toSend.IV_SPE}**||\n";
                myst += $"**Language**: ||**{(LanguageID)toSend.Language}**||";

                EchoUtil.EchoEmbed(Sphealcl.EmbedEggMystery(toSend, myst, $"{user}'s Mystery Egg"));
                counts.AddCompletedMystery();
                return (toSend, PokeTradeResult.Success);
            }
            if (trade != null && offered.IsNicknamed && trade.Type == LedyResponseType.MatchPool)
                Log($"User's request is for {offered.Nickname}");
            else if (swap == (int)custom.OTSwapItem) //OT Swap for existing mons
            {
                toSend = offered.Clone();
                Log($"User's request is for OT swap using: {GameInfo.GetStrings(1).Species[offered.Species]} with OT Name: {offered.OT_Name}");
                string? msg;

                if (toSend.Tracker != 0 && toSend.Generation == 9)
                    toSend.Tracker = 0;
                var la = new LegalityAnalysis(offered);
                if (toSend.FatefulEncounter || toSend.Generation != 9 && la.Valid)
                {
                    DumpPokemon(DumpSetting.DumpFolder, "clone", toSend);
                    Log($"Sending a clone as I'm not able to OTSwap that");
                    counts.AddCompletedClones();
                    await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
                    await Task.Delay(2_500, token).ConfigureAwait(false);
                    return (toSend, PokeTradeResult.Success);
                }
                else
                {
                    if (!la.Valid)
                    {
                        msg = $"Pokémon: {(Species)offered.Species}";
                        msg += $"\nPokémon OT: {offered.OT_Name}";
                        msg += $"\nUser: {partner.TrainerName}";
                        await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Bad OT Swap:").ConfigureAwait(false);
                        DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);

                        msg = la.Report();
                        await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Legality Report:").ConfigureAwait(false);
                        return (offered, PokeTradeResult.IllegalTrade);
                    }
                    else if (!await SetTradePartnerDetailsSV(toSend, offered, sav, token).ConfigureAwait(false))
                    {
                        msg = $"Pokémon: {(Species)offered.Species}";
                        msg += $"\nPokémon OT: {offered.OT_Name}";
                        msg += $"\nUser: {user}";
                        await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Bad OT Swap:").ConfigureAwait(false);
                        DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);
                        return (toSend, PokeTradeResult.IllegalTrade);
                    }
                    poke.TradeData = toSend;
                    return (toSend, PokeTradeResult.Success);
                }
            }
            //Ball Swapper
            else if (BallSwap(swap) != 0)
            {
                Log($"User's request is for Ball swap using: {GameInfo.GetStrings(1).Species[offered.Species]}");
                toSend = offered.Clone();
                string? not9;
                if (toSend.Tracker != 0 && toSend.Generation == 9)
                    toSend.Tracker = 0;
                else if (toSend.Generation != 9)
                {
                    not9 = $"Pokémon: {(Species)offered.Species}";
                    not9 += $"\n{user} is attempting to Ballswap non Gen 9";
                    not9 += $"\nDue to Home Tracker, bot is unable to do so";
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);
                    await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, not9, "Bad Ball Swap:").ConfigureAwait(false);
                    return (offered, PokeTradeResult.TrainerRequestBad);
                }
                var la = new LegalityAnalysis(offered);
                if (!la.Valid)
                {
                    not9 = $"Pokémon: {(Species)offered.Species}";
                    not9 += $"\nUser: {user}";
                    not9 += $"\nPokémon shown is not legal";
                    await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, not9, "Bad Ball Swap:").ConfigureAwait(false);
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);

                    not9 = la.Report();
                    await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, not9, "Legality Report:").ConfigureAwait(false);
                    return (offered, PokeTradeResult.IllegalTrade);
                }
                else
                {
                    toSend.Ball = BallSwap(offered.HeldItem);
                    toSend.RefreshChecksum();
                    Log($"Ball swapped to: {(Ball)toSend.Ball}");
                    var la2 = new LegalityAnalysis(toSend);
                    if (la2.Valid)
                    {
                        poke.TradeData = toSend;
                        counts.AddCompletedBallSwaps();
                        await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
                        await Task.Delay(2_500, token).ConfigureAwait(false);
                        return (toSend, PokeTradeResult.Success);
                    }
                    else
                    {
                        not9 = $"{user}, {(Species)offered.Species} cannot be in that ball";
                        not9 += $"\nThe ball cannot be swapped";
                        await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, not9, "Bad Ball Swap:").ConfigureAwait(false);
                        DumpPokemon(DumpSetting.DumpFolder, "hacked", toSend);
                        return (toSend, PokeTradeResult.IllegalTrade);
                    }
                }
            }
            else if (swap == (int)custom.TrilogySwapItem || swap == 229) //Trilogy Swap for existing mons (Level/Nickname/Evolve)
            {
                toSend = offered.Clone();
                Log($"User's request is for Trilogy swap using: {GameInfo.GetStrings(1).Species[offered.Species]}");
                string? msg;

                var la = new LegalityAnalysis(offered);
                if (!la.Valid)
                {
                    msg = $"Pokémon: {(Species)offered.Species}";
                    msg += $"\nUser: {user}";
                    msg += $"\nPokémon shown is not legal";
                    await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Bad Trilogy Swap:").ConfigureAwait(false);
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);

                    msg = la.Report();
                    await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Legality Report:").ConfigureAwait(false);
                    return (offered, PokeTradeResult.IllegalTrade);
                }
                else
                {
                    toSend.CurrentLevel = 100;//#1 Set level to 100 (Level Swap)
                    if (swap == 229)
                    {
                        Log($"Evo Species is holding an Everstone");

                        switch (toSend.Species)
                        {
                            case (ushort)Species.Kadabra:
                                toSend.Species = (ushort)Species.Alakazam;
                                break;
                            case (ushort)Species.Machoke:
                                toSend.Species = (ushort)Species.Machamp;
                                break;
                            case (ushort)Species.Gurdurr:
                                toSend.Species = (ushort)Species.Conkeldurr;
                                break;
                            case (ushort)Species.Haunter:
                                toSend.Species = (ushort)Species.Gengar;
                                break;
                            case (ushort)Species.Graveler:
                                toSend.Species = (ushort)Species.Golem;
                                break;
                            case (ushort)Species.Phantump:
                                toSend.Species = (ushort)Species.Trevenant;
                                break;
                            case (ushort)Species.Pumpkaboo:
                                toSend.Species = (ushort)Species.Gourgeist;
                                break;
                            case (ushort)Species.Boldore:
                                toSend.Species = (ushort)Species.Gigalith;
                                break;
                        }
                        toSend.HeldItem = 1882;
                    }
                    else
                    {
                        //#2 Evolve difficult to evolve Species (Evo Swap) - todofuture (PLA/SWSH evos)
                        switch (toSend.Species)
                        {
                            case (ushort)Species.Finizen:
                                toSend.Species = (ushort)Species.Palafin;
                                break;
                            case (ushort)Species.Rellor:
                                toSend.Species = (ushort)Species.Rabsca;
                                break;
                            case (ushort)Species.Pawmo:
                                toSend.Species = (ushort)Species.Pawmot;
                                break;
                            case (ushort)Species.Bramblin:
                                toSend.Species = (ushort)Species.Brambleghast;
                                break;
                            case (ushort)Species.Sliggoo:
                                if (toSend.Form == 0) //Kalos
                                    toSend.Species = (ushort)Species.Goodra;
                                break;
                            case (ushort)Species.Gimmighoul:
                                toSend.Species = (ushort)Species.Gholdengo;
                                toSend.FormArgument = 999;
                                break;
                            case (ushort)Species.Primeape:
                                toSend.Species = (ushort)Species.Annihilape;
                                toSend.FormArgument = 20;
                                break;
                            case (ushort)Species.Bisharp:
                                toSend.Species = (ushort)Species.Kingambit;
                                toSend.FormArgument = 3;
                                break;
                            case (ushort)Species.Basculin:
                                if (toSend.Form == 2) //White
                                {
                                    if (toSend.Gender == 0) //Male
                                    {
                                        toSend.Species = (ushort)Species.Basculegion;
                                        toSend.Form = 0;
                                        toSend.Gender = 0;
                                    }
                                    else if (toSend.Gender == 1) //Female
                                    {
                                        toSend.Species = (ushort)Species.Basculegion;
                                        toSend.Form = 1;
                                        toSend.Gender = 1;
                                    }
                                    toSend.FormArgument = 300;
                                }
                                break;
                        }
                    }
                    if (toSend.AbilityNumber == 1)
                        toSend.RefreshAbility(0);
                    else if (toSend.AbilityNumber == 2)
                        toSend.RefreshAbility(1);
                    else if (toSend.AbilityNumber == 3 || toSend.AbilityNumber == 4)
                        toSend.RefreshAbility(2);

                    //#3 Clear Nicknames
                    if (!toSend.FatefulEncounter || toSend.Met_Location != 30001)
                        toSend.ClearNickname();
                    toSend.RefreshChecksum();

                    var la2 = new LegalityAnalysis(toSend);
                    if (la2.Valid)
                    {
                        if (toSend.HeldItem == 1882)
                            Log($"Purification Success. Sending back: {GameInfo.GetStrings(1).Species[toSend.Species]}.");
                        else
                            Log($"Swap Success. Sending back: {GameInfo.GetStrings(1).Species[toSend.Species]}.");
                        poke.TradeData = toSend;
                        counts.AddCompletedTrilogySwaps();
                        DumpPokemon(DumpSetting.DumpFolder, "trilogy", toSend);
                        await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
                        await Task.Delay(2_500, token).ConfigureAwait(false);
                        return (toSend, PokeTradeResult.Success);
                    }
                    else //Safety Net incase something slips through
                    {
                        if (toSend.HeldItem == 1882)
                        {
                            msg = $"{user}, {(Species)toSend.Species} has failed to purify";
                            msg += $"\nPls refer to LA report";
                            await SphealEmbed.EmbedAlertMessage(toSend, false, offered.FormArgument, msg, "Bad Trade Evo Purification:").ConfigureAwait(false);
                        }
                        else
                        {
                            msg = $"{user}, {(Species)toSend.Species} has a problem";
                            msg += $"\nPls refer to LA report";
                            await SphealEmbed.EmbedAlertMessage(toSend, false, toSend.FormArgument, msg, "Bad Trilogy Swap:").ConfigureAwait(false);
                        }
                        msg = la2.Report();
                        await SphealEmbed.EmbedAlertMessage(toSend, false, toSend.FormArgument, msg, "Legality Report:").ConfigureAwait(false);
                        DumpPokemon(DumpSetting.DumpFolder, "hacked", toSend);
                        return (toSend, PokeTradeResult.IllegalTrade);
                    }
                }
            }
            else if (swap > 0 && evSwap.Contains(swap)) //EV Presets Swap
            {
                toSend = offered.Clone();
                Log($"User's request is for EV swap using: {GameInfo.GetStrings(1).Species[offered.Species]}");
                string? EVS;

                var la = new LegalityAnalysis(offered);
                if (!la.Valid)
                {
                    EVS = $"Pokémon: {(Species)offered.Species}";
                    EVS += $"\nUser: {user}";
                    EVS += $"\nPokémon shown is not legal";
                    await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, EVS, "Bad EV Swap:").ConfigureAwait(false);
                    DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);

                    EVS = la.Report();
                    await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, EVS, "Legality Report:").ConfigureAwait(false);
                    return (offered, PokeTradeResult.IllegalTrade);
                }
                else
                {
                    //EVs, Either reset or apply chosen preset, totalling to 504, User can apply final 6 themself
                    Log($"Resetting EVs...");
                    toSend.EV_HP = 0;
                    toSend.EV_ATK = 0;
                    toSend.EV_DEF = 0;
                    toSend.EV_SPA = 0;
                    toSend.EV_SPD = 0;
                    toSend.EV_SPE = 0;

                    if (swap == (int)custom.EVRaidAtkItem)
                    {
                        Log($"Type of EV Swap: Raid Atk");
                        toSend.EV_HP = 252;
                        toSend.EV_ATK = 252;
                        toSend.StatNature = 3; //Adamant
                    }
                    else if (swap == (int)custom.EVCompAtkItem)
                    {
                        Log($"Type of EV Swap: Comp Atk");
                        toSend.EV_ATK = 252;
                        toSend.EV_SPE = 252;
                    }
                    else if (swap == (int)custom.EVRaidSPAItem)
                    {
                        Log($"Type of EV Swap: Raid SPAtk");
                        toSend.EV_HP = 252;
                        toSend.EV_SPA = 252;
                        toSend.StatNature = 15; //Modest
                    }
                    else if (swap == (int)custom.EVCompSPAItem)
                    {
                        Log($"Type of EV Swap: Comp SPAtk");
                        toSend.EV_SPA = 252;
                        toSend.EV_SPE = 252;
                    }
                    else if (swap == (int)custom.EVGenDEFItem)
                    {
                        Log($"Type of EV Swap: Generic Def");
                        toSend.EV_HP = 252;
                        toSend.EV_DEF = 252;
                    }
                    else if (swap == (int)custom.EVGenSPDItem)
                    {
                        Log($"Type of EV Swap: Generic SPDef");
                        toSend.EV_HP = 252;
                        toSend.EV_SPD = 252;
                    }
                    toSend.RefreshChecksum();

                    var la2 = new LegalityAnalysis(toSend);
                    if (la2.Valid)
                    {
                        Log($"EV Swap Success.");
                        poke.TradeData = toSend;
                        counts.AddCompletedEVSwaps();
                        await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
                        await Task.Delay(2_500, token).ConfigureAwait(false);
                        return (toSend, PokeTradeResult.Success);
                    }
                    else //Safety Net incase something slips through
                    {
                        EVS = $"{user}, {(Species)offered.Species} has a problem";
                        EVS += $"\nPls refer to LA report";
                        await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, EVS, "Bad EV Swap:").ConfigureAwait(false);

                        EVS = la2.Report();
                        await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, EVS, "Legality Report:").ConfigureAwait(false);
                        DumpPokemon(DumpSetting.DumpFolder, "hacked", toSend);
                        return (toSend, PokeTradeResult.IllegalTrade);
                    }
                }
            }
            //Tera Swapper
            else if (teraItem.Length > 1)
            {
                if (teraItem[1] == "Tera")
                {
                    toSend = offered.Clone();
                    Log($"User's request is for Tera swap using: {GameInfo.GetStrings(1).Species[offered.Species]}");
                    string? msg9;

                    var la = new LegalityAnalysis(offered);
                    if (!la.Valid)
                    {
                        msg9 = $"Pokémon: {(Species)offered.Species}";
                        msg9 += $"\nUser: {user}";
                        msg9 += $"\nPokémon shown is not legal";
                        await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg9, "Bad Tera Swap:").ConfigureAwait(false);
                        DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);

                        msg9 = la.Report();
                        await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg9, "Legality Report:").ConfigureAwait(false);
                        return (offered, PokeTradeResult.IllegalTrade);
                    }
                    else
                    {
                        toSend.TeraTypeOverride = (MoveType)Enum.Parse(typeof(MoveType), teraItem[0]);
                        toSend.RefreshChecksum();
                        Log($"Tera swapped to: {toSend.TeraTypeOverride}");
                        poke.TradeData = toSend;
                        counts.AddCompletedTeraSwaps();
                        await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
                        await Task.Delay(2_500, token).ConfigureAwait(false);
                        return (toSend, PokeTradeResult.Success);
                    }
                }
            }
            if (trade != null)
            {
                var tradeevo = new List<ushort>
                {
                    (ushort)Species.Kadabra,
                    (ushort)Species.Machoke,
                    (ushort)Species.Gurdurr,
                    (ushort)Species.Haunter,
                    (ushort)Species.Graveler,
                    (ushort)Species.Phantump,
                    (ushort)Species.Pumpkaboo,
                    (ushort)Species.Boldore,
                };
                var evo = offered.Species;
                if (evo > 0 && tradeevo.Contains(evo))
                {
                    if (swap != 229)
                    {
                        var msg = $"Pokémon: {(Species)offered.Species}";
                        msg += $"\nUser: {user}";
                        msg += $"\nEquip an Everstone to allow trade";
                        msg += $"\nGiven a ticket for Unauthorised goods";
                        await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Unauthorised Trade evolution sent by:").ConfigureAwait(false);
                        return (toSend, PokeTradeResult.TrainerRequestBad);
                    }
                }
                if (trade.Type == LedyResponseType.AbuseDetected)
                {
                    var msg = $"Found {partner.TrainerName} has been detected for abusing Ledy trades.";
                    if (AbuseSettings.EchoNintendoOnlineIDLedy)
                        msg += $"\nID: {partner.TrainerOnlineID}";
                    if (!string.IsNullOrWhiteSpace(AbuseSettings.LedyAbuseEchoMention))
                        msg = $"{AbuseSettings.LedyAbuseEchoMention} {msg}";
                    EchoUtil.Echo(msg);

                    return (toSend, PokeTradeResult.SuspiciousActivity);
                }

                toSend = trade.Receive;
                poke.TradeData = toSend;

                if (Hub.Config.CustomSwaps.AllowTraderOTInformation)
                {
                    poke.SendNotification(this, "Injecting the requested Pokémon.");
                    if (!await SetTradePartnerDetailsSV(toSend, offered, sav, token).ConfigureAwait(false))
                    {
                        await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
                        await Task.Delay(2_500, token).ConfigureAwait(false);
                    }
                }
                else
                    await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
                await Task.Delay(2_500, token).ConfigureAwait(false);
            }
            else if (config.LedyQuitIfNoMatch)
            {
                DumpPokemon(DumpSetting.DumpFolder, "rejects", offered); //Dump copy of failed request
                var msg = $"Pokémon: {(Species)offered.Species}";
                msg += $"\nNickname: {offered.Nickname}";
                msg += $"\nUser: {partner.TrainerName}";
                await SphealEmbed.EmbedAlertMessage(offered, false, offered.FormArgument, msg, "Bad Request From:").ConfigureAwait(false);
                return (toSend, PokeTradeResult.TrainerRequestBad);
            }
            else if (Hub.Config.CustomSwaps.AllowRandomOT) //Random Distribution OT without Ledy Nicknames
            {
                var counts1 = TradeSettings;
                toSend = Hub.Ledy.Pool.GetRandomTrade();
                if (!await SetTradePartnerDetailsSV(toSend, offered, sav, token).ConfigureAwait(false))
                {
                    await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
                    await Task.Delay(2_500, token).ConfigureAwait(false);
                }
                counts1.AddCompletedDistribution();
                return (toSend, PokeTradeResult.Success);
            }
            return (toSend, PokeTradeResult.Success);
        }
        private void WaitAtBarrierIfApplicable(CancellationToken token)
        {
            if (!ShouldWaitAtBarrier)
                return;
            var opt = Hub.Config.Distribution.SynchronizeBots;
            if (opt == BotSyncOption.NoSync)
                return;

            var timeoutAfter = Hub.Config.Distribution.SynchronizeTimeout;
            if (FailedBarrier == 1) // failed last iteration
                timeoutAfter *= 2; // try to re-sync in the event things are too slow.

            var result = Hub.BotSync.Barrier.SignalAndWait(TimeSpan.FromSeconds(timeoutAfter), token);

            if (result)
            {
                FailedBarrier = 0;
                return;
            }

            FailedBarrier++;
            Log($"Barrier sync timed out after {timeoutAfter} seconds. Continuing.");
        }

        /// <summary>
        /// Checks if the barrier needs to get updated to consider this bot.
        /// If it should be considered, it adds it to the barrier if it is not already added.
        /// If it should not be considered, it removes it from the barrier if not already removed.
        /// </summary>
        private void UpdateBarrier(bool shouldWait)
        {
            if (ShouldWaitAtBarrier == shouldWait)
                return; // no change required

            ShouldWaitAtBarrier = shouldWait;
            if (shouldWait)
            {
                Hub.BotSync.Barrier.AddParticipant();
                Log($"Joined the Barrier. Count: {Hub.BotSync.Barrier.ParticipantCount}");
            }
            else
            {
                Hub.BotSync.Barrier.RemoveParticipant();
                Log($"Left the Barrier. Count: {Hub.BotSync.Barrier.ParticipantCount}");
            }
        }
    }
}
