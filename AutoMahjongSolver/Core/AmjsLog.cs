using AutoMahjongSolver.Core.Game;
using Dalamud.Plugin.Services;

namespace AutoMahjongSolver.Core;

internal static class AmjsLog
{
    private static IPluginLog Log => Plugin.Log;

    public static void Info(string message)
    {
        Log.Information($"{AmjsConstants.LogPrefix} {message}");
        EmjAgentProbe.Mirror("INF", message);
    }

    public static void Warn(string message)
    {
        Log.Warning($"{AmjsConstants.LogPrefix} {message}");
        EmjAgentProbe.Mirror("WRN", message);
    }

    public static void Error(string message)
    {
        Log.Error($"{AmjsConstants.LogPrefix} {message}");
        EmjAgentProbe.Mirror("ERR", message);
    }

    public static void Debug(string message)
    {
        Log.Debug($"{AmjsConstants.LogPrefix} {message}");
        EmjAgentProbe.Mirror("DBG", message);
    }
}
