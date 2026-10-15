using MahjongXiv.Engine;

namespace MahjongXiv.Simulator;

/// <summary>
/// The two sides of an <c>ab</c> run. Baseline is the shipped policy; point Candidate at the change
/// under test (e.g. a <see cref="TurnPolicy"/> variant) and compare. Identical sides score exactly 0.
/// </summary>
internal static class Policies
{
    public static readonly Func<TurnInput, TurnDecision> Baseline = TurnPolicy.Decide;

    public static Func<TurnInput, TurnDecision> Candidate { get; set; } = TurnPolicy.Decide;

    /// <summary>
    /// Optional candidate overrides from the command line: <c>key=value</c> pairs over <see cref="EvalOptions"/>,
    /// starting from the shipped defaults. <c>legacy=on</c> starts from <see cref="EvalOptions.Legacy"/> instead.
    /// </summary>
    public static void Configure(IEnumerable<string> overrides)
    {
        EvalOptions opts = EvalOptions.Default;
        bool any = false;
        foreach (string pair in overrides)
        {
            string[] kv = pair.Split('=', 2);
            if (kv.Length != 2)
                continue;
            any = true;
            opts = kv[0].ToLowerInvariant() switch
            {
                "legacy" => kv[1] is "1" or "true" or "on" ? EvalOptions.Legacy : opts,
                "shape" => opts with { ShapeLookahead = kv[1] is "1" or "true" or "on" },
                "shaperef" => opts with { ShapeReference = int.Parse(kv[1]) },
                "tenpai" => opts with { TenpaiLookahead = kv[1] is "1" or "true" or "on" },
                _ => throw new ArgumentException($"unknown candidate option '{kv[0]}'"),
            };
        }

        if (any)
        {
            EvalOptions chosen = opts;
            Candidate = input => TurnPolicy.Decide(input with { Options = chosen });
            Console.WriteLine($"candidate: {chosen}");
        }
    }
}
