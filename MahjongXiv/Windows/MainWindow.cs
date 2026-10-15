using System.Numerics;
using MahjongXiv.Core;
using MahjongXiv.Windows.Sections;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace MahjongXiv.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly LiveBoard board = new();

    public MainWindow(Plugin plugin) : base("MahjongXIV###MahjongXivMain")
    {
        this.plugin = plugin;
        Size = new Vector2(520, 560);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(430, 440),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        AutoPlayController auto = plugin.AutoPlay;
        using var style = Styling.PushWindowStyle();

        board.Refresh();
        Header.Draw(plugin, auto);
        Advisory.Draw(board);

        if (auto.Running)
        {
            RunningPanel.Draw(auto, board);
        }
        else
        {
            IdlePanel.Draw(auto, board);
        }
    }
}
