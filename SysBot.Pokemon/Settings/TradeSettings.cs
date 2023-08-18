using PKHeX.Core;
using SysBot.Base;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace SysBot.Pokemon
{
    public class TradeSettings : IBotStateSettings, ICountSettings
    {
        private const string TradeCode = nameof(TradeCode);
        private const string TradeConfig = nameof(TradeConfig);
        private const string Dumping = nameof(Dumping);
        private const string Counts = nameof(Counts);
        public override string ToString() => "Trade Bot Settings";

        [Category(TradeConfig), Description("Time to wait for a trade partner in seconds.")]
        public int TradeWaitTime { get; set; } = 30;

        [Category(TradeConfig), Description("Max amount of time in seconds pressing A to wait for a trade to process.")]
        public int MaxTradeConfirmTime { get; set; } = 25;

        [Category(TradeCode), Description("Minimum Link Code.")]
        public int MinTradeCode { get; set; } = 8180;

        [Category(TradeCode), Description("Maximum Link Code.")]
        public int MaxTradeCode { get; set; } = 8199;

        [Category(Dumping), Description("Dump Trade: Dumping routine will stop after a maximum number of dumps from a single user.")]
        public int MaxDumpsPerTrade { get; set; } = 20;

        [Category(Dumping), Description("Dump Trade: Dumping routine will stop after spending x seconds in trade.")]
        public int MaxDumpTradeTime { get; set; } = 180;

        [Category(Dumping), Description("Dump Trade: If enabled, Dumping routine will output legality check information to the user.")]
        public bool DumpTradeLegalityCheck { get; set; } = true;

        [Category(TradeConfig), Description("When enabled, the screen will be turned off during normal bot loop operation to save power.")]
        public bool ScreenOff { get; set; }

        /// <summary>
        /// Gets a random trade code based on the range settings.
        /// </summary>
        public int GetRandomTradeCode() => Util.Rand.Next(MinTradeCode, MaxTradeCode + 1);

        private int _completedSurprise;
        private int _completedDistribution;
        private int _completedTrades;
        private int _completedOTSwaps;
        private int _completedBallSwaps;
        private int _completedTeraSwaps;
        private int _completedTrilogySwaps;
        private int _completedGenderSwaps;
        private int _completedEVSwaps;
        private int _completedMystery;
        private int _completedSeedChecks;
        private int _completedClones;
        private int _completedDumps;

        [Category(Counts), Description("Completed Surprise Trades")]
        public int CompletedSurprise
        {
            get => _completedSurprise;
            set => _completedSurprise = value;
        }

        [Category(Counts), Description("Completed Link Trades (Distribution)")]
        public int CompletedDistribution
        {
            get => _completedDistribution;
            set => _completedDistribution = value;
        }

        [Category(Counts), Description("Completed Link Trades (Specific User)")]
        public int CompletedTrades
        {
            get => _completedTrades;
            set => _completedTrades = value;
        }

        [Category(Counts), Description("Completed OT Swap Trades")]
        public int CompletedOTSwaps
        {
            get => _completedOTSwaps;
            set => _completedOTSwaps = value;
        }

        [Category(Counts), Description("Completed Ball Swap Trades")]
        public int CompletedBallSwaps
        {
            get => _completedBallSwaps;
            set => _completedBallSwaps = value;
        }

        [Category(Counts), Description("Completed Tera Swap Trades")]
        public int CompletedTeraSwaps
        {
            get => _completedTeraSwaps;
            set => _completedTeraSwaps = value;
        }

        [Category(Counts), Description("Completed Trilogy Swap Trades")]
        public int CompletedTrilogySwaps
        {
            get => _completedTrilogySwaps;
            set => _completedTrilogySwaps = value;
        }

        [Category(Counts), Description("Completed Gender Swap Trades")]
        public int CompletedGenderSwaps
        {
            get => _completedGenderSwaps;
            set => _completedGenderSwaps = value;
        }

        [Category(Counts), Description("Completed EV Swap Trades")]
        public int CompletedEVSwaps
        {
            get => _completedEVSwaps;
            set => _completedEVSwaps = value;
        }

        [Category(Counts), Description("Completed Mystery Trades [Default: Eggs Only]")]
        public int CompletedMystery
        {
            get => _completedMystery;
            set => _completedMystery = value;
        }

        [Category(Counts), Description("Completed Seed Check Trades")]
        public int CompletedSeedChecks
        {
            get => _completedSeedChecks;
            set => _completedSeedChecks = value;
        }

        [Category(Counts), Description("Completed Clone Trades (Specific User)")]
        public int CompletedClones
        {
            get => _completedClones;
            set => _completedClones = value;
        }

        [Category(Counts), Description("Completed Dump Trades (Specific User)")]
        public int CompletedDumps
        {
            get => _completedDumps;
            set => _completedDumps = value;
        }

        [Category(Counts), Description("When enabled, the counts will be emitted when a status check is requested.")]
        public bool EmitCountsOnStatusCheck { get; set; }

        public void AddCompletedTrade() => Interlocked.Increment(ref _completedTrades);
        public void AddCompletedSeedCheck() => Interlocked.Increment(ref _completedSeedChecks);
        public void AddCompletedSurprise() => Interlocked.Increment(ref _completedSurprise);
        public void AddCompletedDistribution() => Interlocked.Increment(ref _completedDistribution);
        public void AddCompletedOTSwaps() => Interlocked.Increment(ref _completedOTSwaps);
        public void AddCompletedBallSwaps() => Interlocked.Increment(ref _completedBallSwaps);
        public void AddCompletedTeraSwaps() => Interlocked.Increment(ref _completedTeraSwaps);
        public void AddCompletedTrilogySwaps() => Interlocked.Increment(ref _completedTrilogySwaps);
        public void AddCompletedGenderSwaps() => Interlocked.Increment(ref _completedGenderSwaps);
        public void AddCompletedEVSwaps() => Interlocked.Increment(ref _completedEVSwaps);
        public void AddCompletedMystery() => Interlocked.Increment(ref _completedMystery);
        public void AddCompletedDumps() => Interlocked.Increment(ref _completedDumps);
        public void AddCompletedClones() => Interlocked.Increment(ref _completedClones);

        public IEnumerable<string> GetNonZeroCounts()
        {
            if (!EmitCountsOnStatusCheck)
                yield break;
            if (CompletedSeedChecks != 0)
                yield return $"Seed Check Trades: {CompletedSeedChecks}";
            if (CompletedClones != 0)
                yield return $"Clone Trades: {CompletedClones}";
            if (CompletedDumps != 0)
                yield return $"Dump Trades: {CompletedDumps}";
            if (CompletedTrades != 0)
                yield return $"Link Trades: {CompletedTrades}";
            if (CompletedDistribution != 0)
                yield return $"Distribution Trades: {CompletedDistribution}";
            if (CompletedOTSwaps != 0)
                yield return $"OT Swaps: {CompletedOTSwaps}";
            if (CompletedBallSwaps != 0)
                yield return $"Ball Swaps: {CompletedBallSwaps}";
            if (CompletedTeraSwaps != 0)
                yield return $"Tera Swaps: {CompletedTeraSwaps}";
            if (CompletedTrilogySwaps != 0)
                yield return $"Trilogy Swaps: {CompletedTrilogySwaps}";
            if (CompletedGenderSwaps != 0)
                yield return $"Gender Swaps: {CompletedGenderSwaps}";
            if (CompletedEVSwaps != 0)
                yield return $"EV Swaps: {CompletedEVSwaps}";
            if (CompletedMystery != 0)
                yield return $"Mystery Eggs: {CompletedMystery}";
            if (CompletedSurprise != 0)
                yield return $"Surprise Trades: {CompletedSurprise}";
        }
    }
}
