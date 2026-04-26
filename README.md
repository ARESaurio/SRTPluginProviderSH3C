# SRTPluginProviderSH3C

[![Silent Hill 3](https://img.shields.io/badge/Silent%20Hill%203-2003-orange)](https://en.wikipedia.org/wiki/Silent_Hill_3)
[![SRT Host](https://img.shields.io/badge/SRT-Plugin%20Provider-blue)](https://github.com/SpeedRunTooling)
![Platform](https://img.shields.io/badge/platform-x64-lightgrey)

A game memory provider plugin for **Silent Hill 3 (2003) PC** for the [SpeedRunTooling](https://github.com/SpeedRunTooling) SRT Host.  
Reads real-time game data from the running process and exposes it for overlays and other SRT consumer plugins.

---

## Tracked Values

| Category | Fields |
|---|---|
| **Player** | HP, Health State (Fine / Caution / Danger / Dead) |
| **In-Game Time** | Raw seconds, Formatted `hh:mm:ss` |
| **Weapons** | Handgun, Shotgun, Sub-Machine Gun |
| **Items** | Beef Jerky count |
| **Run Stats** | Saves, Items, Shooting Kills, Melee Kills, Damage Taken |
| **Difficulty** | Action Level, Riddle Level |
| **Boss Times** | Split Worm, Missionary, Leonard, Memory of Alessa, God |

---

## Building

1. Clone this repo alongside [SpeedRunTool](https://github.com/SpeedRunTooling/SpeedRunTool):
   ```
   repos/
   ├── SpeedRunTool/
   └── SRTPluginProviderSH3C/
   ```
2. Open `SRTPluginProviderSH3C.sln` in Visual Studio.
3. Build (**x64**). Output is automatically copied to `E:\SRT\plugins\` if the directory exists.

---

## Credits

- **[Ares](https://github.com/ARESaurio)** — Plugin development  
- **[Miguel_mm_95](https://github.com/miguelmm95)** — SH3 NHT memory research

## License

MIT
