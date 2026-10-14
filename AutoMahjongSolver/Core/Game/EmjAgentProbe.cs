using System.IO;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace AutoMahjongSolver.Core.Game;

/// <summary>
/// Read-only diagnostic for the ranked-table crash: logs AgentEmj's internal state machine and the
/// game module's event queue to /xllog. It sends no inputs and calls no game functions; it only reads
/// memory, at offsets taken from a disassembly of ffxiv_dx11.exe (2026-10-13), so they will drift
/// with patches. Every pointer is null- and sanity-checked before it is followed.
/// <list type="bullet">
/// <item>agent+0x28 → data; data+0xF8C = state, data+0xF90 = queued next state; data+0xF80 → module.</item>
/// <item>module+0x8 → inner; event ring at inner+0x10 (slot pointers), +0x18 capacity, +0x20 head,
/// +0x28 count; an event's first dword is its type. State 8 enters state 42 on event 11.</item>
/// <item>State 42 reads standings indices from inner+0x440 (count at [inner+0x468]+0x1418) and the
/// crash read garbage indices there.</item>
/// </list>
/// </summary>
internal sealed unsafe class EmjAgentProbe : IDisposable
{
    private const int OffData = 0x28;
    private const int OffState = 0xF8C;
    private const int OffNextState = 0xF90;
    private const int OffModule = 0xF80;
    private const int OffInner = 0x8;
    private const int OffRing = 0x10;
    private const int OffCapacity = 0x18;
    private const int OffHead = 0x20;
    private const int OffCount = 0x28;
    private const int OffStandings = 0x430 + 0x10;
    private const int OffStandingsOwner = 0x468;
    private const int OffStandingsCount = 0x1418;

    private string? filePath;
    private bool probing;
    private int lastState = int.MinValue;
    private int lastNext = int.MinValue;
    private long lastQueueCount = -1;
    private int lastFrontType = int.MinValue;

    public bool Probing => probing;

    public void SetProbing(bool on)
    {
        if (on == probing)
            return;
        probing = on;
        lastState = lastNext = lastFrontType = int.MinValue;
        lastQueueCount = -1;

        if (on)
        {
            filePath = Path.Combine(Plugin.PluginInterface.GetPluginConfigDirectory(), "probe.log");
            Plugin.Framework.Update += OnUpdate;
            Log($"Probe ON: logging AgentEmj state changes and module events (read-only) to /xllog and {filePath}.");
        }
        else
        {
            Plugin.Framework.Update -= OnUpdate;
            Log("Probe OFF.");
        }
    }

    public void Toggle() => SetProbing(!probing);

    private void OnUpdate(IFramework framework)
    {
        try
        {
            Poll();
        }
        catch (Exception ex)
        {
            AmjsLog.Error($"probe: {ex.Message}");
            SetProbing(false);
        }
    }

    private void Poll()
    {
        AgentModule* agents = AgentModule.Instance();
        if (agents == null)
            return;

        byte* agent = (byte*)agents->GetAgentByInternalId(AgentId.Emj);
        byte* data = agent == null ? null : *(byte**)(agent + OffData);
        if (data == null)
        {
            if (lastState != int.MinValue)
                Log("probe: agent data gone (left the table).");
            lastState = lastNext = int.MinValue;
            return;
        }

        int state = *(int*)(data + OffState);
        int next = *(int*)(data + OffNextState);
        int addonState = EmjBoardReader.ReadStateCode();

        if (state != lastState || next != lastNext)
        {
            Log($"probe: agent state {Fmt(lastState)} -> {state} (next {next}), addon state {addonState}.");
            if (state == 42)
                LogStandings(data);
            lastState = state;
            lastNext = next;
        }

        byte* inner = Inner(data);
        if (inner == null)
            return;

        long count = *(long*)(inner + OffCount);
        int front = count > 0 ? FrontType(inner) : int.MinValue;
        if (count != lastQueueCount || front != lastFrontType)
        {
            if (count > 0)
                Log($"probe: event queue count {count}, front type {Fmt(front)} (agent state {state}, addon state {addonState}).");
            lastQueueCount = count;
            lastFrontType = front;
        }
    }

    private static byte* Inner(byte* data)
    {
        byte* module = *(byte**)(data + OffModule);
        return module == null ? null : *(byte**)(module + OffInner);
    }

    /// <summary>The front event's type, or int.MinValue when the ring looks inconsistent.</summary>
    private static int FrontType(byte* inner)
    {
        byte** ring = *(byte***)(inner + OffRing);
        long capacity = *(long*)(inner + OffCapacity);
        long head = *(long*)(inner + OffHead);
        if (ring == null || capacity <= 0 || capacity > 4096 || (capacity & (capacity - 1)) != 0)
            return int.MinValue;

        byte* evt = ring[head & (capacity - 1)];
        return evt == null ? int.MinValue : *(int*)evt;
    }

    private void LogStandings(byte* data)
    {
        byte* inner = Inner(data);
        if (inner == null)
        {
            Log("probe: state 42 with no module.");
            return;
        }

        byte* owner = *(byte**)(inner + OffStandingsOwner);
        string count = owner == null ? "null owner" : (*(int*)(owner + OffStandingsCount)).ToString();
        int* idx = (int*)(inner + OffStandings);
        Log($"probe: state 42 standings count {count}, indices {idx[0]} {idx[1]} {idx[2]} {idx[3]}.");
    }

    /// <summary>
    /// Log to /xllog and append synchronously to probe.log: the crash this hunts kills the process a
    /// frame after state 42, before the plugin log's async sink may have flushed.
    /// </summary>
    private void Log(string message)
    {
        AmjsLog.Info(message);
        if (filePath is null)
            return;
        try
        {
            File.AppendAllText(filePath, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
    }

    private static string Fmt(int v) => v == int.MinValue ? "-" : v.ToString();

    public void Dispose() => SetProbing(false);
}
