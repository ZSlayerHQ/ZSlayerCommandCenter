<div align="center">

# ZSlayer Command Center

**The ultimate server admin toolkit for SPT 4.0 / FIKA**

[![License: MIT](https://img.shields.io/badge/License-MIT-c8aa6e.svg)](LICENSE)
[![SPT](https://img.shields.io/badge/SPT-4.0.x-c8aa6e.svg)]()
[![FIKA](https://img.shields.io/badge/FIKA-Compatible-4a7c59.svg)]()
[![.NET](https://img.shields.io/badge/.NET-9.0-512bd4.svg)]()
[![Version](https://img.shields.io/badge/Version-2.2.5-c8aa6e.svg)]()

---

A browser-based command center that gives server admins full control over their SPT / FIKA server. Manage players, monitor raids in real time, control the flea market, run headless clients, and more — all from a single tab. No restarts required. No config file hunting.

[Discord](https://discord.gg/ZSlayerHQ) | [YouTube](https://www.youtube.com/@ZSlayerHQ-ImBenCole)

</div>

---

## What Is This?

ZSlayer Command Center is a **server-side mod** for [SPT (Single Player Tarkov)](https://www.sp-tarkov.com/) that adds a full web-based admin panel to your server. It runs alongside the SPT server process and serves a responsive dark-themed UI at `https://<your-server>:6969/zslayer/cc/`.

It's designed for **FIKA server admins** who want real-time visibility and control without touching JSON files or restarting their server. Every feature works through the browser — from giving items to players, to watching live raid telemetry from a headless client, to fine-tuning the entire flea market economy.

The project consists of two components:
- **Server Mod** (required) — the C# mod that runs inside the SPT server
- **Headless Telemetry Plugin** (optional) — a BepInEx plugin for FIKA headless clients that streams live raid data back to the command center

---

## Features

### Dashboard
Real-time server monitoring in a single view.

- **Server Stats** — uptime, player count, mod count, profile count, server version
- **Server Console** — live-streamed server log output with color coding and auto-scroll
- **Headless Console** — live log output from the headless client (auto-detected)
- **Activity Log** — audit trail of all admin actions with timestamps
- **Pop-out Console Window** — dedicated side-by-side view of server + headless logs
- **Send URL to Players** — broadcast a clickable URL to all players via in-game mail

### Raid Info (Live Telemetry)
Real-time raid monitoring powered by the headless telemetry plugin. Watch raids unfold from your browser.

- **Status Bar** — raid status, map, elapsed time, time remaining, in-game time of day
- **Performance Metrics** — FPS (current / avg / min-max range), frame time, RAM usage
- **Live Player Table** — all human players with health bars, level, ping, and live kill count
- **Bot Tracker** — scav, raider, rogue, and boss counts with alive/dead status
- **Kill Feed** — real-time kill events with:
  - Color-coded borders (gold for PMC kills, red for boss kills)
  - Weapon, body part, distance, and headshot indicators
  - Arrow separator between killer and victim
- **Combat Stats Panel** — aggregate combat analytics during and after the raid:
  - Total hits, headshots, headshot %, total damage dealt
  - Longest shot distance, average engagement distance
  - Body part hit distribution as a horizontal bar chart
- **Raid History** — searchable log of all completed raids with:
  - Map, date, duration, player count, kills, deaths
  - Expandable detail view with full player scoreboard (kills by type, damage, XP, outcome badges)
  - Boss status (spawned, alive/dead, killed by)
  - Kill timeline replay with color-coded entries
  - Combat stats with body part visualization

### Player Management
Full control over player profiles and progression.

- **Player Roster** — all registered profiles with level, side, session status
- **Give Items** — search the full item database, select quantity, send via in-game mail
- **Item Presets** — save commonly given item sets (e.g. "Starter Kit", "Ammo Resupply") for one-click distribution
- **XP & Level** — view and modify player experience points
- **Skills** — browse and edit individual skill levels per player
- **Quest Management** — browse all quests with search/filter, view objectives, set quest state (start, complete, fail)
- **Send Mail** — compose and send custom in-game messages to players

### Flea Market Control
Fine-tune the entire flea market economy without touching a config file.

- **Global Price Multipliers** — scale all buy/sell prices up or down
- **Category Multipliers** — independent multipliers for weapons, ammo, armor, medical, provisions, barter items, keys, containers, mods, and special equipment
- **Per-Item Overrides** — set exact prices for specific items
- **Tax Control** — adjust the flea market tax multiplier
- **Offer Settings** — max offers per player, offer duration, barter offer toggle and frequency
- **Market Regeneration** — force-regenerate all NPC offers with one click
- **Restock Interval** — control how often NPC offers refresh
- **Live Preview** — see price changes before applying (original vs modified values)

### Headless Client Manager
Built-in process lifecycle management for FIKA headless clients.

- **Auto-Start** — automatically launch the headless client when the server starts
- **Auto-Restart** — detect crashes and restart automatically
- **Manual Controls** — Start / Stop / Restart buttons in the dashboard
- **Status Monitoring** — process state, PID, uptime displayed in real time
- **Profile Selection** — configure which SPT profile the headless client uses
- **Startup Banner** — formatted server info box on startup showing:
  - Command Center URLs (local + LAN)
  - Server IP detection
  - Headless client status
  - Service status summary

### Access Control
Control who can connect to your server and who can access the admin panel.

- **Whitelist Mode** — only listed profile IDs can access
- **Blacklist Mode** — block specific profile IDs
- **Ban List** — permanent bans
- **Allow All When Empty** — whitelist mode with empty list allows everyone (useful default)
- **Session Validation** — all API requests authenticated via SPT session ID

### Activity Logging
Every admin action is tracked for accountability and debugging.

- **Timestamped Entries** — who did what and when
- **Action Types** — item gives, config changes, quest modifications, server events
- **Retention Controls** — configurable log retention period
- **Dashboard Integration** — recent activity visible on the main dashboard

---

## Installation

### Server Mod (Required)

1. Download the latest release from [Releases](https://github.com/ZSlayerHQ/ZSlayerCommandCenter/releases)
2. Extract the `ZSlayerCommandCenter` folder into your SPT `user/mods/` directory:
   ```
   SPT/
   └── user/
       └── mods/
           └── ZSlayerCommandCenter/
               ├── ZSlayerCommandCenter.dll
               ├── config/
               │   └── config.json
               └── res/
                   ├── commandcenter.html
                   └── banner.png
   ```
3. Start your SPT server
4. The startup banner will display your Command Center URLs:
   ```
   ╔════════════════════════════════════════════════════╗
   ║           ZSLAYER COMMAND CENTER v2.2.5            ║
   ║                                                    ║
   ║  Local:  https://127.0.0.1:6969/zslayer/cc/        ║
   ║  LAN:    https://192.168.x.x:6969/zslayer/cc/      ║
   ║                                                    ║
   ║  Headless: Ready (auto-start enabled)              ║
   ╚════════════════════════════════════════════════════╝
   ```
5. Open the URL in any modern browser

### Headless Telemetry Plugin (Optional)

Required only if you want live raid telemetry on the Raid Info tab.

1. Download `ZSlayerHeadlessTelemetry.dll` from the release
2. Place it in your headless client's BepInEx plugins folder:
   ```
   Headless/
   └── BepInEx/
       └── plugins/
           └── ZSlayerHeadlessTelemetry/
               └── ZSlayerHeadlessTelemetry.dll
   ```
3. The plugin activates automatically on headless clients only — it does nothing on regular game clients
4. It auto-discovers the server URL from SPT's backend config — no manual configuration needed

---

## Configuration

All settings are managed through the web UI. The config file at `config/config.json` is auto-managed and auto-upgrades when new fields are added. Manual editing is supported but not required.

<details>
<summary><strong>Config Sections</strong></summary>

| Section | Purpose |
|---------|---------|
| `access` | Whitelist / blacklist / ban list, access mode |
| `logging` | Enable/disable give event logging |
| `dashboard` | Refresh intervals, console buffer size, polling rate, log retention |
| `items` | Saved item presets for quick distribution |
| `flea` | Global/category/item price multipliers, tax, offers, regen, barter settings |
| `headless` | Auto-start, auto-restart, profile ID, EXE path, launch arguments |

</details>

<details>
<summary><strong>Config Auto-Upgrade</strong></summary>

When the mod updates and adds new config fields, they are automatically merged into your existing `config.json` on next server start. Your existing settings are preserved — only new fields with their defaults are added. No manual migration needed.

</details>

---

## Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                        BROWSER                               │
│           commandcenter.html (single-file UI)                │
│     Tabs: Dashboard | Raid Info | Items | Players | Flea     │
└──────────────────────┬───────────────────────────────────────┘
                       │ HTTP (GET/POST JSON)
                       │ Auth: X-Session-Id header
                       ▼
┌──────────────────────────────────────────────────────────────┐
│                    SPT SERVER                                 │
│                                                              │
│  CommandCenterHttpListener.cs ─── Routes all /zslayer/cc/*   │
│       │                                                      │
│       ├── ConfigService          (config load/save)          │
│       ├── TelemetryService       (live raid data + history)  │
│       ├── ServerStatsService     (server metrics)            │
│       ├── ConsoleBufferService   (server log capture)        │
│       ├── HeadlessLogService     (headless log tailing)      │
│       ├── HeadlessProcessService (process lifecycle)         │
│       ├── ItemSearchService      (item database search)      │
│       ├── ItemGiveService        (mail-based item delivery)  │
│       ├── PlayerManagementService(profile operations)        │
│       ├── FleaPriceService       (price multipliers)         │
│       ├── OfferRegenerationService(NPC offer regen)          │
│       ├── AccessControlService   (whitelist/blacklist)       │
│       └── ActivityLogService     (audit trail)               │
│                                                              │
│  Entry: CommandCenterMod.cs (PostSptModLoader + 1)           │
│  DI: [Injectable(InjectionType.Singleton)]                   │
└──────────────────────┬───────────────────────────────────────┘
                       │
          ┌────────────┴────────────┐
          │  (telemetry POST)       │
          ▼                         │
┌─────────────────────┐             │
│  HEADLESS CLIENT    │             │
│  (BepInEx Plugin)   │             │
│                     │             │
│  Plugin.cs          │             │
│  RaidEventHooks.cs  │◄── Fika Events (game created, raid start/end)
│  OnDamagePatch.cs   │◄── Harmony Patch (Player.ApplyDamageInfo)
│  DamageTracker.cs   │    Static hit/damage accumulator
│  TelemetryReporter.cs│   Async HTTP POST queue
└─────────────────────┘
```

### How Telemetry Works

1. The headless client runs the BepInEx telemetry plugin
2. When a FIKA raid starts, the plugin subscribes to game events and starts a 5-second reporting loop
3. Every 5 seconds it POSTs player status, raid state, and performance data to the server
4. Every 10 seconds it additionally POSTs bot counts and combat damage statistics
5. Kill events are sent immediately as they happen
6. At raid end, a comprehensive summary is posted with per-player stats
7. The server stores everything in memory — the frontend polls `GET /telemetry/current` to render live data
8. Completed raids are archived to a searchable history

---

## API Reference

All endpoints are prefixed with `/zslayer/cc/`. Authentication is via `X-Session-Id` header.

<details>
<summary><strong>Dashboard & Server</strong></summary>

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/auth` | Validate session, get profile name |
| GET | `/dashboard/stats` | Server stats (uptime, players, mods) |
| GET | `/dashboard/console` | Server console log buffer |
| GET | `/dashboard/headless-console` | Headless client log buffer |
| GET | `/dashboard/activity` | Activity log entries |
| POST | `/dashboard/send-url` | Broadcast URL to all players |
| POST | `/headless/start` | Start headless client process |
| POST | `/headless/stop` | Stop headless client process |
| POST | `/headless/restart` | Restart headless client process |
| GET | `/headless/status` | Headless process status |

</details>

<details>
<summary><strong>Telemetry (Raid Info)</strong></summary>

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/telemetry/current` | Full live state (raid, players, bots, perf, damage) |
| GET | `/telemetry/kills` | Kill feed for current raid |
| GET | `/telemetry/history` | Raid history list |
| GET | `/telemetry/history/{id}` | Detailed raid with player scoreboard |
| POST | `/telemetry/raid-state` | (plugin) Report raid status |
| POST | `/telemetry/players` | (plugin) Report player list |
| POST | `/telemetry/performance` | (plugin) Report FPS/memory |
| POST | `/telemetry/bots` | (plugin) Report bot counts |
| POST | `/telemetry/kill` | (plugin) Report kill event |
| POST | `/telemetry/extract` | (plugin) Report extraction/death |
| POST | `/telemetry/damage-stats` | (plugin) Report combat stats |
| POST | `/telemetry/raid-summary` | (plugin) End-of-raid summary |

</details>

<details>
<summary><strong>Items & Players</strong></summary>

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/items/search?q=...` | Search item database |
| POST | `/items/give` | Give items to a player |
| GET | `/items/presets` | List saved item presets |
| POST | `/items/presets` | Save an item preset |
| DELETE | `/items/presets/{name}` | Delete a preset |
| GET | `/players/list` | All registered profiles |
| GET | `/players/{id}/profile` | Player profile details |
| POST | `/players/{id}/skills` | Modify player skills |
| GET | `/quests/browse` | Browse all quests with filters |
| POST | `/quests/state` | Set quest state for a player |

</details>

<details>
<summary><strong>Flea Market</strong></summary>

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/flea/config` | Get flea market configuration |
| POST | `/flea/config` | Update flea config + apply |
| POST | `/flea/regenerate` | Force-regenerate NPC offers |
| POST | `/flea/reset` | Reset all flea settings to defaults |

</details>

<details>
<summary><strong>Access Control</strong></summary>

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/access/config` | Get access control config |
| POST | `/access/config` | Update access mode + lists |
| POST | `/access/ban` | Ban a profile |
| POST | `/access/unban` | Unban a profile |

</details>

---

## Development Roadmap

Development is organized into phases. Each phase is fully completed and tested before the next begins.

| Status | Phase | Feature | Description |
|:------:|:-----:|---------|-------------|
| :white_check_mark: | 1 | **Dashboard** | Server stats, console feed, activity log, send-URL |
| :white_check_mark: | 2 | **Player Management** | Items, XP, skills, quests, mail, presets |
| :white_check_mark: | 2.5 | **Headless Telemetry** | Live raid monitoring, kill feed, combat stats, raid history |
| :white_check_mark: | 3 | **Flea Market** | Price multipliers, tax, offers, regeneration, category controls |
| :hourglass: | 4 | **Trader Control** | Trader inventory, pricing, restock, loyalty, disabling |
| :hourglass: | 5 | **Quest Editor** | Modify quest objectives, rewards, prerequisites |
| :hourglass: | 6 | **Progression & Skills** | XP rates, skill leveling, hideout, insurance, stamina |
| :hourglass: | 7 | **Backup & Restore** | Database snapshots, restore points, wipe tools |
| :hourglass: | 8 | **Scheduler & Events** | Timed events, recurring actions, automation |
| :hourglass: | 9 | **Game Values Editor** | Ammo, armor, weapons, health, bots, loot, airdrops |
| :hourglass: | 10 | **Config Profiles** | Save/load/export/share config presets |
| :hourglass: | 11 | **Gear Presets** | Loadout templates, weapon builds, kit distribution |
| :hourglass: | 12 | **Bounty Board** | Player bounties, kill targets, challenges |
| :hourglass: | 13 | **Clan System** | Clans, shared storage, team stats |
| :hourglass: | 14 | **Client Plugin** | BepInEx client-side mod for in-game overlay |
| :hourglass: | 15 | **Stash Viewer** | Visual inventory browser with item images |
| :hourglass: | 16 | **Polish & Release** | Final cleanup, docs, and v3.0 launch |

---

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Server Mod | C# 12 / .NET 9.0 with SPTarkov dependency injection |
| Telemetry Plugin | C# / .NET Standard 2.1 with BepInEx 5.x + Harmony |
| Frontend | Single HTML file — inline CSS + JS, zero build tools |
| HTTP API | Custom `IHttpListener` routes under `/zslayer/cc/` |
| Serialization | `System.Text.Json` (server), `Newtonsoft.Json` (plugin) |
| Design | Tarkov-inspired dark theme with gold accent (`#c8aa6e`) |

---

## Requirements

| Requirement | Version |
|-------------|---------|
| **SPT** | 4.0.x |
| **FIKA** | Latest (optional — required for headless + telemetry) |
| **Browser** | Chrome, Firefox, Edge, or any Chromium-based browser |
| **.NET Runtime** | Bundled with SPT server — no separate install needed |

---

## FAQ

<details>
<summary><strong>Do players need to install anything?</strong></summary>

No. The command center is entirely server-side. Players don't need any client mods to be managed through it. The optional headless telemetry plugin only runs on the headless client, not on player game clients.

</details>

<details>
<summary><strong>Does it work without FIKA?</strong></summary>

Yes. The server mod works on vanilla SPT. The Dashboard, Items, Players, and Flea tabs all work without FIKA. Only the Headless Client Manager and Raid Info (live telemetry) features require FIKA, since they depend on a headless client.

</details>

<details>
<summary><strong>Will it conflict with other mods?</strong></summary>

Unlikely. The command center loads after other mods (`PostSptModLoader + 1`) and makes changes through the same SPT APIs that the game uses. It doesn't replace or override any core server files. Flea/trader/quest changes are applied on top of whatever other mods have done.

</details>

<details>
<summary><strong>Can I access it from my phone?</strong></summary>

Yes. The UI is responsive and works on mobile browsers. Use the LAN URL from any device on your network.

</details>

<details>
<summary><strong>Is it safe to update mid-wipe?</strong></summary>

Yes. The mod only modifies server-side values and configs. Player profiles are never modified unless you explicitly use the player management features (give items, set skills, etc.). Config auto-upgrade preserves your existing settings.

</details>

---

## Contributing

This is a solo project by [ZSlayerHQ](https://github.com/ZSlayerHQ), but feedback, bug reports, and feature requests are welcome.

- **Bug Reports** — open an issue on GitHub with steps to reproduce
- **Feature Requests** — open an issue or discuss on [Discord](https://discord.gg/ZSlayerHQ)
- **Pull Requests** — reach out on Discord first to discuss the approach

---

## License

[MIT](LICENSE) — Built by [ZSlayerHQ / Ben Cole](https://github.com/ZSlayerHQ)
