<div align="center">

# ZSlayer Command Center

**The ultimate browser-based admin toolkit for SPT 4.0 / FIKA**

[![License: MIT](https://img.shields.io/badge/License-MIT-c8aa6e.svg)](LICENSE)
[![Version](https://img.shields.io/badge/v2.5.1-c8aa6e.svg)](https://github.com/ZSlayerHQ/ZSlayerCommandCenter/releases)
[![SPT](https://img.shields.io/badge/SPT-4.0.x-c8aa6e.svg)]()
[![FIKA](https://img.shields.io/badge/FIKA-Compatible-4a7c59.svg)]()
[![.NET](https://img.shields.io/badge/.NET-9.0-512bd4.svg)]()

---

Manage players, monitor raids in real time, control the flea market, and run headless clients — all from a single browser tab. No restarts. No config hunting. A server-side mod for [SPT](https://www.sp-tarkov.com/) that serves a full web admin panel at `https://<your-server>:6969/zslayer/cc/`.

[Discord](https://discord.gg/ZSlayerHQ) | [YouTube](https://www.youtube.com/@ZSlayerHQ-ImBenCole) | [Releases](https://github.com/ZSlayerHQ/ZSlayerCommandCenter/releases)

</div>

---

## What Is This?

Designed for **FIKA server admins** who want real-time visibility and control without touching JSON files or restarting their server. Every feature works through the browser — give items, watch live raid telemetry, fine-tune the flea market economy, manage headless clients, and more.

**Three components:**

| Component | Repo | Required | Purpose |
|:----------|:-----|:--------:|:--------|
| **Server Mod** | This repo | Yes | C# mod running inside the SPT server — powers the admin panel |
| **Headless Telemetry Plugin** | [ZSlayerHeadlessTelemetry](https://github.com/ZSlayerHQ/ZSlayerHeadlessTelemetry) | No | BepInEx plugin for FIKA headless clients — streams live raid data |
| **Watchdog** | [ZSlayerWatchdog](https://github.com/ZSlayerHQ/ZSlayerWatchdog) | No | WPF desktop app — auto-starts and monitors server + headless processes |

---

## Features

<br />

<table>
<tr>
<td width="50%" valign="top">

### Dashboard

Real-time server monitoring in a single view.

- **Server Stats** — uptime, player count, mod list, profile count, memory usage
- **Server Console** — live-streamed log output with color coding and auto-scroll
- **Headless Console** — live headless client log output (auto-detected)
- **Activity Log** — audit trail of all admin actions with timestamps
- **Pop-out Console** — dedicated side-by-side view of server + headless logs
- **Send URL to Players** — broadcast clickable URLs to all players via in-game mail
- **Login Screen** — profile selector with live server status widget showing online players, raid activity, and per-player In Stash / In Raid status

</td>
<td width="50%" valign="top">

### Raid Info — Live Telemetry

Watch raids unfold in real time from your browser.

- **Status Bar** — raid state, map, elapsed/remaining time, in-game time of day
- **Performance Metrics** — FPS (current / avg / min-max), frame time, RAM, CPU
- **Live Player Table** — all human players with health bars, level, ping, kill count
- **Bot Tracker** — scav, raider, rogue, AI PMC, and boss counts (alive / dead)
- **Kill Feed** — real-time events with weapon, body part, distance, headshot badges
- **Combat Stats** — hits, headshots, damage, longest shot, body part distribution chart
- **Alerts** — boss spawns, kills, extracts with expandable history
- **Raid History** — searchable archive with full player scoreboards and kill timelines

</td>
</tr>
<tr>
<td width="50%" valign="top">

### Live Minimap

Real-time overhead map of the current raid.

- Color-coded entity dots (PMC, scav, boss, raider, follower, dead)
- Multi-layer support with level selector (underground, ground, upper)
- PMC rotation arrows showing facing direction
- Smooth pan and zoom with mouse drag / scroll
- Auto-follow mode tracking logged-in player position
- Map image overlays for all official Tarkov maps
- Adjustable refresh rate slider (50ms to 10s)
- Name labels, dead toggle, layer filtering
- **Pop-out window** — standalone map viewer

</td>
<td width="50%" valign="top">

### Player Management

Full control over player profiles and progression.

- **Player Roster** — all profiles with level, side, online status, wealth
- **Profile Stats** — 5 collapsible sections covering raid stats, combat, economy, survival, progression
- **Give Items** — search the full item database, select quantity, send via mail
- **Item Presets** — save commonly given item sets for one-click distribution
- **XP & Level** — view and modify player experience points
- **Skills** — browse and edit individual skill levels per player
- **Quest Management** — browse all quests, view objectives, set quest state
- **Send Mail** — compose and send custom in-game messages

</td>
</tr>
<tr>
<td width="50%" valign="top">

### Flea Market Control

Fine-tune the entire flea market economy from the browser.

- **Global Price Multiplier** — scale all buy/sell prices up or down
- **Category Multipliers** — weapons, ammo, armor, medical, provisions, barter, keys, containers, mods, special equipment
- **Per-Item Overrides** — set exact prices for specific items
- **Tax Control** — adjust flea market community tax and requirement tax
- **Offer Settings** — max offers per player, duration, barter toggle + frequency
- **Market Regeneration** — force-regenerate all NPC offers with one click
- **Restock Interval** — control how often NPC offers refresh
- **Live Preview** — see price changes before applying

</td>
<td width="50%" valign="top">

### Headless Client Manager

Built-in process lifecycle management for FIKA headless clients.

- **Auto-Start** — launch headless automatically when the server starts
- **Auto-Restart** — detect crashes and restart automatically
- **Manual Controls** — Start / Stop / Restart buttons in the dashboard
- **Status Monitoring** — process state, PID, uptime in real time
- **Profile Selection** — configure which SPT profile the headless client uses
- **Startup Banner** — formatted server info box with URLs, IP detection, service status

</td>
</tr>
<tr>
<td width="50%" valign="top">

### Access Control

Control who can connect to your server and admin panel.

- **Whitelist Mode** — only listed profile IDs can access
- **Blacklist Mode** — block specific profile IDs
- **Ban List** — permanent bans with reason tracking
- **Allow All When Empty** — empty whitelist allows everyone (useful default)
- **Session Validation** — all API requests authenticated via SPT session ID
- **Password Protection** — optional admin password for the web panel

</td>
<td width="50%" valign="top">

### Activity Logging

Every admin action is tracked for accountability.

- **Timestamped Entries** — who did what, when, with full context
- **Action Types** — item gives, config changes, quest mods, server events
- **Retention Controls** — configurable log retention period
- **Dashboard Integration** — recent activity visible on the main dashboard
- **Searchable** — filter and browse the full audit trail

</td>
</tr>
</table>

<br />

---

<br />

## Quick Start

### 1. Install the Server Mod

Download the latest release from [**Releases**](https://github.com/ZSlayerHQ/ZSlayerCommandCenter/releases) and extract into your SPT `user/mods/` directory:

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

Start your SPT server. The startup banner displays your Command Center URLs:

```
╔════════════════════════════════════════════════════╗
║           ZSLAYER COMMAND CENTER v2.4.0            ║
║                                                    ║
║  Local:  https://127.0.0.1:6969/zslayer/cc/        ║
║  LAN:    https://192.168.x.x:6969/zslayer/cc/      ║
║                                                    ║
║  Headless: Ready (auto-start enabled)              ║
╚════════════════════════════════════════════════════╝
```

Open the URL in any modern browser and select your profile to log in.

### 2. Install Headless Telemetry *(optional)*

> Only needed for live raid telemetry on the Raid Info tab.

Place the plugin DLL in your headless client's BepInEx plugins folder:

```
Headless/
└── BepInEx/
    └── plugins/
        └── ZSlayerHeadlessTelemetry/
            └── ZSlayerHeadlessTelemetry.dll
```

- Activates automatically on headless clients only — does nothing on regular game clients
- Auto-discovers the server URL from SPT's backend config — zero manual configuration

<br />

---

<br />

## Configuration

All settings are managed through the web UI. The config file at `config/config.json` is auto-managed and **auto-upgrades** when new fields are added — your existing settings are always preserved.

<details>
<summary><strong>Config Sections</strong></summary>

<br />

| Section | Purpose |
|:--------|:--------|
| `access` | Whitelist / blacklist / ban list, access mode, password |
| `logging` | Enable/disable give event logging |
| `dashboard` | Refresh intervals, console buffer size, polling rate, log retention |
| `items` | Saved item presets for quick distribution |
| `flea` | Global / category / item price multipliers, tax, offers, regen, barter settings |
| `headless` | Auto-start, auto-restart, profile ID, EXE path, launch arguments |

</details>

<br />

---

<br />

## Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                         BROWSER                              │
│            commandcenter.html (single-file UI)               │
│   Dashboard │ Raid Info │ Items │ Players │ Flea │ Access    │
└──────────────────────┬───────────────────────────────────────┘
                       │  HTTPS (GET/POST JSON)
                       │  Auth: X-Session-Id + X-CC-Password
                       ▼
┌──────────────────────────────────────────────────────────────┐
│                      SPT SERVER                              │
│                                                              │
│  CommandCenterHttpListener ──── Routes all /zslayer/cc/*     │
│       │                                                      │
│       ├── ConfigService            Config load / save        │
│       ├── TelemetryService         Live raid data + history  │
│       ├── ServerStatsService       Server metrics            │
│       ├── PlayerStatsService       Player stats + economy    │
│       ├── ConsoleBufferService     Server log capture        │
│       ├── HeadlessLogService       Headless log tailing      │
│       ├── HeadlessProcessService   Process lifecycle         │
│       ├── ItemSearchService        Item database search      │
│       ├── ItemGiveService          Mail-based item delivery  │
│       ├── PlayerManagementService  Profile operations        │
│       ├── PlayerMailService        In-game mail              │
│       ├── QuestBrowserService      Quest browsing            │
│       ├── FleaPriceService         Price multipliers         │
│       ├── OfferRegenerationService NPC offer regeneration    │
│       ├── RaidTrackingService      Raid data tracking        │
│       ├── AccessControlService     Whitelist / blacklist     │
│       └── ActivityLogService       Audit trail               │
│                                                              │
│  Entry: CommandCenterMod.cs (PostSptModLoader + 1)           │
│  DI: [Injectable(InjectionType.Singleton)]                   │
└──────────────────────┬───────────────────────────────────────┘
                       │
          ┌────────────┴────────────┐
          │  Telemetry POST (HTTPS) │
          ▼                         │
┌─────────────────────────┐         │
│   HEADLESS CLIENT       │         │
│   (BepInEx Plugin)      │         │
│                         │         │
│   Plugin.cs             │         │
│   RaidEventHooks.cs     │◄── Fika Events (raid lifecycle)
│   OnDamagePatch.cs      │◄── Harmony Patch (Player.ApplyDamageInfo)
│   DamageTracker.cs      │    Static hit / damage accumulator
│   TelemetryReporter.cs  │    Async HTTP POST queue
└─────────────────────────┘
```

<details>
<summary><strong>How Telemetry Works</strong></summary>

<br />

1. The headless client runs the BepInEx telemetry plugin
2. When a FIKA raid starts, the plugin subscribes to game events and begins a periodic reporting loop
3. Every **5 seconds** — POSTs player status, raid state, positions, and performance data
4. Every **10 seconds** — additionally POSTs bot counts and combat damage statistics
5. **Kill events** are sent immediately as they happen
6. At **raid end**, a comprehensive summary is posted with per-player stats
7. The server stores everything in memory — the frontend polls `GET /telemetry/current` to render live data
8. Completed raids are archived to a searchable history

</details>

<br />

---

<br />

## API Reference

All endpoints are prefixed with `/zslayer/cc/`. Authentication via `X-Session-Id` header (and `X-CC-Password` if password is set).

> Endpoints marked *(plugin)* are POST-only telemetry ingestion routes used by the headless plugin.

<details>
<summary><strong>Public (No Auth)</strong></summary>

<br />

| Method | Endpoint | Description |
|:-------|:---------|:------------|
| `GET` | `/profiles` | List profiles for login screen |
| `GET` | `/server-vitals` | Online count, active raid, online player list, uptime |

</details>

<details>
<summary><strong>Dashboard & Server</strong></summary>

<br />

| Method | Endpoint | Description |
|:-------|:---------|:------------|
| `GET` | `/auth` | Validate session, get profile name + admin flag |
| `GET` | `/dashboard/stats` | Server stats (uptime, players, mods, memory) |
| `GET` | `/dashboard/players` | Player roster with online status + economy |
| `GET` | `/dashboard/raids` | Server-wide raid statistics |
| `GET` | `/dashboard/console` | Server console log buffer |
| `GET` | `/dashboard/headless-console` | Headless client log buffer |
| `GET` | `/dashboard/activity` | Activity log entries |
| `POST` | `/dashboard/send-url` | Broadcast URL to all players via mail |

</details>

<details>
<summary><strong>Headless Client</strong></summary>

<br />

| Method | Endpoint | Description |
|:-------|:---------|:------------|
| `GET` | `/headless/status` | Headless process status + PID + uptime |
| `POST` | `/headless/start` | Start headless client process |
| `POST` | `/headless/stop` | Stop headless client process |
| `POST` | `/headless/restart` | Restart headless client process |

</details>

<details>
<summary><strong>Telemetry (Raid Info)</strong></summary>

<br />

| Method | Endpoint | Description |
|:-------|:---------|:------------|
| `GET` | `/telemetry/current` | Full live state (raid, players, bots, perf, damage, positions) |
| `GET` | `/telemetry/kills` | Kill feed for current raid |
| `GET` | `/telemetry/positions` | Entity positions for live map |
| `GET` | `/telemetry/alerts` | Raid event alerts (boss spawns, kills, etc.) |
| `GET` | `/telemetry/history` | Raid history list |
| `GET` | `/telemetry/history/{id}` | Detailed raid with player scoreboard |
| `POST` | `/telemetry/map-refresh-rate` | Set map position polling interval |
| `POST` | `/telemetry/raid-state` | *(plugin)* Report raid status |
| `POST` | `/telemetry/players` | *(plugin)* Report player list |
| `POST` | `/telemetry/performance` | *(plugin)* Report FPS / memory |
| `POST` | `/telemetry/bots` | *(plugin)* Report bot counts |
| `POST` | `/telemetry/kill` | *(plugin)* Report kill event |
| `POST` | `/telemetry/extract` | *(plugin)* Report extraction / death |
| `POST` | `/telemetry/damage-stats` | *(plugin)* Report combat stats |
| `POST` | `/telemetry/raid-summary` | *(plugin)* End-of-raid summary |

</details>

<details>
<summary><strong>Items & Players</strong></summary>

<br />

| Method | Endpoint | Description |
|:-------|:---------|:------------|
| `GET` | `/items/search?q=...` | Search item database by name / ID |
| `POST` | `/items/give` | Give items to a player via mail |
| `GET` | `/items/presets` | List saved item presets |
| `POST` | `/items/presets` | Save an item preset |
| `DELETE` | `/items/presets/{name}` | Delete a preset |
| `GET` | `/players/list` | All registered profiles with stats |
| `GET` | `/players/{id}/profile` | Full player profile + stats |
| `GET` | `/players/{id}/my-raids` | Per-player raid statistics |
| `POST` | `/players/{id}/skills` | Modify player skills |
| `POST` | `/players/{id}/mail` | Send custom in-game mail |
| `GET` | `/quests/browse` | Browse all quests with search / filter |
| `POST` | `/quests/state` | Set quest state for a player |

</details>

<details>
<summary><strong>Flea Market</strong></summary>

<br />

| Method | Endpoint | Description |
|:-------|:---------|:------------|
| `GET` | `/flea/config` | Get flea market configuration |
| `POST` | `/flea/config` | Update flea config + apply immediately |
| `POST` | `/flea/regenerate` | Force-regenerate all NPC offers |
| `POST` | `/flea/reset` | Reset all flea settings to defaults |

</details>

<details>
<summary><strong>Access Control</strong></summary>

<br />

| Method | Endpoint | Description |
|:-------|:---------|:------------|
| `GET` | `/access/config` | Get access control configuration |
| `POST` | `/access/config` | Update access mode + lists |
| `POST` | `/access/ban` | Ban a profile |
| `POST` | `/access/unban` | Unban a profile |

</details>

<br />

---

<br />

## Roadmap

Development is organized into phases. Each phase is fully completed and tested before the next begins.

| | Phase | Feature | Description |
|:-:|:-----:|:--------|:------------|
| **Done** | 1 | **Dashboard** | Server stats, console feed, activity log, send-URL, login vitals |
| **Done** | 2 | **Player Management** | Items, XP, skills, quests, mail, presets, full profile stats |
| **Done** | 2.5 | **Headless Telemetry** | Live raid monitoring, kill feed, combat stats, minimap, raid history |
| **Done** | 3 | **Flea Market** | Price multipliers, tax, offers, regeneration, category controls |
| | 4 | **Trader Control** | Trader inventory, pricing, restock, loyalty, disabling |
| | 5 | **Quest Editor** | Modify quest objectives, rewards, prerequisites |
| | 6 | **Progression & Skills** | XP rates, skill leveling, hideout, insurance, stamina |
| | 7 | **Backup & Restore** | Database snapshots, restore points, wipe tools |
| | 8 | **Scheduler & Events** | Timed events, recurring actions, automation |
| | 9 | **Game Values Editor** | Ammo, armor, weapons, health, bots, loot, airdrops |
| | 10 | **Config Profiles** | Save / load / export / share config presets |
| | 11 | **Gear Presets** | Loadout templates, weapon builds, kit distribution |
| | 12 | **Bounty Board** | Player bounties, kill targets, challenges |
| | 13 | **Clan System** | Clans, shared storage, team stats |
| | 14 | **Client Plugin** | BepInEx client-side mod for in-game overlay |
| | 15 | **Stash Viewer** | Visual inventory browser with item images |
| | 16 | **Polish & Release** | Final cleanup, docs, and v3.0 launch |

<br />

---

<br />

## Tech Stack

| Component | Technology |
|:----------|:-----------|
| **Server Mod** | C# 12 / .NET 9.0, SPTarkov DI (`[Injectable]`), custom `IHttpListener` |
| **Telemetry Plugin** | C# / .NET Standard 2.1, BepInEx 5.x, Harmony patches |
| **Frontend** | Single HTML file — inline CSS + JS, zero build tools, zero dependencies |
| **API** | RESTful JSON over HTTPS at `/zslayer/cc/` |
| **Serialization** | `System.Text.Json` (server) / `Newtonsoft.Json` (plugin) |
| **Design** | Tarkov-inspired dark theme, gold accent `#c8aa6e`, responsive layout |

<br />

---

<br />

## Requirements

| | Version |
|:--|:--------|
| **SPT** | 4.0.x |
| **FIKA** | Latest *(optional — required for headless + telemetry)* |
| **Browser** | Chrome, Firefox, Edge, or any Chromium-based browser |
| **.NET Runtime** | Bundled with SPT server — no separate install needed |

<br />

---

<br />

## FAQ

<details>
<summary><strong>Do players need to install anything?</strong></summary>

<br />

No. The command center is entirely server-side. Players don't need any client mods. The optional headless telemetry plugin only runs on the headless client, not on player game clients.

</details>

<details>
<summary><strong>Does it work without FIKA?</strong></summary>

<br />

Yes. Dashboard, Items, Players, and Flea tabs all work on vanilla SPT. Only the Headless Client Manager and Raid Info (live telemetry) require FIKA + a headless client.

</details>

<details>
<summary><strong>Will it conflict with other mods?</strong></summary>

<br />

Unlikely. The command center loads after other mods (`PostSptModLoader + 1`) and makes changes through the same SPT APIs the game uses. It doesn't replace any core server files. Changes are applied on top of whatever other mods have done.

</details>

<details>
<summary><strong>Can I access it from my phone?</strong></summary>

<br />

Yes. The UI is responsive and works on mobile browsers. Use the LAN URL from any device on your network.

</details>

<details>
<summary><strong>Is it safe to update mid-wipe?</strong></summary>

<br />

Yes. The mod only modifies server-side values and configs. Player profiles are never touched unless you explicitly use player management features. Config auto-upgrade preserves all existing settings.

</details>

<details>
<summary><strong>How do I report a bug or request a feature?</strong></summary>

<br />

Open an issue on [GitHub](https://github.com/ZSlayerHQ/ZSlayerCommandCenter/issues) or join the [Discord](https://discord.gg/ZSlayerHQ). For bug reports, include your SPT version, mod version, and steps to reproduce.

</details>

<br />

---

<br />

## Contributing

This is a solo project by [**ZSlayerHQ**](https://github.com/ZSlayerHQ), but feedback and bug reports are always welcome.

- **Bug Reports** — [open an issue](https://github.com/ZSlayerHQ/ZSlayerCommandCenter/issues) with steps to reproduce
- **Feature Requests** — open an issue or discuss on [Discord](https://discord.gg/ZSlayerHQ)
- **Pull Requests** — reach out on Discord first to discuss the approach

<br />

---

<div align="center">

<br />

**[MIT License](LICENSE)** &nbsp;&mdash;&nbsp; Built by [ZSlayerHQ / Ben Cole](https://github.com/ZSlayerHQ)

<br />

</div>
