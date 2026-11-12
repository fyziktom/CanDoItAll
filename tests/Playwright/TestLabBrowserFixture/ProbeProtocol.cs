namespace CanDoItAll.Tests.Playwright.TestLab;

public enum TestLabProbeCommand { HoldReadback, FailActivity, Release, Stop }

public static class TestLabProbeProtocol {
    public const string Ready = "TESTLAB-PROBE-READY";
    public const string ReadHeld = "TESTLAB-PROBE-READ-HELD";
    public static string Ack(TestLabProbeCommand command) => $"TESTLAB-PROBE-ACK:{command}";
}
