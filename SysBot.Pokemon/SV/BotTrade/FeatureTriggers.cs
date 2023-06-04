﻿using System;
using System.Collections.Generic;
using System.Text;

namespace SysBot.Pokemon
{
    //Held items to trigger Special Feature Swaps [Currently only for SV]
    //TMs removed
    //Item list extracted from Sinthrill (https://github.com/Sinthrill/SysSwapBot.NET)
    public enum SwapItem
    {
        None = 0,
        AbilityCapsule = 645,
        AbilityPatch = 1606,
        AbilityShield = 1881,
        AbsorbBulb = 545,
        AdamantMint = 1232,
        AdrenalineOrb = 846,
        AguavBerry = 162,
        AirBalloon = 541,
        AmuletCoin = 223,
        Antidote = 18,
        ApicotBerry = 205,
        AspearBerry = 153,
        AssaultVest = 640,
        AuspiciousArmor = 2344,
        Awakening = 21,
        BabiriBerry = 199,
        BalmMushroom = 580,
        BigBambooShoot = 1843,
        BigMushroom = 87,
        BigNugget = 581,
        BigPearl = 89,
        BigRoot = 296,
        BindingBand = 544,
        BlackBelt = 241,
        BlackGlasses = 240,
        BlackSludge = 281,
        BlunderPolicy = 1121,
        BoldMint = 1235,
        BoosterEnergy = 1880,
        BottleCap = 795,
        BraveMint = 1234,
        BrightPowder = 213,
        BugTeraShard = 1873,
        BurnHeal = 19,
        Calcium = 49,
        CalmMint = 1243,
        Carbos = 48,
        CarefulMint = 1245,
        CellBattery = 546,
        Charcoal = 249,
        ChartiBerry = 195,
        CheriBerry = 149,
        ChestoBerry = 150,
        ChilanBerry = 200,
        ChippedPot = 1254,
        ChoiceBand = 220,
        ChoiceScarf = 287,
        ChoiceSpecs = 297,
        ChopleBerry = 189,
        CleanseTag = 224,
        ClearAmulet = 1882,
        CleverFeather = 569,
        CobaBerry = 192,
        ColburBerry = 198,
        CometShard = 583,
        CovertCloak = 1885,
        CrackedPot = 1253,
        DampRock = 285,
        DarkTeraShard = 1877,
        DawnStone = 109,
        DestinyKnot = 280,
        DireHit = 56,
        DragonFang = 250,
        DragonTeraShard = 1876,
        DuskStone = 108,
        EjectButton = 547,
        EjectPack = 1119,
        ElectricSeed = 881,
        ElectricTeraShard = 1865,
        Elixir = 40,
        EnergyPowder = 34,
        EnergyRoot = 35,
        Ether = 38,
        Everstone = 229,
        Eviolite = 538,
        ExpCandyL = 1127,
        ExpCandyM = 1126,
        ExpCandyS = 1125,
        ExpCandyXL = 1128,
        ExpCandyXS = 1124,
        ExpertBelt = 268,
        FairyTeraShard = 1879,
        FightingTeraShard = 1868,
        FigyBerry = 159,
        FireStone = 82,
        FireTeraShard = 1863,
        FlameOrb = 273,
        FloatStone = 539,
        FlyingTeraShard = 1871,
        FocusBand = 230,
        FocusSash = 275,
        FreshWater = 30,
        FullHeal = 27,
        FullRestore = 23,
        GanlonBerry = 202,
        GeniusFeather = 568,
        GentleMint = 1244,
        GhostTeraShard = 1875,
        GoldBottleCap = 796,
        GrassTeraShard = 1866,
        GrassySeed = 884,
        GrepaBerry = 173,
        GripClaw = 286,
        GroundTeraShard = 1870,
        GuardSpec = 55,
        HabanBerry = 197,
        HardStone = 238,
        HastyMint = 1248,
        HealPowder = 36,
        HealthFeather = 565,
        HeatRock = 284,
        HeavyDutyBoots = 1120,
        HondewBerry = 172,
        Honey = 94,
        HPUp = 45,
        HyperPotion = 25,
        IapapaBerry = 163,
        IceHeal = 20,
        IceStone = 849,
        IceTeraShard = 1867,
        IcyRock = 282,
        ImpishMint = 1236,
        Iron = 47,
        IronBall = 278,
        JollyMint = 1249,
        KasibBerry = 196,
        KebiaBerry = 190,
        KeeBerry = 687,
        KelpsyBerry = 170,
        KingsRock = 221,
        LaggingTail = 279,
        LansatBerry = 206,
        LaxMint = 1237,
        LeadersCrest = 2345,
        LeafStone = 85,
        Leftovers = 234,
        Lemonade = 32,
        LeppaBerry = 154,
        LiechiBerry = 201,
        LifeOrb = 270,
        LightBall = 236,
        LightClay = 269,
        LoadedDice = 1886,
        LonelyMint = 1231,
        LuckyEgg = 231,
        LumBerry = 157,
        LuminousMoss = 648,
        Magnet = 242,
        MagoBerry = 161,
        MaliciousArmor = 1861,
        MarangaBerry = 688,
        MaxElixir = 41,
        MaxEther = 39,
        MaxPotion = 24,
        MaxRevive = 29,
        MentalHerb = 219,
        MetalCoat = 233,
        Metronome = 277,
        MildMint = 1240,
        MiracleSeed = 239,
        MirrorHerb = 1883,
        MistySeed = 883,
        ModestMint = 1239,
        MoomooMilk = 33,
        MoonStone = 81,
        MuscleBand = 266,
        MuscleFeather = 566,
        MysticWater = 243,
        NaiveMint = 1250,
        NaughtyMint = 1233,
        NeverMeltIce = 246,
        NormalGem = 564,
        NormalTeraShard = 1862,
        Nugget = 92,
        OccaBerry = 184,
        OranBerry = 155,
        OvalStone = 110,
        ParalyzeHeal = 22,
        PasshoBerry = 185,
        PayapaBerry = 193,
        Pearl = 88,
        PearlString = 582,
        PechaBerry = 151,
        PersimBerry = 156,
        PetayaBerry = 204,
        PinkNectar = 855,
        PoisonBarb = 245,
        PoisonTeraShard = 1869,
        PokéDoll = 63,
        PomegBerry = 169,
        Potion = 17,
        PowerAnklet = 293,
        PowerBand = 292,
        PowerBelt = 290,
        PowerBracer = 289,
        PowerHerb = 271,
        PowerLens = 291,
        PowerWeight = 294,
        PPMax = 53,
        PPUp = 51,
        PrettyFeather = 571,
        ProtectivePads = 880,
        Protein = 46,
        PsychicSeed = 882,
        PsychicTeraShard = 1872,
        PunchingGlove = 1884,
        PurpleNectar = 856,
        QualotBerry = 171,
        QuickClaw = 217,
        QuietMint = 1242,
        RareBone = 106,
        RareCandy = 50,
        RashMint = 1241,
        RawstBerry = 152,
        RazorClaw = 326,
        RedCard = 542,
        RedNectar = 853,
        RelaxedMint = 1238,
        ResistFeather = 567,
        RevivalHerb = 37,
        Revive = 28,
        RindoBerry = 187,
        RingTarget = 543,
        RockTeraShard = 1874,
        RockyHelmet = 540,
        RoomService = 1122,
        RoseliBerry = 686,
        SafetyGoggles = 650,
        SalacBerry = 203,
        SassyMint = 1246,
        ScopeLens = 232,
        SeriousMint = 1251,
        SharpBeak = 244,
        ShedShell = 295,
        ShellBell = 253,
        ShinyStone = 107,
        ShucaBerry = 191,
        SilkScarf = 251,
        SilverPowder = 222,
        SitrusBerry = 158,
        SmokeBall = 228,
        SmoothRock = 283,
        Snowball = 649,
        SodaPop = 31,
        SoftSand = 237,
        SootheBell = 218,
        SpellTag = 247,
        Stardust = 90,
        StarfBerry = 207,
        StarPiece = 91,
        SteelTeraShard = 1878,
        StickyBarb = 288,
        SunStone = 80,
        SuperPotion = 26,
        SweetApple = 1116,
        SwiftFeather = 570,
        TamatoBerry = 174,
        TangaBerry = 194,
        TartApple = 1117,
        TerrainExtender = 879,
        ThroatSpray = 1118,
        ThunderStone = 83,
        TimidMint = 1247,
        TinyBambooShoot = 1842,
        TinyMushroom = 86,
        ToxicOrb = 272,
        TwistedSpoon = 248,
        UtilityUmbrella = 1123,
        WacanBerry = 186,
        WaterStone = 84,
        WaterTeraShard = 1864,
        WeaknessPolicy = 639,
        WhiteHerb = 214,
        WideLens = 265,
        WikiBerry = 160,
        WiseGlasses = 267,
        XAccuracy = 60,
        XAttack = 57,
        XDefense = 58,
        XSpAtk = 61,
        XSpDef = 62,
        XSpeed = 59,
        YacheBerry = 188,
        YellowNectar = 854,
        Zinc = 52,
        ZoomLens = 276,
        MAX_COUNT,
    }
    public enum TradeEvoSpecies : ushort
    {
        Kadabra = 64,
        Machoke = 67,
        Graveler = 75,
        Haunter = 93,
        Gurdurr = 533,
        Phantump = 708,
        Pumpkaboo = 710,
    }
}