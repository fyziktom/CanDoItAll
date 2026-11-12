using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Composition;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Web.Infrastructure;

public enum ProjectDeletionRecoveryCommandKind { Inspect = 1, Reconcile = 2, InspectLegacy = 3, InstallLegacy = 4 }

public sealed record ProjectDeletionRecoveryCommand(ProjectDeletionRecoveryCommandKind Kind,
    ProjectDeletionCompletionAddress Address, ProjectDeletionRecoveryDigest? ExpectedPlan,
    ProjectDeletionRecoveryPrecondition? Precondition, ProjectWriteAdmission? LegacyProject = null,
    ProjectDeletionLegacyPrecondition? LegacyPrecondition = null) {
    public const string CommandName = "project-deletion-recovery";
    private const string ProfileOption = "--profile-id";
    private const string NamespaceOption = "--expected-namespace";
    private const string CompletionOption = "--completion-id";
    private const string PlanOption = "--expected-plan-digest";
    private const string StoppedOption = "--writers-stopped";
    private const string FormerSourceStoppedOption = "--former-source-hosts-stopped";
    private const string ProjectOption = "--project-id";
    private const string LifetimeOption = "--lifetime-id";
    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    public static ProjectDeletionRecoveryCommand? Parse(string[] args) {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 0 || args[0] != CommandName) {
            return null;
        }
        if (args.Length is < 8 or > 16) {
            throw new ArgumentException("Deletion recovery requires one action and exact profile, namespace and completion IDs.");
        }
        var kind = args[1] switch {
            "inspect" => ProjectDeletionRecoveryCommandKind.Inspect,
            "reconcile" => ProjectDeletionRecoveryCommandKind.Reconcile,
            "inspect-legacy" => ProjectDeletionRecoveryCommandKind.InspectLegacy,
            "install-legacy" => ProjectDeletionRecoveryCommandKind.InstallLegacy,
            _ => throw new ArgumentException("Unsupported deletion recovery action.")
        };
        var values = new Dictionary<string, Guid>(StringComparer.Ordinal);
        ProjectDeletionRecoveryDigest? plan = null;
        bool stopped = false;
        bool formerSourceStopped = false;
        for (int index = 2; index < args.Length; index++) {
            string option = args[index];
            if (option == FormerSourceStoppedOption) {
                if (formerSourceStopped) {
                    throw new ArgumentException("The former-source-host acknowledgement cannot be repeated.");
                }
                formerSourceStopped = true;
                continue;
            }
            if (option == StoppedOption) {
                if (stopped) {
                    throw new ArgumentException("The stopped-writer acknowledgement cannot be repeated.");
                }
                stopped = true;
                continue;
            }
            if (++index == args.Length) {
                throw new ArgumentException("Every recovery value option requires a value.");
            }
            if (option == PlanOption) {
                if (plan is not null) {
                    throw new ArgumentException("The expected recovery plan cannot be repeated.");
                }
                plan = new(args[index]);
            } else if (option is not (ProfileOption or NamespaceOption or CompletionOption or ProjectOption or LifetimeOption) ||
                !Guid.TryParseExact(args[index], "D", out Guid value) || value == Guid.Empty || !values.TryAdd(option, value)) {
                throw new ArgumentException("Recovery accepts only distinct supported options with exact nonempty D-format UUIDs.");
            }
        }
        bool legacy = kind is ProjectDeletionRecoveryCommandKind.InspectLegacy or ProjectDeletionRecoveryCommandKind.InstallLegacy;
        bool mutation = kind is ProjectDeletionRecoveryCommandKind.Reconcile or ProjectDeletionRecoveryCommandKind.InstallLegacy;
        if (values.Count != (legacy ? 5 : 3) || !values.ContainsKey(ProfileOption) || !values.ContainsKey(NamespaceOption) ||
                !values.ContainsKey(CompletionOption) || legacy && (!values.ContainsKey(ProjectOption) || !values.ContainsKey(LifetimeOption)) ||
                !legacy && (values.ContainsKey(ProjectOption) || values.ContainsKey(LifetimeOption)) ||
                mutation != stopped || mutation != (plan is not null) ||
                formerSourceStopped != (kind == ProjectDeletionRecoveryCommandKind.InstallLegacy)) {
            throw new ArgumentException("Recovery writes require the inspected digest and --writers-stopped. Legacy actions require exact project/lifetime IDs; install-legacy also requires --former-source-hosts-stopped.");
        }
        return new(kind, new(values[ProfileOption], new DatasetNamespaceId(values[NamespaceOption]), new(values[CompletionOption])), plan,
            stopped ? ProjectDeletionRecoveryPrecondition.AllWriterHostsStopped : null,
            legacy ? new(values[ProfileOption], values[ProjectOption], values[LifetimeOption]) : null,
            formerSourceStopped ? ProjectDeletionLegacyPrecondition.AllWriterAndFormerSourceOwnerHostsStopped : null);
    }

    public static async Task<int> RunSelectedAsync(string[] args, CancellationToken cancellationToken = default) {
        ProjectDeletionRecoveryResult? result = null;
        try {
            var command = Parse(args) ?? throw new ArgumentException("The deletion recovery command was not selected.");
            var builder = WebApplication.CreateBuilder(Array.Empty<string>());
            using var configuration = (IDisposable)builder.Configuration;
            using var logging = LoggerFactory.Create(logging => logging.AddConsole(options =>
                options.LogToStandardErrorThreshold = LogLevel.Trace));
            await using var session = await ProjectDeletionRecoveryComposition.OpenAsync(builder.Configuration,
                builder.Environment, command.Address.DatabaseProfileId, logging.CreateLogger<ProjectDeletionRecoveryCoordinator>(), cancellationToken);
            result = command.Kind switch {
                ProjectDeletionRecoveryCommandKind.Inspect => await session.Coordinator.InspectAsync(command.Address, cancellationToken),
                ProjectDeletionRecoveryCommandKind.Reconcile => await session.Coordinator.ReconcileAsync(command.Address,
                    command.ExpectedPlan!, command.Precondition!.Value, cancellationToken),
                ProjectDeletionRecoveryCommandKind.InspectLegacy => await session.LegacyInstaller.InspectAsync(
                    new(command.Address, command.LegacyProject!), cancellationToken),
                ProjectDeletionRecoveryCommandKind.InstallLegacy => await session.LegacyInstaller.InstallAsync(
                    new(command.Address, command.LegacyProject!), command.ExpectedPlan!, command.LegacyPrecondition!.Value, cancellationToken),
                _ => throw new ArgumentException("Unsupported deletion recovery action.")
            };
        } catch (Exception failure) {
            Console.Error.WriteLine($"Offline deletion recovery setup or terminal cleanup failed: {failure.GetType().Name}.");
            result = PreserveAfterCleanupFailure(result);
            if (result is null) {
                Console.WriteLine(JsonSerializer.Serialize(new { status = "Refused", reason = "CommandOrProfileUnavailable" }, Json));
                return 2;
            }
        }
        string status = result switch {
            ProjectDeletionRecoveryResult.Observed => "Observed",
            ProjectDeletionRecoveryResult.LegacyObserved => "LegacyObserved",
            ProjectDeletionRecoveryResult.InstalledAwaitingRelease => "InstalledAwaitingRelease",
            ProjectDeletionRecoveryResult.Released => "Released",
            ProjectDeletionRecoveryResult.Refused => "Refused",
            ProjectDeletionRecoveryResult.Unconfirmed => "Unconfirmed",
            _ => throw new InvalidOperationException("Recovery returned no closed outcome.")
        };
        Console.WriteLine(JsonSerializer.Serialize(new { status, result = JsonSerializer.SerializeToElement(result, result.GetType(), Json) }, Json));
        return result switch {
            ProjectDeletionRecoveryResult.Observed => 0,
            ProjectDeletionRecoveryResult.LegacyObserved => 0,
            ProjectDeletionRecoveryResult.InstalledAwaitingRelease { CleanupUnconfirmed: false, DiagnosticsUnavailable: false } => 0,
            ProjectDeletionRecoveryResult.InstalledAwaitingRelease => 4,
            ProjectDeletionRecoveryResult.Released { CleanupUnconfirmed: false, DiagnosticsUnavailable: false } => 0,
            ProjectDeletionRecoveryResult.Released => 4,
            ProjectDeletionRecoveryResult.Refused => 2,
            ProjectDeletionRecoveryResult.Unconfirmed => 3,
            _ => 2
        };
    }

    internal static ProjectDeletionRecoveryResult? PreserveAfterCleanupFailure(ProjectDeletionRecoveryResult? result)
        => result switch {
            ProjectDeletionRecoveryResult.Released released => released with { CleanupUnconfirmed = true },
            ProjectDeletionRecoveryResult.Unconfirmed unconfirmed => unconfirmed with { CleanupUnconfirmed = true },
            ProjectDeletionRecoveryResult.InstalledAwaitingRelease installed => installed with { CleanupUnconfirmed = true },
            _ => null
        };

    private static JsonSerializerOptions CreateJsonOptions() {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
