<div align="center">

# ZSlayer Command Center

**The ultimate server admin toolkit for SPT 4.0 / FIKA**

[![License: MIT](https://img.shields.io/badge/License-MIT-c8aa6e.svg)](LICENSE)
[![SPT](https://img.shields.io/badge/SPT-4.0.x-c8aa6e.svg)]()
[![.NET](https://img.shields.io/badge/.NET-9.0-c8aa6e.svg)]()
[![Version](https://img.shields.io/badge/Version-2.2.0-c8aa6e.svg)]()

---

A browser-based command center that gives server admins full control over their SPT / FIKA server — manage players, tweak the flea market, control headless clients, and much more. No restarts. No config file hunting. Just open a tab and run your server.

</div>

---

## Features

### Live Dashboard
Real-time server monitoring with console output, activity log, player counts, and system stats — all in one view.

### Player Management
Give items, adjust XP, modify skills, complete quests, and send in-game mail. Supports item presets for quick distribution.

### Flea Market Control
Fine-tune the flea economy with global, per-category, and per-item price multipliers. Adjust tax rates, toggle barter/currency offers, set offer duration, and regenerate the market on demand.

### Headless Client Manager
Built-in process manager for FIKA headless clients. Auto-start on server boot, auto-restart on crash, and full Start / Stop / Restart controls from the dashboard. No more separate tools.

### Access Control
Whitelist, blacklist, and ban system to control who connects to your server.

### Activity Logging
Tracks admin actions (item gives, config changes, server events) with retention controls.

---

## Planned Features

> Development is organized into phases. Each phase adds a major feature to the command center.

| Status | Phase | Feature | Description |
|:------:|:-----:|---------|-------------|
| :white_check_mark: | 0 | **Rebrand** | Project rename and restructure |
| :white_check_mark: | 1 | **Dashboard** | Server stats, console, activity log |
| :white_check_mark: | 2 | **Player Management** | Items, XP, skills, quests, mail |
| :construction: | 3 | **Flea Market** | Price multipliers, tax, offers, regeneration |
| :hourglass: | 4 | **Trader Control** | Trader inventory, pricing, restock |
| :hourglass: | 5 | **Quest Editor** | Create and modify quests |
| :hourglass: | 6 | **Progression & Skills** | Skill management and leveling |
| :hourglass: | 7 | **Backup & Restore** | Database snapshots and recovery |
| :hourglass: | 8 | **Scheduler & Events** | Timed events and automation |
| :hourglass: | 9 | **Game Values Editor** | Direct database value tweaking |
| :hourglass: | 10 | **Config Profiles** | Save, load, export, and share configs |
| :hourglass: | 11 | **Gear Presets** | Loadout templates and distribution |
| :hourglass: | 12 | **Bounty Board** | Player bounty system |
| :hourglass: | 13 | **Clan System** | Clans and shared storage |
| :hourglass: | 14 | **Client Plugin** | In-game overlay and notifications |
| :hourglass: | 15 | **Stash Viewer** | Visual inventory browser |
| :hourglass: | 16 | **Polish & Release** | Final cleanup and v3.0 launch |

---

## Quick Start

### Installation
1. Download the latest release
2. Extract `ZSlayerCommandCenter` into your `SPT/user/mods/` folder
3. Start the server

### Access
The command center URL is printed in the server console on startup:
```
Local:   https://127.0.0.1:6969/zslayer/cc/
LAN:     https://<your-lan-ip>:6969/zslayer/cc/
```
Share the LAN or public URL with players for remote access.

### Headless Client (Optional)
If you're running a FIKA headless client, the mod will auto-detect `EscapeFromTarkov.exe` and offer process management through the dashboard. Set a Profile ID in the headless settings to enable auto-start.

---

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Server mod | C# / .NET 9.0 with SPTarkov DI |
| Frontend | Single HTML file — inline CSS/JS, no build tools |
| API | Custom HTTP routes under `/zslayer/cc/` |
| Design | Dark theme with gold accent (`#c8aa6e`) |

---

## Configuration

All settings are managed through the web UI. The config file at `config/config.json` is auto-managed — manual editing is supported but not required.

<details>
<summary>Config sections</summary>

- **access** — Whitelist / blacklist / ban list
- **dashboard** — Refresh intervals, console buffer, log retention
- **items** — Item presets for quick distribution
- **flea** — Price multipliers, tax, offer settings
- **headless** — Auto-start, restart, profile ID, EXE path

</details>

---

## Requirements

- **SPT** 4.0.x
- **FIKA** (optional — required for headless client features)
- A modern browser (Chrome, Firefox, Edge)

---

## License

[MIT](LICENSE) — Built by [ZSlayerHQ](https://github.com/ZSlayerHQ)
