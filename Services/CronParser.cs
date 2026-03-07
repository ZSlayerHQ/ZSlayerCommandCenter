namespace ZSlayerCommandCenter.Services;

/// <summary>
/// 5-field cron parser: minute hour day-of-month month day-of-week.
/// Supports: *, numbers, ranges (1-5), steps (*/15, 1-5/2), lists (1,3,5),
/// day names (SUN-SAT / 0-6), month names (JAN-DEC / 1-12).
/// </summary>
public static class CronParser
{
    private static readonly string[] MonthNames = ["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"];
    private static readonly string[] DayNames = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"];

    public static bool IsValid(string expression)
    {
        try
        {
            Parse(expression);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static DateTime? GetNextOccurrence(string expression, DateTime after)
    {
        var (minutes, hours, daysOfMonth, months, daysOfWeek) = Parse(expression);

        var candidate = new DateTime(after.Year, after.Month, after.Day, after.Hour, after.Minute, 0, after.Kind);
        candidate = candidate.AddMinutes(1); // always move forward at least 1 minute

        // Search up to 4 years ahead
        var limit = after.AddYears(4);

        while (candidate < limit)
        {
            if (!months.Contains(candidate.Month))
            {
                candidate = NextMonth(candidate);
                continue;
            }

            if (!daysOfMonth.Contains(candidate.Day) || !daysOfWeek.Contains((int)candidate.DayOfWeek))
            {
                candidate = candidate.AddDays(1).Date; // next day, midnight
                continue;
            }

            if (!hours.Contains(candidate.Hour))
            {
                candidate = candidate.AddHours(1);
                candidate = new DateTime(candidate.Year, candidate.Month, candidate.Day, candidate.Hour, 0, 0, candidate.Kind);
                continue;
            }

            if (!minutes.Contains(candidate.Minute))
            {
                candidate = candidate.AddMinutes(1);
                continue;
            }

            return candidate;
        }

        return null;
    }

    public static string Describe(string expression)
    {
        try
        {
            var parts = expression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 5) return expression;

            var min = parts[0];
            var hr = parts[1];
            var dom = parts[2];
            var mon = parts[3];
            var dow = parts[4];

            var desc = new List<string>();

            // Time
            if (min == "*" && hr == "*") desc.Add("Every minute");
            else if (min.StartsWith("*/")) desc.Add($"Every {min[2..]} minutes");
            else if (hr == "*") desc.Add($"At minute {min} of every hour");
            else desc.Add($"At {hr.PadLeft(2, '0')}:{min.PadLeft(2, '0')}");

            // Day of week
            if (dow != "*") desc.Add($"on {FormatDow(dow)}");

            // Day of month
            if (dom != "*") desc.Add($"on day {dom}");

            // Month
            if (mon != "*") desc.Add($"in {FormatMonth(mon)}");

            return string.Join(" ", desc);
        }
        catch
        {
            return expression;
        }
    }

    private static string FormatDow(string field)
    {
        var names = new Dictionary<string, string>
        {
            ["0"] = "Sun", ["1"] = "Mon", ["2"] = "Tue", ["3"] = "Wed",
            ["4"] = "Thu", ["5"] = "Fri", ["6"] = "Sat",
            ["SUN"] = "Sun", ["MON"] = "Mon", ["TUE"] = "Tue", ["WED"] = "Wed",
            ["THU"] = "Thu", ["FRI"] = "Fri", ["SAT"] = "Sat"
        };
        var parts = field.Split(',');
        return string.Join(", ", parts.Select(p => names.GetValueOrDefault(p.ToUpper(), p)));
    }

    private static string FormatMonth(string field)
    {
        var parts = field.Split(',');
        return string.Join(", ", parts.Select(p =>
        {
            if (int.TryParse(p, out var m) && m >= 1 && m <= 12) return MonthNames[m - 1];
            return p;
        }));
    }

    private static DateTime NextMonth(DateTime dt)
    {
        if (dt.Month == 12) return new DateTime(dt.Year + 1, 1, 1, 0, 0, 0, dt.Kind);
        return new DateTime(dt.Year, dt.Month + 1, 1, 0, 0, 0, dt.Kind);
    }

    private static (HashSet<int> minutes, HashSet<int> hours, HashSet<int> daysOfMonth,
        HashSet<int> months, HashSet<int> daysOfWeek) Parse(string expression)
    {
        var parts = expression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5)
            throw new FormatException($"Cron expression must have 5 fields, got {parts.Length}: '{expression}'");

        return (
            ParseField(parts[0], 0, 59, null),
            ParseField(parts[1], 0, 23, null),
            ParseField(parts[2], 1, 31, null),
            ParseField(parts[3], 1, 12, MonthNames),
            ParseField(parts[4], 0, 6, DayNames)
        );
    }

    private static HashSet<int> ParseField(string field, int min, int max, string[]? names)
    {
        var result = new HashSet<int>();

        foreach (var part in field.Split(','))
        {
            var token = part.Trim();
            if (string.IsNullOrEmpty(token)) continue;

            // Handle step: */N or range/N
            int step = 1;
            var slashIdx = token.IndexOf('/');
            if (slashIdx >= 0)
            {
                step = int.Parse(token[(slashIdx + 1)..]);
                token = token[..slashIdx];
            }

            if (token == "*")
            {
                for (int i = min; i <= max; i += step)
                    result.Add(i);
            }
            else if (token.Contains('-'))
            {
                var rangeParts = token.Split('-');
                var start = ResolveValue(rangeParts[0], names, min);
                var end = ResolveValue(rangeParts[1], names, min);
                for (int i = start; i <= end; i += step)
                    result.Add(i);
            }
            else
            {
                result.Add(ResolveValue(token, names, min));
            }
        }

        return result;
    }

    private static int ResolveValue(string token, string[]? names, int baseOffset)
    {
        if (int.TryParse(token, out var num)) return num;

        if (names != null)
        {
            var upper = token.ToUpper();
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == upper) return i + baseOffset;
            }
        }

        throw new FormatException($"Invalid cron value: '{token}'");
    }
}
