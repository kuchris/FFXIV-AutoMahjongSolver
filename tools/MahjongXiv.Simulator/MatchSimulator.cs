using MahjongXiv.Engine;

namespace MahjongXiv.Simulator;

internal sealed class SimStats
{
    public int Hands;
    public int Draws;
    public int Tsumos;
    public int Rons;
    public long WinPointsTotal;
    public long WinHanTotal;
    public readonly int[] Wins = new int[4];
    public readonly int[] DealIns = new int[4];
    public readonly int[] Riichis = new int[4];
    public readonly int[] PlacementSum = new int[4];
    public readonly long[] ScoreSum = new long[4];
    public int Matches;

    public void Add(SimStats other)
    {
        Hands += other.Hands;
        Draws += other.Draws;
        Tsumos += other.Tsumos;
        Rons += other.Rons;
        WinPointsTotal += other.WinPointsTotal;
        WinHanTotal += other.WinHanTotal;
        Matches += other.Matches;
        for (int s = 0; s < 4; s++)
        {
            Wins[s] += other.Wins[s];
            DealIns[s] += other.DealIns[s];
            Riichis[s] += other.Riichis[s];
            PlacementSum[s] += other.PlacementSum[s];
            ScoreSum[s] += other.ScoreSum[s];
        }
    }
}

/// <summary>Final standing of one match, by seat.</summary>
internal readonly record struct MatchResult(int[] Scores, int[] Places);

/// <summary>An east-round match: dealer repeats on a dealer win or dealer tenpai draw, capped to bound runtime.</summary>
internal static class MatchSimulator
{
    public const int StartScore = 25000;
    private const int MaxHands = 16;

    public static MatchResult Play(Func<TurnInput, TurnDecision>[] policies, Random rng, SimStats stats)
    {
        var scores = new int[4];
        Array.Fill(scores, StartScore);

        int dealer = 0;
        int dealerships = 1;
        int pot = 0;
        int hands = 0;

        while (dealerships <= 4 && hands < MaxHands)
        {
            HandOutcome outcome = HandSimulator.Play(dealer, policies, rng, pot);
            hands++;
            stats.Hands++;

            for (int s = 0; s < 4; s++)
            {
                scores[s] += outcome.Deltas[s];
                if (outcome.RiichiDeclared[s])
                    stats.Riichis[s]++;
            }

            if (outcome.ExhaustiveDraw)
            {
                stats.Draws++;
            }
            else
            {
                stats.Wins[outcome.Winner]++;
                stats.WinPointsTotal += outcome.WinPoints;
                stats.WinHanTotal += outcome.WinHan;
                if (outcome.DealIn >= 0)
                {
                    stats.Rons++;
                    stats.DealIns[outcome.DealIn]++;
                }
                else
                {
                    stats.Tsumos++;
                }
            }

            pot = outcome.SticksLeft;

            if (scores.Any(s => s < 0))
                break; // busted: the match ends immediately

            if (!outcome.DealerKeeps)
            {
                dealer = (dealer + 1) % 4;
                dealerships++;
            }
        }

        int[] bySeat = [0, 1, 2, 3];
        Array.Sort(bySeat, (a, b) => scores[b].CompareTo(scores[a]));
        var places = new int[4];
        for (int place = 0; place < 4; place++)
            places[bySeat[place]] = place + 1;
        for (int s = 0; s < 4; s++)
        {
            stats.PlacementSum[s] += places[s];
            stats.ScoreSum[s] += scores[s];
        }

        stats.Matches++;
        return new MatchResult(scores, places);
    }
}
