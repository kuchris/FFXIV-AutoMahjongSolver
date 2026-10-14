namespace AutoMahjongSolver.Engine.Tests;

/// <summary>
/// The original recursive regular-form shanten, kept verbatim as the oracle the table-driven
/// <see cref="ShantenCalculator"/> is checked against.
/// </summary>
internal static class ReferenceShanten
{
    public static int Regular(ReadOnlySpan<int> counts, int meldCount)
    {
        Span<int> work = stackalloc int[Tiles.TypeCount];
        counts.CopyTo(work);

        int best = Eval(work, meldCount, hasHead: false);

        for (int t = 0; t < Tiles.TypeCount; t++)
        {
            if (work[t] >= 2)
            {
                work[t] -= 2;
                best = Math.Min(best, Eval(work, meldCount, hasHead: true));
                work[t] += 2;
            }
        }

        return best;
    }

    private static int Eval(Span<int> work, int meldCount, bool hasHead)
    {
        int best = int.MaxValue;
        Decompose(work, 0, meldCount, sets: 0, partials: 0, hasHead, ref best);
        return best;
    }

    private static void Decompose(Span<int> c, int i, int meldCount, int sets, int partials, bool hasHead, ref int best)
    {
        while (i < Tiles.TypeCount && c[i] == 0)
            i++;

        if (i >= Tiles.TypeCount)
        {
            int blocks = meldCount + sets;
            int usefulPartials = Math.Clamp(partials, 0, Math.Max(0, 4 - blocks));
            int shanten = 8 - (2 * blocks) - usefulPartials - (hasHead ? 1 : 0);
            if (shanten < best)
                best = shanten;
            return;
        }

        bool number = Tiles.IsNumber(i);
        int rank = Tiles.Rank(i);

        if (c[i] >= 3)
        {
            c[i] -= 3;
            Decompose(c, i, meldCount, sets + 1, partials, hasHead, ref best);
            c[i] += 3;
        }

        if (number && rank <= 7 && c[i + 1] > 0 && c[i + 2] > 0)
        {
            c[i]--; c[i + 1]--; c[i + 2]--;
            Decompose(c, i, meldCount, sets + 1, partials, hasHead, ref best);
            c[i]++; c[i + 1]++; c[i + 2]++;
        }

        if (c[i] >= 2)
        {
            c[i] -= 2;
            Decompose(c, i, meldCount, sets, partials + 1, hasHead, ref best);
            c[i] += 2;
        }

        if (number && rank <= 8 && c[i + 1] > 0)
        {
            c[i]--; c[i + 1]--;
            Decompose(c, i, meldCount, sets, partials + 1, hasHead, ref best);
            c[i]++; c[i + 1]++;
        }

        if (number && rank <= 7 && c[i + 2] > 0)
        {
            c[i]--; c[i + 2]--;
            Decompose(c, i, meldCount, sets, partials + 1, hasHead, ref best);
            c[i]++; c[i + 2]++;
        }

        int saved = c[i];
        c[i] = 0;
        Decompose(c, i + 1, meldCount, sets, partials, hasHead, ref best);
        c[i] = saved;
    }
}
