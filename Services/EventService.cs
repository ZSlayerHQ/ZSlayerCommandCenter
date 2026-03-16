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
    PlayerStatsService playerStatsService,
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
            Description = "Reduce trader buy prices by a percentage. Target specific traders, cycle randomly, or affect all.",
            DefaultMultiplier = 0.5,
            Icon = "sale",
            SupportsTargets = true,
            TargetLabel = "Traders (empty = all)",
            MultiplierLabel = "Price Factor (0.5 = 50% off)",
            SupportsCycleRandom = true
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
        },
        new()
        {
            Type = EventType.InsuranceExpress,
            Name = "Insurance Express",
            Description = "Reduce insurance return times. Lower factor = faster returns (0.25 = 4x faster).",
            DefaultMultiplier = 0.25,
            Icon = "insurance",
            SupportsTargets = false,
            MultiplierLabel = "Return Time Factor (0.25 = 4x faster)",
            IsInverse = true
        },
        new()
        {
            Type = EventType.SkillBoost,
            Name = "Skill Boost",
            Description = "Increase skill leveling speed for all or specific skills.",
            DefaultMultiplier = 2.0,
            Icon = "skillboost",
            SupportsTargets = true,
            TargetLabel = "Skills (empty = all)",
            MultiplierLabel = "Skill Speed Multiplier",
            SupportsCycleRandom = false
        },
        new()
        {
            Type = EventType.AirdropFrenzy,
            Name = "Airdrop Frenzy",
            Description = "Massively increase airdrop chances on specific maps.",
            DefaultMultiplier = 3.0,
            Icon = "airdrop",
            SupportsTargets = true,
            TargetLabel = "Maps (empty = all)",
            MultiplierLabel = "Airdrop Chance Multiplier",
            SupportsExclusion = true
        },
        new()
        {
            Type = EventType.HardcoreMode,
            Name = "Hardcore Mode",
            Description = "Reduced loot, 100% boss spawns, no insurance returns. The ultimate challenge.",
            DefaultMultiplier = 2.0,
            Icon = "hardcore",
            SupportsTargets = false,
            MultiplierLabel = "Difficulty Factor",
            IsInverse = true
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
                Templates = Templates,
                SavedTemplates = _state.SavedTemplates.OrderByDescending(t => t.CreatedAt).ToList(),
                RecentHistory = _state.History.OrderByDescending(h => h.EndedAt).Take(20).ToList()
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
                CycleRandomTarget = req.CycleRandomTarget,
                BroadcastOnStart = req.BroadcastOnStart,
                BroadcastOnEnd = req.BroadcastOnEnd,
                CustomStartMessage = req.CustomStartMessage,
                CustomEndMessage = req.CustomEndMessage,
                StackingMode = req.StackingMode,
                WeekdayFilter = req.WeekdayFilter,
                MinPlayersOnline = req.MinPlayersOnline,
                MaxActivations = req.MaxActivations,
                WarmupMinutes = req.WarmupMinutes,
                CooldownMinutes = req.CooldownMinutes,
                MultiplierMin = req.MultiplierMin,
                MultiplierMax = req.MultiplierMax,
                NextEventId = req.NextEventId,
                ExcludeTargetIds = req.ExcludeTargetIds ?? [],
                CreatedBy = adminSessionId,
                Status = req.ActivateNow ? EventStatus.Active : EventStatus.Scheduled
            };

            if (req.ActivateNow)
            {
                PerformActivation(evt, "created-now");
                activityLogService.LogAction(ActionType.EventStart, adminSessionId,
                    $"Event '{evt.Name}' activated ({evt.Type}, {GetEffectiveMultiplierDisplay(evt)}x)");
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

            // Max activations check
            if (evt.MaxActivations.HasValue && evt.ActivationCount >= evt.MaxActivations.Value)
                return evt;

            PerformActivation(evt, "manual");

            SaveState();

            activityLogService.LogAction(ActionType.EventStart, adminSessionId,
                $"Event '{evt.Name}' activated ({evt.Type}, {GetEffectiveMultiplierDisplay(evt)}x)");

            return evt;
        }
    }

    public ServerEvent? DeactivateEvent(string id, string adminSessionId)
    {
        lock (_lock)
        {
            var evt = _state.Events.FirstOrDefault(e => e.Id == id);
            if (evt == null || evt.Status != EventStatus.Active) return evt;

            PerformDeactivation(evt, "manual");
            TriggerChainedEvent(evt);
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
                PerformDeactivation(evt, "cancelled");
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

    private int _onlineCountCache;
    private DateTime _onlineCountCacheTime = DateTime.MinValue;

    private int GetOnlineCount()
    {
        var now = DateTime.UtcNow;
        if ((now - _onlineCountCacheTime).TotalSeconds < 30)
            return _onlineCountCache;

        try
        {
            var overview = playerStatsService.GetPlayerOverview();
            _onlineCountCache = overview.OnlineCount;
        }
        catch { _onlineCountCache = 0; }
        _onlineCountCacheTime = now;
        return _onlineCountCache;
    }

    private bool PassesWeekdayFilter(WeekdayFilter filter)
    {
        if (filter == WeekdayFilter.None) return true;
        var dow = DateTime.UtcNow.DayOfWeek;
        var isWeekend = dow is DayOfWeek.Saturday or DayOfWeek.Sunday;
        return filter == WeekdayFilter.WeekendsOnly ? isWeekend : !isWeekend;
    }

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

                    if (!shouldStart) continue;

                    // Weekday filter
                    if (!PassesWeekdayFilter(evt.WeekdayFilter))
                        continue;

                    // Player threshold
                    if (evt.MinPlayersOnline.HasValue && GetOnlineCount() < evt.MinPlayersOnline.Value)
                        continue;

                    // Max activations
                    if (evt.MaxActivations.HasValue && evt.ActivationCount >= evt.MaxActivations.Value)
                    {
                        evt.Status = EventStatus.Expired;
                        evt.CronExpression = null; // stop recurring
                        logger.Info($"[ZSlayerHQ] Scheduler: Event '{evt.Name}' reached max activations ({evt.MaxActivations})");
                        dirty = true;
                        continue;
                    }

                    PerformActivation(evt, "scheduled");
                    logger.Info($"[ZSlayerHQ] Scheduler: Event '{evt.Name}' auto-started");
                    activityLogService.LogAction(ActionType.EventStart, "",
                        $"Scheduled event '{evt.Name}' auto-started ({evt.Type}, {GetEffectiveMultiplierDisplay(evt)}x)");
                    dirty = true;
                }

                // Check active events that should expire
                foreach (var evt in _state.Events.Where(e => e.Status == EventStatus.Active).ToList())
                {
                    if (evt.ExpiresAt.HasValue && evt.ExpiresAt.Value <= now)
                    {
                        // If recurring (has cron), reschedule instead of expire
                        if (!string.IsNullOrEmpty(evt.CronExpression))
                        {
                            PerformDeactivation(evt, "recurring-cycle");

                            // Cycle random target for next activation
                            if (evt.CycleRandomTarget && evt.Type == EventType.TraderSale)
                            {
                                var allTraders = traderApplyService.GetAllTraderIds();
                                if (allTraders.Count > 0)
                                {
                                    var rng = new Random();
                                    var pick = allTraders[rng.Next(allTraders.Count)];
                                    evt.TargetIds = [pick];
                                    logger.Info($"[ZSlayerHQ] Scheduler: Cycled trader sale to '{pick}' for next run");
                                }
                            }

                            // Check if max activations reached after this cycle
                            if (evt.MaxActivations.HasValue && evt.ActivationCount >= evt.MaxActivations.Value)
                            {
                                evt.Status = EventStatus.Expired;
                                evt.CronExpression = null;
                                logger.Info($"[ZSlayerHQ] Scheduler: Recurring event '{evt.Name}' reached max activations");
                            }
                            else
                            {
                                evt.Status = EventStatus.Scheduled;
                                evt.ActivatedAt = null;
                                evt.ExpiresAt = null;
                                evt.ResolvedMultiplier = null;
                                logger.Info($"[ZSlayerHQ] Scheduler: Recurring event '{evt.Name}' ended, re-scheduled");
                            }
                        }
                        else
                        {
                            PerformDeactivation(evt, "expired");
                            evt.Status = EventStatus.Expired;
                            logger.Info($"[ZSlayerHQ] Scheduler: Event '{evt.Name}' expired");
                        }

                        TriggerChainedEvent(evt);

                        activityLogService.LogAction(ActionType.EventEnd, "",
                            $"Event '{evt.Name}' ended ({evt.Type})");
                        dirty = true;
                    }
                }

                // Recalculate factors if any active events have warmup/cooldown (interpolation changes over time)
                if (_state.Events.Any(e => e.Status == EventStatus.Active && (e.WarmupMinutes > 0 || e.CooldownMinutes > 0)))
                    RecalculateAllEventFactors();

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

    // ── Activation / Deactivation helpers ──

    private void PerformActivation(ServerEvent evt, string triggerReason)
    {
        evt.Status = EventStatus.Active;
        evt.ActivatedAt = DateTime.UtcNow;
        evt.ExpiresAt = evt.EndTime ?? DateTime.UtcNow.AddMinutes(evt.DurationMinutes);
        evt.ActivationCount++;

        // Roll random multiplier if range specified
        if (evt.MultiplierMin.HasValue && evt.MultiplierMax.HasValue)
        {
            var rng = new Random();
            evt.ResolvedMultiplier = evt.MultiplierMin.Value +
                rng.NextDouble() * (evt.MultiplierMax.Value - evt.MultiplierMin.Value);
            evt.ResolvedMultiplier = Math.Round(evt.ResolvedMultiplier.Value, 2);
        }

        RecalculateAllEventFactors();
        BroadcastEventStart(evt);
    }

    private void PerformDeactivation(ServerEvent evt, string triggerReason)
    {
        RecordHistory(evt, triggerReason);
        evt.Status = EventStatus.Expired;
        RemoveEventEffects(evt);
        BroadcastEventEnd(evt);
    }

    private void TriggerChainedEvent(ServerEvent evt)
    {
        if (string.IsNullOrEmpty(evt.NextEventId)) return;
        var next = _state.Events.FirstOrDefault(e => e.Id == evt.NextEventId);
        if (next == null || next.Status == EventStatus.Active) return;
        if (next.Status is EventStatus.Scheduled or EventStatus.Draft)
        {
            PerformActivation(next, $"chained-from:{evt.Id}");
            logger.Info($"[ZSlayerHQ] Scheduler: Chained event '{next.Name}' auto-started from '{evt.Name}'");
            activityLogService.LogAction(ActionType.EventStart, "",
                $"Chained event '{next.Name}' auto-started from '{evt.Name}'");
        }
    }

    private void RecordHistory(ServerEvent evt, string reason)
    {
        _state.History.Add(new EventHistoryEntry
        {
            EventId = evt.Id,
            Name = evt.Name,
            Type = evt.Type,
            Multiplier = evt.ResolvedMultiplier ?? evt.Multiplier,
            StartedAt = evt.ActivatedAt ?? evt.CreatedAt,
            EndedAt = DateTime.UtcNow,
            TriggerReason = reason
        });

        // Cap history at 500
        while (_state.History.Count > 500)
            _state.History.RemoveAt(0);
    }

    private static double GetEffectiveMultiplierDisplay(ServerEvent evt)
        => evt.ResolvedMultiplier ?? evt.Multiplier;

    /// <summary>
    /// Get the effective multiplier for an event, accounting for warmup/cooldown interpolation.
    /// </summary>
    private static double GetEffectiveMultiplier(ServerEvent evt)
    {
        var baseMult = evt.ResolvedMultiplier ?? evt.Multiplier;
        if (evt.Status != EventStatus.Active || !evt.ActivatedAt.HasValue || !evt.ExpiresAt.HasValue)
            return baseMult;

        var now = DateTime.UtcNow;
        var totalDuration = (evt.ExpiresAt.Value - evt.ActivatedAt.Value).TotalMinutes;

        // Warmup: interpolate from 1.0 to baseMult over warmup period
        if (evt.WarmupMinutes > 0)
        {
            var elapsed = (now - evt.ActivatedAt.Value).TotalMinutes;
            if (elapsed < evt.WarmupMinutes)
            {
                var t = elapsed / evt.WarmupMinutes;
                return 1.0 + (baseMult - 1.0) * t;
            }
        }

        // Cooldown: interpolate from baseMult to 1.0 over cooldown period at end
        if (evt.CooldownMinutes > 0)
        {
            var remaining = (evt.ExpiresAt.Value - now).TotalMinutes;
            if (remaining < evt.CooldownMinutes)
            {
                var t = remaining / evt.CooldownMinutes;
                return 1.0 + (baseMult - 1.0) * t;
            }
        }

        return baseMult;
    }

    private void ApplyEventEffects(ServerEvent evt)
    {
        RecalculateAllEventFactors();
    }

    private void RemoveEventEffects(ServerEvent evt)
    {
        RecalculateAllEventFactors();
    }

    /// <summary>
    /// Apply stacking logic for a group of events of the same type.
    /// Returns the combined factor.
    /// </summary>
    private static double ApplyStacking(List<(double mult, StackingMode mode)> values)
    {
        if (values.Count == 0) return 1.0;

        // Group by mode priority: Override > Max > Multiply
        var overrides = values.Where(v => v.mode == StackingMode.Override).ToList();
        if (overrides.Count > 0) return overrides[^1].mult; // last override wins

        var maxes = values.Where(v => v.mode == StackingMode.Max).ToList();
        var multiplies = values.Where(v => v.mode == StackingMode.Multiply).ToList();

        double result = 1.0;
        foreach (var (mult, _) in multiplies)
            result *= mult;

        if (maxes.Count > 0)
            result = Math.Max(result, maxes.Max(v => v.mult));

        return result;
    }

    /// <summary>
    /// Recalculate combined event factors from ALL active events and push to services.
    /// Handles multiplicative stacking, warmup/cooldown, and all event types.
    /// </summary>
    private void RecalculateAllEventFactors()
    {
        var activeEvents = _state.Events.Where(e => e.Status == EventStatus.Active).ToList();

        // Collect stacking groups
        var xpValues = new List<(double mult, StackingMode mode)>();
        var lootValues = new List<(double mult, StackingMode mode)>();
        var skillValues = new List<(double mult, StackingMode mode)>();
        var insuranceTimeValues = new List<(double mult, StackingMode mode)>();
        var insuranceChanceValues = new List<(double mult, StackingMode mode)>();

        double traderPriceFactor = 1.0;
        var perTraderPriceFactors = new Dictionary<string, double>();

        // Map-specific factors
        var mapLootFactors = new Dictionary<string, double>();
        var mapBossChances = new Dictionary<string, double>();
        var mapAirdropFactors = new Dictionary<string, double>();
        string? mapOfTheDay = null;
        double mapOfTheDayMult = 1.0;

        foreach (var evt in activeEvents)
        {
            var mult = GetEffectiveMultiplier(evt);
            var targets = evt.TargetIds.Except(evt.ExcludeTargetIds).ToList();

            switch (evt.Type)
            {
                case EventType.DoubleXP:
                    xpValues.Add((mult, evt.StackingMode));
                    break;

                case EventType.LootBoost:
                    lootValues.Add((mult, evt.StackingMode));
                    break;

                case EventType.TraderSale:
                {
                    if (targets.Count > 0)
                    {
                        foreach (var traderId in targets)
                        {
                            if (!perTraderPriceFactors.ContainsKey(traderId))
                                perTraderPriceFactors[traderId] = 1.0;
                            perTraderPriceFactors[traderId] *= mult;
                        }
                    }
                    else
                    {
                        traderPriceFactor *= mult;
                    }
                    break;
                }

                case EventType.MapLootBoost:
                {
                    var mapTargets = targets.Count > 0 ? targets : locationService.GetPlayableLocationIds().ToList();
                    foreach (var mapId in mapTargets)
                    {
                        if (!mapLootFactors.ContainsKey(mapId)) mapLootFactors[mapId] = 1.0;
                        mapLootFactors[mapId] *= mult;
                    }
                    break;
                }

                case EventType.MapBossRush:
                {
                    var mapTargets = targets.Count > 0 ? targets : locationService.GetPlayableLocationIds().ToList();
                    foreach (var mapId in mapTargets)
                        mapBossChances[mapId] = mult;
                    break;
                }

                case EventType.MapOfTheDay:
                {
                    var eligibleMaps = locationService.GetPlayableLocationIds()
                        .Where(m => m is not ("factory4_day" or "factory4_night" or "sandbox" or "sandbox_high")).ToArray();
                    if (eligibleMaps.Length > 0)
                    {
                        var dayHash = DateTime.UtcNow.Date.GetHashCode();
                        var idx = ((dayHash % eligibleMaps.Length) + eligibleMaps.Length) % eligibleMaps.Length;
                        mapOfTheDay = eligibleMaps[idx];
                        mapOfTheDayMult = mult;
                        evt.ResolvedTarget = mapOfTheDay; // expose resolved map to UI
                    }
                    break;
                }

                case EventType.InsuranceExpress:
                    insuranceTimeValues.Add((mult, evt.StackingMode));
                    break;

                case EventType.SkillBoost:
                    skillValues.Add((mult, evt.StackingMode));
                    break;

                case EventType.AirdropFrenzy:
                {
                    var mapTargets = targets.Count > 0 ? targets : locationService.GetPlayableLocationIds().ToList();
                    foreach (var mapId in mapTargets)
                    {
                        if (!mapAirdropFactors.ContainsKey(mapId)) mapAirdropFactors[mapId] = 1.0;
                        mapAirdropFactors[mapId] *= mult;
                    }
                    break;
                }

                case EventType.HardcoreMode:
                {
                    // Divide loot by factor
                    lootValues.Add((1.0 / Math.Max(0.01, mult), evt.StackingMode));
                    // 100% bosses on all maps
                    foreach (var mapId in locationService.GetPlayableLocationIds())
                        mapBossChances[mapId] = 100.0;
                    // Zero insurance return chance
                    insuranceChanceValues.Add((0.0, StackingMode.Override));
                    break;
                }
            }
        }

        // Build EventFactors bag
        var factors = new EventFactors
        {
            XpFactor = ApplyStacking(xpValues),
            LootFactor = ApplyStacking(lootValues),
            SkillSpeedFactor = ApplyStacking(skillValues),
            InsuranceReturnTimeFactor = ApplyStacking(insuranceTimeValues),
            InsuranceReturnChanceFactor = insuranceChanceValues.Count > 0 ? ApplyStacking(insuranceChanceValues) : 1.0,
            AirdropChanceFactor = 1.0 // per-map handled separately
        };

        // Push factors to services and re-apply
        progressionControlService.SetEventFactors(factors);
        traderApplyService.SetEventPriceFactor(traderPriceFactor,
            perTraderPriceFactors.Count > 0 ? perTraderPriceFactors : null);
        locationService.SetEventMapFactors(mapLootFactors, mapBossChances, mapOfTheDay, mapOfTheDayMult, mapAirdropFactors);
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
        EventType.InsuranceExpress => "Insurance Express",
        EventType.SkillBoost => "Skill Boost",
        EventType.AirdropFrenzy => "Airdrop Frenzy",
        EventType.HardcoreMode => "Hardcore Mode",
        _ => type.ToString()
    };

    private static string GetEventDescription(ServerEvent evt)
    {
        var mult = evt.ResolvedMultiplier ?? evt.Multiplier;
        return evt.Type switch
        {
            EventType.DoubleXP => $"{mult}x XP gains",
            EventType.TraderSale => evt.TargetIds.Count > 0
                ? $"{(1 - mult) * 100:F0}% off {string.Join(", ", evt.TargetIds)}"
                : $"{(1 - mult) * 100:F0}% off all trader prices",
            EventType.LootBoost => $"{mult}x loot spawns",
            EventType.MapLootBoost => $"{mult}x loot on {(evt.TargetIds.Count > 0 ? string.Join(", ", evt.TargetIds) : "all maps")}",
            EventType.MapBossRush => $"{mult}% boss chance on {(evt.TargetIds.Count > 0 ? string.Join(", ", evt.TargetIds) : "all maps")}",
            EventType.MapOfTheDay => $"{mult}x loot on featured map",
            EventType.InsuranceExpress => $"{mult}x insurance return time ({1.0 / Math.Max(0.01, mult):F1}x faster)",
            EventType.SkillBoost => $"{mult}x skill speed{(evt.TargetIds.Count > 0 ? $" ({string.Join(", ", evt.TargetIds)})" : "")}",
            EventType.AirdropFrenzy => $"{mult}x airdrop chance on {(evt.TargetIds.Count > 0 ? string.Join(", ", evt.TargetIds) : "all maps")}",
            EventType.HardcoreMode => $"Hardcore: {mult}x difficulty (reduced loot, max bosses, no insurance)",
            _ => evt.Type.ToString()
        };
    }

    private static string FormatDuration(int minutes)
    {
        if (minutes < 60) return $"{minutes}m";
        if (minutes < 1440) return $"{minutes / 60}h {minutes % 60}m";
        var days = minutes / 1440;
        var hrs = (minutes % 1440) / 60;
        return hrs > 0 ? $"{days}d {hrs}h" : $"{days}d";
    }

    // ═══════════════════════════════════════════════════════════════
    //  HISTORY
    // ═══════════════════════════════════════════════════════════════

    public List<EventHistoryEntry> GetHistory(int limit = 50, EventType? typeFilter = null)
    {
        lock (_lock)
        {
            IEnumerable<EventHistoryEntry> query = _state.History.OrderByDescending(h => h.EndedAt);
            if (typeFilter.HasValue)
                query = query.Where(h => h.Type == typeFilter.Value);
            return query.Take(limit).ToList();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  SAVED TEMPLATES
    // ═══════════════════════════════════════════════════════════════

    public List<SavedEventTemplate> GetSavedTemplates()
    {
        lock (_lock) { return _state.SavedTemplates.OrderByDescending(t => t.CreatedAt).ToList(); }
    }

    public SavedEventTemplate SaveTemplate(string name, CreateEventRequest config)
    {
        lock (_lock)
        {
            var template = new SavedEventTemplate
            {
                Name = name,
                Config = config
            };
            _state.SavedTemplates.Add(template);
            SaveState();
            return template;
        }
    }

    public bool DeleteTemplate(string id)
    {
        lock (_lock)
        {
            var template = _state.SavedTemplates.FirstOrDefault(t => t.Id == id);
            if (template == null) return false;
            _state.SavedTemplates.Remove(template);
            SaveState();
            return true;
        }
    }

    public ServerEvent? CreateFromTemplate(string templateId, string adminSessionId)
    {
        lock (_lock)
        {
            var template = _state.SavedTemplates.FirstOrDefault(t => t.Id == templateId);
            if (template == null) return null;
            return CreateEvent(template.Config, adminSessionId);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  QUICK CREATE
    // ═══════════════════════════════════════════════════════════════

    private static readonly Dictionary<string, CreateEventRequest> QuickPresets = new()
    {
        ["doubleXp1h"] = new CreateEventRequest
        {
            Name = "Double XP (1hr)", Type = EventType.DoubleXP, Multiplier = 2.0,
            DurationMinutes = 60, ActivateNow = true
        },
        ["flashSale30m"] = new CreateEventRequest
        {
            Name = "Flash Sale (30m)", Type = EventType.TraderSale, Multiplier = 0.5,
            DurationMinutes = 30, ActivateNow = true
        },
        ["lootBoost2h"] = new CreateEventRequest
        {
            Name = "Loot Boost (2hr)", Type = EventType.LootBoost, Multiplier = 2.0,
            DurationMinutes = 120, ActivateNow = true
        },
        ["weekendBoss"] = new CreateEventRequest
        {
            Name = "Weekend Boss Rush", Type = EventType.MapBossRush, Multiplier = 100.0,
            DurationMinutes = 480, ActivateNow = true, WeekdayFilter = WeekdayFilter.WeekendsOnly
        }
    };

    public ServerEvent? QuickCreate(string preset, string adminSessionId)
    {
        if (!QuickPresets.TryGetValue(preset, out var req)) return null;
        return CreateEvent(req, adminSessionId);
    }

    public static List<string> GetQuickPresetNames() => QuickPresets.Keys.ToList();

    // ═══════════════════════════════════════════════════════════════
    //  CALENDAR
    // ═══════════════════════════════════════════════════════════════

    public object GetCalendar(DateTime from, DateTime to)
    {
        lock (_lock)
        {
            // Past events from history
            var pastEvents = _state.History
                .Where(h => h.EndedAt >= from && h.StartedAt <= to)
                .Select(h => new
                {
                    id = h.EventId,
                    name = h.Name,
                    type = h.Type.ToString(),
                    multiplier = h.Multiplier,
                    start = h.StartedAt,
                    end = h.EndedAt,
                    status = "past"
                })
                .ToList<object>();

            // Active events
            var activeEvents = _state.Events
                .Where(e => e.Status == EventStatus.Active &&
                    e.ActivatedAt.HasValue && e.ExpiresAt.HasValue &&
                    e.ExpiresAt.Value >= from && e.ActivatedAt.Value <= to)
                .Select(e => new
                {
                    id = e.Id,
                    name = e.Name,
                    type = e.Type.ToString(),
                    multiplier = e.ResolvedMultiplier ?? e.Multiplier,
                    start = e.ActivatedAt!.Value,
                    end = e.ExpiresAt!.Value,
                    status = "active"
                })
                .ToList<object>();

            // Scheduled events (projected)
            var scheduled = _state.Events
                .Where(e => e.Status == EventStatus.Scheduled)
                .SelectMany(e =>
                {
                    var projections = new List<object>();

                    if (e.StartTime.HasValue && e.StartTime.Value >= from && e.StartTime.Value <= to)
                    {
                        projections.Add(new
                        {
                            id = e.Id,
                            name = e.Name,
                            type = e.Type.ToString(),
                            multiplier = e.Multiplier,
                            start = e.StartTime.Value,
                            end = e.EndTime ?? e.StartTime.Value.AddMinutes(e.DurationMinutes),
                            status = "scheduled"
                        });
                    }
                    else if (!string.IsNullOrEmpty(e.CronExpression))
                    {
                        // Project next few occurrences within range
                        var cursor = e.ActivatedAt ?? e.CreatedAt;
                        for (int i = 0; i < 10; i++)
                        {
                            var next = CronParser.GetNextOccurrence(e.CronExpression, cursor);
                            if (!next.HasValue || next.Value > to) break;
                            if (next.Value >= from)
                            {
                                projections.Add(new
                                {
                                    id = e.Id,
                                    name = e.Name,
                                    type = e.Type.ToString(),
                                    multiplier = e.Multiplier,
                                    start = next.Value,
                                    end = next.Value.AddMinutes(e.DurationMinutes),
                                    status = "scheduled"
                                });
                            }
                            cursor = next.Value;
                        }
                    }
                    return projections;
                })
                .ToList();

            return new
            {
                from,
                to,
                entries = pastEvents.Concat(activeEvents).Concat(scheduled)
                    .OrderBy(e => ((dynamic)e).start)
                    .ToList()
            };
        }
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
