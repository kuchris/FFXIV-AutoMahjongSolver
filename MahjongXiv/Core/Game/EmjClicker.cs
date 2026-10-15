using FFXIVClientStructs.FFXIV.Component.GUI;

namespace MahjongXiv.Core.Game;

/// <summary>
/// Discards by simulating a real click on a hand tile instead of firing the [7, slot] callback directly.
/// A direct callback skips the addon's own click handling, which on the ranked EmjL table left the
/// "Time remaining" panel up; the stale panel later fired its own discard ([7, 12]) and, on the next
/// real click, an op 14 that sent AgentEmj into its end-of-match path. A simulated click runs the
/// addon's handler, which fires [7, slot] itself and tidies its own state, exactly like a mouse click.
/// The click replays the event the addon registered on the tile's button node.
/// </summary>
internal static unsafe class EmjClicker
{
    /// <summary>
    /// Discard by simulated click (default). <c>/mj click-discard off</c> falls back to the raw callback
    /// for the current session.
    /// </summary>
    public static bool Enabled { get; set; } = true;

    private static bool resultButtonsLogged;

    /// <summary>Dump the result screen's controls once per session, to find the hand-change button.</summary>
    public static void DumpButtonsOnce()
    {
        if (resultButtonsLogged)
            return;
        resultButtonsLogged = true;
        DumpButtons();
    }

    /// <summary>Click hand slot 0-13 (13 = drawn tile). False when the slot or its click event can't be found.</summary>
    public static bool TryClickSlot(int slot)
    {
        if (!EmjBoardReader.TryGetAddon(out AtkUnitBase* addon))
            return false;

        AtkResNode* button = SlotButton(addon, slot);
        if (button == null)
        {
            MjLog.Warn($"click: slot {slot} has no button node.");
            return false;
        }

        AtkEvent* evt = PickClickEvent(addon, button);
        if (evt == null)
        {
            MjLog.Warn($"click: slot {slot} button has no click event registered by the addon.");
            return false;
        }

        var data = new AtkEventData();
        addon->ReceiveEvent(evt->State.EventType, (int)evt->Param, evt, &data);
        MjLog.Info($"click: slot {slot} via {evt->State.EventType} (param {evt->Param}).");
        return true;
    }

    /// <summary>Log every event registered on each hand slot's component node and button node.</summary>
    public static void DumpEvents()
    {
        if (!EmjBoardReader.TryGetAddon(out AtkUnitBase* addon))
        {
            MjLog.Info("events: no table addon.");
            return;
        }

        for (int slot = 0; slot <= 13; slot++)
        {
            AtkResNode* node = SlotNode(addon, slot);
            AtkResNode* button = SlotButton(addon, slot);
            MjLog.Info($"events: slot {slot} node {Describe(addon, node)} | button {Describe(addon, button)}");
        }
    }

    /// <summary>
    /// Log every visible node in the table addon that carries an event registered by the addon — the
    /// clickable controls (result-screen buttons, call buttons). Used to find what to click instead of
    /// firing the raw advance/call callbacks.
    /// </summary>
    public static void DumpButtons()
    {
        if (!EmjBoardReader.TryGetAddon(out AtkUnitBase* addon))
        {
            MjLog.Info("buttons: no table addon.");
            return;
        }

        int state = addon->AtkValues != null ? addon->AtkValues[0].Int : -1;
        MjLog.Info($"buttons: addon state {state}, {addon->UldManager.NodeListCount} top-level nodes.");
        int found = 0;
        WalkUld(addon, &addon->UldManager, "", ref found, depth: 0);
        MjLog.Info($"buttons: {found} visible node(s) with addon events.");
    }

    private static void WalkUld(AtkUnitBase* addon, AtkUldManager* uld, string path, ref int found, int depth)
    {
        if (uld == null || depth > 6)
            return;

        for (int i = 0; i < uld->NodeListCount; i++)
        {
            AtkResNode* node = uld->NodeList[i];
            if (node == null || !node->IsVisible())
                continue;

            string here = $"{path}/{node->NodeId}";
            bool hasAddonEvent = false;
            for (AtkEvent* evt = node->AtkEventManager.Event; evt != null; evt = evt->NextEvent)
                if (evt->Listener == (AtkEventListener*)addon)
                    hasAddonEvent = true;

            if (hasAddonEvent && found < 80)
            {
                found++;
                MjLog.Info($"buttons: {here} {Describe(addon, node)}");
            }

            if ((int)node->Type >= 1000)
            {
                AtkComponentBase* component = ((AtkComponentNode*)node)->Component;
                if (component != null)
                    WalkUld(addon, &component->UldManager, here, ref found, depth + 1);
            }
        }
    }

    private static string Describe(AtkUnitBase* addon, AtkResNode* node)
    {
        if (node == null)
            return "null";

        var parts = new List<string>();
        for (AtkEvent* evt = node->AtkEventManager.Event; evt != null && parts.Count < 16; evt = evt->NextEvent)
        {
            string owner = evt->Listener == (AtkEventListener*)addon ? "addon" : $"0x{(nint)evt->Listener:X}";
            parts.Add($"{evt->State.EventType}/param {evt->Param}/{owner}");
        }

        return $"id {node->NodeId} [{(parts.Count == 0 ? "no events" : string.Join(", ", parts))}]";
    }

    /// <summary>
    /// Prefer the addon's ButtonClick event, then any other event the addon registered, then the first
    /// event on the node.
    /// </summary>
    private static AtkEvent* PickClickEvent(AtkUnitBase* addon, AtkResNode* node)
    {
        AtkEvent* anyAddon = null;
        for (AtkEvent* evt = node->AtkEventManager.Event; evt != null; evt = evt->NextEvent)
        {
            if (evt->Listener != (AtkEventListener*)addon)
                continue;
            if (evt->State.EventType == AtkEventType.ButtonClick)
                return evt;
            if (anyAddon == null)
                anyAddon = evt;
        }

        return anyAddon != null ? anyAddon : node->AtkEventManager.Event;
    }

    private static AtkResNode* SlotNode(AtkUnitBase* addon, int slot)
    {
        if (slot is < 0 or > 13)
            return null;
        uint id = slot < MjConstants.HandTileNodeIds.Length
            ? MjConstants.HandTileNodeIds[slot]
            : MjConstants.DrawnTileNodeId;
        return addon->GetNodeById(id);
    }

    /// <summary>Hand tile component node -> its button node (Uld NodeList[HandButtonNodeListIndex]).</summary>
    private static AtkResNode* SlotButton(AtkUnitBase* addon, int slot)
    {
        AtkResNode* node = SlotNode(addon, slot);
        if (node == null || (int)node->Type < 1000)
            return null;

        AtkComponentBase* component = ((AtkComponentNode*)node)->Component;
        if (component == null)
            return null;

        AtkUldManager* uld = &component->UldManager;
        int index = MjConstants.HandButtonNodeListIndex;
        return index < uld->NodeListCount ? uld->NodeList[index] : null;
    }
}
