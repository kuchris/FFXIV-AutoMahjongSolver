using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Interface.Windowing;
using MahjongXiv.Core;
using MahjongXiv.Engine;

namespace MahjongXiv.Windows;

/// <summary>
/// The single compact window: table status, mode, start/stop, what the solver suggests or the bot is
/// doing, the session tally, and the settings in a collapsible section.
/// </summary>
public sealed class MainWindow : Window, IDisposable
{
    private const int AlternativeCount = 3;

    private readonly Plugin plugin;
    private readonly LiveBoard board = new();

    public MainWindow(Plugin plugin) : base("MahjongXIV###MahjongXivMain")
    {
        this.plugin = plugin;
        Size = new Vector2(340, 300);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 180),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        AutoPlayController auto = plugin.AutoPlay;
        Configuration cfg = Plugin.Cfg;

        board.Refresh();

        DrawStatus(auto);
        DrawControls(auto);
        ImGui.Separator();

        if (auto.BlockedReason is { } blocked)
            ImGui.TextColored(Styling.AccentAmber, blocked);
        DrawWarnings();

        if (auto.Running && auto.Mode == PlayMode.Auto)
            DrawAutoActivity(auto);
        else
            DrawSuggestion();

        ImGui.Spacing();
        if (ImGui.CollapsingHeader("Settings"))
            DrawSettings(cfg);

        ImGui.Spacing();
        ImGui.TextColored(Styling.TextMuted, "Fork of FFXIV-AutoMahjongSolver by Xeldar Alz · AGPL-3.0");
    }

    // ---- Status and controls ---------------------------------------------

    private void DrawStatus(AutoPlayController auto)
    {
        (string text, Vector4 color) = board.Phase switch
        {
            BoardPhase.OurTurn => ("Your turn", Styling.AccentSoft),
            BoardPhase.CallOffered => ("Call offered", Styling.AccentAmberSoft),
            BoardPhase.BetweenHands => ("Between hands", Styling.AccentBlueSoft),
            BoardPhase.Watching => ("Watching the table", Styling.TextDim),
            _ => ("No match open", Styling.TextMuted),
        };

        ImGui.TextColored(color, $"● {text}");
        ImGui.SameLine();
        string engaged = !auto.Running ? "stopped" : auto.Mode == PlayMode.Auto ? "auto-play on" : "suggesting";
        ImGui.TextColored(auto.Running ? Styling.AccentMint : Styling.TextMuted, $"· {engaged}");
    }

    private static void DrawControls(AutoPlayController auto)
    {
        if (ImGui.RadioButton("Suggest", auto.Mode == PlayMode.Suggest))
            auto.SetMode(PlayMode.Suggest);
        ImGui.SameLine();
        if (ImGui.RadioButton("Auto-play", auto.Mode == PlayMode.Auto))
            auto.SetMode(PlayMode.Auto);

        ImGui.SameLine();
        float width = 80f * ImGui.GetIO().FontGlobalScale;
        ImGui.SetCursorPosX(Math.Max(ImGui.GetCursorPosX(), ImGui.GetWindowContentRegionMax().X - width));
        if (ImGui.Button(auto.Running ? "Stop" : "Start", new Vector2(width, 0)))
            auto.SetRunning(!auto.Running);
    }

    private void DrawWarnings()
    {
        if (board.TilesUnreadable)
            ImGui.TextColored(Styling.AccentRose, "Tiles not recognized: use the default tile design.");
        if (Plugin.ClientState.ClientLanguage != ClientLanguage.English)
            ImGui.TextColored(Styling.AccentAmber, "Non-English client: seat wind and riichi may be misread.");
    }

    // ---- Suggest mode -----------------------------------------------------

    private void DrawSuggestion()
    {
        if (!board.Present)
        {
            ImGui.TextColored(Styling.TextDim, "Open a Doman Mahjong table to see suggestions.");
            return;
        }

        if (!board.HandRead)
        {
            ImGui.TextColored(Styling.TextDim, "Reading your hand…");
            return;
        }

        HandReport report = board.Report;
        ImGui.TextColored(Styling.TextSecondary, $"Hand: {board.Snapshot.Notation}   ({ShantenText(report.Shanten)})");

        if (board.HandComplete)
        {
            ImGui.TextColored(Styling.AccentAmber, "Complete hand: declare the win.");
            return;
        }

        if (report.Shape == HandShape.Discard && report.Discards.Count > 0)
        {
            DiscardOption best = report.Discards[0];
            ImGui.Text("Discard");
            ImGui.SameLine();
            ImGui.TextColored(Styling.AccentSoft, Tiles.Name(best.Tile));
            ImGui.SameLine();
            ImGui.TextColored(Styling.TextDim, $"→ {ShantenText(best.Shanten)}, {best.UkeireCount} tiles accept");

            for (int i = 1; i <= AlternativeCount && i < report.Discards.Count; i++)
            {
                DiscardOption alt = report.Discards[i];
                ImGui.TextColored(Styling.TextMuted, $"   or {Tiles.Name(alt.Tile)}: {ShantenText(alt.Shanten)}, {alt.UkeireCount} tiles");
            }
        }
        else if (report.Shape == HandShape.Wait && report.Wait.Count > 0)
        {
            string waits = string.Join(" ", report.Wait.Tiles.Select(t => Tiles.Name(t.Tile)));
            ImGui.TextColored(Styling.TextSecondary, $"Waiting on {waits} ({report.Wait.Count} tiles)");
        }
    }

    private static string ShantenText(int shanten) => shanten switch
    {
        ShantenCalculator.Complete => "complete",
        0 => "tenpai",
        _ => $"{shanten}-shanten",
    };

    // ---- Auto mode --------------------------------------------------------

    private void DrawAutoActivity(AutoPlayController auto)
    {
        string now = board.Phase switch
        {
            BoardPhase.OurTurn when board.HandComplete => "Declaring the win",
            BoardPhase.OurTurn => "Playing the turn",
            BoardPhase.CallOffered => board.RonOffered ? "Taking the ron" : "Answering the call",
            BoardPhase.BetweenHands => "Waiting for the next hand",
            BoardPhase.Watching => "Watching the table",
            _ => "Waiting for a match",
        };
        ImGui.TextColored(Styling.AccentSoft, now);

        SessionStats stats = auto.Stats;
        TimeSpan t = stats.Elapsed;
        string elapsed = t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
        ImGui.TextColored(Styling.TextSecondary, $"Wins {stats.Wins}   Discards {stats.Discards}   Riichi {stats.Riichis}   {elapsed}");

        if (stats.Recent.Count > 0)
            ImGui.TextColored(Styling.TextMuted, $"Last: {stats.Recent[0].Label}");
    }

    // ---- Settings ---------------------------------------------------------

    private static void DrawSettings(Configuration cfg)
    {
        bool ranked = cfg.AllowRankedAutoPlay;
        if (ImGui.Checkbox("Auto-play on ranked tables", ref ranked))
        {
            cfg.AllowRankedAutoPlay = ranked;
            cfg.Save();
        }

        bool highlight = cfg.HighlightSuggestedTile;
        if (ImGui.Checkbox("Highlight the suggested tile", ref highlight))
        {
            cfg.HighlightSuggestedTile = highlight;
            cfg.Save();
        }

        if (cfg.HighlightSuggestedTile)
        {
            EnumCombo("Style", cfg.HighlightStyle, v => cfg.HighlightStyle = v);
            EnumCombo("Colour", cfg.HighlightColor, v => cfg.HighlightColor = v);
        }

        ImGui.TextColored(Styling.TextDim, "Auto-play delays (ms, random within range)");
        DelayRange("Discard", () => cfg.TurnDelayMinMs, v => cfg.TurnDelayMinMs = v, () => cfg.TurnDelayMaxMs, v => cfg.TurnDelayMaxMs = v, cfg);
        DelayRange("Call", () => cfg.CallDelayMinMs, v => cfg.CallDelayMinMs = v, () => cfg.CallDelayMaxMs, v => cfg.CallDelayMaxMs = v, cfg);
        DelayRange("Results", () => cfg.ResultDelayMinMs, v => cfg.ResultDelayMinMs = v, () => cfg.ResultDelayMaxMs, v => cfg.ResultDelayMaxMs = v, cfg);
    }

    private static void EnumCombo<T>(string label, T current, Action<T> set) where T : struct, Enum
    {
        ImGui.SetNextItemWidth(140f * ImGui.GetIO().FontGlobalScale);
        if (!ImGui.BeginCombo(label, current.ToString()))
            return;

        foreach (T value in Enum.GetValues<T>())
        {
            if (ImGui.Selectable(value.ToString(), value.Equals(current)))
            {
                set(value);
                Plugin.Cfg.Save();
            }
        }

        ImGui.EndCombo();
    }

    private static void DelayRange(string label, Func<int> getMin, Action<int> setMin, Func<int> getMax, Action<int> setMax, Configuration cfg)
    {
        float width = 110f * ImGui.GetIO().FontGlobalScale;

        int min = getMin();
        ImGui.SetNextItemWidth(width);
        if (ImGui.SliderInt($"##{label}_min", ref min, 0, 6000))
        {
            setMin(min);
            if (getMax() < min)
                setMax(min);
            cfg.SaveDebounced();
        }

        ImGui.SameLine();
        int max = getMax();
        ImGui.SetNextItemWidth(width);
        if (ImGui.SliderInt($"{label}##{label}_max", ref max, 0, 6000))
        {
            setMax(max);
            if (getMin() > max)
                setMin(max);
            cfg.SaveDebounced();
        }
    }
}
