using MahjongXiv.Engine;
using Xunit;

namespace MahjongXiv.Engine.Tests;

public class ShantenTableTests
{
    /// <summary>
    /// The table-driven regular form must agree with the original recursion on every hand shape the
    /// bot produces: 13/14 concealed tiles minus three per meld, from a real 136-tile wall.
    /// </summary>
    [Fact]
    public void Regular_MatchesReferenceRecursion_OnRandomHands()
    {
        var rng = new Random(20261014);
        int[] wall = Enumerable.Range(0, 136).ToArray();
        var counts = new int[Tiles.TypeCount];

        for (int n = 0; n < 200_000; n++)
        {
            int melds = rng.Next(5);
            int size = 13 - 3 * melds + rng.Next(2);
            rng.Shuffle(wall);
            Array.Clear(counts);
            for (int i = 0; i < size; i++)
                counts[wall[i] / 4]++;

            int expected = ReferenceShanten.Regular(counts, melds);
            int actual = ShantenCalculator.Regular(counts, melds);
            if (expected != actual)
                Assert.Fail($"{Describe(counts)} melds={melds}: reference {expected}, table {actual}");
        }
    }

    /// <summary>Single-suit hands stress the in-group decomposition far harder than random draws.</summary>
    [Fact]
    public void Regular_MatchesReferenceRecursion_OnChinitsuHands()
    {
        var rng = new Random(7);
        var counts = new int[Tiles.TypeCount];

        for (int n = 0; n < 50_000; n++)
        {
            Array.Clear(counts);
            int suit = rng.Next(3) * 9;
            int size = 13 + rng.Next(2);
            for (int placed = 0; placed < size;)
            {
                int t = suit + rng.Next(9);
                if (counts[t] < 4)
                {
                    counts[t]++;
                    placed++;
                }
            }

            int expected = ReferenceShanten.Regular(counts, 0);
            int actual = ShantenCalculator.Regular(counts, 0);
            if (expected != actual)
                Assert.Fail($"{Describe(counts)}: reference {expected}, table {actual}");
        }
    }

    private static string Describe(int[] counts)
        => string.Join(",", counts.Select((c, t) => (c, t)).Where(x => x.c > 0).Select(x => $"{x.t}x{x.c}"));
}
