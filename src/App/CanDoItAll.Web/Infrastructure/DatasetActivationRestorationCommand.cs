using System.Text.Json;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.ControlPlane;

namespace CanDoItAll.Web.Infrastructure;

internal static class DatasetActivationRestorationCommand {
    internal static async Task<int> RunAsync(DatasetActivationCommand command, CancellationToken cancellationToken) {
        DatasetRestorationOutcome result = new DatasetRestorationOutcome.Refused();
        try {
            if (command.Kind != DatasetActivationCommandKind.RestorePrevious || command.ExpectedPlanId is null ||
                    command.ExpectedNamespace is null || command.MaintenanceId is not null) {
                throw new ArgumentException("Restoration requires its explicit original selection.");
            }
            var builder = WebApplication.CreateBuilder(Array.Empty<string>());
            using var configuration = (IDisposable)builder.Configuration;
            using var logging = LoggerFactory.Create(logging => logging.AddConsole(options =>
                options.LogToStandardErrorThreshold = LogLevel.Trace));
            await using var session = await DatasetActivationRestorationComposition.OpenAsync(builder.Configuration,
                builder.Environment, command.ProfileId, logging.CreateLogger<DatasetActivationRestorationCoordinator>(), cancellationToken);
            result = await session.Coordinator.RestorePreviousAsync(command.ExpectedPlanId.Value, command.ExpectedNamespace,
                DatasetRestorationPrecondition.AllWriterHostsStopped, cancellationToken);
        } catch (Exception failure) {
            Console.Error.WriteLine($"Previous dataset selection setup or terminal cleanup failed: {failure.GetType().Name}.");
            if (result is DatasetRestorationOutcome.Restored restored) {
                result = restored with { CleanupUnconfirmed = true };
            }
        }
        string status = result switch {
            DatasetRestorationOutcome.Restored => "Restored",
            DatasetRestorationOutcome.Refused => "Refused",
            DatasetRestorationOutcome.Unconfirmed => "Unconfirmed",
            _ => throw new InvalidOperationException("Restoration returned no closed outcome.")
        };
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Console.WriteLine(JsonSerializer.Serialize(new { status, result = JsonSerializer.SerializeToElement(result, result.GetType(), json) }, json));
        return result switch {
            DatasetRestorationOutcome.Restored { CleanupUnconfirmed: false, DiagnosticsUnavailable: false } => 0,
            DatasetRestorationOutcome.Restored => 4,
            DatasetRestorationOutcome.Unconfirmed => 3,
            _ => 2
        };
    }
}
