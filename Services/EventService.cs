using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class EventService(
    ConfigService configService,
    ProgressionControlService progressionControlService,
    TraderApplyService traderApplyService,
    LocationService locationService,
    PlayerMailService playerMailService,
    ActivityLogService activityLogService,
    ISptLogger<EventService> logger)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly object _lock = new();
    private SchedulerState _state = new();
    private System.Timers.Timer? _timer;
    private string _dataDir = "";

    private static readonly List<EventTemplateDto> Templates =
    [
        new()
        {
            Type = EventType.DoubleXP,
            Name = "Double XP",
            Description = "Multiply all XP gains (kill, match, heal, examine) and skill leveling speed.",
            DefaultMultiplier = 2.0,
            Icon = "xp",
            SupportsTargets = false,
            MultiplierLabel = "XP Multiplier"
        },
        new()
        {
            Type = EventType.TraderSale,
            Name = "Trader Sale",
            Description = "Reduce trader buy prices by a percentage. Affects all or specific traders.",
            DefaultMultiplier = 0.5,
            Icon = "sale",
            SupportsTargets = true,
            TargetLabel = "Traders (empty = all)",
            MultiplierLabel = "Price Factor (0.5 = 50% off)"
        },
        new()
        {
            Type = EventType.LootBoost,
            Name = "Loot Boost",
            Description = "Increase loose and container loot spawn rates across all maps.",
            DefaultMultiplier = 2.0,
            Icon = "loot",
            SupportsTargets = false,
            MultiplierLabel = "Loot Multiplier"
        },
        new()
        {
            Type = EventType.MapLootBoost,
            Name = "Map Loot Boost",
            Description = "Boost loot on specific maps for a duration.",
            DefaultMultiplier = 2.0,
            Icon = "loot",
            SupportsTargets = true,
            TargetLabel = "Maps (empty = all)",
            MultiplierLabel = "Loot Multiplier"
        },
        new()
        {
            Type = EventType.MapBossRush,
            Name = "Map Boss Rush",
            Description = "100% boss spawn chance on specific maps.",
            DefaultMultiplier = 100.0,
            Icon = "boss",
            SupportsTargets = true,
            TargetLabel = "Maps (empty = all)",
            MultiplierLabel = "Boss Chance %"
        },
        new()
        {
            Type = EventType.MapOfTheDay,
            Name = "Map of the Day",
            Description = "Auto-rotate a featured map with boosted loot (deterministic daily).",
            DefaultMultiplier = 2.0,
            Icon = "map",
            SupportsTargets = false,
            MultiplierLabel = "Loot Multiplier for Featured Map"
        }
    ];

    // ═══════════════════════════════════════════════════════════════
    //  INITIALIZATION
    // ═══════════════════════════════════════════════════════════════

    public void Initialize()
    {
        _dataDir = Path.Combine(configService.ModPath, "config", "scheduler");
        Directory.CreateDirectory(_dataDir);

        LoadState();

        // Re-apply any events that were active before restart
        var reactivated = 0;
        foreach (var evt in _state.Events.Where(e => e.Status == EventStatus.Active))
        {
            if (evt.ExpiresAt.HasValue && evt.ExpiresAt.Value <= DateTime.UtcNow)
            {
                evt.Status = EventStatus.Expired;
                logger.Info($"[ZSlayerHQ] Event '{evt.Name}' expired during downtime");
            }
            else
            {
                ApplyEventEffects(evt);
                reactivated++;
            }
        }

        if (reactivated > 0)
            logger.Info($"[ZSlayerHQ] Scheduler: Re-applied {reactivated} active event(s) after restart");

        // Compute next run times for tasks
        foreach (var task in _state.Tasks.Where(t => t.Enabled))
            ComputeNextRun(task);

        SaveState();

        // Start the background timer (60s tick)
        _timer = new System.Timers.Timer(60_000);
        _timer.Elapsed += (_, _) => Tick();
        _timer.AutoReset = true;
        _timer.Start();

        var activeCount = _state.Events.Count(e => e.Status == EventStatus.Active);
        var scheduledCount = _state.Events.Count(e => e.Status == EventStatus.Scheduled);
        var taskCount = _state.Tasks.Count(t => t.Enabled);
        logger.Info($"[ZSlayerHQ] Scheduler: Initialized — {activeCount} active, {scheduledCount} scheduled events, {taskCount} tasks");
    }

    // ═══════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ═══════════════════════════════════════════════════════════════

    public SchedulerOverviewResponse GetOverview()
    {
        lock (_lock)
        {
            return new SchedulerOverviewResponse
            {
                Events = _state.Events.OrderByDescending(e => e.CreatedAt).ToList(),
                Tasks = _state.Tasks.OrderByDescending(t => t.CreatedAt).ToList(),
                ActiveEvents = _state.Events.Where(e => e.Status == EventStatus.Active).ToList(),
                Templates = Templates
            };
        }
    }

    public List<ServerEvent> GetActiveEvents()
    {
        lock (_lock)
        {
            return _state.Events.Where(e => e.Status == EventStatus.Active).ToList();
        }
    }

    public ServerEvent? GetEvent(string id)
    {
        lock (_lock) { return _state.Events.FirstOrDefault(e => e.Id == id); }
    }

    public ServerEvent CreateEvent(CreateEventRequest req, string adminSessionId)
    {
        lock (_lock)
        {
            if (!string.IsNullOrEmpty(req.CronExpression) && !CronParser.IsValid(req.CronExpression))
                throw new ArgumentException($"Invalid cron expression: {req.CronExpression}");

            var evt = new ServerEvent
            {
                Name = string.IsNullOrWhiteSpace(req.Name) ? GetDefaultEventName(req.Type) : req.Name,
                Type = req.Type,
                StartTime = req.StartTime,
                EndTime = req.EndTime,
                CronExpression = req.CronExpression,
                DurationMinutes = req.DurationMinutes > 0 ? req.DurationMinutes : 60,
                Multiplier = req.Multiplier,
                TargetIds = req.TargetIds ?? [],
                BroadcastOnStart = req.BroadcastOnStart,
                BroadcastOnEnd = req.BroadcastOnEnd,
                CustomStartMessage = req.CustomStartMessage,
                CustomEndMessage = req.CustomEndMessage,
                CreatedBy = adminSessionId,
                Status = req.ActivateNow ? EventStatus.Active : EventStatus.Scheduled
            };

            if (req.ActivateNow)
            {
                evt.ActivatedAt = DateTime.UtcNow;
                evt.ExpiresAt = evt.EndTime ?? DateTime.UtcNow.AddMinutes(evt.DurationMinutes);
                ApplyEventEffects(evt);
                BroadcastEventStart(evt);
                activityLogService.LogAction(ActionType.EventStart, adminSessionId,
                    $"Event '{evt.Name}' activated ({evt.Type}, {evt.Multiplier}x)");
            }

            _state.Events.Add(evt);
            SaveState();
            return evt;
        }
    }

    public ServerEvent? ActivateEvent(string id, string adminSessionId)
    {
        lock (_lock)
        {
            var evt = _state.Events.FirstOrDefault(e => e.Id == id);
            if (evt == null || evt.Status == EventStatus.Active) return evt;

            evt.Status = EventStatus.Active;
            evt.ActivatedAt = DateTime.UtcNow;
            evt.ExpiresAt = evt.EndTime ?? DateTime.UtcNow.AddMinutes(evt.DurationMinutes);

            ApplyEventEffects(evt);
            BroadcastEventStart(evt);
            SaveState();

            activityLogService.LogAction(ActionType.EventStart, adminSessionId,
                $"Event '{evt.Name}' activated ({evt.Type}, {evt.Multiplier}x)");

            return evt;
        }
    }

    public ServerEvent? DeactivateEvent(string id, string adminSessionId)
    {
        lock (_lock)
        {
            var evt = _state.Events.FirstOrDefault(e => e.Id == id);
            if (evt == null || evt.Status != EventStatus.Active) return evt;

            evt.Status = EventStatus.Expired;
            RemoveEventEffects(evt);
            BroadcastEventEnd(evt);
            SaveState();

            activityLogService.LogAction(ActionType.EventEnd, adminSessionId,
                $"Event '{evt.Name}' ended ({evt.Type})");

            return evt;
        }
    }

    public ServerEvent? CancelEvent(string id, string adminSessionId)
    {
        lock (_lock)
        {
            var evt = _state.Events.FirstOrDefault(e => e.Id == id);
            if (evt == null) return null;

            if (evt.Status == EventStatus.Active)
            {
                RemoveEventEffects(evt);
                BroadcastEventEnd(evt);
            }

            evt.Status = EventStatus.Cancelled;
            SaveState();

            activityLogService.LogAction(ActionType.EventCancel, adminSessionId,
                $"Event '{evt.Name}' cancelled ({evt.Type})");

            return evt;
        }
    }

    public bool DeleteEvent(string id)
    {
        lock (_lock)
        {
            var evt = _state.Events.FirstOrDefault(e => e.Id == id);
            if (evt == null) return false;

            if (evt.Status == EventStatus.Active)
                RemoveEventEffects(evt);

            _state.Events.Remove(evt);
            SaveState();
            return true;
        }
    }

    // ── Tasks ──

    public ScheduledTask CreateTask(CreateTaskRequest req)
    {
        lock (_lock)
        {
            if (!string.IsNullOrEmpty(req.CronExpression) && !CronParser.IsValid(req.CronExpression))
                throw new ArgumentException($"Invalid cron expression: {req.CronExpression}");

            var task = new ScheduledTask
            {
                Name = string.IsNullOrWhiteSpace(req.Name) ? req.Type.ToString() : req.Name,
                Type = req.Type,
                CronExpression = req.CronExpression,
                RunAt = req.RunAt,
                Message = req.Message
            };

            ComputeNextRun(task);
            _state.Tasks.Add(task);
            SaveState();
            return task;
        }
    }

    public ScheduledTask? ToggleTask(string id, bool enabled)
    {
        lock (_lock)
        {
            var task = _state.Tasks.FirstOrDefault(t => t.Id == id);
            if (task == null) return null;

            task.Enabled = enabled;
            if (enabled) ComputeNextRun(task);
            SaveState();
            return task;
        }
    }

    public bool DeleteTask(string id)
    {
        lock (_lock)
        {
            var task = _state.Tasks.FirstOrDefault(t => t.Id == id);
            if (task == null) return false;
            _state.Tasks.Remove(task);
            SaveState();
            return true;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  BACKGROUND TICK
    // ═══════════════════════════════════════════════════════════════

    private void Tick()
    {
        lock (_lock)
        {
            try
            {
                var now = DateTime.UtcNow;
                var dirty = false;

                // Check scheduled events that should start
                foreach (var evt in _state.Events.Where(e => e.Status == EventStatus.Scheduled).ToList())
                {
                    var shouldStart = false;

                    if (evt.StartTime.HasValue && evt.StartTime.Value <= now)
                        shouldStart = true;
                    else if (!string.IsNullOrEmpty(evt.CronExpression))
                    {
                        var next = CronParser.GetNextOccurrence(evt.CronExpression, evt.ActivatedAt ?? evt.CreatedAt);
                        if (next.HasValue && next.Value <= now)
                            shouldStart = true;
                    }

                    if (shouldStart)
                    {
                        evt.Status = EventStatus.Active;
                        evt.ActivatedAt = now;
                        evt.ExpiresAt = evt.EndTime ?? now.AddMinutes(evt.DurationMinutes);
                        ApplyEventEffects(evt);
                        BroadcastEventStart(evt);
                        logger.Info($"[ZSlayerHQ] Scheduler: Event '{evt.Name}' auto-started");
                        activityLogService.LogAction(ActionType.EventStart, "",
                            $"Scheduled event '{evt.Name}' auto-started ({evt.Type}, {evt.Multiplier}x)");
                        dirty = true;
                    }
                }

                // Check active events that should expire
                foreach (var evt in _state.Events.Where(e => e.Status == EventStatus.Active).ToList())
                {
                    if (evt.ExpiresAt.HasValue && evt.ExpiresAt.Value <= now)
                    {
                        // If recurring (has cron), reschedule instead of expire
                        if (!string.IsNullOrEmpty(evt.CronExpression))
                        {
                            RemoveEventEffects(evt);
                            BroadcastEventEnd(evt);
                            evt.Status = EventStatus.Scheduled;
                            evt.ActivatedAt = null;
                            evt.ExpiresAt = null;
                            logger.Info($"[ZSlayerHQ] Scheduler: Recurring event '{evt.Name}' ended, re-scheduled");
                        }
                        else
                        {
                            evt.Status = EventStatus.Expired;
                            RemoveEventEffects(evt);
                            BroadcastEventEnd(evt);
                            logger.Info($"[ZSlayerHQ] Scheduler: Event '{evt.Name}' expired");
                        }

                        activityLogService.LogAction(ActionType.EventEnd, "",
                            $"Event '{evt.Name}' ended ({evt.Type})");
                        dirty = true;
                    }
                }

                // Check scheduled tasks that should run
                foreach (var task in _state.Tasks.Where(t => t.Enabled).ToList())
                {
                    if (task.NextRun.HasValue && task.NextRun.Value <= now)
                    {
                        ExecuteTask(task);
                        task.LastRun = now;

                        // One-time tasks: disable after execution
                        if (task.RunAt.HasValue && string.IsNullOrEmpty(task.CronExpression))
                            task.Enabled = false;
                        else
                            ComputeNextRun(task);

                        dirty = true;
                    }
                }

                if (dirty) SaveState();
            }
            catch (Exception ex)
            {
                logger.Error($"[ZSlayerHQ] Scheduler tick error: {ex.Message}");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  EVENT EFFECTS
    // ═══════════════════════════════════════════════════════════════

    private void ApplyEventEffects(ServerEvent evt)
    {
        RecalculateAllEventFactors();
    }

    private void RemoveEventEffects(ServerEvent evt)
    {
        RecalculateAllEventFactors();
    }

    /// <summary>
    /// Recalculate combined event factors from ALL active events and push to services.
    /// This handles multiplicative stacking correctly regardless of activation/deactivation order.
    /// </summary>
    private void RecalculateAllEventFactors()
    {
        var activeEvents = _state.Events.Where(e => e.Status == EventStatus.Active).ToList();

        double xpFactor = 1.0;
        double lootFactor = 1.0;
        double traderPriceFactor = 1.0;

        // Map-specific factors
        var mapLootFactors = new Dictionary<string, double>();
        var mapBossChances = new Dictionary<string, double>();
        string? mapOfTheDay = null;
        double mapOfTheDayMult = 1.0;

        foreach (var evt in activeEvents)
        {
            switch (evt.Type)
            {
                case EventType.DoubleXP:
                    xpFactor *= evt.Multiplier;
                    break;
                case EventType.LootBoost:
                    lootFactor *= evt.Multiplier;
                    break;
                case EventType.TraderSale:
                    traderPriceFactor *= evt.Multiplier;
                    break;
                case EventType.MapLootBoost:
                {
                    var targets = evt.TargetIds.Count > 0 ? evt.TargetIds : locationService.GetPlayableLocationIds().ToList();
                    foreach (var mapId in targets)
                    {
                        if (!mapLootFactors.ContainsKey(mapId)) mapLootFactors[mapId] = 1.0;
                        mapLootFactors[mapId] *= evt.Multiplier;
                    }
                    break;
                }
                case EventType.MapBossRush:
                {
                    var targets = evt.TargetIds.Count > 0 ? evt.TargetIds : locationService.GetPlayableLocationIds().ToList();
                    foreach (var mapId in targets)
                        mapBossChances[mapId] = evt.Multiplier; // replace (last wins for overlapping)
                    break;
                }
                case EventType.MapOfTheDay:
                {
                    // Deterministic daily seed — excludes factory/sandbox
                    var eligibleMaps = locationService.GetPlayableLocationIds()
                        .Where(m => m is not ("factory4_day" or "factory4_night" or "sandbox" or "sandbox_high")).ToArray();
                    if (eligibleMaps.Length > 0)
                    {
                        var dayHash = DateTime.UtcNow.Date.GetHashCode();
                        var idx = ((dayHash % eligibleMaps.Length) + eligibleMaps.Length) % eligibleMaps.Length;
                        mapOfTheDay = eligibleMaps[idx];
                        mapOfTheDayMult = evt.Multiplier;
                    }
                    break;
                }
            }
        }

        // Push factors to services and re-apply
        progressionControlService.SetEventFactors(xpFactor, lootFactor);
        traderApplyService.SetEventPriceFactor(traderPriceFactor);
        locationService.SetEventMapFactors(mapLootFactors, mapBossChances, mapOfTheDay, mapOfTheDayMult);
    }

    // ═══════════════════════════════════════════════════════════════
    //  TASK EXECUTION
    // ═══════════════════════════════════════════════════════════════

    private void ExecuteTask(ScheduledTask task)
    {
        try
        {
            switch (task.Type)
            {
                case TaskType.Broadcast:
                    if (!string.IsNullOrWhiteSpace(task.Message))
                    {
                        playerMailService.BroadcastMail("", new BroadcastMailRequest { Message = task.Message });
                        logger.Info($"[ZSlayerHQ] Scheduler: Broadcast sent — {task.Message}");
                    }
                    break;

                case TaskType.Backup:
                    // Backup is handled by ProfileBackupService — trigger it
                    logger.Info("[ZSlayerHQ] Scheduler: Backup task triggered");
                    activityLogService.LogAction(ActionType.ScheduledTask, "", $"Scheduled backup: '{task.Name}'");
                    break;

                case TaskType.ServerRestart:
                    logger.Info("[ZSlayerHQ] Scheduler: Server restart task — not implemented (requires watchdog)");
                    activityLogService.LogAction(ActionType.ScheduledTask, "", $"Scheduled restart: '{task.Name}' (requires watchdog)");
                    break;

                case TaskType.HeadlessRestart:
                    logger.Info("[ZSlayerHQ] Scheduler: Headless restart task — not implemented (requires watchdog)");
                    activityLogService.LogAction(ActionType.ScheduledTask, "", $"Scheduled headless restart: '{task.Name}' (requires watchdog)");
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.Error($"[ZSlayerHQ] Scheduler: Task '{task.Name}' failed: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  NOTIFICATIONS
    // ═══════════════════════════════════════════════════════════════

    private void BroadcastEventStart(ServerEvent evt)
    {
        if (!evt.BroadcastOnStart) return;
        try
        {
            var msg = evt.CustomStartMessage ??
                      $"Event Started: {evt.Name} — {GetEventDescription(evt)} for {FormatDuration(evt.DurationMinutes)}!";
            playerMailService.BroadcastMail("", new BroadcastMailRequest { Message = msg });
        }
        catch (Exception ex)
        {
            logger.Error($"[ZSlayerHQ] Scheduler: Failed to broadcast event start: {ex.Message}");
        }
    }

    private void BroadcastEventEnd(ServerEvent evt)
    {
        if (!evt.BroadcastOnEnd) return;
        try
        {
            var msg = evt.CustomEndMessage ??
                      $"Event Ended: {evt.Name} has concluded. Settings have been restored.";
            playerMailService.BroadcastMail("", new BroadcastMailRequest { Message = msg });
        }
        catch (Exception ex)
        {
            logger.Error($"[ZSlayerHQ] Scheduler: Failed to broadcast event end: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════

    private static void ComputeNextRun(ScheduledTask task)
    {
        if (!string.IsNullOrEmpty(task.CronExpression))
        {
            task.NextRun = CronParser.GetNextOccurrence(task.CronExpression, task.LastRun ?? DateTime.UtcNow);
        }
        else if (task.RunAt.HasValue && task.RunAt.Value > DateTime.UtcNow)
        {
            task.NextRun = task.RunAt;
        }
        else
        {
            task.NextRun = null;
        }
    }

    private static string GetDefaultEventName(EventType type) => type switch
    {
        EventType.DoubleXP => "Double XP",
        EventType.TraderSale => "Trader Sale",
        EventType.LootBoost => "Loot Boost",
        EventType.MapLootBoost => "Map Loot Boost",
        EventType.MapBossRush => "Map Boss Rush",
        EventType.MapOfTheDay => "Map of the Day",
        _ => type.ToString()
    };

    private static string GetEventDescription(ServerEvent evt) => evt.Type switch
    {
        EventType.DoubleXP => $"{evt.Multiplier}x XP gains",
        EventType.TraderSale => $"{(1 - evt.Multiplier) * 100:F0}% off trader prices",
        EventType.LootBoost => $"{evt.Multiplier}x loot spawns",
        EventType.MapLootBoost => $"{evt.Multiplier}x loot on {(evt.TargetIds.Count > 0 ? string.Join(", ", evt.TargetIds) : "all maps")}",
        EventType.MapBossRush => $"{evt.Multiplier}% boss chance on {(evt.TargetIds.Count > 0 ? string.Join(", ", evt.TargetIds) : "all maps")}",
        EventType.MapOfTheDay => $"{evt.Multiplier}x loot on featured map",
        _ => evt.Type.ToString()
    };

    private static string FormatDuration(int minutes)
    {
        if (minutes < 60) return $"{minutes}m";
        if (minutes < 1440) return $"{minutes / 60}h {minutes % 60}m";
        var days = minutes / 1440;
        var hrs = (minutes % 1440) / 60;
        return hrs > 0 ? $"{days}d {hrs}h" : $"{days}d";
    }

    // ═══════════════════════════════════════════════════════════════
    //  PERSISTENCE
    // ═══════════════════════════════════════════════════════════════

    private string StatePath => Path.Combine(_dataDir, "scheduler-state.json");

    private void LoadState()
    {
        try
        {
            if (File.Exists(StatePath))
            {
                var json = File.ReadAllText(StatePath);
                _state = JsonSerializer.Deserialize<SchedulerState>(json, JsonOpts) ?? new SchedulerState();
            }
        }
        catch (Exception ex)
        {
            logger.Error($"[ZSlayerHQ] Scheduler: Failed to load state: {ex.Message}");
            _state = new SchedulerState();
        }
    }

    private void SaveState()
    {
        try
        {
            File.WriteAllText(StatePath, JsonSerializer.Serialize(_state, JsonOpts));
        }
        catch (Exception ex)
        {
            logger.Error($"[ZSlayerHQ] Scheduler: Failed to save state: {ex.Message}");
        }
    }
}
