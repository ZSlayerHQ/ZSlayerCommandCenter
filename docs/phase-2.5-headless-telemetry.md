# Phase 2.5 — Headless Telemetry Plugin

> The headless client runs a full game instance but has no UI. This phase adds a lightweight BepInEx plugin that runs **only on the headless client**, hooks into game events (kills, player status, boss spawns, extracts, raid lifecycle), and POSTs telemetry to the Command Center server API. The server stores this data and exposes it to the dashboard via GET endpoints.
>
> The dashboard gets three new panels: **Live Raid**, **Kill Feed**, and **Raid History**.

---

## Architecture

```
Headless Client (BepInEx)              Server Mod (existing)              Dashboard (HTML)
─────────────────────────              ─────────────────────              ────────────────
ZSlayerHeadlessTelemetry.dll           TelemetryService.cs                Live Raid panel
  ├── Plugin.cs                        (receives POSTs,                   Kill Feed panel
  ├── TelemetryReporter.cs              stores state,                     Raid History panel
  └── RaidEventHooks.cs                 resolves locale names)
       │                                    │
       ├─ POST /telemetry/raid-state ──▸    ├─ GET /telemetry/current ──────▸ polls
       ├─ POST /telemetry/kill ────────▸    ├─ GET /telemetry/kill-feed ────▸ polls
       ├─ POST /telemetry/players ─────▸    ├─ GET /telemetry/raid-history ─▸ polls
       ├─ POST /telemetry/bots ────────▸    │
       ├─ POST /telemetry/boss-spawn ──▸    │
       ├─ POST /telemetry/extract ─────▸    │
       ├─ POST /telemetry/raid-summary ▸    │
       └─ POST /telemetry/performance ─▸    │
```

### Build Order

| Step | Project | Scope |
|:----:|---------|-------|
| 1 | Server mod | Telemetry models + service + HTTP routes *(testable with curl)* |
| 2 | BepInEx plugin | New project — hooks game events, POSTs to server |
| 3 | Dashboard UI | Live Raid, Kill Feed, Raid History panels |
---

## Part 1 — Server Mod Changes

### Files to Create / Modify

| File | Action | Purpose |
|------|:------:|---------|
| `Models/TelemetryModels.cs` | Create | DTOs for all POST/GET payloads |
| `Services/TelemetryService.cs` | Create | Stores telemetry state, resolves locale names, serves GET data |
| `Http/CommandCenterHttpListener.cs` | Modify | Add `telemetry/` route prefix, inject TelemetryService |
| `Models/DashboardModels.cs` | Modify | May add small models here if simpler |

### Inbound Models (from plugin POSTs)

| Model | Fields |
|-------|--------|
| `RaidStatePayload` | status, map, raidTimer, raidTimeLeft, timeOfDay, weather, player counts |
| `PerformancePayload` | fps, fpsAvg, fpsMin, fpsMax, frameTimeMs, memoryMb |
| `KillPayload` | timestamp, raidTime, map, killer{name,type,level}, victim{name,type}, weapon, ammo, bodyPart, distance, isHeadshot |
| `PlayerStatusPayload` | map, players[]{name, profileId, type, side, level, alive, health, pingMs} |
| `BotCountPayload` | map, scavs{alive,dead}, raiders{alive,dead}, rogues{alive,dead}, bosses[], totalAI{alive,dead} |
| `BossSpawnPayload` | map, boss, followers, spawnPoint, raidTime |
| `ExtractPayload` | timestamp, map, raidTime, player{}, outcome, extractPoint, killedBy, raidDuration |
| `RaidSummaryPayload` | map, raidDuration, players[], bosses[], totalKills, totalDeaths |

### Outbound Models (to dashboard GETs)

| Model | Description |
|-------|-------------|
| `TelemetryCurrentDto` | Combines raid state + performance + players + bots (one poll) |
| `KillFeedDto` | List of resolved kill entries (weapon/ammo names from locale) |
| `RaidHistoryDto` | List of raid summaries |
| `RaidHistoryDetailDto` | Single raid with full kill feed + player stats |
### TelemetryService (Singleton)

**State:**

| Field | Type | Description |
|-------|------|-------------|
| `_currentRaidState` | `RaidStatePayload?` | Latest raid status |
| `_currentPerformance` | `PerformancePayload?` | Latest FPS data |
| `_currentPlayers` | `PlayerStatusPayload?` | Latest player list |
| `_currentBots` | `BotCountPayload?` | Latest bot counts |
| `_killFeed` | `List` (ring buffer, max 100) | Resolved kill entries |
| `_raidHistory` | `List` (max 50) | Stored raid summaries with kill feeds |
| `_currentRaidKills` | `List` | Kills for current raid *(moved to history on summary)* |

**Locale resolution** — reuse existing pattern from `FleaPriceService.cs:ResolveItemName`:

```csharp
// localeService.GetLocaleDb("en") → locales.TryGetValue($"{templateId} Name", out var name)
```

**Methods:**

| Method | Behaviour |
|--------|-----------|
| `UpdateRaidState(payload)` | Store; if status → `"idle"`, clear stale data |
| `UpdatePerformance(payload)` | Store |
| `AddKill(payload)` | Resolve names, add to ring buffer + current raid kills |
| `UpdatePlayers(payload)` | Store |
| `UpdateBots(payload)` | Store |
| `AddBossSpawn(payload)` | Store (merge into bot state) |
| `AddExtract(payload)` | Store (merge into current raid player outcomes) |
| `FinishRaid(payload)` | Archive current raid to history, clear current state |
| `GetCurrent()` | → `TelemetryCurrentDto` |
| `GetKillFeed(limit)` | → list of kills |
| `GetRaidHistory()` | → list of summaries |
| `GetRaidDetail(id)` | → single raid detail |
### HTTP Routes

Add route prefix handling in `CommandCenterHttpListener.cs`:

```csharp
if (path.StartsWith("telemetry/"))
{
    await HandleTelemetryRoute(context, headerSessionId, path, method);
    return;
}
```

**POST routes** *(from plugin — no session ID required):*

| Route | Handler |
|-------|---------|
| `POST telemetry/raid-state` | `telemetryService.UpdateRaidState()` |
| `POST telemetry/performance` | `telemetryService.UpdatePerformance()` |
| `POST telemetry/kill` | `telemetryService.AddKill()` |
| `POST telemetry/players` | `telemetryService.UpdatePlayers()` |
| `POST telemetry/bots` | `telemetryService.UpdateBots()` |
| `POST telemetry/boss-spawn` | `telemetryService.AddBossSpawn()` |
| `POST telemetry/extract` | `telemetryService.AddExtract()` |
| `POST telemetry/raid-summary` | `telemetryService.FinishRaid()` |

**GET routes** *(from dashboard — normal session auth):*

| Route | Handler |
|-------|---------|
| `GET telemetry/current` | `telemetryService.GetCurrent()` |
| `GET telemetry/kill-feed` | `telemetryService.GetKillFeed()` |
| `GET telemetry/raid-history` | `telemetryService.GetRaidHistory()` |
| `GET telemetry/raid-history/{id}` | `telemetryService.GetRaidDetail(id)` |

> **Auth note:** POST endpoints from the headless plugin can't easily supply a valid session ID. Skip auth for `telemetry/` POST routes — server and headless run on the same machine, so localhost-only access is sufficient.
---

## Part 2 — BepInEx Plugin (New Project)

### Project Setup

New project at `Development/Projects/ZSlayerHeadlessTelemetry/`

```
ZSlayerHeadlessTelemetry/
├── ZSlayerHeadlessTelemetry.csproj
├── Plugin.cs
├── TelemetryReporter.cs
└── RaidEventHooks.cs
```

**`.csproj` config:**

| Setting | Value |
|---------|-------|
| TargetFramework | `netstandard2.1` |
| Packages | `BepInEx.Core 5.*`, `BepInEx.Analyzers 1.*`, `BepInEx.PluginInfoProps 1.*` |
| Assembly refs | `Assembly-CSharp`, `UnityEngine`, `UnityEngine.CoreModule`, `Fika.Core`, `spt-common`, `spt-reflection`, `Newtonsoft.Json`, `Comfort`, `Comfort.Unity` |
| Deploy target | `BepInEx/plugins/ZSlayerHeadlessTelemetry/` |

> **Reference:** `UIFixes-main` pattern for `AssemblySearchPaths` pointing at local SPT install's Managed + BepInEx folders.

### Plugin.cs — Entry Point

```csharp
[BepInPlugin("com.zslayerhq.headlesstelemetry", "ZSlayer Headless Telemetry", "1.0.0")]
[BepInDependency("com.fika.core", BepInDependency.DependencyFlags.HardDependency)]
public class Plugin : BaseUnityPlugin
```

- **`Awake()`:** Check `FikaBackendUtils.IsHeadless` — if `false`, log `"Not headless — telemetry disabled"` and `return`
- Initialize `TelemetryReporter` with server URL from `FikaBackendUtils.GetBackendUrl()` + `/zslayer/cc/telemetry/`
- Initialize `RaidEventHooks` with reference to reporter
- Async ping server to verify reachable
### TelemetryReporter.cs — HTTP POST Logic

| Concern | Implementation |
|---------|---------------|
| HTTP client | Single `HttpClient` instance, 3s timeout, ignore SSL errors (self-signed) |
| Queue | `ConcurrentQueue<QueuedPost>` ring buffer (max 64 items) |
| Send loop | Background thread drains queue, POSTs JSON |
| Serialization | Newtonsoft.Json *(already in game)* |
| Failure handling | Log once, drop silently, **never retry** |

### RaidEventHooks.cs — Game Event Subscriptions

**Raid lifecycle:**

| Event | Action |
|-------|--------|
| `FikaGameCreatedEvent` | POST raid-state `status: "loading"`, extract map name |
| `FikaRaidStartedEvent` | POST raid-state `status: "in-raid"`, start periodic reporting (5s coroutine) |
| `FikaGameEndedEvent` | POST raid-summary → raid-state `status: "idle"`, stop periodic reporting |

**Periodic reports (every 5s while in-raid):**

| Report | Source |
|--------|--------|
| Raid state | `CoopGame` timer, map, player counts |
| Players | `CoopHandler.HumanPlayers` → name, side, level, alive, health %, ping |
| Performance | `1f / Time.deltaTime`, track min/max/avg |
| Bots *(every 10s)* | `CoopHandler.Players` excluding humans → count by role |
**Kill events** *(immediate, fire-and-forget):*

- Hook `Player.OnDead` or subscribe to FIKA's kill notification
- Extract: killer name/type/level, victim name/type, weapon templateId, ammo templateId, bodyPart, `Vector3.Distance(killer.Position, victim.Position)`
- POST to `telemetry/kill`

**Extract events** *(immediate):*

- Hook `CoopGame.Extract` — fires when player extracts
- Extract: player info, outcome, extract point name
- POST to `telemetry/extract`

**Boss spawns:**

- Detect new bot spawns with `WildSpawnType` boss types
- POST to `telemetry/boss-spawn`

**Player health calculation:**

```
health = sum(bodyPart.currentHp) / sum(bodyPart.maxHp)    // 0.0 → 1.0
```

**Ping extraction:**

- Headless runs as FIKA server → `Singleton<FikaServer>.Instance.NetServer`
- Each connected peer has `NetPeer.Ping` (RTT in ms)
- Map peer → player via FIKA's connection tracking
### FIKA Source Reference Table

| Need | Source File | Detail |
|------|------------|--------|
| Headless detection | `FikaBackendUtils.cs` | `FikaBackendUtils.IsHeadless` static bool |
| Backend URL | `FikaBackendUtils.cs` | `FikaBackendUtils.GetBackendUrl()` |
| Raid events | `FikaEventDispatcher.cs` | `DispatchEvent()` + event classes |
| Game instance | `CoopGame.cs` | Current game, map, timer |
| Human players | `CoopHandler.cs` | `HumanPlayers` list, `Players` dict |
| Player data | `CoopPlayer.cs` / `FikaPlayer` | Name, side, level, health, position |
| Kill data | `Player.cs` / `DamageInfo` | `OnDead`, `WeaponId`, `BodyPartType`, `KillerId` |
| FPS | Unity `Time.deltaTime` | `1f / Time.deltaTime` |
| Peer ping | `FikaServer.cs` | `NetServer` → `NetPeer.Ping` |
| Bot type | `BotData` / `WildSpawnType` | Role enum for boss detection |
---

## Part 3 — Dashboard UI

### Live Raid Panel

> Appears when a raid is active. Hidden when idle. Polls `GET telemetry/current` every 3s.

```
┌──────────────────────────────────────────────────────────────────┐
│  🎯 LIVE RAID — Customs                                         │
│                                                                  │
│  Status: In Raid    Timer: 14:07 / 30:00    FPS: 58             │
│                                                                  │
│  Players                                                         │
│  ● Scottish      Lvl 69  USEC  Alive   32ms                     │
│  ● SheLovesPsalm Lvl 19  USEC  Alive   85ms                     │
│  ● ZSlayerHQ     Lvl 17  USEC  Dead (Head,Eyes - Reshala)       │
│  ● Callum        Lvl 12  USEC  Alive   41ms                     │
│                                                                  │
│  Bots: 8 alive / 4 dead    Bosses: Reshala ☠️                    │
└──────────────────────────────────────────────────────────────────┘
```

### Kill Feed Panel

> Scrolling feed, newest at top. Polls `GET telemetry/kill-feed` every 3s.

```
┌──────────────────────────────────────────────────────────────────┐
│  💀 KILL FEED                                                    │
│                                                                  │
│  14:07  Scottish ──[AK-74M]──▸ Reshala (Head, 42m)              │
│  13:52  ZSlayerHQ ──[MP-155]──▸ Scav (Thorax, 8m)               │
│  13:41  Reshala ──[TT]──▸ ZSlayerHQ (Head, 15m)                 │
│  12:30  SheLovesPsalm ──[M4A1]──▸ Scav (Legs, 65m)              │
└──────────────────────────────────────────────────────────────────┘
```
| Element | Colour |
|---------|--------|
| PMC names | Gold `#c8aa6e` |
| Boss names | Red `#8b3a3a` |
| Scav names | Grey `#7a7060` |
| Headshots | 🎯 indicator or red highlight |

### Raid History Panel

> Table of past raids. Click to expand full details. Polls `GET telemetry/raid-history` on tab load.

| Map | Date | Duration | Players | Survived | Kills | Bosses |
|-----|------|----------|:-------:|:--------:|:-----:|--------|
| Customs | 21 Feb 19:45 | 24:07 | 4 | 3/4 | 12 | Reshala ☠️ |
| Factory | 21 Feb 18:20 | 08:32 | 2 | 2/2 | 6 | Tagilla ☠️ |
| Shoreline | 21 Feb 17:05 | 31:00 | 4 | 1/4 | 8 | Sanitar ✓ |

### Colour Coding Reference

**Ping:**

| Range | Colour | Hex |
|-------|--------|-----|
| < 50ms | 🟢 Green | `#4a7c59` |
| 50–100ms | 🟡 Gold | `#c8aa6e` |
| 100–150ms | 🟠 Orange | `#c87a3e` |
| > 150ms | 🔴 Red | `#8b3a3a` |

**FPS:**

| Range | Colour | Hex |
|-------|--------|-----|
| > 60 | 🟢 Green | `#4a7c59` |
| 30–60 | 🟠 Orange | `#c87a3e` |
| ≤ 30 | 🔴 Red | `#8b3a3a` |
---

## Deployment

| Component | Deploy Path |
|-----------|-------------|
| Server mod | Existing — `SPT/user/mods/ZSlayerCommandCenter/` |
| BepInEx plugin | `Deploy/Client Mod/BepInEx/plugins/ZSlayerHeadlessTelemetry/ZSlayerHeadlessTelemetry.dll` |

> The plugin DLL deploys to the headless client's `BepInEx/plugins/` folder on the server machine.

---

## Verification Checklist

### Server Mod

- [ ] Build passes
- [ ] POST endpoints accept JSON payloads (test with curl or plugin)
- [ ] GET endpoints return stored telemetry
- [ ] Template IDs resolved to display names via locale
- [ ] Kill feed ring buffer caps at 100, raid history at 50
- [ ] Stale data clears when raid status goes to `"idle"`

### BepInEx Plugin

- [ ] Plugin loads on headless client (check BepInEx log)
- [ ] Plugin disables itself on non-headless clients
- [ ] Startup < 100ms, no game thread blocking
- [ ] Raid state reported correctly (status, map, timer)
- [ ] Kill events fire with weapon, ammo, body part, distance
- [ ] Player health and ping reported per-player
- [ ] Bot counts and boss spawns tracked
- [ ] Extract events fire with correct outcome
- [ ] Post-raid summary sent
- [ ] Failed POSTs dropped silently (no crash, no lag)
- [ ] < 1% CPU overhead, < 10 MB memory

### Dashboard

- [ ] Live Raid panel appears during raid, hides when idle
- [ ] Kill feed scrolls with formatted entries
- [ ] Player ping colour-coded
- [ ] Boss spawn indicators (alive/dead/not spawned)
- [ ] Raid history table populated and expandable
- [ ] No errors when plugin not installed (graceful empty state)