using System.ComponentModel;
using System.Diagnostics.Metrics;
using System.Threading;

namespace SysBot.Pokemon
{
    public class CustomEmbedSettings
    {
        private const string CustomEmbed = nameof(CustomEmbed);
        private int _sphealfactcount;
        public override string ToString() => "Custom Embed Settings";

        [Category(CustomEmbed), Description("Removes the commands section for Direct Trade Embeds")]
        public bool ShortDTEmbed { get; set; } = false;

        [Category(CustomEmbed), Description("Toggle ON ALL user custom GIFs [Default: False]\nEmpty boxes = Use Default")]
        public bool CustomGIFs { get; set; } = false;

        [Category(CustomEmbed), Description("Toggle ON ALL user custom emojis, To disable specific ones, leave the box blank\nTo get format in discord, type backslash followed by the emoji")]
        public bool CustomEmoji { get; set; } = false;

        [Category(CustomEmbed), Description("Custom GIF for users connecting during cooldown")]
        public string CooldownGIF { get; set; } = "";

        [Category(CustomEmbed), Description("Custom GIF for user got banned embed")]
        public string BanEmbedGIF { get; set; } = "";

        [Category(CustomEmbed), Description("Custom GIF for banned user connecting embed")]
        public string BanUEmbedGIF { get; set; } = "";

        [Category(CustomEmbed), Description("Custom additional msg for ping command\nThis message has a higher chance to appear")]
        public string CustomPingMsg { get; set; } = "";

        [Category(CustomEmbed), Description("Emoji for SV display on Trade Embeds")]
        public string TEGameIconSV{ get; set; } = "";

        [Category(CustomEmbed), Description("Emoji for SWSH display on Trade Embeds")]
        public string TEGameIconSWSH { get; set; } = "";

        [Category(CustomEmbed), Description("Emoji for PLA display on Trade Embeds")]
        public string TEGameIconPLA { get; set; } = "";

        [Category(CustomEmbed), Description("Emoji for BDSP display on Trade Embeds")]
        public string TEGameIconBDSP { get; set; } = "";

        [Category(CustomEmbed), Description("Emoji for Shinyness display on Trade Embeds")]
        public string TEShiny { get; set; } = ":star2:";

        [Category(CustomEmbed), Description("Emoji for Level 100 display on Trade Embeds")]
        public string TELevel { get; set; } = ":100:";

        [Category(CustomEmbed), Description("Emoji for Jumbo Size display on Trade Embeds")]
        public string TESizeJumbo { get; set; } = ":small_blue_diamond:";

        [Category(CustomEmbed), Description("Emoji for Mini Size display on Trade Embeds")]
        public string TESizeMini { get; set; } = ":large_blue_diamond:";

        [Category(CustomEmbed), Description("Emoji for Bug Tera on Trade Embeds")]
        public string TeraIMGBug { get; set; } = "<:terabug:1165430460705407017>";

        [Category(CustomEmbed), Description("Emoji for Dark Tera on Trade Embeds")]
        public string TeraIMGDark { get; set; } = "<:teradark:1165430464799068240>";

        [Category(CustomEmbed), Description("Emoji for Dragon Tera on Trade Embeds")]
        public string TeraIMGDragon { get; set; } = "<:teradragon:1165430466585821365>";

        [Category(CustomEmbed), Description("Emoji for Electric Tera on Trade Embeds")]
        public string TeraIMGElectric { get; set; } = "<:teraelectric:1165430470289403954>";

        [Category(CustomEmbed), Description("Emoji for Fairy Tera on Trade Embeds")]
        public string TeraIMGFairy { get; set; } = "<:terafairy:1165430472034226316>";

        [Category(CustomEmbed), Description("Emoji for Fighting Tera on Trade Embeds")]
        public string TeraIMGFighting { get; set; } = "<:terafighting:1165430475888803950>";

        [Category(CustomEmbed), Description("Emoji for Fire Tera on Trade Embeds")]
        public string TeraIMGFire { get; set; } = "<:terafire:1165430479374266428>";

        [Category(CustomEmbed), Description("Emoji for Flying Tera on Trade Embeds")]
        public string TeraIMGFlying { get; set; } = "<:teraflying:1165430481475608586>";

        [Category(CustomEmbed), Description("Emoji for Ghost Tera on Trade Embeds")]
        public string TeraIMGGhost { get; set; } = "<:teraghost:1165430485401485353>";

        [Category(CustomEmbed), Description("Emoji for Grass Tera on Trade Embeds")]
        public string TeraIMGGrass { get; set; } = "<:teragrass:1165430491760038000>";

        [Category(CustomEmbed), Description("Emoji for Ground Tera on Trade Embeds")]
        public string TeraIMGGround { get; set; } = "<:teraground:1165430496487018617>";

        [Category(CustomEmbed), Description("Emoji for Ice Tera on Trade Embeds")]
        public string TeraIMGIce { get; set; } = "<:teraice:1165430498709999758>";

        [Category(CustomEmbed), Description("Emoji for Normal Tera on Trade Embeds")]
        public string TeraIMGNormal { get; set; } = "<:teranormal:1165430502598119424>";

        [Category(CustomEmbed), Description("Emoji for Poison Tera on Trade Embeds")]
        public string TeraIMGPoison { get; set; } = "<:terapoison:1165430504389083287>";

        [Category(CustomEmbed), Description("Emoji for Psychic Tera on Trade Embeds")]
        public string TeraIMGPsychic { get; set; } = "<:terapsychic:1165430508050731039>";

        [Category(CustomEmbed), Description("Emoji for Rock Tera on Trade Embeds")]
        public string TeraIMGRock { get; set; } = "<:terarock:1165430511989170228>";

        [Category(CustomEmbed), Description("Emoji for Steel Tera on Trade Embeds")]
        public string TeraIMGSteel { get; set; } = "<:terasteel:1165430514111496302>";

        [Category(CustomEmbed), Description("Emoji for Water Tera on Trade Embeds")]
        public string TeraIMGWater { get; set; } = "<:terawater:1165430518142218241>";

        [Category(CustomEmbed), Description("Emoji for Stellar Tera on Trade Embeds")]
        public string TeraIMGStellar { get; set; } = "<:terastellar:1184712293137776650>";

        [Category(CustomEmbed), Description("Spheal Facts Counter")]
        public int SphealFactsCounter
        {
            get => _sphealfactcount;
            set => _sphealfactcount = value;
        }
        public void AddSphealCount() => Interlocked.Increment(ref _sphealfactcount);
    }
}