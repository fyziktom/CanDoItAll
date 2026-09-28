using System.Globalization;
using CanDoItAll.Tests.Playwright.TestLab;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Mvc.Testing;

internal static class TestLabBrowserEntry {
    public static async Task Main(string[] args) {
        if (args.Length != 2) {
            throw new ArgumentException("Provide the Web content root and owned loopback port.");
        }
        var probe = new OwnerPostcommitTestProbe();
        await using var app = new TestLabBrowserApplication(Path.GetFullPath(args[0]), probe);
        app.UseKestrel(int.Parse(args[1], CultureInfo.InvariantCulture));
        using var client = app.CreateClient();
        Console.WriteLine(TestLabProbeProtocol.Ready);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try {
            while (await Console.In.ReadLineAsync() is { } line) {
                var command = Enum.Parse<TestLabProbeCommand>(line);
                switch (command) {
                    case TestLabProbeCommand.HoldReadback:
                        released = new(TaskCreationOptions.RunContinuationsAsynchronously);
                        probe.AfterActivity = (owner, _, _) => {
                            if (owner == PostcommitOwner.TestPlan) {
                                probe.AfterActivity = null;
                                probe.BeforeRead = async (actual, token) => {
                                    if (actual == PostcommitOwner.TestPlan) {
                                        probe.BeforeRead = null;
                                        Console.WriteLine(TestLabProbeProtocol.ReadHeld);
                                        await released.Task.WaitAsync(token);
                                    }
                                };
                            }
                            return Task.CompletedTask;
                        };
                        break;
                    case TestLabProbeCommand.FailActivity:
                        probe.ArmFault(PostcommitOwner.TestPlan, PostcommitFault.Activity, new InvalidOperationException("Controlled browser postcommit activity failure"));
                        break;
                    case TestLabProbeCommand.Release:
                        released.TrySetResult();
                        break;
                    case TestLabProbeCommand.Stop:
                        return;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(command));
                }
                Console.WriteLine(TestLabProbeProtocol.Ack(command));
            }
        } finally {
            released.TrySetResult();
        }
    }
}

internal sealed class TestLabBrowserApplication(string webRoot, OwnerPostcommitTestProbe probe) : WebApplicationFactory<Program> {
    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        builder.UseEnvironment("Development");
        builder.UseContentRoot(webRoot);
        builder.ConfigureServices(probe.ConfigureServices);
    }
}
