﻿using PKHeX.Core;
using System.ComponentModel;

namespace SysBot.Pokemon
{
    public class CustomSwapSettings
    {
        private const string CustomSwap = nameof(CustomSwap);
        public override string ToString() => "Custom Swap Settings";

        [Category(CustomSwap), Description("When set to something other than None, the Random Trades will accept this alternate species in addition to the nickname match.")]
        public Species LedySpecies2 { get; set; } = Species.None;

        [Category(CustomSwap), Description("Species selector for $it command. Default: Finizen")]
        public Species ItemTradeSpecies { get; set; } = Species.Finizen;

        [Category(CustomSwap), Description("Held item used to trigger OT Swap.")]
        public SwapItem OTSwapItem { get; set; } = SwapItem.Antidote;

        [Category(CustomSwap), Description("Held item used to trigger Trilogy Swap.")]
        public SwapItem TrilogySwapItem { get; set; } = SwapItem.Awakening;

        [Category(CustomSwap), Description("Held item used to trigger EV Reset")]
        public SwapItem EVResetItem { get; set; } = SwapItem.TinyMushroom;

        [Category(CustomSwap), Description("Held item used to trigger EV Swap : Raid Attack")]
        public SwapItem EVRaidAtkItem { get; set; } = SwapItem.XAttack;

        [Category(CustomSwap), Description("Held item used to trigger EV Swap : Comp Attack")]
        public SwapItem EVCompAtkItem { get; set; } = SwapItem.MuscleFeather;

        [Category(CustomSwap), Description("Held item used to trigger EV Swap : Raid Special Attack")]
        public SwapItem EVRaidSPAItem { get; set; } = SwapItem.XSpAtk;

        [Category(CustomSwap), Description("Held item used to trigger EV Swap : Comp Special Attack")]
        public SwapItem EVCompSPAItem { get; set; } = SwapItem.GeniusFeather;

        [Category(CustomSwap), Description("Held item used to trigger EV Swap : Generic Defence")]
        public SwapItem EVGenDEFItem { get; set; } = SwapItem.XDefense;

        [Category(CustomSwap), Description("Held item used to trigger EV Swap : Generic Special Defence")]
        public SwapItem EVGenSPDItem { get; set; } = SwapItem.XSpDef;

        [Category(CustomSwap), Description("Nickname to trigger Mystery Eggs")]
        public string MysteryEgg { get; set; } = "Mystery";

        [Category(CustomSwap), Description("Input Nickname for Spheal Event.")]
        public string SphealEvent { get; set; } = "SphealEventPlaceholder";

        [Category(CustomSwap), Description("Enable OT Changing")]
        public bool AllowTraderOTInformation { get; set; } = true;

        [Category(CustomSwap), Description("Enable Non Ledy OT, Requires config <LedyQuitIfNoMatch> to be false")]
        public bool AllowRandomOT { get; set; } = true;
    }
}