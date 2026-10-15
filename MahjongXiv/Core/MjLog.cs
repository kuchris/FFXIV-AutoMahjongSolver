using MahjongXiv.Core.Game;
using Dalamud.Plugin.Services;

namespace MahjongXiv.Core;

internal static class MjLog
{
    private static IPluginLog Log => Plugin.Log;

    public static void Info(string message)
    {
        Log.Information($"{MjConstants.LogPrefix} {message}");
        EmjAgentProbe.Mirror("INF", message);
    }

    public static void Warn(string message)
    {
        Log.Warning($"{MjConstants.LogPrefix} {message}");
        EmjAgentProbe.Mirror("WRN", message);
    }

    public static void Error(string message)
    {
        Log.Error($"{MjConstants.LogPrefix} {message}");
        EmjAgentProbe.Mirror("ERR", message);
    }

    public static void Debug(string message)
    {
        Log.Debug($"{MjConstants.LogPrefix} {message}");
        EmjAgentProbe.Mirror("DBG", message);
    }
}
