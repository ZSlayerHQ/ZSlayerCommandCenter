<div align="center">

# ZSlayer Command Center

**The ultimate browser-based admin toolkit for SPT 4.0 / FIKA**

[![License: MIT](https://img.shields.io/badge/License-MIT-c8aa6e.svg)](LICENSE)
[![Version](https://img.shields.io/badge/v2.9.3-c8aa6e.svg)](https://github.com/ZSlayerHQ/ZSlayerCommandCenter/releases)
[![Phases](https://img.shields.io/badge/Phases_Complete-8%2F16-c8aa6e.svg)]()
[![SPT](https://img.shields.io/badge/SPT-4.0.x-c8aa6e.svg)]()
[![FIKA](https://img.shields.io/badge/FIKA-Compatible-4a7c59.svg)]()
[![.NET](https://img.shields.io/badge/.NET-9.0-512bd4.svg)]()

---

A complete server administration platform for [SPT](https://www.sp-tarkov.com/) that gives you total control over your server from a single browser tab. Manage players, watch raids unfold in real time on a live minimap, reshape the entire economy, customize every trader's inventory down to individual items, edit quests, control XP and progression rates, schedule automated events, back up and restore profiles, and orchestrate headless clients — all without touching a config file or restarting your server.

**One mod. One URL. Full control.**

`https://<your-server>:6969/zslayer/cc/`

[Discord](https://discord.gg/ZSlayerHQ) | [YouTube](https://www.youtube.com/@ZSlayerHQ-ImBenCole) | [Releases](https://github.com/ZSlayerHQ/ZSlayerCommandCenter/releases)

</div>

---

## Why Command Center?

Running an SPT / FIKA server means juggling dozens of JSON config files, restarting the server for every tweak, and having zero visibility into what's happening during raids. Command Center replaces all of that with a polished, real-time web interface that works from any device on your network.

- **Zero config files** — every setting is adjustable through the UI
- **Zero restarts** — changes apply instantly to the live server
- **Zero client mods** — entirely server-side, players install nothing
- **Zero dependencies** — single-file frontend, no npm, no build tools, no frameworks
- **Auto-upgrade** — config files automatically gain new fields on update, preserving your settings

**Three components work together:**

| Component | Repo | Required | Purpose |
|:----------|:-----|:--------:|:--------|
| **Server Mod** | This repo | Yes | C# mod running inside the SPT server — powers the entire admin panel |
| **Headless Telemetry Plugin** | [ZSlayerHeadlessTelemetry](https://github.com/ZSlayerHQ/ZSlayerHeadlessTelemetry) | No | BepInEx plugin for FIKA headless clients — streams live raid data, positions, kills |
| **Watchdog** | [ZSlayerWatchdog](https://github.com/ZSlayerHQ/ZSlayerWatchdog) | No | WPF desktop app — auto-starts and monitors server + headless processes with crash recovery |

---

## Feature Overview

| Feature | Highlights |
|:--------|:-----------|
| **Dashboard** | Real-time server stats, dual console feeds, player roster with online/in-raid tracking, activity audit log |
| **Live Raid Telemetry** | Watch raids in real time — player health, bot counts, kill feed, combat stats, performance metrics |
| **Live Minimap** | Overhead map with color-coded entities, rotation arrows, multi-layer support, auto-follow, pop-out window |
| **Player Management** | Full profile control — give items, set XP/skills/quests, send mail, browse 50+ stats across 5 categories |
| **Quest Editor** | Global and per-quest overrides for objectives, rewards, FIR requirements, and level gating. Visual quest tree explorer |
| **Progression & Skills** | XP multipliers, skill speed, loot rates, insurance, stamina, health regen — all adjustable in real time |
| **Flea Market Control** | Global/category/per-item price multipliers, tax control, offer settings, NPC regeneration, live preview |
| **Trader Control** | Auto-discovers all traders (vanilla + modded), per-trader buy/sell/stock multipliers, restock timers, loyalty shifts, currency override, disabled items, per-item price overrides, add custom items to any trader, custom avatars and display names, saveable presets |
| **Scheduler & Events** | Cron-based scheduling for Double XP, Trader Sales, Loot Boost events with multiplicative stacking. Automated tasks for broadcasts, backups, and restarts |
| **Backup & Restore** | Full profile backup system with timestamped snapshots, selective restore, and server wipe tools |
| **Admin Panel** | Centralized server/headless management, watchdog monitoring, FIKA settings, security controls, system metrics |
| **Headless Client Manager** | Auto-start, auto-restart on crash, Start/Stop/Restart from the browser, status + uptime monitoring |
| **Access Control** | Whitelist, blacklist, and ban system with password protection and session-based authentication |
| **Raid History** | Searchable archive of completed raids with full player scoreboards, kill timelines, and combat breakdowns |
| **Profile Customization** | Icon picker with persistent avatars, profile cards on the login screen with faction/level/raid stats |

---

## Detailed Features

### Dashboard

The central hub for monitoring your server at a glance.

- **Server Statistics** — live-updating display of uptime, connected players, registered profiles, loaded mods, memory usage, and server version
- **Server Console Feed** — real-time streamed log output from the SPT server with ANSI color coding, auto-scroll, and manual scroll lock. Filter by log level to cut through the noise
- **Headless Console Feed** — dedicated live log viewer for your FIKA headless client, auto-detected from the game's log directory with 10-second directory cache for performance. Color-coded by severity
- **Pop-out Console** — launch a dedicated side-by-side window showing server and headless logs simultaneously for monitoring during raids
- **Activity Log** — a complete audit trail of every admin action. Every item give, config change, quest modification, and server event is timestamped and attributed to the admin who performed it. Configurable retention period and searchable history
- **Send URL to Players** — broadcast a clickable URL to every player's in-game mailbox. Useful for sharing Discord invites, patch notes, or server rules
- **Player Roster** — dual-view player list (simple cards or detailed table) showing avatars, faction pills, hover-expandable stats, online/offline/in-raid status, and current map. Sorted with in-raid players first for instant visibility during active raids

### Login Screen

The first thing admins see — a polished profile selector that doubles as a server status dashboard.

- **Profile Cards** — 2-row cards showing player name, faction icon, level, total raids, survival rate, and online status. Sorted by level descending so the most active players appear first
- **Live Status Bar** — compact inline bar at the top showing server status (online/offline), headless status (running/stopped), connected player count, and active raid info (map + time remaining)
- **Profile Avatars** — persistent icon system with a picker offering BEAR, USEC, Scav, boss, and custom icons. Avatars are stored server-side and display everywhere — login screen, dashboard roster, and player management

---

### Raid Info — Live Telemetry

Watch raids unfold in real time without ever being in the game. Powered by the companion [Headless Telemetry Plugin](https://github.com/ZSlayerHQ/ZSlayerHeadlessTelemetry) running on your FIKA headless client.

- **Raid Status Bar** — current raid state (waiting/in-progress/transitioning), map name, elapsed and remaining time, in-game time of day
- **Performance Metrics** — live FPS readout (current, average, min-max range), frame time in milliseconds, RAM usage, and CPU load from the headless client
- **Live Player Table** — every human player in the raid with real-time health bars, level, ping, kill count, extraction status, and damage dealt/received
- **Bot Tracker** — categorized bot counts (Scav, Raider, Rogue, AI PMC, Bosses) showing alive vs dead, with named boss tracking
- **Kill Feed** — instant real-time kill notifications showing killer, victim, weapon, body part, distance in meters, and headshot/boss-kill badges. Scrollable history for the entire raid
- **Combat Statistics** — aggregate stats including total hits, headshot percentage, damage dealt, damage received, longest shot distance, and an interactive body part distribution chart
- **Alert System** — boss spawn notifications, high-value kill alerts, and player extraction events with expandable history panel
- **Raid History Archive** — every completed raid is saved to a searchable archive. Each entry includes full player scoreboards with K/D/A, damage, loot value, and a complete kill timeline. Filter by map, date, or player name

### Live Minimap

A real-time overhead tactical map rendered directly in your browser.

- **Color-coded entities** — PMCs (blue), Scavs (yellow), Bosses (red), Raiders (orange), Boss followers (dark red), and dead entities (grey). Instantly understand the battlefield
- **Multi-layer support** — level selector for maps with underground, ground, and upper floors. Each layer renders independently
- **PMC rotation arrows** — directional indicators showing which way each PMC is facing, updated in real time
- **Smooth navigation** — pan with mouse drag, zoom with scroll wheel, pixel-perfect scaling at any zoom level
- **Auto-follow mode** — lock the camera to the logged-in player's position and smoothly track their movement
- **Map overlays** — high-resolution map images for all official Tarkov maps, properly scaled and aligned to game coordinates
- **Adjustable refresh rate** — slider from 50ms (20 FPS) to 10 seconds, letting you balance smoothness against network bandwidth
- **Entity filtering** — toggle name labels, dead entities, and specific entity types on/off
- **Pop-out window** — launch the minimap in its own standalone browser window for dedicated monitoring on a second screen

---

### Player Management

Complete control over every player profile on your server.

- **Player Roster** — browse all registered profiles with level, faction, online status, stash value, and last seen time
- **Full Profile Stats** — 5 collapsible sections covering 50+ statistics:
  - **Raid Stats** — total raids, survival rate, run-throughs, MIA/KIA counts, average raid time
  - **Combat** — PMC kills, boss kills, headshot ratio, longest shot, favorite weapon, body part accuracy
  - **Economy** — stash value, total money earned/spent, flea market transactions, insurance claims
  - **Survival** — total healing, damage taken, times bled out, limbs lost, food/water consumed
  - **Progression** — level, experience, skills summary, quest completion percentage, hideout level
- **Give Items** — search the complete SPT item database (10,000+ items) with instant search, select quantity, and deliver via in-game mail. Items arrive in the player's mailbox immediately
- **Item Presets** — save commonly distributed item sets (starter kits, event rewards, compensation packages) for one-click distribution to any player
- **Gear Presets** — browse and distribute saved weapon builds and equipment loadouts
- **XP & Level Control** — view and directly modify a player's experience points and level
- **Skill Editor** — browse every skill in the game and set individual skill levels per player
- **Quest Management** — browse all quests with search and filter, view detailed objectives and rewards, and set quest completion state (locked/available/started/completed/failed) for any player
- **Send Mail** — compose and send custom in-game messages to any player with configurable sender name

---

### Flea Market Control

Reshape the entire flea market economy from your browser. Every change applies instantly — no server restart required.

- **Global Price Multiplier** — scale all flea market buy and sell prices with a single slider. Useful for economy-wide inflation or deflation events
- **Category Multipliers** — independent multipliers for 10 item categories: Weapons, Ammo, Armor, Medical, Provisions, Barter Goods, Keys, Containers, Weapon Mods, and Special Equipment. Fine-tune each market segment independently
- **Per-Item Overrides** — set exact price multipliers for specific items. Pin a rare item's price high or make quest items affordable. Overrides take priority over category and global multipliers
- **Tax Control** — adjust the Community Item Tax and Requirement Tax percentages that apply to all flea market listings. Changes take effect on next client login
- **Offer Settings** — control max offers per player, offer duration, barter trade toggle, and how frequently the market regenerates
- **Market Regeneration** — force-regenerate all NPC flea market offers with one click. Useful after major price changes to immediately populate the market with correctly-priced items
- **Restock Interval** — set how often NPC offers refresh, from rapid cycling to slow rotation
- **Live Preview** — see exactly how price changes will affect specific items before committing
- **Snapshot-and-Restore** — all modifications use a snapshot pattern. Original values are preserved, and every apply cycle restores from the snapshot before applying fresh. No compounding errors, ever

---

### Trader Control

The most powerful trader management system available for SPT. Auto-discovers every trader on your server — vanilla and modded — and gives you granular control over every aspect of their inventory, pricing, and behavior.

#### Auto-Discovery & Modded Trader Support
- **Automatic trader detection** — scans the SPT database on startup and discovers all registered traders, including those added by other mods. No manual configuration needed
- **Locale-aware naming** — resolves trader names through multiple fallback paths (base nickname → locale database → raw ID) to correctly name modded traders that use non-standard naming
- **Rich detail panel** — click any trader to open a comprehensive detail view showing avatar, full name, description, location, currency, loyalty level count, item count, restock timing, and all active multipliers

#### Pricing Control
- **Global buy/sell multipliers** — scale all trader buy and sell prices server-wide with slider controls
- **Per-trader buy/sell multipliers** — override the global multiplier for individual traders. Override replaces (not multiplies) the global value, preventing confusion
- **Per-item price overrides** — set exact buy/sell multipliers for specific items on specific traders. The override chain is Global → Trader → Item, with each level replacing the one above it
- **Min/max price clamping** — configurable floor and ceiling for all trader prices in roubles
- **Currency override** — force all traders (or individual traders) to transact in Roubles, Dollars, or Euros

#### Stock & Inventory
- **Global stock multiplier** — scale all trader stock counts up or down
- **Per-trader stock multiplier** — override stock scaling for individual traders
- **Global stock cap** — hard limit on maximum stock for any single item
- **Disabled items** — remove specific items from a trader's inventory entirely. Disabled items show as greyed out in the item browser and can be re-enabled at any time
- **Add items to traders** — inject completely new items into any trader's assortment. Set exact price, currency, loyalty level, stock count, and unlimited stock toggle. Added items use exact pricing that is never affected by multipliers. Add the same item at multiple loyalty levels or price points. Items persist across apply cycles and are included in preset save/load

#### Restock & Timing
- **Global restock timer** — set min/max restock intervals for all traders (displayed and input in minutes, stored in seconds)
- **Per-trader restock timers** — override restock timing for individual traders

#### Loyalty & Progression
- **Global loyalty level shift** — shift all items down by 1-3 loyalty levels server-wide (e.g., make all LL4 items available at LL1)
- **Per-trader loyalty shift** — apply loyalty shifts to individual traders independently

#### Display Customization
- **Custom trader names** — rename any trader's display name in the game client
- **Custom descriptions** — set a custom description paragraph for any trader
- **Custom avatars** — upload custom avatar images for any trader. Images are served through SPT's asset pipeline and display in-game
- **Avatar management** — preview, upload, and remove custom avatars through the detail panel

#### Item Browser
- **Searchable item table** — browse every item in a trader's inventory with instant search, loyalty level filtering, and pagination
- **Live data** — see current prices alongside original prices, with color coding (green = cheaper, red = more expensive)
- **Status badges** — items show "ADDED" badge (green) for admin-injected items, star icon for items with per-item overrides, and dimmed opacity for disabled items
- **Inline actions** — add per-item overrides or disable items directly from the item browser

#### Presets
- **Save presets** — snapshot the entire trader configuration (all globals, all per-trader overrides, all added items, all item overrides) into a named preset file
- **Load presets** — restore a saved configuration with one click. Display customizations (names, avatars) are preserved when loading presets
- **Import/Export** — download preset files as JSON for sharing, or upload preset files from other servers
- **Delete presets** — manage your preset library from the UI

---

### Headless Client Manager

Built-in process lifecycle management for FIKA headless clients, directly integrated into the dashboard.

- **Auto-Start** — automatically launch the headless client when the SPT server starts. Configurable delay to ensure the server is fully loaded
- **Auto-Restart on Crash** — detect when the headless process exits unexpectedly and automatically restart it. Keeps your server populated with AI players 24/7
- **Manual Controls** — Start, Stop, and Restart buttons in the dashboard with instant feedback
- **Status Monitoring** — real-time display of process state (running/stopped/starting), PID, and uptime duration
- **Profile Selection** — configure which SPT profile the headless client uses through the config
- **Path Discovery** — automatically locates the EFT executable and SPT server URL relative to the mod installation path
- **Startup Banner** — formatted info box printed to the server console on startup showing Command Center URL (local + LAN), headless status, and all enabled services

---

### Access Control

Multi-layered security for your server and admin panel.

- **Whitelist Mode** — only profiles on the whitelist can access the server. Empty whitelist allows everyone (sensible default)
- **Blacklist Mode** — block specific profiles while allowing everyone else
- **Ban System** — permanent bans with reason tracking and timestamped entries
- **Password Protection** — optional admin password required to access the Command Center web panel
- **Session Authentication** — every API request is authenticated via SPT session ID headers. Invalid or expired sessions are rejected
- **Config Persistence** — access lists survive server restarts and are stored in the auto-managed config file

---

### Activity Logging

Every admin action is tracked for accountability and auditing.

- **Timestamped Entries** — precise timestamps on every action, attributed to the admin's session ID and profile
- **Comprehensive Coverage** — item gives, config changes, quest modifications, player management actions, trader changes, flea market adjustments, access control modifications, and server events
- **Dashboard Integration** — recent activity displayed directly on the main dashboard for quick review
- **Configurable Retention** — set how long activity logs are kept before automatic cleanup
- **Searchable History** — filter and browse the complete audit trail

---

### Quest Editor

Full control over every quest in the game — objectives, rewards, prerequisites, and more.

- **Global Multipliers** — scale all quest objective counts and reward amounts (XP, money, items) with a single multiplier. Make quests easier or harder server-wide
- **FIR Removal** — globally remove Found-in-Raid requirements from all quest objectives. One toggle to end FIR frustration
- **Level Shift** — shift all quest level requirements down by a configurable amount. Make endgame quests accessible earlier
- **Per-Quest Overrides** — set exact multipliers for individual quest objectives and rewards. Pin a specific quest's difficulty without affecting others
- **Quest Browser** — searchable list of all quests with trader, level, status filtering. Paginated for large modded quest pools
- **Quest Detail Panel** — expand any quest to see all objectives (with current multiplied values), all rewards, and quest chain dependencies
- **Quest Tree Explorer** — visual prerequisite tree showing quest chains and unlock paths
- **Trader & Location Filters** — filter quests by issuing trader or required map
- **Per-Player Quest State** — set any quest to locked/available/started/completed/failed for individual players from the player management panel
- **Snapshot-and-Restore** — all modifications use the same snapshot pattern as other features. Original quest data is always recoverable

---

### Progression & Skills

Control XP rates, skill leveling, loot generation, insurance, and survival mechanics — all adjustable in real time without server restart.

- **XP Multiplier** — global experience multiplier applied to all XP gains (kill, loot, quest, explore). Works multiplicatively with active Double XP events
- **Skill Speed Multiplier** — scale how fast all skills level up. Stacks with event XP factors
- **Loot Multipliers** — independent multipliers for loose loot and container loot spawn rates. Stacks with Loot Boost events
- **Insurance Settings** — control insurance return chance, min/max return time, and insurance cost multiplier
- **Stamina & Health** — adjust stamina drain/recovery rates, out-of-raid health regeneration speed, and energy/hydration drain
- **Preset Buttons** — one-click presets for common configurations (Hardcore, Relaxed, Default, etc.)
- **Real-Time Apply** — all changes take effect immediately on the live server, no restart required

---

### Scheduler & Events

Automate server events and recurring tasks with full 5-field cron scheduling.

#### Events
- **Double XP** — multiply all experience and skill gains for a set duration. Configurable multiplier (default 2x)
- **Trader Sale** — reduce all trader buy prices for a set duration. Configurable discount multiplier
- **Loot Boost** — increase loose and container loot spawn rates for a set duration. Configurable multiplier
- **Multiplicative Stacking** — multiple concurrent events multiply together (e.g., 2x XP + 3x XP = 6x XP). No override conflicts
- **Schedule Modes** — activate now, schedule for a specific date/time, or set a recurring cron schedule
- **Broadcast Notifications** — optional in-game mail notifications to all players when events start and end, with customizable messages
- **Active Event Banner** — live countdown timers showing all active events at the top of the Events tab

#### Scheduled Tasks
- **Broadcast** — send automated messages to all players on a schedule
- **Backup** — trigger profile backups at regular intervals
- **Server Restart** — schedule server restarts (daily maintenance windows, etc.)
- **Headless Restart** — schedule headless client restarts independently
- **Enable/Disable** — toggle individual tasks on or off without deleting them

#### Cron Scheduling
- **Full 5-field syntax** — `minute hour day-of-month month day-of-week`
- **All standard features** — wildcards (`*`), ranges (`1-5`), steps (`*/15`), lists (`1,3,5`), day names (`SUN-SAT`), month names (`JAN-DEC`)
- **Preset buttons** — quick-select common schedules (every hour, daily at midnight, weekends only, etc.)
- **Validation** — real-time cron expression validation with human-readable description and next-run preview
- **Restart-safe** — scheduler state persists to disk. Active events resume after server restart; missed tasks are caught up

---

### Backup & Restore

Profile backup system with timestamped snapshots and server wipe tools, accessible from the Admin tab.

- **Manual Backups** — create a named backup of all player profiles with one click
- **Scheduled Backups** — automate backups via the Scheduler tab's cron-based task system
- **Selective Restore** — browse backup history and restore individual profiles or the entire server
- **Server Wipe** — full server wipe tool with confirmation safeguards
- **Backup Management** — view, download, and delete backup files from the UI

---

### Admin Panel

Centralized administration hub for server infrastructure, process management, and system settings.

- **System Metrics** — at-a-glance cards showing active instances, connected players, system health score, and server uptime with mini gauges
- **Process Management** — two-column layout with SPT Server controls (start/stop/restart, connected watchdogs, security settings) and Headless Client controls (start/stop/restart, headless settings, FIKA settings)
- **FIKA Settings** — toggle headless-only raids (block players from self-hosting), manage client plugin blacklist by GUID
- **Watchdog Integration** — real-time watchdog connection status, WebSocket-based communication for remote process control
- **Security Settings** — security token management for watchdog authentication (show/copy/regenerate)
- **Global Search** — filter activity log entries and console output by keyword
- **Console Output** — server and headless consoles relocated to the admin panel for focused monitoring

---

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
                ├── banner.svg
                └── Profile Icons/
                    └── *.png
```

Start your SPT server. The startup banner displays your Command Center URLs:

```
╔════════════════════════════════════════════════════╗
║           ZSLAYER COMMAND CENTER v2.9.3            ║
║                                                    ║
║  Local:  https://127.0.0.1:6969/zslayer/cc/        ║
║  LAN:    https://192.168.x.x:6969/zslayer/cc/      ║
║                                                    ║
║  Headless: Ready (auto-start enabled)              ║
╚════════════════════════════════════════════════════╝
```

Open the URL in any modern browser and select your profile to log in.

### 2. Install Headless Telemetry *(optional)*

> Only needed for live raid telemetry, live minimap, and the Raid Info tab.

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
- Sends raid state, player positions, bot counts, kill events, damage stats, and performance metrics
- Each data category reports independently on its own interval with full error isolation

### 3. Install Watchdog *(optional)*

> Desktop app for automated server + headless process management.

Download the Watchdog from [ZSlayerWatchdog](https://github.com/ZSlayerHQ/ZSlayerWatchdog) and configure it to point at your SPT server and headless client executables.

---

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
| `traders` | Global/per-trader multipliers, overrides, added items, display customizations, restock, loyalty |
| `quests` | Global/per-quest objective & reward multipliers, FIR removal, level shift |
| `progression` | XP multiplier, skill speed, loot rates, insurance, stamina, health regen |
| `scheduler` | Persisted events and scheduled tasks (separate file: `config/scheduler/scheduler-state.json`) |

</details>

---

## Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                         BROWSER                              │
│            commandcenter.html (single-file UI)               │
│  Dashboard │ Raid │ Items │ Players │ Quests │ Progression │
│        │ Flea │ Traders │ Events │ Admin                  │
└──────────────────────┬───────────────────────────────────────┘
                       │  HTTPS (GET/POST/DELETE JSON)
                       │  Auth: X-Session-Id + X-Password
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
│       ├── ItemSearchService        Item database search      │
│       ├── ItemGiveService          Mail-based item delivery  │
│       ├── PlayerManagementService  Profile operations        │
│       ├── PlayerMailService        In-game mail              │
│       ├── PlayerBuildService       Gear presets / builds     │
│       ├── FleaPriceService         Price multipliers         │
│       ├── OfferRegenerationService NPC offer regeneration    │
│       ├── TraderDiscoveryService   Trader auto-discovery     │
│       ├── TraderPriceService       Buy/sell multipliers      │
│       ├── TraderStockService       Stock, restock, loyalty   │
│       ├── TraderApplyService       Orchestrator + presets    │
│       ├── QuestDiscoveryService    Quest auto-discovery      │
│       ├── QuestOverrideService     Quest multipliers + FIR   │
│       ├── QuestLocaleService       Quest locale updates      │
│       ├── ProgressionControlService XP, loot, insurance      │
│       ├── SkillEditorService       Per-player skill editing  │
│       ├── EventService             Scheduler + event engine  │
│       ├── CronParser               5-field cron parsing      │
│       ├── ProfileBackupService     Profile backup/restore    │
│       ├── WipeService              Server wipe tools         │
│       ├── FikaConfigService        FIKA server config        │
│       ├── WatchdogManager          Watchdog WebSocket mgmt   │
│       ├── RaidTrackingService      Raid data tracking        │
│       ├── AccessControlService     Whitelist / blacklist     │
│       ├── ProfileActivityService   Login/online tracking     │
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

Each data category has independent error handling — a failure in one report type never blocks the others. Per-entity safety wrapping means a single corrupt player or bot entry can't crash the reporting loop.

</details>

<details>
<summary><strong>How Snapshot-and-Restore Works</strong></summary>

<br />

Every feature that modifies SPT database values follows a strict pattern to prevent compounding errors:

1. **Snapshot** — on first load, the original value of every modified field is deep-copied into an in-memory snapshot
2. **Restore** — on every apply cycle, ALL values are restored from the snapshot first
3. **Apply** — modifications are calculated and applied fresh against the original values

This means you can freely adjust multipliers, add items, change settings, and reset — the original game data is always preserved and recoverable. Critical implementation detail: SPT caches internal references to collection objects, so all modifications are done **in-place** on existing collections, never replacing them wholesale.

</details>

---

## API Reference

All endpoints are prefixed with `/zslayer/cc/`. Authentication via `X-Session-Id` header (and `X-Password` if password is set).

> Endpoints marked *(plugin)* are POST-only telemetry ingestion routes used by the headless plugin.

<details>
<summary><strong>Public (No Auth)</strong></summary>

<br />

| Method | Endpoint | Description |
|:-------|:---------|:------------|
| `GET` | `/profiles` | List profiles for login screen (name, level, faction, raid stats, online status) |
| `GET` | `/server-vitals` | Online count, active raid info, headless status, uptime |

</details>

<details>
<summary><strong>Dashboard & Server</strong></summary>

<br />

| Method | Endpoint | Description |
|:-------|:---------|:------------|
| `GET` | `/auth` | Validate session, get profile name + admin flag |
| `GET` | `/dashboard/stats` | Server stats (uptime, players, mods, memory) |
| `GET` | `/dashboard/players` | Enriched player roster (avatars, raid stats, stash value, in-raid status, last map) |
| `GET` | `/console` | Server console log buffer |
| `GET` | `/headless-console` | Headless client log buffer |
| `GET` | `/activity` | Activity log entries |
| `POST` | `/send-url` | Broadcast URL to all players via mail |

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
| `GET` | `/telemetry/history/{id}` | Detailed raid with player scoreboard + kill timeline |
| `POST` | `/telemetry/map-refresh-rate` | Set map position polling interval |
| `POST` | `/telemetry/raid-state` | *(plugin)* Report raid status |
| `POST` | `/telemetry/players` | *(plugin)* Report player list + positions |
| `POST` | `/telemetry/performance` | *(plugin)* Report FPS / memory / CPU |
| `POST` | `/telemetry/bots` | *(plugin)* Report bot counts by category |
| `POST` | `/telemetry/kill` | *(plugin)* Report kill event |
| `POST` | `/telemetry/extract` | *(plugin)* Report extraction / death |
| `POST` | `/telemetry/damage-stats` | *(plugin)* Report combat statistics |
| `POST` | `/telemetry/raid-summary` | *(plugin)* End-of-raid summary with per-player stats |

</details>

<details>
<summary><strong>Items & Players</strong></summary>

<br />

| Method | Endpoint | Description |
|:-------|:---------|:------------|
| `GET` | `/items?search=...` | Search item database by name / template ID |
| `GET` | `/categories` | Item category listing |
| `POST` | `/give` | Give items to a player via in-game mail |
| `GET` | `/presets` | List saved item presets |
| `POST` | `/preset` | Give items from a saved preset |
| `GET` | `/player-builds` | List saved gear presets / weapon builds |
| `GET` | `/player/{id}` | Full player profile with stats |
| `POST` | `/player/{id}/set-xp` | Modify player XP |
| `POST` | `/player/{id}/set-skill` | Modify individual skill level |
| `POST` | `/player/{id}/set-quest` | Set quest completion state |
| `POST` | `/player/{id}/send-mail` | Send custom in-game message |
| `POST` | `/player/{id}/set-trader-loyalty` | Set trader loyalty level |

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
| `GET` | `/flea/items/search?q=...` | Search items for flea per-item override picker |

</details>

<details>
<summary><strong>Traders</strong></summary>

<br />

| Method | Endpoint | Description |
|:-------|:---------|:------------|
| `GET` | `/traders/config` | Get full trader configuration |
| `POST` | `/traders/config` | Replace full trader config + apply |
| `POST` | `/traders/config/global` | Update global multipliers + apply |
| `POST` | `/traders/config/trader` | Update per-trader overrides + apply |
| `POST` | `/traders/config/trader/item` | Set per-item price override |
| `DELETE` | `/traders/config/trader/item/{traderId}/{templateId}` | Remove per-item override |
| `POST` | `/traders/config/trader/add-item` | Add a new item to a trader's inventory |
| `DELETE` | `/traders/config/trader/added-item/{traderId}/{index}` | Remove an added item |
| `GET` | `/traders/list` | List all discovered traders with metadata |
| `GET` | `/traders/status` | Trader system status (counts, apply state) |
| `GET` | `/traders/{traderId}/items` | Paginated item browser with search + LL filter |
| `POST` | `/traders/apply` | Force re-apply trader config |
| `POST` | `/traders/reset` | Reset all traders to defaults |
| `POST` | `/traders/reset/{traderId}` | Reset a single trader |
| `POST` | `/traders/display` | Set custom display name / description |
| `POST` | `/traders/display/avatar` | Upload custom trader avatar |
| `DELETE` | `/traders/display/avatar/{traderId}` | Remove custom avatar |
| `GET` | `/traders/presets` | List trader presets |
| `POST` | `/traders/presets/save` | Save current config as preset |
| `POST` | `/traders/presets/load` | Load and apply a preset |
| `POST` | `/traders/presets/upload` | Import a preset from JSON |
| `GET` | `/traders/presets/{name}/download` | Export a preset as JSON file |
| `DELETE` | `/traders/presets/{name}` | Delete a preset |

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

<details>
<summary><strong>Quests</strong></summary>

<br />

| Method | Endpoint | Description |
|:-------|:---------|:------------|
| `GET` | `/quests/config` | Get quest override configuration |
| `POST` | `/quests/config` | Update quest config + apply |
| `POST` | `/quests/apply` | Force re-apply quest overrides |
| `POST` | `/quests/reset` | Reset all quest overrides to defaults |
| `GET` | `/quests/status` | Quest system status (counts, apply state) |
| `GET` | `/quests/tree` | Quest prerequisite tree data |
| `GET` | `/quests/traders` | List traders that issue quests |
| `GET` | `/quests/locations` | List quest locations / maps |
| `GET` | `/quests/{questId}` | Quest detail with objectives + rewards |

</details>

<details>
<summary><strong>Progression & Skills</strong></summary>

<br />

| Method | Endpoint | Description |
|:-------|:---------|:------------|
| `GET` | `/progression/config` | Get progression configuration |
| `POST` | `/progression/config` | Update progression config + apply |
| `POST` | `/progression/reset` | Reset progression to defaults |

</details>

<details>
<summary><strong>Scheduler & Events</strong></summary>

<br />

| Method | Endpoint | Description |
|:-------|:---------|:------------|
| `GET` | `/scheduler` | Full overview (events, tasks, active events, templates) |
| `GET` | `/scheduler/active` | Currently active events only |
| `POST` | `/scheduler/event` | Create a new event |
| `GET` | `/scheduler/event/{id}` | Get event details |
| `DELETE` | `/scheduler/event/{id}` | Delete an event |
| `POST` | `/scheduler/event/{id}/activate` | Start an event now |
| `POST` | `/scheduler/event/{id}/deactivate` | Stop an active event |
| `POST` | `/scheduler/event/{id}/cancel` | Cancel a scheduled event |
| `POST` | `/scheduler/task` | Create a new scheduled task |
| `DELETE` | `/scheduler/task/{id}` | Delete a scheduled task |
| `POST` | `/scheduler/task/{id}/toggle` | Enable/disable a task |
| `POST` | `/scheduler/cron/validate` | Validate cron expression + get description |

</details>

<details>
<summary><strong>Backup & Admin</strong></summary>

<br />

| Method | Endpoint | Description |
|:-------|:---------|:------------|
| `GET` | `/backups` | List available backups |
| `POST` | `/backup` | Create a new backup |
| `POST` | `/backup/restore` | Restore from a backup |
| `DELETE` | `/backup/{name}` | Delete a backup |
| `POST` | `/wipe` | Server wipe (with confirmation) |
| `GET` | `/watchdog/status` | Watchdog connection status |
| `GET` | `/fika/config` | Get FIKA settings |
| `POST` | `/fika/config` | Update FIKA settings |

</details>

---

## Roadmap

Development is organized into phases. Each phase is fully completed and tested before the next begins.

| | Phase | Feature | Description |
|:-:|:-----:|:--------|:------------|
| **Done** | 1 | **Dashboard** | Server stats, console feed, activity log, send-URL, login vitals, player roster |
| **Done** | 2 | **Player Management** | Items, XP, skills, quests, mail, presets, builds, full profile stats |
| **Done** | 2.5 | **Headless Telemetry** | Live raid monitoring, kill feed, combat stats, minimap, raid history |
| **Done** | 3 | **Flea Market** | Price multipliers, tax, offers, regeneration, category controls |
| **Done** | 4 | **Trader Control** | Full trader management — pricing, stock, restock, loyalty, item injection, presets, display customization |
| **Done** | 5 | **Quest Editor** | Global/per-quest objective & reward multipliers, FIR removal, level shift, quest tree explorer |
| **Done** | 6 | **Progression & Skills** | XP rates, skill speed, loot multipliers, insurance, stamina, health regen |
| **Done** | 7 | **Backup & Restore** | Profile backups, selective restore, server wipe tools |
| **Done** | 8A | **Scheduler & Events** | Cron scheduling, Double XP / Trader Sale / Loot Boost events, automated tasks |
| | 9 | **Game Values Editor** | Ammo, armor, weapons, health, bots, loot, airdrops |
| | 10 | **Config Profiles** | Save / load / export / share config presets |
| | 11 | **Gear Presets** | Loadout templates, weapon builds, kit distribution |
| | 12 | **Bounty Board** | Player bounties, kill targets, challenges |
| | 13 | **Clan System** | Clans, shared storage, team stats |
| | 14 | **Client Plugin** | BepInEx client-side mod for in-game overlay |
| | 15 | **Stash Viewer** | Visual inventory browser with item images |
| | 16 | **Polish & Release** | Final cleanup, docs, and v3.0 launch |

---

## Tech Stack

| Component | Technology |
|:----------|:-----------|
| **Server Mod** | C# 12 / .NET 9.0, SPTarkov DI (`[Injectable]`), custom `IHttpListener`, 40+ source files |
| **Telemetry Plugin** | C# / .NET Standard 2.1, BepInEx 5.x, Harmony patches, async HTTP queue |
| **Watchdog** | C# / WPF, .NET 9.0, process monitoring with crash detection, WebSocket communication |
| **Frontend** | Single HTML file — inline CSS + JS, zero build tools, zero dependencies, zero frameworks |
| **API** | RESTful JSON over HTTPS at `/zslayer/cc/`, 80+ endpoints |
| **Serialization** | `System.Text.Json` (server) / `Newtonsoft.Json` (plugin) |
| **Design** | Tarkov-inspired dark theme, gold accent `#c8aa6e`, monospace typography, responsive layout |

---

## Requirements

| | Version |
|:--|:--------|
| **SPT** | 4.0.x |
| **FIKA** | Latest *(optional — required for headless + telemetry)* |
| **Browser** | Chrome, Firefox, Edge, or any Chromium-based browser |
| **.NET Runtime** | Bundled with SPT server — no separate install needed |

---

## FAQ

<details>
<summary><strong>Do players need to install anything?</strong></summary>

<br />

No. The command center is entirely server-side. Players don't need any client mods. The optional headless telemetry plugin only runs on the headless client, not on player game clients.

</details>

<details>
<summary><strong>Does it work without FIKA?</strong></summary>

<br />

Yes. Dashboard, Items, Players, Quests, Progression, Flea, Traders, Events, and Admin tabs all work on vanilla SPT. Only the Headless Client Manager, Raid Info (live telemetry + minimap), and FIKA Settings require FIKA + a headless client.

</details>

<details>
<summary><strong>Will it conflict with other mods?</strong></summary>

<br />

Unlikely. The command center loads after other mods (`PostSptModLoader + 1`) and makes changes through the same SPT APIs the game uses. It doesn't replace any core server files. Changes are applied on top of whatever other mods have done. Modded traders are automatically discovered and fully manageable.

</details>

<details>
<summary><strong>Can I access it from my phone?</strong></summary>

<br />

Yes. The UI is responsive and works on mobile browsers. Use the LAN URL from any device on your network. The live minimap supports touch gestures for pan and zoom.

</details>

<details>
<summary><strong>Is it safe to update mid-wipe?</strong></summary>

<br />

Yes. The mod only modifies server-side values and configs. Player profiles are never touched unless you explicitly use player management features. Config auto-upgrade preserves all existing settings, and the snapshot-and-restore system means game data is never permanently altered.

</details>

<details>
<summary><strong>What happens if I remove the mod?</strong></summary>

<br />

Everything reverts to normal. Because all modifications use the snapshot-and-restore pattern, removing the mod means the server loads with its original unmodified data. No permanent changes are made to any SPT database files.

</details>

<details>
<summary><strong>Can multiple admins use it at the same time?</strong></summary>

<br />

Yes. The web UI is stateless — multiple admins can be logged in simultaneously from different browsers or devices. All operations are thread-safe with proper locking. The activity log tracks which admin performed each action.

</details>

<details>
<summary><strong>How do I report a bug or request a feature?</strong></summary>

<br />

Open an issue on [GitHub](https://github.com/ZSlayerHQ/ZSlayerCommandCenter/issues) or join the [Discord](https://discord.gg/ZSlayerHQ). For bug reports, include your SPT version, mod version, and steps to reproduce.

</details>

---

## Contributing

This is a solo project by [**ZSlayerHQ**](https://github.com/ZSlayerHQ), but feedback and bug reports are always welcome.

- **Bug Reports** — [open an issue](https://github.com/ZSlayerHQ/ZSlayerCommandCenter/issues) with steps to reproduce
- **Feature Requests** — open an issue or discuss on [Discord](https://discord.gg/ZSlayerHQ)
- **Pull Requests** — reach out on Discord first to discuss the approach

---

<div align="center">

<br />

**[MIT License](LICENSE)** &nbsp;&mdash;&nbsp; Built by [ZSlayerHQ / Ben Cole](https://github.com/ZSlayerHQ)

<br />

</div>
