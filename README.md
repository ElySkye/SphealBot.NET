# SysBot.NET
![License](https://img.shields.io/badge/License-AGPLv3-blue.svg)

## Support Discord:

This is a fork of [SysBot](https://github.com/kwsch/SysBot.NET). Do not join the official PDP discord and ask for assistance.
For any issues, wait for an update. Do not bother the devs in PDP.
Else, feel free to join our discord for support:
[Sphealtopia](http://discord.gg/sphealtopia)

## Special Thanks & Credits:

- [Kurt](https://github.com/kwsch/SysBot.NET) for making SysBot & PKHeX
- [architdate](https://github.com/architdate/PKHeX-Plugins) for PKHEX Plugins
- [Lusamine](https://github.com/Lusamine/SysBot.NET) for maintaining them
- [PDP server](https://discord.gg/tDMvSRv) I've learnt a lot from just reading some of the conversations
- [PhantomL98](https://github.com/PhantomL98/SysBot.NET) for all the guidance, teaching and collaboration help
- [Sinthrill](https://github.com/Sinthrill/SysSwapBot.NET) for getting me started in programming and all the cool tricks you taught me
- [Berichan](https://github.com/Sinthrill/SysSwapBot.NET) for OT details
- [easyworld](https://github.com/easyworld/SysBot.NET) for reference on how to do SWSH OT
- [Koi](https://github.com/Koi-3088/ForkBot.NET) & [zyro](https://github.com/zyro670/NotForkBot.NET) for the various useful discord commands
- [BakaKaito](https://github.com/BakaKaito/MergeBot.NET) for HomeImages & inspiration to do trade embeds

## SysBot.Base:
- Base logic library to be built upon in game-specific projects.
- Contains a synchronous and asynchronous Bot connection class to interact with sys-botbase.

## SysBot.Tests:
- Unit Tests for ensuring logic behaves as intended :)

# Example Implementations

The driving force to develop this project is automated bots for Nintendo Switch Pokémon games. An example implementation is provided in this repo to demonstrate interesting tasks this framework is capable of performing. Refer to the [Wiki](https://github.com/kwsch/SysBot.NET/wiki) for more details on the supported Pokémon features.

## SysBot.Pokemon:
- Class library using SysBot.Base to contain logic related to creating & running Sword/Shield bots.

## SysBot.Pokemon.WinForms:
- Simple GUI Launcher for adding, starting, and stopping Pokémon bots (as described above).
- Configuration of program settings is performed in-app and is saved as a local json file.

## SysBot.Pokemon.Discord:
- Discord interface for remotely interacting with the WinForms GUI.
- Provide a discord login token and the Roles that are allowed to interact with your bots.
- Commands are provided to manage & join the distribution queue.

## SysBot.Pokemon.Twitch:
- Twitch.tv interface for remotely announcing when the distribution starts.
- Provide a Twitch login token, username, and channel for login.

## SysBot.Pokemon.YouTube:
- YouTube.com interface for remotely announcing when the distribution starts.
- Provide a YouTube login ClientID, ClientSecret, and ChannelID for login.

Uses [Discord.Net](https://github.com/discord-net/Discord.Net) , [TwitchLib](https://github.com/TwitchLib/TwitchLib) and [StreamingClientLibary](https://github.com/SaviorXTanren/StreamingClientLibrary) as a dependency via Nuget.

## Other Dependencies
Pokémon API logic is provided by [PKHeX](https://github.com/kwsch/PKHeX/), and template generation is provided by [AutoMod](https://github.com/architdate/PKHeX-Plugins/).

# License
Refer to the `License.md` for details regarding licensing.
