using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Web.Api;

namespace CanDoItAll.Tests.Integration.Runtime;

internal enum ReceiptPerformanceVariant { Baseline, Candidate }
internal enum ReceiptPerformanceSampleKind { ColdOwner, Warmup, Measured }
internal sealed record ReceiptPerformanceSample(int Ordinal, ReceiptPerformanceSampleKind Kind, double ElapsedMilliseconds,
    long ProcessAllocatedBytes, ReceiptPerformanceOperation[] Results, ReceiptPerformanceStoreCost Store,
    ReceiptPerformanceMemoryWindow Memory, string[] FailureTypes);

internal sealed class AgentReceiptStorePerformanceEvidence {
    internal const string OutputRootVariable = "CDITALL_RECEIPT_PERFORMANCE_OUTPUT_ROOT";
    internal const string SourceRootVariable = "CDITALL_RECEIPT_PERFORMANCE_SOURCE_ROOT";
    internal const string SourceManifestVariable = "CDITALL_RECEIPT_PERFORMANCE_SOURCE_MANIFEST";
    internal const string VariantVariable = "CDITALL_RECEIPT_PERFORMANCE_VARIANT";
    internal const long Gibibyte = 1024L * 1024 * 1024;
    internal const long ApplicationEnvelopeBytes = 8 * Gibibyte;
    internal const long IdleReserveBytes = 2 * Gibibyte;
    internal const long IncrementalReferenceBytes = 3 * Gibibyte;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly string outputRoot;
    private readonly string prefix;
    private readonly string rowId;
    private readonly string sourceIdentity;
    private static int rowStarted;
    private int sequence;

    private AgentReceiptStorePerformanceEvidence(string outputRoot, string prefix, string fixtureRoot,
        string rowId, string sourceIdentity) {
        this.outputRoot = outputRoot;
        this.prefix = prefix;
        this.rowId = rowId;
        this.sourceIdentity = sourceIdentity;
        FixtureRoot = fixtureRoot;
    }

    internal string FixtureRoot { get; }

    internal AgentReceiptStoreDiagnosticSession? CreateDiagnosticSession()
        => AgentReceiptStoreDiagnosticSession.CreateIfEnabled(outputRoot, rowId, sourceIdentity);

    internal void ValidateFixtureTree() {
        RequireNested(outputRoot, FixtureRoot);
        RequireNoReparsePoints(FixtureRoot);
        ValidateTree(FixtureRoot);
    }

    internal static AgentReceiptStorePerformanceEvidence Create(ReceiptPerformanceWorkload workload, ReceiptPerformanceShape shape,
        ReceiptPerformanceTopology topology, int concurrency) {
        if (Interlocked.Exchange(ref rowStarted, 1) != 0) {
            throw new InvalidOperationException("Each performance row requires its own verified fresh testhost; a second row is refused.");
        }
        var output = RequiredExistingPath(OutputRootVariable, directory: true);
        var source = RequiredExistingPath(SourceRootVariable, directory: true);
        var sourceManifest = RequiredExistingPath(SourceManifestVariable, directory: false);
        if (!Enum.TryParse<ReceiptPerformanceVariant>(Environment.GetEnvironmentVariable(VariantVariable), ignoreCase: false,
            out var variant) || !Enum.IsDefined(variant)) {
            throw new InvalidOperationException("The owned performance runner must select Baseline or Candidate explicitly.");
        }
        var id = Guid.NewGuid().ToString("N");
        var prefix = $"receipt-store-{id}";
        var fixture = Path.Combine(output, $"{prefix}-fixtures");
        var sourceIdentity = Hash(sourceManifest);
        var evidence = new AgentReceiptStorePerformanceEvidence(output, prefix, fixture, id, sourceIdentity);
        using var process = Process.GetCurrentProcess();
        evidence.Write("intent", new {
            schema = 1, id, variant, workload, shape, topology, concurrency,
            processId = Environment.ProcessId, processStartedUtc = process.StartTime.ToUniversalTime(),
            recordedUtc = DateTimeOffset.UtcNow, intendedFixtureRoot = fixture,
            sourceManifest = new { path = sourceManifest, sha256 = sourceIdentity },
            source = SourceStamps(source), binaries = BinaryStamps(),
            runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
            architecture = RuntimeInformation.ProcessArchitecture, serverGc = GCSettings.IsServerGC,
            gcLatency = GCSettings.LatencyMode, processors = Environment.ProcessorCount,
            gcConfiguration = new[] { "DOTNET_GCHeapHardLimit", "DOTNET_GCHeapHardLimitPercent", "DOTNET_gcServer", "DOTNET_GCConserveMemory" }
                .ToDictionary(key => key, Environment.GetEnvironmentVariable),
            reference = new { applicationBytes = ApplicationEnvelopeBytes, idleReserveBytes = IdleReserveBytes,
                maximumIncrementalObservedBytes = IncrementalReferenceBytes, independentRuns = 4, enforcedOsLimit = false },
            measurement = new { cold = 1, warmups = 2, measured = 7, freshOwnerPerSample = true,
                cumulativeRunCursorAndRevisions = true, physicalIoCollected = false,
                extraProviderClaimChecksPerLookup = 1, forcedGc = false,
                syntheticSeedVersion = 1, initialTimestampUtc = "2026-10-01T00:00:00+00:00",
                laterOwnerValues = "System clock UpdatedAtUtc/checkpoint observations and fresh dispatch lease IDs remain nondeterministic; no clock substitution or extra full-run measurement read",
                nativeEncoding = "bounded continuation only; real native state acceptance is measured separately",
                memory = "observed maxima; GC heap/LOH/committed describe the last GC; transient peaks may be missed",
                allocation = "precise process-wide cumulative delta per cohort; includes sampler/background work",
                qualification = "characterization; peak/retention qualification additionally requires the reviewed diagnostic trace" }
        });
        if (Directory.Exists(fixture) || File.Exists(fixture)) {
            throw new InvalidOperationException("The pre-recorded performance fixture root already exists.");
        }
        Directory.CreateDirectory(fixture);
        return evidence;
    }

    internal void Write<T>(string phase, T payload) {
        var name = $"{prefix}-{Interlocked.Increment(ref sequence):D3}-{phase}.json";
        var path = Path.Combine(outputRoot, name);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(stream, payload, Json);
        stream.Flush(flushToDisk: true);
    }

    internal void VerifyAndRemoveEmptyFixtureParents() {
        ValidateFixtureTree();
        if (!Directory.Exists(FixtureRoot)) {
            throw new IOException("The recorded fixture root disappeared before cleanup verification.");
        }
        RemoveEmpty(FixtureRoot);
        if (Directory.Exists(FixtureRoot)) {
            throw new IOException("Owned fixture data remains; the performance row requires explicit cleanup reconciliation.");
        }
    }

    private static void ValidateTree(string path) {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) {
            throw new IOException("An owned fixture entry became a reparse point; automatic cleanup is refused.");
        }
        foreach (var entry in Directory.EnumerateFileSystemEntries(path)) {
            if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) {
                throw new IOException("An owned fixture entry became a reparse point; automatic cleanup is refused.");
            }
            if (Directory.Exists(entry)) {
                ValidateTree(entry);
            }
        }
    }

    private static void RemoveEmpty(string path) {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) {
            throw new IOException("An owned fixture path became a reparse point; automatic cleanup is refused.");
        }
        foreach (var child in Directory.EnumerateDirectories(path)) {
            RemoveEmpty(child);
        }
        if (!Directory.EnumerateFileSystemEntries(path).Any()) {
            Directory.Delete(path, recursive: false);
        }
    }

    private static string RequiredExistingPath(string name, bool directory) {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value)) {
            throw new InvalidOperationException($"The parent runner must provide the explicit absolute {name} path.");
        }
        var path = Path.GetFullPath(value);
        if (directory ? !Directory.Exists(path) : !File.Exists(path)) {
            throw new InvalidOperationException($"The parent runner's {name} path does not exist.");
        }
        if (Path.GetPathRoot(path) == Path.TrimEndingDirectorySeparator(path)) {
            throw new InvalidOperationException("A filesystem root cannot be an owned performance output root.");
        }
        RequireNoReparsePoints(path);
        return path;
    }

    private static void RequireNoReparsePoints(string path) {
        for (var current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current)) {
            if (File.Exists(current) || Directory.Exists(current)) {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) {
                    throw new IOException("The performance ownership path contains a reparse point.");
                }
            }
        }
    }

    private static void RequireNested(string parent, string child) {
        var relative = Path.GetRelativePath(parent, child);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) {
            throw new IOException("The recorded fixture root escaped its owned output root.");
        }
    }

    private static object[] SourceStamps(string root) {
        string[] paths = [
            "src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceJsonStore.cs",
            "src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceStore.cs",
            "src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceExecutionSliceStore.cs",
            "src/MAF/Common/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolAdmissionJournal.Effects.cs",
            "src/MAF/Common/CanDoItAll.AgentFramework.Models/Execution/AgentToolJournalModels.cs",
            "src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentChatSession.cs",
            "tests/Integration/CanDoItAll.Tests.Integration/AgentToolAdmissionJournalFixture.cs",
            "tests/Integration/CanDoItAll.Tests.Integration/AgentReceiptStorePerformanceTests.cs",
            "tests/Integration/CanDoItAll.Tests.Integration/AgentReceiptStorePerformanceFixture.cs",
            "tests/Integration/CanDoItAll.Tests.Integration/AgentReceiptStorePerformanceProbe.cs",
            "tests/Integration/CanDoItAll.Tests.Integration/AgentReceiptStorePerformanceEvidence.cs",
            "tests/Integration/CanDoItAll.Tests.Integration/AgentReceiptStoreDiagnosticSession.cs"
        ];
        return paths.Select(relative => (object)new { relativePath = relative, sha256 = Hash(Path.Combine(root, relative)) }).ToArray();
    }

    private static object[] BinaryStamps() {
        Type[] types = [typeof(AgentReceiptStorePerformanceEvidence), typeof(AgentToolAdmissionJournal), typeof(FileSandboxWorkspaceStore),
            typeof(AgentToolJournalRecord), typeof(AgentChatSession), typeof(AgentApiResponseMapper)];
        return types.Select(type => type.Assembly).Distinct().Select(assembly => (object)new {
            name = assembly.GetName().Name, path = assembly.Location, sha256 = Hash(assembly.Location),
            moduleVersionId = assembly.ManifestModule.ModuleVersionId
        }).ToArray();
    }

    private static string Hash(string path) {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
