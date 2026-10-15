using FFXIVClientStructs.FFXIV.Component.GUI;

namespace MahjongXiv.Core.Game;

/// <summary>
/// Actuates Doman Mahjong inputs by replaying the FireCallback protocol captured from manual play.
/// A discard is a two-call handshake: [15, iconId] selects/highlights the tile (and moves the
/// after-draw surface from state 6 to state 30), then [7, slot] commits it. Manual play always lets
/// the surface reach state 30 before committing, and never sends the commit in the same frame as a
/// highlight (captured on EmjL, 2026-10-14). The bot mirrors that: highlight at 6, highlight again
/// at 30, and commit alone on a later tick. Firing [15] and [7] in one frame was followed both
/// times by a client crash in AgentEmj.Update ~3s later on the ranked EmjL table.
/// Gated on the turn state so a mis-timed call cannot wedge the addon.
/// </summary>
internal static unsafe class EmjActuator
{
    private const int OpHighlight = 15;
    private const int OpDiscardCommit = 7;
    private const int OpCallOption = 11;

    private const int StateAfterDraw = 6;       // self-action surface (riichi/tsumo/kan or discard)
    private const int StateAfterCall = 28;      // post-call discard surface (verified live after a pon)
    private const int StateDiscardTurn = 30;    // tile-selection discard surface

    /// <summary>Re-send the state-6 highlight at most this often while waiting for the surface to reach 30.</summary>
    private const int HighlightRetryMs = 400;

    /// <summary>
    /// If the surface is still at state 6 this long after the first highlight, commit there anyway
    /// (the original same-frame protocol, verified on Emj) rather than let the turn timer run out.
    /// </summary>
    private const int HighlightGiveUpMs = 1200;

    /// <summary>The commit waits at least this long after the state-30 highlight, so the two never share a frame.</summary>
    private const int CommitAfterHighlightMs = 100;

    private static long lastHighlightTick;
    private static long highlightSince;
    private static int highlightSlot = -1;

    // The state-30 highlight that a commit must follow: which slot, and when.
    private static int armedSlot = -1;
    private static long armedTick;

    /// <summary>Discard the given hand slot (0-12 hand position, 13 = drawn tile).</summary>
    public static bool DiscardSlot(int slot)
    {
        if (slot is < 0 or > 13)
        {
            MjLog.Warn($"discard: slot {slot} out of range.");
            return false;
        }

        if (!EmjBoardReader.TryReadHand(out HandSnapshot snap) || snap.SlotTiles[slot] < 0)
        {
            MjLog.Warn($"discard: slot {slot} is empty or hand unreadable.");
            return false;
        }

        return Commit(slot, snap.SlotIcons[slot]);
    }

    /// <summary>
    /// Find the given tile in the current hand and discard it, preferring a plain copy over a red
    /// five so the aka's extra han stays in the hand whenever possible.
    /// </summary>
    public static bool DiscardTile(int tile)
    {
        if (!EmjBoardReader.TryReadHand(out HandSnapshot snap))
        {
            MjLog.Warn("discard: could not read hand.");
            return false;
        }

        int akaSlot = -1;
        for (int slot = 0; slot < snap.SlotTiles.Length; slot++)
        {
            if (snap.SlotTiles[slot] != tile)
                continue;
            if (!EmjTiles.IsAkaIcon(snap.SlotIcons[slot]))
                return Commit(slot, snap.SlotIcons[slot]);
            akaSlot = slot;
        }

        if (akaSlot >= 0)
            return Commit(akaSlot, snap.SlotIcons[akaSlot]);

        MjLog.Warn($"discard: tile {Engine.Tiles.Name(tile)} not in hand.");
        return false;
    }

    private static bool Commit(int slot, int iconId)
    {
        if (!EmjBoardReader.TryGetAddon(out AtkUnitBase* addon))
        {
            MjLog.Warn("discard: Emj addon not present.");
            return false;
        }

        int state = addon->AtkValues != null ? addon->AtkValues[0].Int : -1;
        if (state != StateDiscardTurn && state != StateAfterDraw && state != StateAfterCall)
        {
            MjLog.Warn($"discard: not our turn (state={state}, need {StateAfterDraw}, {StateAfterCall} or {StateDiscardTurn}).");
            return false;
        }

        long now = Environment.TickCount64;
        if (state == StateAfterDraw && iconId > 0)
        {
            if (slot != highlightSlot || now - lastHighlightTick > HighlightGiveUpMs + HighlightRetryMs)
            {
                highlightSlot = slot;
                highlightSince = now;
                lastHighlightTick = 0;
            }

            if (now - highlightSince < HighlightGiveUpMs)
            {
                // Highlight only; the commit waits for the surface to move to state 30, as in manual play.
                if (now - lastHighlightTick >= HighlightRetryMs)
                {
                    lastHighlightTick = now;
                    FireInts(addon, OpHighlight, iconId);
                    MjLog.Debug($"discard: highlighted [15, {iconId}] at state 6; committing at state 30.");
                }
                return false;
            }

            MjLog.Warn($"discard: still at state 6 {now - highlightSince}ms after highlighting slot {slot}; committing there.");
        }

        highlightSlot = -1;

        if (state == StateDiscardTurn && iconId > 0 && (armedSlot != slot || now - armedTick > HighlightGiveUpMs))
        {
            // Select the tile on this tick; the commit follows alone on a later one, as in manual play.
            FireInts(addon, OpHighlight, iconId);
            armedSlot = slot;
            armedTick = now;
            return false;
        }

        if (state == StateDiscardTurn && iconId > 0 && now - armedTick < CommitAfterHighlightMs)
            return false;

        armedSlot = -1;

        if (state == StateDiscardTurn && EmjClicker.Enabled)
        {
            if (EmjClicker.TryClickSlot(slot))
            {
                MjLog.Info($"discard: clicked slot {slot} (icon {iconId}, state {state}).");
                return true;
            }

            MjLog.Warn($"discard: click on slot {slot} unavailable; falling back to the callback.");
        }

        // States 6 (after the give-up) and 28 keep the original same-frame handshake.
        if (iconId > 0 && state != StateDiscardTurn)
            FireInts(addon, OpHighlight, iconId);
        FireInts(addon, OpDiscardCommit, slot);

        MjLog.Info($"discard: fired [7, {slot}] (icon {iconId}, state {state}).");
        return true;
    }

    /// <summary>
    /// Pick a call / self-action option ([11, option]) in a call (15) or self-action (6/30) popup.
    /// State 30 carries the Riichi/Pass prompt (captured live: manual riichi fired [11, 0] at state 30).
    /// </summary>
    public static bool Call(int option)
    {
        if (!EmjBoardReader.TryGetAddon(out AtkUnitBase* addon))
        {
            MjLog.Warn("call: Emj addon not present.");
            return false;
        }

        int state = addon->AtkValues != null ? addon->AtkValues[0].Int : -1;
        if (state is not (6 or 15 or 30))
        {
            MjLog.Warn($"call: not a call/self-action state (state={state}).");
            return false;
        }

        FireInts(addon, OpCallOption, option);
        MjLog.Info($"call: fired [11, {option}] (state {state}).");
        return true;
    }

    /// <summary>Fire the captured advance opcode for the current result / new-hand state (test only).</summary>
    public static bool Advance()
    {
        if (!EmjBoardReader.TryGetAddon(out AtkUnitBase* addon))
        {
            MjLog.Warn("advance: Emj addon not present.");
            return false;
        }

        int state = addon->AtkValues != null ? addon->AtkValues[0].Int : -1;
        int op = state switch
        {
            29 => 14, // agari / result modal
            32 => 17, // end-of-hand transitional
            2 => 9,   // new-hand acknowledge
            _ => -1,
        };

        if (op < 0)
        {
            MjLog.Warn($"advance: nothing to advance (state={state}).");
            return false;
        }

        FireOne(addon, op);
        MjLog.Info($"advance: fired [{op}] (state {state}).");
        return true;
    }

    private static void FireInts(AtkUnitBase* addon, int op, int arg)
    {
        AtkValue* values = stackalloc AtkValue[2];
        values[0].SetInt(op);
        values[1].SetInt(arg);
        addon->FireCallback(2, values, true);
    }

    private static void FireOne(AtkUnitBase* addon, int op)
    {
        AtkValue* values = stackalloc AtkValue[1];
        values[0].SetInt(op);
        addon->FireCallback(1, values, true);
    }
}
