using System.Diagnostics;
using System.Globalization;

namespace Rota.Core.Generation;

/// <summary>
/// On-call rota generator (ported from the WinForms ShiftsAlgorithmV2).
///
/// Policy:
///  - Every person gets an equal total (leftover +1 goes to ExtraShift people first).
///  - Weekend/holiday shifts are shared by weight (PreferWeekendHoliday = larger share).
///  - Nobody works consecutive days unless nobody else can cover (reported as a warning).
///  - Shifts 2-3 days apart are discouraged.
///  - Preferred dates are honoured first and never moved afterwards.
///  - Weekend/holiday targets balance past load (RotaPerson.PriorWeekendShifts) against this rota.
///  - Targets never exceed what a person can work around their leave.
///
/// Approach: greedy construction (most constrained days first) + local search
/// (reassign / swap moves), repeated from several random starts; the best schedule wins.
/// </summary>
public sealed class RotaGenerator
{
    private const int MAX_LOCAL_SEARCH_PASSES = 50;

    private const int PREFER_WEEKEND_WEIGHT = 2;

    // Greedy scoring
    private const double URGENCY_WEIGHT = 1.0;
    private const double EXTRA_SHIFT_BONUS = 5.0;

    // Objective / spacing costs
    private const double CONSECUTIVE_COST = 50.0;
    private const double GAP2_COST = 0.6;
    private const double GAP3_COST = 0.3;
    private const double UNASSIGNED_COST = 1000.0;

    private readonly int _restarts;

    public RotaGenerator(int restarts = 30)
    {
        if (restarts < 1) throw new ArgumentOutOfRangeException(nameof(restarts));
        _restarts = restarts;
    }

    /// <summary>Average prior weekend/holiday count over people who have one (0 if nobody has).</summary>
    public static double AverageHistory(IEnumerable<RotaPerson> people)
    {
        var known = people.Where(p => p.PriorWeekendShifts.HasValue).Select(p => p.PriorWeekendShifts!.Value).ToList();
        return known.Count > 0 ? known.Average() : 0;
    }

    public RotaResult Generate(RotaInput input, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.People.Count == 0)
            throw new ArgumentException("People list is empty.", nameof(input));
        if (input.End < input.Start)
            throw new ArgumentException("End date is before start date.", nameof(input));

        var warnings = new List<string>();
        var ctx = new Context(input);

        CalculateTargets(ctx, warnings);

        int seedBase = input.Seed ?? Environment.TickCount;
        Schedule best = null!;
        double bestCost = double.MaxValue;

        for (int r = 0; r < _restarts; r++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rng = new Random(unchecked(seedBase + r * 7919));
            var s = new Schedule(ctx);

            AssignPreferredDates(ctx, s, rng);
            GreedyFill(ctx, s, rng);
            LocalSearch(ctx, s, rng);

            double cost = Objective(ctx, s);
            if (cost < bestCost)
            {
                bestCost = cost;
                best = s;
            }

            progress?.Report((r + 1) * 100 / _restarts);
        }

        ReportPreferences(ctx, best, warnings);
        ReportSummary(ctx, best, warnings);
        Debug.WriteLine($"RotaGenerator: best objective {bestCost:F2} (seed {seedBase})");

        return BuildResult(ctx, best, warnings, seedBase, bestCost);
    }

    private static string Fmt(DateOnly d) => d.ToString("d/M/yyyy", CultureInfo.InvariantCulture);

    // ---------------- MODEL ----------------

    /// <summary>Static problem data. Day index = days since start date.</summary>
    private sealed class Context
    {
        public readonly IReadOnlyList<RotaPerson> People;
        public readonly int N;                  // people
        public readonly int M;                  // days
        public readonly DateOnly[] Days;
        public readonly bool[] IsWeekend;       // Saturday, Sunday or public holiday
        public readonly bool[,] OnLeave;        // [person, day]
        public readonly List<int>[] Preferred;  // preferred day indices per person
        public readonly int[] AvailableCount;   // people not on leave, per day

        public int[] TotalTarget = [];
        public int[] WeekendTarget = [];

        public Context(RotaInput input)
        {
            People = input.People;
            N = People.Count;

            M = input.End.DayNumber - input.Start.DayNumber + 1;
            Days = Enumerable.Range(0, M).Select(i => input.Start.AddDays(i)).ToArray();

            var holidaySet = input.Holidays as IReadOnlySet<DateOnly> ?? input.Holidays.ToHashSet();
            IsWeekend = Days.Select(d =>
                d.DayOfWeek == DayOfWeek.Saturday ||
                d.DayOfWeek == DayOfWeek.Sunday ||
                holidaySet.Contains(d)).ToArray();

            OnLeave = new bool[N, M];
            Preferred = new List<int>[N];
            AvailableCount = new int[M];

            for (int p = 0; p < N; p++)
            {
                foreach (var ld in People[p].Leave)
                {
                    int i = IndexOf(ld);
                    if (i >= 0) OnLeave[p, i] = true;
                }

                Preferred[p] = People[p].Preferred
                    .Select(IndexOf)
                    .Where(i => i >= 0)
                    .Distinct()
                    .OrderBy(i => i)
                    .ToList();
            }

            for (int d = 0; d < M; d++)
                for (int p = 0; p < N; p++)
                    if (!OnLeave[p, d]) AvailableCount[d]++;
        }

        public int IndexOf(DateOnly date)
        {
            if (M == 0) return -1;
            int i = date.DayNumber - Days[0].DayNumber;
            return i >= 0 && i < M ? i : -1;
        }

        public int WeekdayTarget(int p) => TotalTarget[p] - WeekendTarget[p];
    }

    /// <summary>Mutable schedule state for one restart.</summary>
    private class Schedule
    {
        public readonly int[] Assign;   // day -> person, -1 = unassigned
        public readonly bool[] Locked;  // preferred-date assignments
        public readonly int[] Total;
        public readonly int[] Weekend;

        public Schedule(Context ctx)
        {
            Assign = Enumerable.Repeat(-1, ctx.M).ToArray();
            Locked = new bool[ctx.M];
            Total = new int[ctx.N];
            Weekend = new int[ctx.N];
        }

        public void Set(Context ctx, int day, int person)
        {
            int old = Assign[day];
            if (old >= 0)
            {
                Total[old]--;
                if (ctx.IsWeekend[day]) Weekend[old]--;
            }

            Assign[day] = person;

            if (person >= 0)
            {
                Total[person]++;
                if (ctx.IsWeekend[day]) Weekend[person]++;
            }
        }

        public bool WorksOn(int person, int day) =>
            day >= 0 && day < Assign.Length && Assign[day] == person;

        public bool HasAdjacent(int person, int day) =>
            WorksOn(person, day - 1) || WorksOn(person, day + 1);
    }

    // ---------------- TARGETS ----------------

    private void CalculateTargets(Context ctx, List<string> warnings)
    {
        int n = ctx.N;
        var total = new int[n];
        int baseShare = ctx.M / n;
        int leftover = ctx.M % n;

        for (int p = 0; p < n; p++)
            total[p] = baseShare;

        var available = Enumerable.Range(0, n)
            .Select(p => Enumerable.Range(0, ctx.M).Count(d => !ctx.OnLeave[p, d]))
            .ToArray();

        var leftoverOrder = Enumerable.Range(0, n)
            .OrderByDescending(p => ctx.People[p].ExtraShift)
            .ThenByDescending(p => available[p])
            .ThenBy(p => p);

        foreach (int p in leftoverOrder)
        {
            if (leftover-- <= 0) break;
            total[p]++;
        }

        // Nobody can be given more than they can work without consecutive days (e.g. long leave).
        // Their excess goes to people with room, ExtraShift people first.
        var capacity = Enumerable.Range(0, n).Select(p => Capacity(ctx, p, false)).ToArray();
        int excess = 0;
        for (int p = 0; p < n; p++)
        {
            if (total[p] <= capacity[p]) continue;
            warnings.Add($"{ctx.People[p].Name} can work at most {capacity[p]} shifts without consecutive days " +
                         $"because of leave; target reduced from {total[p]} to {capacity[p]}.");
            excess += total[p] - capacity[p];
            total[p] = capacity[p];
        }
        while (excess > 0)
        {
            int pick = Enumerable.Range(0, n)
                .Where(p => total[p] < capacity[p])
                .OrderByDescending(p => ctx.People[p].ExtraShift)
                .ThenBy(p => total[p])
                .ThenByDescending(p => available[p])
                .DefaultIfEmpty(-1)
                .First();
            if (pick < 0) break;
            total[pick]++;
            excess--;
        }

        // Weekend/holiday shifts: water-fill on projected load (history + this rota) / weight,
        // so people with fewer past weekend shifts catch up. Capped by total and by the
        // weekend/holiday days the person can actually work.
        var weekendCap = Enumerable.Range(0, n).Select(p => Math.Min(total[p], Capacity(ctx, p, true))).ToArray();
        int weekendDays = ctx.IsWeekend.Count(w => w);
        var weight = ctx.People
            .Select(pp => pp.PreferWeekendHoliday
                ? Math.Max(PREFER_WEEKEND_WEIGHT, pp.WeekendWeight)
                : Math.Max(1, pp.WeekendWeight))
            .ToArray();

        double average = AverageHistory(ctx.People);
        var history = ctx.People.Select(pp => pp.PriorWeekendShifts ?? average).ToArray();

        var weekend = new int[n];
        for (int k = 0; k < weekendDays; k++)
        {
            int pick = -1;
            double bestLoad = double.MaxValue;
            for (int p = 0; p < n; p++)
            {
                if (weekend[p] >= weekendCap[p]) continue;
                double load = (history[p] + weekend[p] + 1) / weight[p];
                if (load < bestLoad - 1e-9 || (Math.Abs(load - bestLoad) <= 1e-9 && weekend[p] < weekend[pick]))
                {
                    bestLoad = load;
                    pick = p;
                }
            }
            if (pick < 0) break;
            weekend[pick]++;
        }

        ctx.TotalTarget = total;
        ctx.WeekendTarget = weekend;
    }

    /// <summary>
    /// Max shifts a person can work without consecutive days: sum over runs of
    /// consecutive workable days of ceil(len / 2). weekendOnly counts weekend/holiday days only.
    /// </summary>
    private static int Capacity(Context ctx, int p, bool weekendOnly)
    {
        int capacity = 0, run = 0;
        for (int d = 0; d <= ctx.M; d++)
        {
            if (d < ctx.M && !ctx.OnLeave[p, d] && (!weekendOnly || ctx.IsWeekend[d]))
            {
                run++;
            }
            else
            {
                capacity += (run + 1) / 2;
                run = 0;
            }
        }
        return capacity;
    }

    // ---------------- CONSTRUCTION ----------------

    private void AssignPreferredDates(Context ctx, Schedule s, Random rng)
    {
        var byDay = new Dictionary<int, List<int>>();
        for (int p = 0; p < ctx.N; p++)
        {
            foreach (int d in ctx.Preferred[p])
            {
                if (ctx.OnLeave[p, d]) continue;
                if (!byDay.TryGetValue(d, out var list))
                    byDay[d] = list = new List<int>();
                list.Add(p);
            }
        }

        foreach (int d in byDay.Keys.OrderBy(k => k))
        {
            int pick = byDay[d]
                .Where(p => !s.HasAdjacent(p, d) && s.Total[p] < ctx.TotalTarget[p])
                .OrderBy(p => s.Total[p] - ctx.TotalTarget[p] + rng.NextDouble() * 0.01)
                .DefaultIfEmpty(-1)
                .First();

            if (pick < 0) continue;

            s.Set(ctx, d, pick);
            s.Locked[d] = true;
        }
    }

    private void GreedyFill(Context ctx, Schedule s, Random rng)
    {
        // Remaining open days of each type a person could still take: [person, weekend?1:0]
        var remaining = new int[ctx.N, 2];
        for (int d = 0; d < ctx.M; d++)
        {
            if (s.Assign[d] >= 0) continue;
            int t = ctx.IsWeekend[d] ? 1 : 0;
            for (int p = 0; p < ctx.N; p++)
                if (!ctx.OnLeave[p, d]) remaining[p, t]++;
        }

        // Weekends/holidays first, then hardest-to-staff days first.
        var order = Enumerable.Range(0, ctx.M)
            .Where(d => s.Assign[d] < 0)
            .Select(d => new { d, key = rng.NextDouble() })
            .OrderByDescending(x => ctx.IsWeekend[x.d])
            .ThenBy(x => ctx.AvailableCount[x.d])
            .ThenBy(x => x.key)
            .Select(x => x.d)
            .ToList();

        foreach (int d in order)
        {
            int t = ctx.IsWeekend[d] ? 1 : 0;
            int pick = -1;
            int bestTier = int.MaxValue;
            double bestCost = double.MaxValue;

            for (int p = 0; p < ctx.N; p++)
            {
                if (ctx.OnLeave[p, d]) continue;

                int tier;
                double cost = GreedyCost(ctx, s, p, d, remaining[p, t], rng);

                if (s.HasAdjacent(p, d))
                {
                    tier = 3;
                }
                else if (s.Total[p] < ctx.TotalTarget[p])
                {
                    tier = 1;
                }
                else
                {
                    tier = 2;
                    if (ctx.People[p].ExtraShift) cost -= EXTRA_SHIFT_BONUS;
                }

                if (tier < bestTier || (tier == bestTier && cost < bestCost))
                {
                    bestTier = tier;
                    bestCost = cost;
                    pick = p;
                }
            }

            for (int p = 0; p < ctx.N; p++)
                if (!ctx.OnLeave[p, d]) remaining[p, t]--;

            if (pick >= 0)
                s.Set(ctx, d, pick);
        }
    }

    private double GreedyCost(Context ctx, Schedule s, int p, int d, int remainingOfType, Random rng)
    {
        bool weekend = ctx.IsWeekend[d];

        int assignedOfType = weekend ? s.Weekend[p] : s.Total[p] - s.Weekend[p];
        int targetOfType = weekend ? ctx.WeekendTarget[p] : ctx.WeekdayTarget(p);

        double cost = (assignedOfType - targetOfType) + (s.Total[p] - ctx.TotalTarget[p]);

        // People who have few open days left relative to what they still need go first.
        int need = targetOfType - assignedOfType;
        if (need > 0)
            cost -= URGENCY_WEIGHT * need / Math.Max(1, remainingOfType);

        cost += SoftSpacing(s, p, d);
        cost += rng.NextDouble() * 0.01;
        return cost;
    }

    private static double SoftSpacing(Schedule s, int p, int d)
    {
        double c = 0;
        if (s.WorksOn(p, d - 2)) c += GAP2_COST;
        if (s.WorksOn(p, d + 2)) c += GAP2_COST;
        if (s.WorksOn(p, d - 3)) c += GAP3_COST;
        if (s.WorksOn(p, d + 3)) c += GAP3_COST;
        return c;
    }

    // ---------------- LOCAL SEARCH ----------------

    private void LocalSearch(Context ctx, Schedule s, Random rng)
    {
        var free = Enumerable.Range(0, ctx.M).Where(d => !s.Locked[d]).ToArray();

        for (int pass = 0; pass < MAX_LOCAL_SEARCH_PASSES; pass++)
        {
            bool improved = false;
            Shuffle(free, rng);

            // Move: give a day to someone else.
            foreach (int d in free)
            {
                for (int q = 0; q < ctx.N; q++)
                {
                    int p = s.Assign[d];
                    if (q == p || ctx.OnLeave[q, d]) continue;
                    if (TryReassign(ctx, s, d, q)) improved = true;
                }
            }

            // Swap: exchange two days between two people (totals unchanged).
            for (int i = 0; i < free.Length; i++)
            {
                for (int j = i + 1; j < free.Length; j++)
                {
                    int d1 = free[i], d2 = free[j];
                    int p = s.Assign[d1], q = s.Assign[d2];
                    if (p < 0 || q < 0 || p == q) continue;
                    if (ctx.OnLeave[q, d1] || ctx.OnLeave[p, d2]) continue;
                    if (TrySwap(ctx, s, d1, d2)) improved = true;
                }
            }

            if (!improved) break;
        }
    }

    private bool TryReassign(Context ctx, Schedule s, int d, int q)
    {
        int p = s.Assign[d];
        double before = LocalCost(ctx, s, d, -1, p, q);
        s.Set(ctx, d, q);
        double after = LocalCost(ctx, s, d, -1, p, q);

        if (after < before - 1e-9) return true;

        s.Set(ctx, d, p);
        return false;
    }

    private bool TrySwap(Context ctx, Schedule s, int d1, int d2)
    {
        int p = s.Assign[d1], q = s.Assign[d2];
        double before = LocalCost(ctx, s, d1, d2, p, q);
        s.Set(ctx, d1, q);
        s.Set(ctx, d2, p);
        double after = LocalCost(ctx, s, d1, d2, p, q);

        if (after < before - 1e-9) return true;

        s.Set(ctx, d1, p);
        s.Set(ctx, d2, q);
        return false;
    }

    /// <summary>
    /// Part of the objective that can change when days d1/d2 change hands between persons p/q.
    /// </summary>
    private double LocalCost(Context ctx, Schedule s, int d1, int d2, int p, int q)
    {
        double c = 0;
        if (p >= 0) c += PersonCost(ctx, s, p);
        if (q >= 0 && q != p) c += PersonCost(ctx, s, q);

        c += SpacingAround(s, d1, -1);
        if (d2 >= 0) c += SpacingAround(s, d2, d1);

        if (s.Assign[d1] < 0) c += UNASSIGNED_COST;
        if (d2 >= 0 && s.Assign[d2] < 0) c += UNASSIGNED_COST;
        return c;
    }

    /// <summary>Spacing cost of pairs involving day d (the pair with 'skip' is counted elsewhere).</summary>
    private static double SpacingAround(Schedule s, int d, int skip)
    {
        int p = s.Assign[d];
        if (p < 0) return 0;

        double c = 0;
        for (int k = 1; k <= 3; k++)
        {
            if (d - k != skip && s.WorksOn(p, d - k)) c += GapCost(k);
            if (d + k != skip && s.WorksOn(p, d + k)) c += GapCost(k);
        }
        return c;
    }

    private static double GapCost(int distance)
    {
        switch (distance)
        {
            case 1: return CONSECUTIVE_COST;
            case 2: return GAP2_COST;
            case 3: return GAP3_COST;
            default: return 0;
        }
    }

    private static double PersonCost(Context ctx, Schedule s, int p)
    {
        double t = s.Total[p] - ctx.TotalTarget[p];
        double w = s.Weekend[p] - ctx.WeekendTarget[p];
        return t * t + w * w;
    }

    private double Objective(Context ctx, Schedule s)
    {
        double c = 0;
        for (int p = 0; p < ctx.N; p++)
            c += PersonCost(ctx, s, p);

        for (int d = 0; d < ctx.M; d++)
        {
            int p = s.Assign[d];
            if (p < 0)
            {
                c += UNASSIGNED_COST;
                continue;
            }
            // Count each pair once (forward only).
            for (int k = 1; k <= 3; k++)
                if (s.WorksOn(p, d + k)) c += GapCost(k);
        }
        return c;
    }

    // ---------------- OUTPUT ----------------

    private static RotaResult BuildResult(Context ctx, Schedule s, List<string> warnings, int seed, double objective)
    {
        var assignments = new List<ShiftAssignment>(ctx.M);
        for (int d = 0; d < ctx.M; d++)
        {
            int p = s.Assign[d];
            assignments.Add(new ShiftAssignment(ctx.Days[d], p >= 0 ? ctx.People[p].Id : null, ctx.IsWeekend[d]));
        }

        var stats = Enumerable.Range(0, ctx.N)
            .Select(p => new PersonStats(
                ctx.People[p].Id,
                ctx.TotalTarget[p],
                ctx.WeekendTarget[p],
                s.Total[p],
                s.Total[p] - s.Weekend[p],
                s.Weekend[p]))
            .ToList();

        return new RotaResult(assignments, warnings, stats, seed, objective);
    }

    private void ReportPreferences(Context ctx, Schedule s, List<string> warnings)
    {
        for (int p = 0; p < ctx.N; p++)
        {
            foreach (int d in ctx.Preferred[p])
            {
                if (s.Assign[d] == p) continue;

                int owner = s.Assign[d];
                string reason = ctx.OnLeave[p, d]
                    ? "on leave that day"
                    : owner >= 0 && ctx.Preferred[owner].Contains(d)
                        ? $"also preferred by {ctx.People[owner].Name}"
                        : "conflicts with other shifts or quota";
                warnings.Add($"{ctx.People[p].Name}: preferred date {Fmt(ctx.Days[d])} not honoured ({reason}).");
            }
        }
    }

    private void ReportSummary(Context ctx, Schedule s, List<string> warnings)
    {
        for (int d = 0; d + 1 < ctx.M; d++)
        {
            int p = s.Assign[d];
            if (p >= 0 && s.Assign[d + 1] == p)
                warnings.Add($"{ctx.People[p].Name} works consecutive days {Fmt(ctx.Days[d])} and {Fmt(ctx.Days[d + 1])} (no one else available).");
        }

        int unassigned = s.Assign.Count(a => a < 0);
        if (unassigned > 0)
            warnings.Add($"{unassigned} date(s) could not be assigned (everyone on leave).");

        warnings.Add($"Shifts per person: {s.Total.Min()}-{s.Total.Max()}, " +
                     $"weekend/holiday per person: {s.Weekend.Min()}-{s.Weekend.Max()}.");

        int known = ctx.People.Count(p => p.PriorWeekendShifts.HasValue);
        if (known > 0)
        {
            var combined = ctx.People.Select((p, i) =>
                (p.PriorWeekendShifts ?? AverageHistory(ctx.People)) + s.Weekend[i]).ToList();
            warnings.Add($"Weekend history balanced: history + this rota now {combined.Min():0.#}-{combined.Max():0.#} per person" +
                         (known < ctx.N ? $" ({ctx.N - known} without history used the average {AverageHistory(ctx.People):0.#})." : "."));
        }
    }

    private static void Shuffle(int[] a, Random rng)
    {
        for (int i = a.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            int tmp = a[i];
            a[i] = a[j];
            a[j] = tmp;
        }
    }
}
