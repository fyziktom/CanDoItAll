using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Tests.Maintenance;

internal enum MaintenanceFrameKind { Ready, Start, CycleObservation, HealthTransition, Failure, Stop, Stopped }
internal enum MaintenanceStop { Empty, Contention, Budget, BatchLimit, Failed, HostCancellation }
internal enum MaintenanceEligibility { Unknown, NoneObserved, Present }
internal enum MaintenanceHealth { NoProgress, CapacityPressure, Recovery }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record MaintenanceCycleObservation(long DeletedReceipts, long DeletedPayloadBytes,
    MaintenanceStop Stop, MaintenanceEligibility Eligibility, double DurationMilliseconds,
    int? EligibleObserved, double? ObservedEligibilityAgeSeconds, double? SinceProgressSeconds,
    DateTimeOffset CompletedAtUtc, MaintenanceEligibility PoolEligibility);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record MaintenanceHostFrame(int Version, Guid RunId, int Worker, long Sequence,
    MaintenanceFrameKind Kind, DateTimeOffset AtUtc, Guid? Profile = null, Guid? Namespace = null,
    Guid? Maintenance = null, MaintenanceCycleObservation? Cycle = null, MaintenanceHealth? Health = null,
    string? FailureType = null, string? SqlState = null, double? ProcessCpuMilliseconds = null, int[]? BackendProcesses = null);

internal static class MaintenanceHostProtocol {
    internal const int Version = 2;
    internal const int MaximumFrameBytes = 4096;
    internal const int MaximumFrames = 2048;
    internal const int MaximumQueuedFrames = 256;
    internal const int MaximumBackendProcesses = 16;
    internal const string RootEnvironment = "CANDOITALL_MAINTENANCE_ROOT";
    internal const string Meter = "CanDoItAll.FiniteOperations.Maintenance";
    internal const string Receipts = "candoitall.finite_operations.maintenance.deleted_receipts";
    internal const string Bytes = "candoitall.finite_operations.maintenance.deleted_payload_bytes";
    internal const string Cycles = "candoitall.finite_operations.maintenance.cycles";
    internal const string Duration = "candoitall.finite_operations.maintenance.duration";
    internal const string Lateness = "candoitall.finite_operations.maintenance.scheduled_lateness";
    internal const string Eligible = "candoitall.finite_operations.maintenance.eligible_observed";
    internal const string Age = "candoitall.finite_operations.maintenance.observed_eligibility_age";
    internal const string SinceProgress = "candoitall.finite_operations.maintenance.since_progress";
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        RespectRequiredConstructorParameters = true,
        MaxDepth = 8,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    internal static bool IsDefinitionMetric(ReadOnlySpan<KeyValuePair<string, object?>> tags, bool family) {
        Guid? pool = null;
        Guid? receiptFamily = null;
        foreach (var tag in tags) {
            if (tag.Key == "pool" && pool is null && tag.Value is Guid poolId && poolId != Guid.Empty) {
                pool = poolId;
            } else if (family && tag.Key == "family" && receiptFamily is null && tag.Value is Guid familyId && familyId != Guid.Empty) {
                receiptFamily = familyId;
            } else if (family || tag.Key is not ("stop" or "eligibility")) {
                throw new InvalidDataException("The maintenance metric identity is invalid or duplicated.");
            }
        }
        if (pool is null || family && receiptFamily is null) {
            throw new InvalidDataException("The maintenance metric is missing its pool or family identity.");
        }
        return pool == ProjectFiniteOperationPool.Pool.Value &&
            (!family || receiptFamily == ProjectFiniteOperationPool.DefinitionFamily.Value);
    }

    internal static string Encode(MaintenanceHostFrame frame) {
        Validate(frame);
        var json = JsonSerializer.Serialize(frame, Json);
        if (Encoding.UTF8.GetByteCount(json) > MaximumFrameBytes) {
            throw new InvalidDataException("The maintenance observation exceeded its frame bound.");
        }
        return json;
    }

    internal static async Task<MaintenanceHostFrame?> ReadAsync(TextReader reader, CancellationToken token) {
        var line = new StringBuilder();
        var character = new char[1];
        while (await reader.ReadAsync(character.AsMemory(), token) != 0) {
            if (character[0] == '\n') {
                if (Encoding.UTF8.GetByteCount(line.ToString()) > MaximumFrameBytes) {
                    throw new InvalidDataException("The maintenance transport exceeded its UTF-8 frame bound.");
                }
                using var document = JsonDocument.Parse(line.ToString(), new JsonDocumentOptions { MaxDepth = 8 });
                RequireUniqueMembers(document.RootElement);
                var frame = document.RootElement.Deserialize<MaintenanceHostFrame>(Json)
                    ?? throw new InvalidDataException("The maintenance frame was null.");
                Validate(frame);
                return frame;
            }
            line.Append(character[0]);
            if (line.Length > MaximumFrameBytes) {
                throw new InvalidDataException("The maintenance transport exceeded its line bound.");
            }
        }
        if (line.Length != 0) {
            throw new InvalidDataException("The maintenance transport ended inside a frame.");
        }
        return null;
    }

    internal static void Validate(MaintenanceHostFrame frame) {
        if (frame.Version != Version || frame.RunId == Guid.Empty || frame.Worker is < 1 or > 2 ||
            frame.Sequence is < 1 or > MaximumFrames || !Enum.IsDefined(frame.Kind) || frame.AtUtc == default || frame.AtUtc.Offset != TimeSpan.Zero ||
            (frame.Kind == MaintenanceFrameKind.CycleObservation) != (frame.Cycle is not null) ||
            (frame.Kind == MaintenanceFrameKind.HealthTransition) != (frame.Health is not null) ||
            frame.Health is { } health && !Enum.IsDefined(health) || !FiniteNonnegative(frame.ProcessCpuMilliseconds)) {
            throw new InvalidDataException("The maintenance transport has inconsistent evidence.");
        }
        var identityPresent = frame.Profile is not null || frame.Namespace is not null || frame.Maintenance is not null;
        if (frame.Kind == MaintenanceFrameKind.Ready
                ? frame.Profile is null || frame.Profile == Guid.Empty || frame.Namespace is null || frame.Namespace == Guid.Empty ||
                    frame.Maintenance is null || frame.Maintenance == Guid.Empty
                : identityPresent) {
            throw new InvalidDataException("The maintenance Ready identity is incomplete or appears on another frame.");
        }
        if (frame.Kind == MaintenanceFrameKind.Failure
                ? string.IsNullOrEmpty(frame.FailureType) || frame.FailureType.Length > 128 ||
                    frame.FailureType.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('_' or '.' or '+')) ||
                    frame.SqlState is { } code && (code.Length != 5 || code.Any(character => !char.IsAsciiDigit(character) && character is not (>= 'A' and <= 'Z')))
                : frame.FailureType is not null || frame.SqlState is not null) {
            throw new InvalidDataException("The maintenance failure observation has invalid or misplaced diagnostics.");
        }
        if ((frame.Kind == MaintenanceFrameKind.Stopped) != (frame.BackendProcesses is not null) ||
            frame.BackendProcesses is { } processes && (processes.Length is < 1 or > MaximumBackendProcesses ||
                processes.Any(pid => pid <= 0) || processes.Distinct().Count() != processes.Length)) {
            throw new InvalidDataException("Backend process evidence is bounded, unique and exclusive to Stopped.");
        }
        if (frame.Cycle is { } cycle && (cycle.DeletedReceipts < 0 || cycle.DeletedPayloadBytes < cycle.DeletedReceipts ||
            cycle.DeletedReceipts > ProjectDefinitionOperationOptions.MaximumMaintenanceBatches * ProjectDefinitionOperationOptions.PurgeBatchCount ||
            cycle.DeletedPayloadBytes > (long)ProjectDefinitionOperationOptions.MaximumMaintenanceBatches * ProjectDefinitionOperationOptions.PurgeBatchBytes ||
            cycle.DeletedReceipts == 0 && cycle.DeletedPayloadBytes != 0 ||
            !Enum.IsDefined(cycle.Stop) || !Enum.IsDefined(cycle.Eligibility) || !Enum.IsDefined(cycle.PoolEligibility) ||
            cycle.PoolEligibility == MaintenanceEligibility.NoneObserved && cycle.Eligibility != MaintenanceEligibility.NoneObserved ||
            cycle.PoolEligibility == MaintenanceEligibility.Present && cycle.Eligibility == MaintenanceEligibility.Unknown ||
            !double.IsFinite(cycle.DurationMilliseconds) || cycle.DurationMilliseconds < 0 ||
            cycle.CompletedAtUtc == default || cycle.CompletedAtUtc.Offset != TimeSpan.Zero ||
            !FiniteNonnegative(cycle.ObservedEligibilityAgeSeconds) || !FiniteNonnegative(cycle.SinceProgressSeconds) ||
            cycle.EligibleObserved is not (null or 0 or 1) ||
            cycle.Eligibility == MaintenanceEligibility.Unknown && cycle.EligibleObserved is not null ||
            cycle.Eligibility == MaintenanceEligibility.NoneObserved && cycle.EligibleObserved != 0 ||
            cycle.Eligibility == MaintenanceEligibility.Present && cycle.EligibleObserved != 1)) {
            throw new InvalidDataException("The maintenance cycle evidence is invalid.");
        }
    }

    internal static void ValidateControl(MaintenanceHostFrame frame) {
        Validate(frame);
        if (frame.Kind is not (MaintenanceFrameKind.Start or MaintenanceFrameKind.Stop) || frame.ProcessCpuMilliseconds is not null) {
            throw new InvalidDataException("The parent sent an observation instead of a control frame.");
        }
    }

    internal static void ValidateObservation(MaintenanceHostFrame frame) {
        Validate(frame);
        if (frame.Kind == MaintenanceFrameKind.Stop || frame.ProcessCpuMilliseconds is null) {
            throw new InvalidDataException("The child sent a control frame or omitted its process observation.");
        }
    }

    private static bool FiniteNonnegative(double? value) => value is null || double.IsFinite(value.Value) && value >= 0;

    private static void RequireUniqueMembers(JsonElement element, bool root = true) {
        if (element.ValueKind == JsonValueKind.Object) {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject()) {
                if (!names.Add(property.Name)) {
                    throw new JsonException("The maintenance transport contains duplicate members.");
                }
                if (root && property.Name.Equals("backendProcesses", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.Array) {
                    if (property.Value.GetArrayLength() > MaximumBackendProcesses) {
                        throw new JsonException("The maintenance backend evidence exceeds its bound.");
                    }
                } else {
                    RequireUniqueMembers(property.Value, false);
                }
            }
        } else if (element.ValueKind == JsonValueKind.Array) {
            throw new JsonException("Maintenance frames do not contain arrays.");
        }
    }

    internal static Dictionary<string, string> ParseArguments(string[] arguments) {
        if (arguments.Length != 12) {
            throw new ArgumentException("The maintenance host requires its six explicit bounded arguments.");
        }
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < arguments.Length; index += 2) {
            var name = arguments[index];
            if (name is not ("--profile-id" or "--run-id" or "--ordinal" or "--interval-seconds" or "--warning-seconds" or "--lifetime-seconds") ||
                !values.TryAdd(name, arguments[index + 1])) {
                throw new ArgumentException("The maintenance host arguments are duplicated or unsupported.");
            }
        }
        return values;
    }

    internal static int Integer(Dictionary<string, string> values, string name) =>
        int.Parse(values[name], NumberStyles.None, CultureInfo.InvariantCulture);
}

internal sealed class MaintenanceMetricSequence {
    private long? receipts;
    private long? bytes;
    private MaintenanceStop? stop;
    private MaintenanceEligibility? eligibility;
    private double? duration;
    private int? observed;
    private double? age;
    private double? progress;
    private DateTimeOffset completedAt;

    internal MaintenanceCycleObservation? Begin(long count) {
        var previous = Complete();
        receipts = count;
        return previous;
    }

    internal void SetBytes(long value) {
        if (receipts is null || bytes is not null) {
            throw new InvalidDataException("Maintenance byte metrics arrived out of order.");
        }
        bytes = value;
    }

    internal void SetCycle(long value, MaintenanceStop valueStop, MaintenanceEligibility valueEligibility) {
        if (value != 1 || bytes is null || stop is not null) {
            throw new InvalidDataException("Maintenance cycle metrics arrived out of order.");
        }
        stop = valueStop;
        eligibility = valueEligibility;
    }

    internal void SetDuration(double value, MaintenanceStop valueStop, MaintenanceEligibility valueEligibility) {
        if (stop != valueStop || eligibility != valueEligibility || duration is not null) {
            throw new InvalidDataException("Maintenance duration metrics contradict their cycle.");
        }
        duration = value;
        completedAt = DateTimeOffset.UtcNow;
    }

    internal void SetOptional(string instrument, double value) {
        if (duration is null) {
            throw new InvalidDataException("Maintenance optional metrics arrived before duration.");
        }
        switch (instrument) {
            case MaintenanceHostProtocol.Eligible when observed is null && value is 0 or 1:
                observed = (int)value;
                break;
            case MaintenanceHostProtocol.Age when age is null && double.IsFinite(value) && value >= 0:
                age = value;
                break;
            case MaintenanceHostProtocol.SinceProgress when progress is null && double.IsFinite(value) && value >= 0:
                progress = value;
                break;
            default:
                throw new InvalidDataException("Maintenance optional metrics are invalid or duplicated.");
        }
    }

    internal MaintenanceCycleObservation? Complete() {
        if (receipts is null) {
            return null;
        }
        if (bytes is null || stop is null || eligibility is null || duration is null ||
            eligibility != MaintenanceEligibility.Unknown && observed is null ||
            eligibility == MaintenanceEligibility.NoneObserved && observed != 0 ||
            observed is not (null or 0 or 1)) {
            throw new InvalidDataException("Maintenance metrics ended with missing or contradictory evidence.");
        }
        var familyEligibility = observed switch {
            0 => MaintenanceEligibility.NoneObserved,
            1 => MaintenanceEligibility.Present,
            _ => MaintenanceEligibility.Unknown
        };
        var result = new MaintenanceCycleObservation(receipts.Value, bytes.Value, stop.Value, familyEligibility,
            duration.Value, observed, age, progress, completedAt, eligibility.Value);
        receipts = null;
        bytes = null;
        stop = null;
        eligibility = null;
        duration = null;
        observed = null;
        age = null;
        progress = null;
        return result;
    }
}
