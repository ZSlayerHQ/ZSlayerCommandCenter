# ZSlayer Command Center — Full In-Depth Audit Report (Revision 2)

Date: 2026-02-24  
Repository: `ZSlayerCommandCenter`  
Audit goal: deep architecture, reliability, performance, SPT/FIKA correctness, and maintainability review for a server-management + raid-telemetry mod.

---

## Scope & Methodology

This audit pass was re-run in full and intentionally expanded.

### Files reviewed

All source/config/docs assets under version control were reviewed directly, including:

- Server bootstrap / metadata:
  - `CommandCenterMod.cs`
  - `ModMetadata.cs`
  - `ZSlayerCommandCenter.csproj`
- HTTP / router layer:
  - `Http/CommandCenterHttpListener.cs`
  - `Routers/RaidDataRouter.cs`
- All service classes (`Services/*.cs`)
- All model/DTO/config classes (`Models/*.cs`)
- Frontend:
  - `res/commandcenter.html`
  - `res/banner.svg`
- Config and docs:
  - `config/config.json`
  - `README.md`
  - `docs/phase-2.5-headless-telemetry.md`

### Command-based verification used during audit

- `rg --files` (enumerate files)
- `nl -ba <file>` (line-accurate code reading)
- `rg -n "..."` for route and timer/event extraction
- `dotnet build -c Release` (environment validation + warning surface)

---

## Phase 1 — Full Codebase Read & Mental Model

## 1.1 What this mod is architecturally

ZSlayer Command Center is a **server-hosted admin and telemetry console** layered onto SPT/FIKA with three major planes:

1. **Administrative control plane** (player management, mail/gives, quest state, flea economics, access control).
2. **Observability plane** (server/headless console streams, activity logs, status dashboards).
3. **Live telemetry plane** (headless raid telemetry ingestion, in-memory raid state, persisted raid history, realtime frontend polling).

The backend is C# with direct SPT service integration, while frontend is a monolithic single-file HTML app with embedded JS/CSS.

## 1.2 Runtime composition graph

### Bootstrap sequence (`CommandCenterMod.OnLoad`)

1. Load + upgrade config (`ConfigService.LoadConfig`) (`CommandCenterMod.cs:56-60`, `ConfigService.cs:35-78`)
2. Inject seasonal service into telemetry (`TelemetryService.SetSeasonService`) (`CommandCenterMod.cs:62`, `TelemetryService.cs:430-431`)
3. Load persisted telemetry raid history (`TelemetryService.Initialize`) (`CommandCenterMod.cs:65`, `TelemetryService.cs:791-816`)
4. Install server console interception (`ConsoleBufferService.InstallConsoleInterceptor`) (`CommandCenterMod.cs:68-69`, `ConsoleBufferService.cs:48-52`)
5. Configure headless log + process services (`CommandCenterMod.cs:72-73`)
6. Apply flea globals/config (`OfferRegenerationService.ApplyGlobalsAndConfig`) (`CommandCenterMod.cs:75-76`)
7. Cleanup + write activity startup log (`CommandCenterMod.cs:79-80`)
8. Derive and print user-facing URLs/startup banner + auto-start headless timer (`CommandCenterMod.cs:82-134`)

## 1.3 Backend service boundaries

### High-cohesion services (good)

- `TelemetryService`: owns telemetry state machine, ring buffers, alerts, performance history, lifetime aggregation, disk persistence.
- `AccessControlService`: central authz + banlist semantics.
- `HeadlessProcessService`: lifecycle controls for EFT headless process.
- `FleaPriceService`: market/flea re-pricing + presets + tax simulation.

### Low-cohesion / broad responsibility areas (risk)

- `CommandCenterHttpListener`: huge route multiplexer with high constructor fan-in (26 injected deps), route parsing, auth, body handling, proxying, and direct orchestration.
- `PlayerManagementService`: broad profile domain + reset/modify actions + economy lookups + audit side effects.
- `res/commandcenter.html`: all tabs, state, polling, rendering logic in one file.

## 1.4 Frontend architecture snapshot

The frontend centers around:

- global `BASE='/zslayer/cc/'` and `api()` wrapper with auth headers (`res/commandcenter.html:4740-4745`)
- tab-specific loaders and periodic polling (`setInterval` around `5561-5572`, `5888`, `6957`, `7956`)
- console/map popout windows with duplicated polling loops (`6546+`, `6689+`, `6907+`)

Overall: functionally rich, but stateful global scripting creates lifecycle complexity and high regression surface.

---

## Phase 2 — Deep Architecture Audit

## 2.1 End-to-end request lifecycle (detailed)

## A) UI-authenticated path

1. Browser loads `/zslayer/cc/` → HTML served by `ServeHtml()`.
2. UI obtains/chooses profile and sets `X-Session-Id` (+ optional password header).
3. Every protected route runs through `ValidateAccess()`:
   - header presence check
   - `AccessControlService.IsAuthorized(sessionId)`
   - optional password compare against config
4. Handler delegates to service and `WriteJson()` serializes response.

Key implementation:
- Root and redirects: `Http/CommandCenterHttpListener.cs:80-104`
- Header extraction: `121`
- Access validation: `1704-1731`
- JSON write helper: `1740-1748`

## B) Headless telemetry ingest path

1. Headless telemetry plugin posts raid events to `/telemetry/*`.
2. `HandleTelemetryRoute()` POST switch parses payloads via generic `ReadBody<T>()`.
3. Telemetry service mutates in-memory state under lock.
4. On raid summary, raid snapshot persisted to disk (`data/raid-history.json`).
5. Frontend polls telemetry GET endpoints.

Key implementation:
- Telemetry route dispatch: `144-146`, `1055-1246`
- POST handlers: `1062-1167`
- GET handlers: `1179-1238`
- Persistence: `TelemetryService.cs:818-839`

## 2.2 Full HTTP route inventory

## Root / static

- `GET /zslayer/cc/` → UI HTML
- `GET /zslayer/cc/banner` (and `banner.svg`)
- static file serving from `res/` with extension allowlist
- legacy redirect `/zslayer/itemgui/*` → `/zslayer/cc/*`

## Auth / profile

- `GET auth`
- `GET profiles`
- `GET profile-icons`
- `POST profile-icon`

## Item / preset / build

- `GET items`
- `GET categories`
- `POST give`
- `GET presets`
- `POST preset`
- `GET player-builds`
- `POST player-build/give`

## Dashboard / server state

- `GET dashboard/status`
- `GET dashboard/players`
- `GET dashboard/economy`
- `GET dashboard/raids`
- `GET dashboard/my-raids`
- `GET dashboard/config`
- `POST dashboard/broadcast`
- `POST dashboard/send-urls`
- `GET dashboard/activity`

## Console

- `GET console`
- `GET console/history`
- `GET headless-console`

## Quests

- `GET quests`
- dynamic:
  - `GET quests/{questId}`
  - `POST quests/{questId}/state`

## Players

- `GET players`
- `GET players/bans`
- `POST players/ban`
- `POST players/unban`
- `POST player/broadcast`
- `POST player/give-all`
- dynamic:
  - `GET player/{id}`
  - `GET player/{id}/stats`
  - `GET player/{id}/stash`
  - `POST player/{id}/mail`
  - `POST player/{id}/give`
  - `POST player/{id}/reset`
  - `POST player/{id}/modify`

## Telemetry

### POST

- `telemetry/hello`
- `telemetry/raid-state`
- `telemetry/performance`
- `telemetry/kill`
- `telemetry/players`
- `telemetry/bots`
- `telemetry/boss-spawn`
- `telemetry/extract`
- `telemetry/raid-summary`
- `telemetry/damage-stats`
- `telemetry/positions`
- `telemetry/map-refresh-rate`

### GET

- `telemetry/current`
- `telemetry/kill-feed`
- `telemetry/raid-history`
- `telemetry/raid-history/{id}`
- `telemetry/lifetime-stats`
- `telemetry/performance-history`
- `telemetry/alerts`
- `telemetry/positions`
- `telemetry/map-refresh-rate`

## Watchdog proxy

- `GET watchdog/status`
- `POST watchdog/start|stop|restart`

## Headless manager

- `GET headless/status`
- `POST headless/start|stop|restart|config`

## FIKA config bridge

- `GET fika/config`
- `POST fika/config`

## Flea management

- `GET flea/config|categories|status|debug|debug/tax|presets`
- `POST flea/config|config/global|config/market|config/category|config/item|regenerate|presets|presets/load|presets/import`
- `DELETE flea/presets/{name}`
- `DELETE flea/config/item/{templateId}`

Reference: route switches in `Http/CommandCenterHttpListener.cs:218-304, 869-927, 985-999, 1055-1246, 1316-1623`.

## 2.3 Telemetry data flow deep map

## Inputs

- `RaidStatePayload`, `PerformancePayload`, `KillPayload`, `PlayerStatusPayload`, `BotCountPayload`, `BossSpawnPayload`, `ExtractPayload`, `RaidSummaryPayload`, `DamageStatsPayload`, `PositionPayload`.
- DTO definitions are robust and strongly typed in `Models/TelemetryModels.cs`.

## In-memory state model (`TelemetryService`)

- Current raid state snapshots:
  - `_currentRaidState`, `_currentPerformance`, `_currentPlayers`, `_currentBots`, `_currentDamageStats`, `_currentPositions`
- Rolling buffers:
  - `_killFeed` max 100
  - `_perfHistory` max 60 samples
  - `_alerts` max 200
- Current raid accumulators:
  - `_currentRaidKills`, `_currentRaidExtracts`, `_currentRaidId`, `_currentRaidStart`
- Persisted aggregate:
  - `_raidHistory` loaded/saved as JSON

Reference: `TelemetryService.cs:25-43, 522-547, 741-743`.

## Transition logic

- `UpdateRaidState()` controls startup/idle transitions, kill-feed reset, and stale data cleanup (`163-209`).
- `AddKill()` normalizes names (Unity tag strip + type semantics), resolves localized item names, updates buffers, emits alerts (`220-286`).
- `FinishRaid()` enriches player kill counts from observed kill feed, archives summary/detail, clears per-raid accumulators, persists (`346-396`).

## Output APIs

- `GetCurrent()`, `GetKillFeed()`, `GetRaidHistory()`, `GetRaidDetail()`, `GetLifetimeStats()`, `GetPerformanceHistory()`, `GetAlerts()`.

## Persistence behavior

- Load-on-start: `Initialize()` reads `data/raid-history.json` (`791-816`).
- Write-on-raid-complete: full snapshot serialization (`818-839`).

## 2.4 Dependency and coupling audit

## Constructor fan-in (selected)

- `CommandCenterHttpListener`: 26 injected dependencies (high coupling)
- `PlayerManagementService`: 12 dependencies (complex orchestration)
- `TelemetryService`: 3 dependencies (focused)

## Static/shared-state coupling

- `CommandCenterMod.ServerUrls` static mutable state (`CommandCenterMod.cs:28`)
- `ConsoleBufferService.Instance` global static accessor (`ConsoleBufferService.cs:12`)
- `RaidDataRouter` static service fields (`RaidDataRouter.cs:16-20`)

## Circular dependency status

- No direct DI constructor cycle found.
- There is lifecycle coupling and hidden state coupling via static fields/global singleton pattern.

---

## Phase 3 — Performance & Reliability Audit (Expanded)

## 3.1 Blocking/synchronous code paths

### Finding P1-A: Startup network sync-over-async

`GetPublicIp()` uses `HttpClient.GetStringAsync(...).Result` (`CommandCenterMod.cs:417-423`) and is called during startup URL derivation.

**Impact**
- can block mod load for timeout duration
- ties startup reliability to external service availability (`api.ipify.org`)

**Fix**
- Convert to fully async and await in `OnLoad`
- or defer into background task and print “Public IP pending” then update later

### Finding P1-B: Potential expensive per-request static file reads

`ServeStaticFile()` reads entire file bytes on each request (`1698`).

**Impact**
- small for icons/assets, but avoidable repeat I/O for frequently requested static assets

**Fix**
- optional in-memory cache for stable static assets with mtime invalidation

## 3.2 File I/O patterns

### Finding P1-C: Activity log is read-modify-write whole-file per action

`ActivityLogService.LogAction()`:
- read current day JSON file (`45`)
- deserialize full list (`46`)
- append one entry (`49`)
- write full list (`50`)

**Impact**
- O(n) write amplification as logs grow
- avoidable lock/contention if multiple actions land quickly

### Finding P2-A: RaidTrackingService flush strategy is full snapshot per record

`RecordRaid()` calls `FlushToDisk()` every raid (`35`), which serializes all records each time (`123-124`).

**Impact**
- probably acceptable at low raid throughput, but still linear scaling cost

**Fix**
- debounce writes or switch to append log + periodic compaction

## 3.3 Concurrency and race analysis

### Finding P0-A: Telemetry ingest authentication bypass

Telemetry POST endpoints bypass `ValidateAccess()` intentionally (`HandleTelemetryRoute` POST branch `1057-1169`). Combined with wildcard CORS (`1735`) this permits any caller able to reach the endpoint to post telemetry.

**Impact for this mod specifically**
- raid dashboard trust is compromised
- attacker can flood fake kills/alerts and distort admin decisions
- can create memory churn through accepted telemetry spam

**Fix options**
- require telemetry API key (`X-Telemetry-Key`)
- source IP allowlist (loopback/LAN)
- per-endpoint rate limiting and payload bounds

### Finding P1-D: Out-of-order telemetry events can bleed across raid sessions

There is no required `raidId` correlation across payload types. If delayed events arrive after idle/new raid start, they can populate wrong session buffers.

**Fix**
- require raid-scoped UUID in all telemetry payloads
- reject events with stale/non-current raid ID

### Finding P2-B: Console buffer trimming uses `ConcurrentQueue.Count` loop

`ConsoleBufferService.Add()` checks `while (_buffer.Count > _maxSize)` (`30-31`). `Count` on concurrent collections is not cheap.

**Fix**
- track approximate count via `Interlocked`
- or use lock-protected ring buffer

## 3.4 Error handling behavior

### Finding P1-E: Silent catch blocks reduce diagnosability

Examples:
- `RaidDataRouter.cs:64`
- `TelemetryService.cs:455`
- `HeadlessLogService.cs:123-126`
- `Config/cleanup/other service paths` with `catch {}` patterns

**Impact**
- failures become invisible in production
- hard to triage data quality issues or integration drift

**Fix**
- log at debug/warn with suppression/rate-limit
- include operation context and identifiers

## 3.5 Headless process and log-tail mechanics

### Strengths

- `Start()` drains stdout/stderr via `BeginOutputReadLine/BeginErrorReadLine` to prevent deadlock (`HeadlessProcessService.cs:143-145`).
- `TryAttachExisting()` handles externally launched process recovery (`193-227`).
- `HeadlessLogService` handles truncation/rotation by resetting position when file shrinks (`97-100`).

### Risks

- hardcoded launch args force backend URL/version (`118-120`) and can break custom SPT configs.
- `_process.Exited` event handlers are not explicitly detached on stop/restart (not necessarily leaking due process lifecycle, but cleanup hygiene can improve).

---

## Phase 4 — Code Quality Audit (Expanded)

## 4.1 Hotspots by maintainability risk

## Hotspot 1: `Http/CommandCenterHttpListener.cs` (1764 lines)

- combines routing, auth, body parsing, proxy behavior, static serving, and many feature controllers.
- route logic mostly switch-driven and repetitive.

**Recommendation**
- split into route modules (`TelemetryController`, `PlayerController`, `FleaController`, etc.) with shared middleware helpers.

## Hotspot 2: `res/commandcenter.html` (~10k lines)

- monolithic CSS + JS + markup for all tabs
- many global vars/timers/listeners

**Recommendation**
- split into modular scripts (or TS build) with lifecycle-aware cleanup per tab.

## Hotspot 3: `PlayerManagementService.cs` (762 lines)

- broad domain operations + side effects + logging

**Recommendation**
- extract domain-specific collaborators (`PlayerResetService`, `PlayerEconomyService`, `PlayerMutationService`).

## 4.2 Dead code / unused variables / unreachable paths

From build warnings and read-through:

- `CommandCenterMod.cs`: local function `LeftAlign` declared but unused (`line ~264` warning).
- `CommandCenterHttpListener` constructor dependency `raidTrackingService` appears unused (warning surfaced in build).

No obvious unreachable control-flow branches beyond standard default 404/405 handlers.

## 4.3 Duplication findings

- Repeated request body validation and response envelope creation in listener endpoints.
- Similar date/file load patterns across Activity and Raid history stores.
- Multiple frontend polling loops with similar logic duplicated between main page and popouts.

## 4.4 Naming consistency

Mostly good and domain-readable. Minor inconsistencies:

- mixed naming around “headless-console” endpoint returning generic `ConsoleResponse`.
- route naming mostly pluralized, but some legacy singular forms remain.

## 4.5 TODO/FIXME/HACK markers

No explicit TODO/FIXME/HACK comment markers found in source scan.

---

## Phase 5 — SPT/FIKA-Specific Review

## 5.1 Lifecycle hook correctness

- `CommandCenterMod` uses `[Injectable(TypePriority = OnLoadOrder.PostSptModLoader + 1)]` and implements `IOnLoad` correctly.
- `RaidDataRouter` uses `[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]` and pass-through route design for `/client/match/local/end`.

This is structurally correct for observing raid-end without altering core endpoint behavior.

## 5.2 SPT API stability concerns

`ConfigServer` and `ConfigServer.GetConfig<T>()` are used in several classes and now produce deprecation warnings in build output.

Locations include:
- `CommandCenterMod`
- `CommandCenterHttpListener`
- `OfferRegenerationService`

**Risk**
- forward compatibility issue when upgrading to SPT 4.2+

**Recommendation**
- refactor to direct configuration injection pattern now to reduce migration risk.

## 5.3 FIKA compatibility and multiplayer considerations

### Good

- dedicated `FikaConfigService` for config bridge.
- headless lifecycle controls + status endpoints align with hosted FIKA admin needs.

### Risks

- hardcoded backend url/version for headless launch harms non-default installs.
- telemetry trust model currently assumes local-only sender but does not enforce it.

## 5.4 Profile switches / restarts / reload handling

### Good

- process attach/recovery path after restart (`TryAttachExisting`).
- log tail reset support (`HeadlessLogService.Reset`).

### Gaps

- telemetry in-memory state isn’t explicitly profile-scoped; stale cross-profile semantics possible if plugins post delayed events.

---

## Phase 6 — Prioritized Improvement Plan (Detailed)

## P0 — Critical

### P0-1 Secure telemetry ingest

**Problem**
- unauthenticated POST telemetry endpoints + wildcard CORS.

**Why it matters for this mod**
- telemetry dashboard must represent trustworthy raid data for server administration decisions.

**Concrete fix**

1. Add config section:
```json
"telemetry": {
  "sharedKey": "change-me",
  "allowLoopbackOnly": true,
  "maxRequestsPerMinute": 1200
}
```

2. Enforce in `HandleTelemetryRoute()` before switch:
```csharp
if (method == "POST")
{
    var key = context.Request.Headers["X-Telemetry-Key"].FirstOrDefault() ?? "";
    if (key != configService.GetConfig().Telemetry.SharedKey)
    {
        await WriteJson(context, 401, new { error = "Invalid telemetry key" });
        return;
    }
}
```

3. Optional source-IP check and lightweight rate-limit.

---

## P1 — High

### P1-1 Remove startup sync-over-async public IP call

Refactor `GetPublicIp()` to async and avoid `.Result`.

### P1-2 Replace read-modify-write activity logs with append-only JSONL

Implement append lines:
```csharp
File.AppendAllText(path, JsonSerializer.Serialize(entry) + "\n");
```
Add read parser that streams recent lines.

### P1-3 Introduce raid correlation IDs in telemetry schema

Add `raidId` to every telemetry payload; reject stale raid IDs.

### P1-4 Replace hardcoded headless backend launch config

Derive backend URL and version from SPT runtime config instead of literals.

### P1-5 Add structured, throttled warning logging for swallowed catches

Keep robustness but improve observability.

---

## P2 — Medium

### P2-1 Split HTTP listener into feature controllers

Proposed modules:
- `TelemetryHttpController`
- `PlayerHttpController`
- `FleaHttpController`
- `HeadlessHttpController`
- shared auth/body middleware helpers

### P2-2 Consolidate raid history storage strategy

Unify `RaidTrackingService` and `TelemetryService` persistence paths/schema to avoid divergence.

### P2-3 Replace static mutable service state patterns

- convert `RaidDataRouter` static fields to instance fields
- remove/limit `ConsoleBufferService.Instance`

### P2-4 Frontend modularization

Break monolith into logically isolated scripts with explicit mount/unmount cleanup.

---

## P3 — Low

### P3-1 Add API contract tests for all route surfaces

Smoke-test each route status code + auth behavior.

### P3-2 Adaptive poll/backoff policies

When raid inactive, reduce telemetry polling interval to decrease server/UI load.

### P3-3 Add load-test harness for telemetry bursts

Simulate multiple concurrent FIKA clients posting telemetry to validate lock contention and memory behavior.

---

## Appendix A — Service-by-Service Findings

- **AccessControlService**: straightforward, config-backed, but no cache for `saveServer.GetProfiles()` calls under heavy request volume.
- **ActivityLogService**: robust basic behavior; write pattern scales poorly.
- **ConfigService**: good auto-upgrade + migration path from v1; no write lock around `SaveConfig` if multiple endpoints mutate config concurrently.
- **ConsoleBufferService**: simple and effective; queue count/trimming could be optimized.
- **FikaConfigService**: direct JSON node manipulation is pragmatic; no explicit backup before write.
- **FleaPriceService**: feature-rich; large class with combined concerns (pricing calc, storage, preview).
- **HeadlessLogService**: efficient tailing strategy and rotation awareness.
- **HeadlessProcessService**: good lifecycle control + attach; hardcoded backend args are primary issue.
- **ItemGiveService / PlayerMailService**: integrated with activity logs; side-effect orchestration is sensible.
- **PlayerManagementService**: extensive power/control surface; should be decomposed for maintainability.
- **PlayerStatsService**: useful aggregation from profile counters; ensure nullability warnings are addressed.
- **QuestBrowserService**: good quest projection, includes nullability warning area at quest lookup.
- **RaidTrackingService**: intentionally simple; could share infra with telemetry persistence.
- **ServerStatsService**: concise and useful metadata aggregation.
- **TelemetryService**: strongest component from domain perspective; only trust and correlation hardening missing.

---

## Appendix B — Build Result Snapshot (Post Environment Fix)

- `dotnet build -c Release` succeeds.
- Current environment build run: 13 warnings, 0 errors.
- Warning categories include SPT deprecation (`ConfigServer`), nullability diagnostics, and minor hygiene warnings (unused local function, unread injected parameter).

---

## Appendix C — Audit Confidence and Next Steps

Confidence level: **high** on architecture/route/dataflow findings; **medium-high** on runtime performance implications (static analysis-based, no synthetic load test executed).

Recommended execution order:

1. **Security hardening for telemetry POST** (P0)
2. **Headless launch/config robustness + async startup cleanup** (P1)
3. **I/O and observability improvements** (P1)
4. **Listener/UI modularization** (P2)
5. **Long-term test/lint/perf harness** (P3)

