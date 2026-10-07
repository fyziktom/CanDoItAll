namespace CanDoItAll.Tests.Playwright.Plugins;

public enum PluginsProbeCommand { HoldReadback, FailReadback, Release, Stop }

public static class PluginsProbeProtocol {
    public const string Ready = "PLUGINS-PROBE-READY";
    public const string ReadHeld = "PLUGINS-PROBE-READ-HELD";
    public const string Stopping = "PLUGINS-PROBE-STOPPING";
    public static string Ack(PluginsProbeCommand command) => $"PLUGINS-PROBE-ACK:{command}";
}
