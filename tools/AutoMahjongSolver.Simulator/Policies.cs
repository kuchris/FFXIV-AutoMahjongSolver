using AutoMahjongSolver.Engine;

namespace AutoMahjongSolver.Simulator;

/// <summary>
/// The two sides of an <c>ab</c> run. Baseline is the shipped policy; point Candidate at the change
/// under test (e.g. a <see cref="TurnPolicy"/> variant) and compare. Identical sides score exactly 0.
/// </summary>
internal static class Policies
{
    public static readonly Func<TurnInput, TurnDecision> Baseline = TurnPolicy.Decide;

    public static readonly Func<TurnInput, TurnDecision> Candidate = TurnPolicy.Decide;
}
