using System.Diagnostics;
using MahjongXiv.Engine;
using MahjongXiv.Simulator;

// Usage:
//   [matches] [seed]          self-play: every seat on the baseline policy
//   ab [matches] [seed] [key=value ...]
//                             candidate vs baseline: the candidate takes each seat in turn against
//                             three baselines on the same walls, so seat and wall luck cancel out.
//                             key=value pairs override the candidate's EvalOptions (see Policies).
bool ab = args.Length > 0 && args[0] == "ab";
string[] rest = ab ? args[1..] : args;
if (ab)
    Policies.Configure(rest.Where(a => a.Contains('=')));
rest = rest.Where(a => !a.Contains('=')).ToArray();
int matches = rest.Length > 0 && int.TryParse(rest[0], out int m) ? m : 100;
int seed = rest.Length > 1 && int.TryParse(rest[1], out int s) ? s : 42;

if (ab)
    RunAb(matches, seed);
else
    RunSelfPlay(matches, seed);

static void RunSelfPlay(int matches, int seed)
{
    var policies = new Func<TurnInput, TurnDecision>[4];
    Array.Fill(policies, Policies.Baseline);

    var stats = new SimStats();
    var gate = new object();

    var sw = Stopwatch.StartNew();
    Parallel.For(0, matches, i =>
    {
        var local = new SimStats();
        MatchSimulator.Play(policies, MatchRng(seed, i), local);
        lock (gate)
            stats.Add(local);
    });
    sw.Stop();

    int wins = stats.Tsumos + stats.Rons;

    Console.WriteLine($"self-play: {stats.Matches} matches, {stats.Hands} hands, seed {seed}, {sw.Elapsed.TotalSeconds:F1}s ({stats.Hands / Math.Max(sw.Elapsed.TotalSeconds, 0.001):F1} hands/s)");
    Console.WriteLine($"  outcomes: {wins} wins ({stats.Tsumos} tsumo / {stats.Rons} ron), {stats.Draws} exhaustive draws ({100.0 * stats.Draws / Math.Max(stats.Hands, 1):F1}%)");
    if (wins > 0)
        Console.WriteLine($"  avg win: {stats.WinPointsTotal / wins} points, {(double)stats.WinHanTotal / wins:F2} han");
    Console.WriteLine();
    Console.WriteLine("  seat  wins  win%   deal-in  deal-in%  riichi%  avg place  avg score");
    for (int seat = 0; seat < 4; seat++)
    {
        double winPct = 100.0 * stats.Wins[seat] / Math.Max(stats.Hands, 1);
        double dealInPct = 100.0 * stats.DealIns[seat] / Math.Max(stats.Hands, 1);
        double riichiPct = 100.0 * stats.Riichis[seat] / Math.Max(stats.Hands, 1);
        double avgPlace = (double)stats.PlacementSum[seat] / Math.Max(stats.Matches, 1);
        long avgScore = stats.ScoreSum[seat] / Math.Max(stats.Matches, 1);
        Console.WriteLine($"  {seat,4}  {stats.Wins[seat],4}  {winPct,4:F1}%  {stats.DealIns[seat],7}  {dealInPct,7:F1}%  {riichiPct,6:F1}%  {avgPlace,9:F2}  {avgScore,9}");
    }
}

static void RunAb(int matches, int seed)
{
    // Per match: the candidate's score delta and placement, averaged over its four seats.
    var deltas = new double[matches];
    var places = new double[matches];
    var candidate = new SimStats(); // the candidate's counters, folded onto seat index 0
    var gate = new object();

    var sw = Stopwatch.StartNew();
    Parallel.For(0, matches, i =>
    {
        double delta = 0, place = 0;
        var local = new SimStats();
        for (int seat = 0; seat < 4; seat++)
        {
            var policies = new Func<TurnInput, TurnDecision>[4];
            Array.Fill(policies, Policies.Baseline);
            policies[seat] = Policies.Candidate;

            var match = new SimStats();
            MatchResult result = MatchSimulator.Play(policies, MatchRng(seed, i), match);
            // Relative to the table average: riichi sticks left on the table at match end vanish,
            // so the raw start-score delta would drift negative even for identical policies.
            delta += result.Scores[seat] - result.Scores.Average();
            place += result.Places[seat];

            local.Hands += match.Hands;
            local.Wins[0] += match.Wins[seat];
            local.DealIns[0] += match.DealIns[seat];
            local.Riichis[0] += match.Riichis[seat];
        }

        deltas[i] = delta / 4;
        places[i] = place / 4;
        lock (gate)
            candidate.Add(local);
    });
    sw.Stop();

    (double meanDelta, double seDelta) = MeanAndError(deltas);
    (double meanPlace, double sePlace) = MeanAndError(places);
    int hands = Math.Max(candidate.Hands, 1);

    Console.WriteLine($"ab: {matches} matches x 4 seats, seed {seed}, {candidate.Hands} candidate hands, {sw.Elapsed.TotalSeconds:F1}s");
    Console.WriteLine($"  candidate score vs table: {meanDelta,8:+0;-0;0} ± {seDelta:F0} per match (0 = same as baseline)");
    Console.WriteLine($"  candidate avg place:      {meanPlace,8:F3} ± {sePlace:F3} (2.500 = same as baseline)");
    Console.WriteLine($"  candidate win {100.0 * candidate.Wins[0] / hands:F1}%  deal-in {100.0 * candidate.DealIns[0] / hands:F1}%  riichi {100.0 * candidate.Riichis[0] / hands:F1}%");
    Console.WriteLine("  (± is one standard error; treat |mean| < 2 SE as noise)");
}

// Each match gets its own wall stream, so results do not depend on thread scheduling.
static Random MatchRng(int seed, int match)
{
    ulong z = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL + (ulong)match + 1;
    z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
    z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
    return new Random((int)(z ^ (z >> 31)));
}

static (double Mean, double Error) MeanAndError(double[] values)
{
    double mean = values.Average();
    if (values.Length < 2)
        return (mean, 0);
    double variance = values.Sum(v => (v - mean) * (v - mean)) / (values.Length - 1);
    return (mean, Math.Sqrt(variance / values.Length));
}
