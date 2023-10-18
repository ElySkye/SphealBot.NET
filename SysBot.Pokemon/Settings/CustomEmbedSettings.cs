using System.ComponentModel;

namespace SysBot.Pokemon
{
    public class CustomEmbedSettings
    {
        private const string CustomEmbed = nameof(CustomEmbed);
        public override string ToString() => "Custom Embed Settings";

        [Category(CustomEmbed), Description("Toggle ON ALL user custom GIFs [Default: False]\nEnsure none of them are empty boxes")]
        public bool CustomGIFs { get; set; } = false;

        [Category(CustomEmbed), Description("Custom GIF for users connecting during cooldown")]
        public string CooldownGIF { get; set; } = "";

        [Category(CustomEmbed), Description("Custom GIF for user got banned embed")]
        public string BanEmbedGIF { get; set; } = "";

        [Category(CustomEmbed), Description("Custom GIF for banned user connecting embed")]
        public string BanUEmbedGIF { get; set; } = "";
    }
}