namespace MahjongXiv.Engine;

/// <summary>
/// Exact shanten (distance-to-tenpai) over a 34-length tile-count array.
/// Convention: -1 = complete hand, 0 = tenpai, positive = tiles-of-progress away.
/// Takes the minimum of the regular, seven-pairs, and thirteen-orphans forms.
/// </summary>
public static class ShantenCalculator
{
    public const int Complete = -1;

    /// <param name="counts">Concealed tile counts (length 34).</param>
    /// <param name="meldCount">Number of called (open) melds; each is a completed set.</param>
    public static int Calculate(ReadOnlySpan<int> counts, int meldCount = 0)
    {
        int best = Regular(counts, meldCount);

        if (meldCount == 0)
        {
            best = Math.Min(best, SevenPairs(counts));
            best = Math.Min(best, ThirteenOrphans(counts));
        }

        return best;
    }

    public static int Regular(ReadOnlySpan<int> counts, int meldCount)
    {
        // acc[head * 5 + sets] = most partials over the groups merged so far (-1 = unreachable).
        Span<int> acc = stackalloc int[Slots];
        Span<int> next = stackalloc int[Slots];
        Unpack(GroupTable(counts, 0, 9, SuitTable, sequences: true), acc);
        Merge(acc, GroupTable(counts, 9, 9, SuitTable, sequences: true), next);
        Merge(next, GroupTable(counts, 18, 9, SuitTable, sequences: true), acc);
        Merge(acc, GroupTable(counts, 27, 7, HonorTable, sequences: false), next);

        int best = int.MaxValue;
        for (int head = 0; head <= 1; head++)
        {
            for (int sets = 0; sets <= MaxGroupSets; sets++)
            {
                int partials = next[head * (MaxGroupSets + 1) + sets];
                if (partials < 0)
                    continue;

                int blocks = meldCount + sets;
                int usefulPartials = Math.Min(partials, Math.Max(0, 4 - blocks));
                int shanten = 8 - (2 * blocks) - usefulPartials - head;
                if (shanten < best)
                    best = shanten;
            }
        }

        return best;
    }

    private const int Slots = 2 * (MaxGroupSets + 1);

    private static void Unpack(int packed, Span<int> into)
    {
        for (int i = 0; i < Slots; i++)
            into[i] = ((packed >> (3 * i)) & 7) - 1;
    }

    /// <summary>Max-plus merge of one more group into the running (head, sets) -> partials table; at most one head overall.</summary>
    private static void Merge(ReadOnlySpan<int> acc, int packed, Span<int> into)
    {
        Span<int> group = stackalloc int[Slots];
        Unpack(packed, group);
        into.Fill(Unreachable);

        for (int ha = 0; ha <= 1; ha++)
        for (int sa = 0; sa <= MaxGroupSets; sa++)
        {
            int pa = acc[ha * (MaxGroupSets + 1) + sa];
            if (pa < 0)
                continue;

            for (int hg = 0; hg <= 1 - ha; hg++)
            for (int sg = 0; sg <= MaxGroupSets - sa; sg++)
            {
                int pg = group[hg * (MaxGroupSets + 1) + sg];
                if (pg < 0)
                    continue;

                int slot = (ha + hg) * (MaxGroupSets + 1) + sa + sg;
                int partials = Math.Min(pa + pg, 4);
                if (partials > into[slot])
                    into[slot] = partials;
            }
        }
    }

    // ---- Per-group tables ---------------------------------------------------
    //
    // Sets and partials never span suits, so the regular form splits into four independent groups
    // (three suits + honors). For each group pattern we memoize, per (head held in this group?,
    // sets taken), the most partials the rest of the group can form. Recombining the four groups
    // reproduces the full recursive decomposition exactly, at the cost of a few table reads.
    // Partials are stored capped at 4: the hand can never use more than 4 - blocks of them anyway.

    private const int MaxGroupSets = 4;
    private const int Unreachable = -1;
    private const int ComputedFlag = 1 << 30;

    /// <summary>Lazily filled, indexed by the group's counts in base 5. Racy fills are benign: every writer stores the same value.</summary>
    private static readonly int[] SuitTable = new int[1953125]; // 5^9
    private static readonly int[] HonorTable = new int[78125];  // 5^7

    private static int GroupTable(ReadOnlySpan<int> counts, int offset, int length, int[] table, bool sequences)
    {
        int key = 0;
        for (int i = length - 1; i >= 0; i--)
            key = key * 5 + Math.Min(counts[offset + i], 4);

        int packed = table[key];
        if (packed == 0)
        {
            packed = BuildGroup(counts.Slice(offset, length), sequences) | ComputedFlag;
            table[key] = packed;
        }

        return packed;
    }

    private static int BuildGroup(ReadOnlySpan<int> group, bool sequences)
    {
        Span<int> work = stackalloc int[9];
        work.Clear();
        for (int i = 0; i < group.Length; i++)
            work[i] = Math.Min(group[i], 4);
        work = work[..group.Length];

        Span<int> best = stackalloc int[Slots];
        best.Fill(Unreachable);

        DecomposeGroup(work, 0, sequences, sets: 0, partials: 0, best[..(MaxGroupSets + 1)]);
        for (int t = 0; t < work.Length; t++)
        {
            if (work[t] >= 2)
            {
                work[t] -= 2;
                DecomposeGroup(work, 0, sequences, sets: 0, partials: 0, best[(MaxGroupSets + 1)..]);
                work[t] += 2;
            }
        }

        int packed = 0;
        for (int i = 0; i < best.Length; i++)
            packed |= (Math.Min(best[i], 4) + 1) << (3 * i);
        return packed;
    }

    /// <summary>The original single-hand recursion, restricted to one group: record the best partials per set count.</summary>
    private static void DecomposeGroup(Span<int> c, int i, bool sequences, int sets, int partials, Span<int> best)
    {
        while (i < c.Length && c[i] == 0)
            i++;

        if (i >= c.Length)
        {
            if (sets <= MaxGroupSets && partials > best[sets])
                best[sets] = partials;
            return;
        }

        if (c[i] >= 3)
        {
            c[i] -= 3;
            DecomposeGroup(c, i, sequences, sets + 1, partials, best);
            c[i] += 3;
        }

        if (sequences && i <= 6 && c[i + 1] > 0 && c[i + 2] > 0)
        {
            c[i]--; c[i + 1]--; c[i + 2]--;
            DecomposeGroup(c, i, sequences, sets + 1, partials, best);
            c[i]++; c[i + 1]++; c[i + 2]++;
        }

        if (c[i] >= 2)
        {
            c[i] -= 2;
            DecomposeGroup(c, i, sequences, sets, partials + 1, best);
            c[i] += 2;
        }

        if (sequences && i <= 7 && c[i + 1] > 0)
        {
            c[i]--; c[i + 1]--;
            DecomposeGroup(c, i, sequences, sets, partials + 1, best);
            c[i]++; c[i + 1]++;
        }

        if (sequences && i <= 6 && c[i + 2] > 0)
        {
            c[i]--; c[i + 2]--;
            DecomposeGroup(c, i, sequences, sets, partials + 1, best);
            c[i]++; c[i + 2]++;
        }

        int saved = c[i];
        c[i] = 0;
        DecomposeGroup(c, i + 1, sequences, sets, partials, best);
        c[i] = saved;
    }

    public static int SevenPairs(ReadOnlySpan<int> counts)
    {
        int pairs = 0;
        int kinds = 0;
        for (int t = 0; t < Tiles.TypeCount; t++)
        {
            if (counts[t] >= 1) kinds++;
            if (counts[t] >= 2) pairs++;
        }

        int shanten = 6 - pairs;
        if (kinds < 7)
            shanten += 7 - kinds;
        return shanten;
    }

    public static int ThirteenOrphans(ReadOnlySpan<int> counts)
    {
        int distinct = 0;
        bool hasPair = false;
        foreach (int t in Tiles.TerminalsAndHonors)
        {
            if (counts[t] >= 1) distinct++;
            if (counts[t] >= 2) hasPair = true;
        }

        return 13 - distinct - (hasPair ? 1 : 0);
    }
}
