using System.Data.Common;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration;

public sealed partial class ResourceMetadataOwnerTests {
    private readonly ITestOutputHelper capacityOutput;

    public ResourceMetadataOwnerTests(ITestOutputHelper output) => capacityOutput = output;

    [Fact]
    [Trait("Category", "LongRunning")]
    public async Task Measure_bounded_resource_metadata_owner_capacity() {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        using var telemetry = new CapacityTelemetry();
        Fixture? fixture = null;
        var workers = new List<AsyncServiceScope>(32);
        var files = new List<string>(32);
        TaskCompletionSource? activeGate = null;
        Task<CapacityOutcome>[] activeTasks = [];
        Exception? primary = null;
        var completedBatches = 0;
        var attemptedCalls = 0;
        var capacityFailures = new List<string>();
        try {
            fixture = await Fixture.CreateAsync(configure: telemetry.Configure);
            var token = deadline.Token;
            var projects = new ProjectWriteAdmission[32];
            projects[0] = fixture.Admission;
            for (var index = 0; index < projects.Length; index++) {
                workers.Add(fixture.Application.Services.CreateAsyncScope());
                if (index > 0) {
                    projects[index] = await OwnerPostcommitTestProbe.CreateProjectAsync(fixture.Services, "Resource capacity cohort");
                }
                token.ThrowIfCancellationRequested();
            }
            var context = await fixture.Service.ReadContextAsync(token);
            var source = await PrepareCapacityFilesAsync(fixture, files, token);
            var healthCharacters = CalibrateCapacityPayload(fixture, context);
            await WriteCapacityEnvironmentAsync(fixture, token);
            await RunCapacityControlsAsync(fixture, workers, projects, context, token);
            CapacityCell[] cells = [new(CapacityTopology.Single, 1), new(CapacityTopology.IndependentProjects, 8),
                new(CapacityTopology.SameProject, 8), new(CapacityTopology.SameResource, 8),
                new(CapacityTopology.IndependentProjects, 32), new(CapacityTopology.SameProject, 32),
                new(CapacityTopology.SameResource, 32)];
            foreach (var operation in Enum.GetValues<CapacityOperation>()) {
                foreach (var shape in Enum.GetValues<CapacityShape>()) {
                    foreach (var cell in cells) {
                        for (var sample = 1; sample <= 3; sample++) {
                            token.ThrowIfCancellationRequested();
                            var opening = Stopwatch.GetTimestamp();
                            var openingAllocated = GC.GetTotalAllocatedBytes(true);
                            var commands = await PrepareCapacityBatchAsync(fixture, projects, context, source, operation,
                                shape, cell, healthCharacters, token);
                            var preparation = new { Milliseconds = Stopwatch.GetElapsedTime(opening).TotalMilliseconds,
                                ProcessAllocatedBytes = GC.GetTotalAllocatedBytes(true) - openingAllocated };
                            var before = await CapacityStorageAsync(fixture, token);
                            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                            var observations = Enumerable.Range(0, cell.Writers).Select(index => new CapacityCall(index)).ToArray();
                            activeGate = ready;
                            var pending = commands.Select((command, index) => RunCapacityCallAsync(workers[index].ServiceProvider,
                                command, observations[index], ready.Task, token)).ToArray();
                            activeTasks = pending;
                            var allocated = GC.GetTotalAllocatedBytes(true);
                            var collections = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
                            using var process = Process.GetCurrentProcess();
                            var privateBefore = process.PrivateMemorySize64;
                            var started = Stopwatch.GetTimestamp();
                            ready.SetResult();
                            var outcomes = await Task.WhenAll(pending);
                            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                            var allocation = GC.GetTotalAllocatedBytes(true) - allocated;
                            var gc = Enumerable.Range(0, 3).Select(index => GC.CollectionCount(index) - collections[index]).ToArray();
                            process.Refresh();
                            attemptedCalls += outcomes.Length;
                            completedBatches++;
                            WriteCapacityOutcomes(() => new { Kind = "batch", Operation = operation.ToString(), Shape = shape.ToString(),
                                Topology = cell.Topology.ToString(), cell.Writers, Sample = sample, Preparation = preparation,
                                Calls = outcomes.Length, Outcomes = outcomes.GroupBy(item => item.Label).ToDictionary(group => group.Key, group => group.Count()),
                                ElapsedMilliseconds = elapsed, ProcessAllocatedBytes = allocation,
                                ProcessAllocatedBytesPerAttempt = allocation / outcomes.Length, GcCollections = gc,
                                PrivateBytesBefore = privateBefore, PrivateBytesAfter = process.PrivateMemorySize64,
                                Owner = CapacityPercentiles(observations.Select(item => item.ElapsedMilliseconds)),
                                Accepted = CapacityPercentiles(observations.Where((_, index) => outcomes[index].Accepted).Select(item => item.ElapsedMilliseconds)),
                                Refused = CapacityPercentiles(observations.Where((_, index) => !outcomes[index].Accepted).Select(item => item.ElapsedMilliseconds)),
                                ProjectCommand = CapacityPercentiles(observations.SelectMany(item => item.Ef).Where(item => item.Kind == CapacityCommand.ProjectLock).Select(item => item.Milliseconds)),
                                ResourceCommand = CapacityPercentiles(observations.SelectMany(item => item.Ef).Where(item => item.Kind == CapacityCommand.ResourceLock).Select(item => item.Milliseconds)),
                                PromotionCommand = CapacityPercentiles(observations.SelectMany(item => item.Ef).Where(item => item.Kind == CapacityCommand.PromotionLock).Select(item => item.Milliseconds)),
                                PromotionOwner = CapacityPercentiles(observations.Where(item => item.PromotionOwnerMilliseconds.HasValue).Select(item => item.PromotionOwnerMilliseconds!.Value)),
                                MaximumPostProjectOverlap = CapacityOverlap(observations),
                                CompletionOrder = observations.OrderBy(item => item.Finished).Select(item => item.Worker).ToArray(),
                                CallsObserved = observations.Select(item => item.Report()).ToArray() }, outcomes);
                            activeTasks = [];
                            activeGate = null;
                            foreach (var observation in observations) {
                                observation.RequireComplete();
                                Assert.InRange(observation.Ef.Count(item => item.Kind == CapacityCommand.PromotionLock), 0,
                                    operation == CapacityOperation.Promotion ? 1 : 0);
                            }
                            Assert.False(outcomes.Any(outcome => outcome.Unconfirmed || outcome.Diagnostic is not null), "Unconfirmed effect or cleanup stops the matrix.");
                            Assert.False(outcomes.Any(outcome => outcome.Rejection is { } rejection && rejection != ResourceMetadataRejectionReason.TransactionConflict &&
                                !(cell.Topology == CapacityTopology.SameResource &&
                                    (operation == CapacityOperation.Save && rejection == ResourceMetadataRejectionReason.ResourceChanged ||
                                     operation == CapacityOperation.Delete && rejection == ResourceMetadataRejectionReason.ResourceMissing))),
                                "A non-contention refusal is an invariant failure, not a capacity sample to continue past.");
                            await VerifyCapacityBatchAsync(fixture, commands, outcomes, token);
                            var after = await CapacityStorageAsync(fixture, token);
                            WriteCapacity(new { Kind = "verified-storage", Operation = operation.ToString(), Shape = shape.ToString(),
                                Topology = cell.Topology.ToString(), cell.Writers, Sample = sample, Before = before, After = after,
                                AcceptedPayloads = outcomes.Where(item => item.Snapshot is not null).Select(item => CapacityPayload(item.Snapshot!)).ToArray(),
                                EncodedOutcomeBytes = outcomes.Select((item, index) => CapacityOutcomeBytes(commands[index], item)).ToArray() });
                            if (!CapacityExpected(operation, cell, outcomes)) {
                                capacityFailures.Add($"{operation}/{shape}/{cell.Topology}/{cell.Writers}/{sample}");
                            }
                            await ClearCapacityResourcesAsync(fixture, token);
                        }
                    }
                }
            }
            Assert.Equal(126, completedBatches);
            Assert.Equal(2178, attemptedCalls);
            Assert.Empty(capacityFailures);
        } catch (Exception failure) {
            primary = failure;
            throw;
        } finally {
            var cleanup = new List<Exception>();
            try {
                deadline.Cancel();
            } catch (Exception failure) {
                cleanup.Add(failure);
            }
            activeGate?.TrySetResult();
            if (activeTasks.Length > 0) {
                try {
                    var drained = await Task.WhenAll(activeTasks);
                    foreach (var failure in drained.Select(item => item.Failure).OfType<Exception>().Where(item => item is not OperationCanceledException)) {
                        cleanup.Add(failure);
                    }
                } catch (Exception failure) {
                    cleanup.Add(failure);
                }
            }
            foreach (var file in files) {
                try {
                    File.Delete(file);
                } catch (Exception failure) {
                    cleanup.Add(failure);
                }
            }
            foreach (var worker in workers) {
                try {
                    await worker.DisposeAsync();
                } catch (Exception failure) {
                    cleanup.Add(failure);
                }
            }
            if (fixture is not null) {
                try {
                    await fixture.DisposeAsync();
                } catch (Exception failure) {
                    cleanup.Add(failure);
                }
            }
            try {
                WriteCapacity(new { Kind = "terminal", CompletedBatches = completedBatches, AttemptedCalls = attemptedCalls,
                    ExpectedBatches = 126, ExpectedCalls = 2178, CapacityFailures = capacityFailures,
                    PrimaryFailureType = primary?.GetType().FullName, CleanupFailureTypes = cleanup.Select(item => item.GetType().FullName).ToArray(),
                    Cleanup = cleanup.Count == 0 ? "terminal-owned-disposal" : "unconfirmed" });
            } catch (Exception failure) {
                cleanup.Add(failure);
            }
            if (cleanup.Count > 0) {
                if (primary is not null) {
                    cleanup.Insert(0, primary);
                }
                throw new AggregateException("Resource capacity operation or cleanup failed.", cleanup);
            }
        }
    }

    private enum CapacityOperation { Save, Delete, Promotion }
    private enum CapacityShape { Small, Escaped }
    private enum CapacityTopology { Single, IndependentProjects, SameProject, SameResource }
    private enum CapacityCommand { Other, ConnectionOpen, Lifecycle, ProjectLock, ResourceLock, PromotionLock, ResourceRead, ResourceWrite, SearchRead, SearchWrite, Transaction }
    private enum CapacityFault { None, CancelAfterSearch, LostAcknowledgementAndCleanup, AcknowledgedCleanup }
    private sealed record CapacityCell(CapacityTopology Topology, int Writers);
    private sealed record CapacitySource(ResourceFileSourceKey Key, FileBrowserItemKey[] Items);
    private sealed record CapacityRequest(ReplaceResourceMetadataCommand? Save, DeleteResourceMetadataCommand? Delete,
        ResourceStorageObjectPromotionCommand? Promotion, ResourceMetadataSnapshot? Original) {
        public Guid ResourceId => Save?.Expected.ResourceId ?? Delete?.Expected.ResourceId ?? Promotion!.ResourceId;
    }
    private sealed record CapacityOutcome(bool Accepted, bool Created, ResourceMetadataSnapshot? Snapshot,
        ResourceMetadataRejectionReason? Rejection, ResourceMetadataSecondaryDiagnostic? Diagnostic,
        bool Unconfirmed, Exception? Failure) {
        public string Label => Failure?.GetType().FullName ?? (Unconfirmed ? "Unconfirmed" : Accepted ? Created ? "Created" : "Accepted" : "Rejected:" + Rejection);
    }

    private static ResourceEditorModel CapacityEditor(ProjectWriteAdmission project, CapacityShape shape, int healthCharacters, int worker) {
        var editor = new ResourceEditorModel { ProjectId = project.ProjectId, ExpectedProjectAdmission = project,
            ConnectorPluginKey = WebhookResourceConnectorPlugin.PluginKey, ConfigSchemaVersion = "1.0", ConfigJson = "{}",
            Name = shape == CapacityShape.Small ? $"Resource {worker}" : $"{worker:D2}" + new string('<', 198),
            Description = shape == CapacityShape.Small ? "Bounded capacity" : new string('<', ResourceMetadataLimits.DescriptionBytes) };
        editor.Configuration.SetText(ResourceConnectorFieldKeys.EndpointUrl, "https://example.invalid/" + new string('a', shape == CapacityShape.Small ? 8 : 976));
        editor.Configuration.SetText(ResourceConnectorFieldKeys.HttpMethod, "post");
        editor.Configuration.SetText(ResourceConnectorFieldKeys.HealthPath, shape == CapacityShape.Small ? "/health" : new string('<', healthCharacters));
        return editor;
    }

    private int CalibrateCapacityPayload(Fixture fixture, ResourceMetadataContext context) {
        var preparation = fixture.Services.GetRequiredService<ResourceMetadataPreparation>();
        var maximumWidthTimestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(1234560);
        var low = 0;
        var high = ResourceMetadataLimits.ConfigurationValueBytes;
        while (low < high) {
            var candidate = low + (high - low + 1) / 2;
            if (Fits(candidate)) {
                low = candidate;
            } else {
                high = candidate - 1;
            }
        }
        Assert.True(low > 0);
        Assert.True(Fits(low));
        Assert.False(Fits(low + 1));
        WriteCapacity(new { Kind = "payload-calibration", HealthCharacters = low, NextCharacterRefused = true,
            DescriptionUtf8Bytes = ResourceMetadataLimits.DescriptionBytes, EncodedSnapshotLimit = ResourceMetadataLimits.EncodedSnapshotBytes,
            ReservedTimestampFractionalDigits = 6, ReservedTimestampFields = 2 });
        return low;

        bool Fits(int length) {
            try {
                var command = new ReplaceResourceMetadataCommand(new ResourceMetadataExpectation.Absent(context, Guid.NewGuid()), fixture.Admission,
                    ResourceMetadataValues.Capture(CapacityEditor(fixture.Admission, CapacityShape.Escaped, length, 0)));
                var prepared = preparation.Prepare(command);
                _ = new ResourceMetadataSnapshot(new(context, command.Expected.ResourceId, new(Guid.NewGuid()),
                    new ResourceProjectBinding.Bound(fixture.Admission.ProjectId, fixture.Admission.LifetimeId)), prepared.Values,
                    maximumWidthTimestamp, maximumWidthTimestamp);
                return true;
            } catch (Exception failure) when (failure is ArgumentException or ResourceMetadataRefusedException) {
                return false;
            }
        }
    }

    private static async Task<CapacitySource> PrepareCapacityFilesAsync(Fixture fixture, List<string> owned, CancellationToken token) {
        var storage = await fixture.Services.GetRequiredService<IStorageCatalogService>().EnsureBootstrapFileSystemStorageAsync(token);
        for (var index = 0; index < 32; index++) {
            var path = Path.Combine(storage.EndpointOrRoot, $"resource-capacity-{Guid.NewGuid():N}.txt");
            owned.Add(path);
            await File.WriteAllTextAsync(path, "Owned Resource capacity content", token);
        }
        var key = ResourceFileSourceKey.ForStorage(storage.Id);
        await using var browse = await fixture.Services.GetRequiredService<ResourceFileBrowseCoordinator>().OpenAsync(key, token);
        await browse.Browser.InitializeAsync(cancellationToken: token);
        return new(key, owned.Select(path => Assert.Single(browse.Browser.Snapshot.Items, item => item.Name == Path.GetFileName(path)).Key).ToArray());
    }

    private static async Task<CapacityRequest[]> PrepareCapacityBatchAsync(Fixture fixture, ProjectWriteAdmission[] projects,
        ResourceMetadataContext context, CapacitySource source, CapacityOperation operation, CapacityShape shape,
        CapacityCell cell, int healthCharacters, CancellationToken token) {
        var requests = new CapacityRequest[cell.Writers];
        ResourceMetadataSnapshot? shared = null;
        for (var index = 0; index < cell.Writers; index++) {
            var project = projects[cell.Topology == CapacityTopology.IndependentProjects ? index : 0];
            if (operation == CapacityOperation.Promotion) {
                requests[index] = new(null, null, new(source.Key, source.Items[cell.Topology == CapacityTopology.SameResource ? 0 : index],
                    project.ProjectId, shape == CapacityShape.Small ? $"Promoted {index}" : $"{index:D2}" + new string('<', 198),
                    project, context, Guid.NewGuid()), null);
                continue;
            }
            var editor = CapacityEditor(project, shape, healthCharacters, index);
            var seed = CapacityEditor(project, shape, healthCharacters, index);
            seed.Name = shape == CapacityShape.Small ? "Seed resource" : new string('s', 200);
            var original = cell.Topology == CapacityTopology.SameResource && shared is not null ? shared :
                Assert.IsType<ResourceMetadataSaveOutcome.Accepted>(await fixture.Service.SaveAsync(new ReplaceResourceMetadataCommand(
                    new ResourceMetadataExpectation.Absent(context, Guid.NewGuid()), project, ResourceMetadataValues.Capture(seed)), token)).Snapshot;
            shared ??= original;
            var values = ResourceMetadataValues.Capture(editor);
            requests[index] = operation == CapacityOperation.Save
                ? new(new(original.Expected, project, values), null, null, original)
                : new(null, new(original.Expected), null, original);
        }
        return requests;
    }

    private static async Task<CapacityOutcome> RunCapacityCallAsync(IServiceProvider services, CapacityRequest request,
        CapacityCall call, Task ready, CancellationToken token, ResourceMetadataWriter? direct = null) {
        await ready;
        CapacityTelemetry.Current.Value = call;
        call.Started = Stopwatch.GetTimestamp();
        try {
            var owner = services.GetRequiredService<ResourcesService>();
            if (request.Save is { } save) {
                var result = direct is null ? await owner.SaveAsync(save, token) : await direct.ReplaceAsync(save, token);
                return result switch {
                    ResourceMetadataSaveOutcome.Accepted accepted => new(true, save.Expected is ResourceMetadataExpectation.Absent,
                        accepted.Snapshot, null, accepted.Diagnostic, false, null),
                    ResourceMetadataSaveOutcome.Rejected rejected => new(false, false, null, rejected.Reason, null, false, null),
                    ResourceMetadataSaveOutcome.Unconfirmed => new(false, false, null, null, null, true, null),
                    _ => throw new InvalidOperationException("Unknown Resource save outcome.")
                };
            }
            if (request.Delete is { } delete) {
                var result = await owner.DeleteAsync(delete, token);
                return result switch {
                    ResourceMetadataDeleteOutcome.Accepted accepted => new(true, false, null, null, accepted.Diagnostic, false, null),
                    ResourceMetadataDeleteOutcome.Rejected rejected => new(false, false, null, rejected.Reason, null, false, null),
                    ResourceMetadataDeleteOutcome.Unconfirmed => new(false, false, null, null, null, true, null),
                    _ => throw new InvalidOperationException("Unknown Resource delete outcome.")
                };
            }
            var promotion = await services.GetRequiredService<ResourceStorageObjectPromotionService>().PromoteAsync(request.Promotion!, token);
            return promotion.Outcome switch {
                StorageObjectResourceWriteResult.Accepted accepted => new(true, accepted.Created, accepted.Snapshot, null,
                    accepted.Diagnostic, false, null),
                StorageObjectResourceWriteResult.Rejected rejected => new(false, false, null, rejected.Reason, null, false, null),
                StorageObjectResourceWriteResult.Unconfirmed => new(false, false, null, null, null, true, null),
                _ => throw new InvalidOperationException("Unknown Resource promotion outcome.")
            };
        } catch (Exception failure) {
            return new(false, false, null, null, null, false, failure);
        } finally {
            call.Finished = Stopwatch.GetTimestamp();
            CapacityTelemetry.Current.Value = null;
        }
    }

    private static bool CapacityExpected(CapacityOperation operation, CapacityCell cell, CapacityOutcome[] outcomes) {
        if (cell.Topology != CapacityTopology.SameResource) {
            return outcomes.All(item => item.Accepted && (operation != CapacityOperation.Promotion || item.Created));
        }
        if (operation == CapacityOperation.Promotion) {
            return outcomes.All(item => item.Accepted) && outcomes.Count(item => item.Created) == 1 &&
                outcomes.Select(item => item.Snapshot).Distinct().Count() == 1;
        }
        var expected = operation == CapacityOperation.Save ? ResourceMetadataRejectionReason.ResourceChanged : ResourceMetadataRejectionReason.ResourceMissing;
        return outcomes.Count(item => item.Accepted) == 1 && outcomes.Count(item => item.Rejection == expected) == outcomes.Length - 1;
    }

    private static async Task VerifyCapacityBatchAsync(Fixture fixture, CapacityRequest[] commands, CapacityOutcome[] outcomes, CancellationToken token) {
        var ids = commands.Select(item => item.ResourceId).Concat(outcomes.Where(item => item.Snapshot is not null)
            .Select(item => item.Snapshot!.Expected.ResourceId)).Distinct().ToArray();
        await using var owner = await fixture.OwnerAsync();
        await using var search = await fixture.SearchAsync();
        var rows = await owner.Set<ProjectResource>().AsNoTracking().Where(row => ids.Contains(row.Id)).ToArrayAsync(token);
        var keys = ids.Select(id => id.ToString()).ToArray();
        var documents = await search.Set<SearchDocument>().AsNoTracking()
            .Where(row => row.SourceType == SearchDocument.ResourceSourceType && keys.Contains(row.SourceKey)).ToArrayAsync(token);
        Assert.Equal(rows.Select(row => row.Id.ToString()).Order(), documents.Select(row => row.SourceKey).Order());
        foreach (var snapshot in outcomes.Where(item => item.Snapshot is not null).Select(item => item.Snapshot!).Distinct()) {
            Assert.Equal(snapshot, await ResourceMetadataTestCommands.FoundAsync(fixture.Service, snapshot.Expected.ResourceId));
            var row = Assert.Single(rows, row => row.Id == snapshot.Expected.ResourceId);
            Assert.Equal(snapshot.Expected.Revision.Value, row.Revision);
            Assert.Equal(snapshot.Values.ConfigJson, row.ConfigJson);
            var document = Assert.Single(documents, item => item.SourceKey == row.Id.ToString());
            Assert.Equal(snapshot.Values.Name, document.Title);
            Assert.Equal(snapshot.Values.Description, document.Summary);
            Assert.Equal(ResourceMetadataPreparation.ProjectId(snapshot.Expected.Project), document.ProjectId);
            var connector = snapshot.Values.ConnectorPluginKey == StorageObjectResourceConnectorPlugin.PluginKey ? "Storage object" :
                fixture.Services.GetRequiredService<ResourceConnectorPluginRegistry>().Resolve(snapshot.Values.ConnectorPluginKey).Manifest.DisplayName;
            Assert.Equal($"{snapshot.Values.LocationOrIdentifier}\nConnector: {connector}\nSensitivity: {snapshot.Values.Sensitivity}\nValidation: {snapshot.Values.ValidationStatus}", document.Body);
            Assert.Equal("Resources", document.Category);
            Assert.Equal($"/resources?resourceId={row.Id}", document.Route);
        }
        for (var index = 0; index < commands.Length; index++) {
            if (commands[index].Save is { } saved && outcomes[index].Snapshot is { } accepted) {
                Assert.Equal(saved.Expected.Context, accepted.Expected.Context);
                Assert.Equal(saved.Expected.ResourceId, accepted.Expected.ResourceId);
                Assert.Equal(new ResourceProjectBinding.Bound(saved.TargetProject.ProjectId, saved.TargetProject.LifetimeId), accepted.Expected.Project);
                Assert.Equal(fixture.Services.GetRequiredService<ResourceMetadataPreparation>().Prepare(saved).Values, accepted.Values);
                if (saved.Expected is ResourceMetadataExpectation.Existing previous) {
                    Assert.NotEqual(previous.Revision, accepted.Expected.Revision);
                }
            }
            if (commands[index].Promotion is { } promoted && outcomes[index] is { Created: true, Snapshot: { } created }) {
                Assert.Equal(promoted.ResourceId, created.Expected.ResourceId);
                Assert.Equal(promoted.ResourceName.Trim(), created.Values.Name);
            }
            if (commands[index].Delete is { } deletion && outcomes[index].Accepted) {
                Assert.DoesNotContain(rows, row => row.Id == deletion.Expected.ResourceId);
            }
            if (outcomes[index].Rejection is ResourceMetadataRejectionReason.TransactionConflict or ResourceMetadataRejectionReason.PersistenceUnavailable &&
                commands[index].Original is { } original && !outcomes.Any(item => item.Snapshot?.Expected.ResourceId == original.Expected.ResourceId) &&
                !commands.Where((_, other) => outcomes[other].Accepted).Any(item => item.Delete?.Expected.ResourceId == original.Expected.ResourceId)) {
                Assert.Equal(original, await ResourceMetadataTestCommands.FoundAsync(fixture.Service, original.Expected.ResourceId));
            }
        }
        Assert.Equal(await owner.Set<ProjectResource>().CountAsync(token), rows.Length);
        Assert.Equal(await search.Set<SearchDocument>().CountAsync(row => row.SourceType == SearchDocument.ResourceSourceType, token), documents.Length);
    }

    private static async Task ClearCapacityResourcesAsync(Fixture fixture, CancellationToken token) {
        await using var search = await fixture.SearchAsync();
        await search.Set<SearchDocument>().Where(row => row.SourceType == SearchDocument.ResourceSourceType).ExecuteDeleteAsync(token);
        await using var owner = await fixture.OwnerAsync();
        await owner.Set<ProjectResource>().ExecuteDeleteAsync(token);
    }

    private static object CapacityPayload(ResourceMetadataSnapshot snapshot) => new {
        SnapshotBytes = JsonSerializer.SerializeToUtf8Bytes(snapshot).Length,
        ConfigJsonBytes = Encoding.UTF8.GetByteCount(snapshot.Values.ConfigJson),
        DescriptionBytes = Encoding.UTF8.GetByteCount(snapshot.Values.Description),
        NameBytes = Encoding.UTF8.GetByteCount(snapshot.Values.Name),
        LocatorBytes = Encoding.UTF8.GetByteCount(snapshot.Values.LocationOrIdentifier)
    };

    private static int? CapacityOutcomeBytes(CapacityRequest request, CapacityOutcome outcome) {
        if (!outcome.Accepted) {
            return null;
        }
        if (request.Save is not null) {
            return JsonSerializer.SerializeToUtf8Bytes(new ResourceMetadataSaveOutcome.Accepted(outcome.Snapshot!, outcome.Diagnostic)).Length;
        }
        return request.Delete is { } delete
            ? JsonSerializer.SerializeToUtf8Bytes(new ResourceMetadataDeleteOutcome.Accepted(delete.Expected, outcome.Diagnostic)).Length
            : JsonSerializer.SerializeToUtf8Bytes(new StorageObjectResourceWriteResult.Accepted(outcome.Snapshot!, outcome.Created, outcome.Diagnostic)).Length;
    }

    private static object CapacityPercentiles(IEnumerable<double> values) {
        var sorted = values.Order().ToArray();
        return new { Count = sorted.Length, P50 = Value(0.5), P95 = Value(0.95), P99 = Value(0.99), Max = Value(1) };
        double? Value(double percentile) => sorted.Length == 0 ? null : sorted[(int)Math.Ceiling(sorted.Length * percentile) - 1];
    }

    private static int CapacityOverlap(IEnumerable<CapacityCall> calls) {
        var points = calls.Where(item => item.ProjectAcquired.HasValue && item.TransactionFinished.HasValue)
            .SelectMany(item => new[] { (At: item.ProjectAcquired!.Value, Delta: 1), (At: item.TransactionFinished!.Value, Delta: -1) })
            .OrderBy(item => item.At).ThenBy(item => item.Delta);
        var active = 0;
        var maximum = 0;
        foreach (var point in points) {
            active += point.Delta;
            maximum = Math.Max(maximum, active);
        }
        return maximum;
    }

    private void WriteCapacity<T>(T value) => capacityOutput.WriteLine(JsonSerializer.Serialize(value));

    private void WriteCapacityOutcomes<T>(Func<T> evidence, params CapacityOutcome[] outcomes) {
        var failures = outcomes.Select(item => item.Failure).OfType<Exception>().ToList();
        try {
            WriteCapacity(evidence());
        } catch (Exception failure) {
            failures.Add(failure);
        }
        if (failures.Count > 0) {
            throw new AggregateException("Resource capacity workers or evidence output failed; workers are terminal.", failures);
        }
    }

    private sealed record CapacityCommandSample(CapacityCommand Kind, double Milliseconds, bool Failed);

    private sealed class CapacityCall(int worker) {
        private readonly object gate = new();
        public int Worker { get; } = worker;
        public long Started { get; set; }
        public long Finished { get; set; }
        public long? ProjectAcquired { get; set; }
        public long? TransactionFinished { get; set; }
        public double? PromotionOwnerMilliseconds { get; set; }
        public CapacityFault Fault { get; init; }
        public CancellationTokenSource? Cancellation { get; init; }
        public Guid OwnerContext { get; set; }
        public int Begins { get; set; }
        public int Commits { get; set; }
        public int Rollbacks { get; set; }
        public int Closes { get; set; }
        public int Disposals { get; set; }
        public int CleanupFaults { get; set; }
        public int SearchBytes { get; set; }
        public double BeginMilliseconds { get; set; }
        public double CommitMilliseconds { get; set; }
        public double RollbackMilliseconds { get; set; }
        public bool SelectedConfig { get; set; }
        public bool Overflow { get; private set; }
        public int UnknownAdvisoryCommands { get; set; }
        public int UnexpectedProviderSpans { get; set; }
        public TaskCompletionSource<int> Waiting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<CapacityCommandSample> Ef { get; } = new(32);
        public List<CapacityCommandSample> Provider { get; } = new(32);
        public double ElapsedMilliseconds => Stopwatch.GetElapsedTime(Started, Finished).TotalMilliseconds;
        public void Record(bool provider, CapacityCommand kind, double milliseconds, bool failed) {
            lock (gate) {
                var records = provider ? Provider : Ef;
                if (records.Count >= 128) {
                    Overflow = true;
                    return;
                }
                records.Add(new(kind, milliseconds, failed));
            }
        }
        public void RequireComplete() {
            Assert.False(Overflow);
            Assert.Equal(0, UnknownAdvisoryCommands);
            Assert.Equal(0, UnexpectedProviderSpans);
            Assert.Equal(1, Begins);
            Assert.True(Closes > 0);
            Assert.Contains(Provider, item => item.Kind == CapacityCommand.Lifecycle);
            Assert.Contains(Ef, item => item.Kind == CapacityCommand.ProjectLock);
            Assert.NotNull(TransactionFinished);
            Assert.True(Finished >= TransactionFinished.Value);
        }
        public object Report() => new { Worker, ElapsedMilliseconds, Begins, Commits, Rollbacks, Closes, Disposals,
            CleanupFaults, SearchBytes, Overflow, UnknownAdvisoryCommands, UnexpectedProviderSpans, BeginMilliseconds, CommitMilliseconds, RollbackMilliseconds,
            EfCommands = Ef.Count, ProviderSpans = Provider.Count,
            ProviderCommands = Provider.Count(item => item.Kind != CapacityCommand.ConnectionOpen),
            ProviderConnectionOpens = Provider.Count(item => item.Kind == CapacityCommand.ConnectionOpen),
            ProviderLifecycle = Provider.Count(item => item.Kind == CapacityCommand.Lifecycle),
            EfFailures = Ef.Count(item => item.Failed), ProviderFailures = Provider.Count(item => item.Failed),
            ObservedCriticalMilliseconds = ProjectAcquired.HasValue && TransactionFinished.HasValue
                ? Stopwatch.GetElapsedTime(ProjectAcquired.Value, TransactionFinished.Value).TotalMilliseconds : (double?)null,
            EfKinds = Ef.GroupBy(item => item.Kind).ToDictionary(group => group.Key.ToString(), group => group.Count()),
            ProviderKinds = Provider.GroupBy(item => item.Kind).ToDictionary(group => group.Key.ToString(), group => group.Count()) };
    }

    private sealed class CapacityTelemetry : IDisposable {
        public static AsyncLocal<CapacityCall?> Current { get; } = new();
        private const string ObservationKey = "resource-capacity-call";
        private const string ProjectPrefix = "project:";
        private const string ResourcePrefix = "resource:";
        private const string PromotionPrefix = "resource-promotion:v1:";
        private readonly ActivityListener listener;

        public CapacityTelemetry() {
            listener = new() {
                ShouldListenTo = source => source.Name == "Npgsql",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => Current.Value is null ? ActivitySamplingResult.None : ActivitySamplingResult.AllData,
                SampleUsingParentId = (ref ActivityCreationOptions<string> _) => Current.Value is null ? ActivitySamplingResult.None : ActivitySamplingResult.AllData,
                ActivityStarted = activity => activity.SetCustomProperty(ObservationKey, Current.Value),
                ActivityStopped = activity => {
                    if (activity.GetCustomProperty(ObservationKey) is CapacityCall call) {
                        var sql = activity.GetTagItem("db.query.text") as string;
                        var physicalOpen = sql is null && activity.OperationName.StartsWith("CONNECT ", StringComparison.Ordinal);
                        if (sql is null && !physicalOpen) {
                            call.UnexpectedProviderSpans++;
                        }
                        call.Record(true, physicalOpen ? CapacityCommand.ConnectionOpen : Classify(sql),
                            activity.Duration.TotalMilliseconds, activity.Status == ActivityStatusCode.Error);
                    }
                }
            };
            ActivitySource.AddActivityListener(listener);
        }

        public void Configure(IServiceCollection services) {
            var command = new CapacityCommandObserver();
            var transaction = new CapacityTransactionObserver();
            var connection = new CapacityConnectionObserver();
            services.ConfigureDbContext<ResourcesDbContext>(options => options.AddInterceptors(command, transaction, connection), ServiceLifetime.Singleton);
            services.ConfigureDbContext<ProjectsDbContext>(options => options.AddInterceptors(command), ServiceLifetime.Singleton);
            services.ConfigureDbContext<SearchDbContext>(options => options.AddInterceptors(command), ServiceLifetime.Singleton);
            services.AddScoped<ISearchIndexService>(provider => new CapacitySearch(provider.GetRequiredService<SearchIndexService>()));
            services.AddScoped<IStorageObjectResourceWriter>(provider =>
                new CapacityPromotionWriter(new StorageObjectResourceWriter(provider.GetRequiredService<ResourceMetadataWriter>())));
        }

        public void Dispose() => listener.Dispose();

        internal static CapacityCommand Classify(string? sql) {
            if (sql is null) {
                return CapacityCommand.Other;
            }
            if (sql.Contains("Infrastructure_DatasetLifecycle", StringComparison.Ordinal)) {
                return CapacityCommand.Lifecycle;
            }
            var writes = sql.Contains("INSERT INTO", StringComparison.Ordinal) || sql.Contains("UPDATE ", StringComparison.Ordinal) || sql.Contains("DELETE FROM", StringComparison.Ordinal);
            if (sql.Contains("Resources_ProjectResources", StringComparison.Ordinal)) {
                return writes ? CapacityCommand.ResourceWrite : CapacityCommand.ResourceRead;
            }
            if (sql.Contains("Infrastructure_SearchDocuments", StringComparison.Ordinal)) {
                return writes ? CapacityCommand.SearchWrite : CapacityCommand.SearchRead;
            }
            return sql is "COMMIT" or "ROLLBACK" ? CapacityCommand.Transaction : CapacityCommand.Other;
        }

        internal static CapacityCommand Classify(DbCommand command) {
            if (command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal)) {
                foreach (DbParameter parameter in command.Parameters) {
                    if (parameter.Value is string key) {
                        if (key.StartsWith(ProjectPrefix, StringComparison.Ordinal) && Guid.TryParseExact(key.AsSpan(ProjectPrefix.Length), "D", out _)) {
                            return CapacityCommand.ProjectLock;
                        }
                        if (key.StartsWith(ResourcePrefix, StringComparison.Ordinal) && Guid.TryParseExact(key.AsSpan(ResourcePrefix.Length), "D", out _)) {
                            return CapacityCommand.ResourceLock;
                        }
                        if (key.StartsWith(PromotionPrefix, StringComparison.Ordinal) && key.Length == PromotionPrefix.Length + 64 &&
                            key.AsSpan(PromotionPrefix.Length).IndexOfAnyExcept("0123456789ABCDEF") < 0) {
                            return CapacityCommand.PromotionLock;
                        }
                    }
                }
                if (Current.Value is { } call) {
                    call.UnknownAdvisoryCommands++;
                }
                throw new InvalidOperationException("Unclassified Resource capacity advisory command.");
            }
            return Classify(command.CommandText);
        }
    }

    private sealed class CapacityCommandObserver : DbCommandInterceptor {
        private static void Starting(DbCommand command) {
            if (CapacityTelemetry.Current.Value is { } call && command.Connection is NpgsqlConnection connection &&
                CapacityTelemetry.Classify(command) == CapacityCommand.ProjectLock) {
                call.Waiting.TrySetResult(connection.ProcessID);
            }
        }
        private static void Ended(DbCommand command, TimeSpan duration, bool failed) {
            if (CapacityTelemetry.Current.Value is not { } call) {
                return;
            }
            var kind = CapacityTelemetry.Classify(command);
            call.Record(false, kind, duration.TotalMilliseconds, failed);
            if (kind == CapacityCommand.ProjectLock && !failed) {
                call.ProjectAcquired = Stopwatch.GetTimestamp();
            }
            if (kind == CapacityCommand.ResourceRead && command.CommandText.Contains("\"ConfigJson\"", StringComparison.Ordinal)) {
                call.SelectedConfig = true;
            }
        }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            Starting(command);
            return ValueTask.FromResult(result);
        }
        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            int result, CancellationToken cancellationToken = default) {
            Ended(command, eventData.Duration, false);
            return ValueTask.FromResult(result);
        }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Starting(command);
            return ValueTask.FromResult(result);
        }
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default) {
            Ended(command, eventData.Duration, false);
            return ValueTask.FromResult(result);
        }
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<object> result, CancellationToken cancellationToken = default) {
            Starting(command);
            return ValueTask.FromResult(result);
        }
        public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            object? result, CancellationToken cancellationToken = default) {
            Ended(command, eventData.Duration, false);
            return ValueTask.FromResult(result);
        }
        public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default) {
            Ended(command, eventData.Duration, true);
            return Task.CompletedTask;
        }
        public override Task CommandCanceledAsync(DbCommand command, CommandEndEventData eventData, CancellationToken cancellationToken = default) {
            Ended(command, eventData.Duration, true);
            return Task.CompletedTask;
        }
    }

    private sealed class CapacityTransactionObserver : DbTransactionInterceptor {
        public override ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection, TransactionEndEventData eventData,
            DbTransaction result, CancellationToken cancellationToken = default) {
            if (CapacityTelemetry.Current.Value is { } call && eventData.Context is ResourcesDbContext context) {
                call.OwnerContext = context.ContextId.InstanceId;
                call.Begins++;
                call.BeginMilliseconds += eventData.Duration.TotalMilliseconds;
            }
            return ValueTask.FromResult(result);
        }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (CapacityTelemetry.Current.Value is { } call && eventData.Context?.ContextId.InstanceId == call.OwnerContext) {
                call.Commits++;
                call.CommitMilliseconds += eventData.Duration.TotalMilliseconds;
                call.TransactionFinished = Stopwatch.GetTimestamp();
                if (call.Fault == CapacityFault.LostAcknowledgementAndCleanup) {
                    throw new IOException("Owned Resource capacity acknowledgement fault after actual commit.");
                }
            }
            return Task.CompletedTask;
        }
        public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (CapacityTelemetry.Current.Value is { } call && eventData.Context?.ContextId.InstanceId == call.OwnerContext) {
                call.Rollbacks++;
                call.RollbackMilliseconds += eventData.Duration.TotalMilliseconds;
                call.TransactionFinished = Stopwatch.GetTimestamp();
            }
            return Task.CompletedTask;
        }
    }

    private sealed class CapacityConnectionObserver : DbConnectionInterceptor {
        public override Task ConnectionClosedAsync(DbConnection connection, ConnectionEndEventData eventData) {
            if (CapacityTelemetry.Current.Value is { } call && eventData.Context?.ContextId.InstanceId == call.OwnerContext) {
                call.Closes++;
                call.TransactionFinished ??= Stopwatch.GetTimestamp();
            }
            return Task.CompletedTask;
        }
        public override Task ConnectionDisposedAsync(DbConnection connection, ConnectionEndEventData eventData) {
            if (CapacityTelemetry.Current.Value is { } call && eventData.Context?.ContextId.InstanceId == call.OwnerContext) {
                call.Disposals++;
                if (call.Fault is CapacityFault.AcknowledgedCleanup or CapacityFault.LostAcknowledgementAndCleanup) {
                    call.CleanupFaults++;
                    throw new IOException("Owned Resource capacity cleanup fault after physical disposal.");
                }
            }
            return Task.CompletedTask;
        }
    }

    private sealed class CapacityPromotionWriter(IStorageObjectResourceWriter actual) : IStorageObjectResourceWriter {
        public async Task<StorageObjectResourceWriteResult> SaveAsync(StorageObjectResourceWriteRequest request, CancellationToken cancellationToken = default) {
            var start = Stopwatch.GetTimestamp();
            try {
                return await actual.SaveAsync(request, cancellationToken);
            } finally {
                if (CapacityTelemetry.Current.Value is { } call) {
                    call.PromotionOwnerMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                }
            }
        }
    }

    private sealed class CapacitySearch(SearchIndexService actual) : ISearchIndexService {
        public Task UpsertAsync(SearchDocumentInput input, CancellationToken cancellationToken = default) => actual.UpsertAsync(input, cancellationToken);
        public Task DeleteAsync(string sourceType, string sourceKey, CancellationToken cancellationToken = default) => actual.DeleteAsync(sourceType, sourceKey, cancellationToken);
        public Task<IReadOnlyList<SearchResult>> SearchAsync(string query, int take = 12, CancellationToken cancellationToken = default) => actual.SearchAsync(query, take, cancellationToken);
        public async Task UpsertForMutationAsync(SearchDocumentInput input, CancellationToken cancellationToken = default) {
            await actual.UpsertForMutationAsync(input, cancellationToken);
            if (CapacityTelemetry.Current.Value is { } call) {
                call.SearchBytes = new[] { input.SourceType, input.SourceKey, input.Category, input.Title, input.Summary, input.Body, input.Route }.Sum(Encoding.UTF8.GetByteCount);
                if (call.Fault == CapacityFault.CancelAfterSearch) {
                    call.Cancellation!.Cancel();
                }
            }
        }
        public Task DeleteForMutationAsync(string sourceType, string sourceKey, CancellationToken cancellationToken = default)
            => actual.DeleteForMutationAsync(sourceType, sourceKey, cancellationToken);
    }

    private async Task RunCapacityControlsAsync(Fixture fixture, List<AsyncServiceScope> workers,
        ProjectWriteAdmission[] projects, ResourceMetadataContext context, CancellationToken token) {
        var request = New(projects[0]);
        var calibration = new CapacityCall(0);
        var result = await RunCapacityCallAsync(workers[0].ServiceProvider, request, calibration, Task.CompletedTask, token);
        WriteCapacityOutcomes(() => new { Kind = "control", Control = "instrumentation-calibration", result.Label, Observation = calibration.Report() }, result);
        Assert.True(result.Accepted);
        calibration.RequireComplete();
        Assert.Equal(1, calibration.Commits);
        Assert.Contains(calibration.Ef, item => item.Kind == CapacityCommand.SearchWrite);
        Assert.Contains(calibration.Ef, item => item.Kind == CapacityCommand.ResourceWrite);
        Assert.Contains(calibration.Ef, item => item.Kind == CapacityCommand.ResourceLock);
        Assert.True(calibration.SearchBytes > 0);
        await VerifyCapacityBatchAsync(fixture, [request], [result], token);
        await ClearCapacityResourcesAsync(fixture, token);

        var absence = New(projects[0]);
        var race = await Task.WhenAll(Enumerable.Range(0, 8).Select(index =>
            RunCapacityCallAsync(workers[index].ServiceProvider, absence, new(index), Task.CompletedTask, token)));
        WriteCapacityOutcomes(() => new { Kind = "control", Control = "explicit-absence-eight", Calls = race.Length,
            Outcomes = race.GroupBy(item => item.Label).ToDictionary(group => group.Key, group => group.Count()) }, race);
        Assert.Equal(1, race.Count(item => item.Accepted));
        Assert.Equal(7, race.Count(item => item.Rejection == ResourceMetadataRejectionReason.ResourceChanged));
        await VerifyCapacityBatchAsync(fixture, Enumerable.Repeat(absence, 8).ToArray(), race, token);
        await ClearCapacityResourcesAsync(fixture, token);

        foreach (var cancel in new[] { false, true }) {
            await WaitControlAsync(cancel);
            await ClearCapacityResourcesAsync(fixture, token);
        }
        foreach (var fault in new[] { CapacityFault.CancelAfterSearch, CapacityFault.LostAcknowledgementAndCleanup, CapacityFault.AcknowledgedCleanup }) {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            var call = new CapacityCall(0) { Fault = fault, Cancellation = cancellation };
            var command = New(projects[0]);
            var options = fixture.Services.GetRequiredService<DbContextOptions<ResourcesDbContext>>();
            var direct = ActivatorUtilities.CreateInstance<ResourceMetadataWriter>(fixture.Services, new CapacityUnpooledFactory(options));
            var outcome = await RunCapacityCallAsync(workers[0].ServiceProvider, command, call, Task.CompletedTask, cancellation.Token, direct);
            WriteCapacityOutcomes(() => new { Kind = "control", Control = fault.ToString(), outcome.Label, outcome.Diagnostic,
                Observation = call.Report(), ContextPooling = "unpooled control only; matrix retains registered factory" }, outcome);
            if (fault == CapacityFault.CancelAfterSearch) {
                Assert.Equal(ResourceMetadataRejectionReason.Cancelled, outcome.Rejection);
                Assert.Equal(0, call.Commits);
                await using var owner = await fixture.OwnerAsync();
                Assert.False(await owner.Set<ProjectResource>().AnyAsync(row => row.Id == command.ResourceId, token));
                await using var search = await fixture.SearchAsync();
                Assert.False(await search.Set<SearchDocument>().AnyAsync(row => row.SourceType == SearchDocument.ResourceSourceType && row.SourceKey == command.ResourceId.ToString(), token));
            } else {
                Assert.Equal(1, call.Commits);
                Assert.Equal(1, call.CleanupFaults);
                Assert.Equal(fault == CapacityFault.LostAcknowledgementAndCleanup, outcome.Unconfirmed);
                Assert.Equal(fault == CapacityFault.AcknowledgedCleanup, outcome.Accepted);
                if (outcome.Accepted) {
                    Assert.Equal(ResourceMetadataSecondaryDiagnostic.CleanupPending, outcome.Diagnostic);
                }
                var stored = await ResourceMetadataTestCommands.FoundAsync(fixture.Service, command.ResourceId);
                Assert.Equal(command.Save!.Values.Name, stored.Values.Name);
                await fixture.AssertSearchAsync(stored);
                Assert.Equal(1, call.Commits);
            }
            await ClearCapacityResourcesAsync(fixture, token);
        }
        var original = Assert.IsType<ResourceMetadataSaveOutcome.Accepted>(await fixture.Service.SaveAsync(New(projects[0]).Save!, token)).Snapshot;
        await using (var owner = await fixture.OwnerAsync()) {
            await owner.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Resources_ProjectResources\" SET \"ConfigJson\"={new string('x', ResourceMetadataLimits.ConfigJsonBytes + 1)}, \"Revision\"={Guid.NewGuid()} WHERE \"Id\"={original.Expected.ResourceId}", token);
        }
        await using (var search = await fixture.SearchAsync()) {
            await search.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Infrastructure_SearchDocuments\" SET \"Body\"={new string('s', 2 * 1024 * 1024)} WHERE \"SourceType\"={SearchDocument.ResourceSourceType} AND \"SourceKey\"={original.Expected.ResourceId.ToString()}", token);
        }
        var header = Assert.IsType<ResourceMetadataReadResult.MetadataOnly>(await fixture.Service.ReadAsync(original.Expected.ResourceId, token));
        var deletion = new CapacityRequest(null, new(header.Expected), null, null);
        var deletionCall = new CapacityCall(0);
        var deleted = await RunCapacityCallAsync(workers[0].ServiceProvider, deletion, deletionCall, Task.CompletedTask, token);
        WriteCapacityOutcomes(() => new { Kind = "control", Control = "oversized-header-only-delete", deleted.Label,
            Observation = deletionCall.Report(), ConfigBytes = ResourceMetadataLimits.ConfigJsonBytes + 1, SearchBodyBytes = 2 * 1024 * 1024 }, deleted);
        Assert.True(deleted.Accepted);
        Assert.False(deletionCall.SelectedConfig);
        await VerifyCapacityBatchAsync(fixture, [deletion], [deleted], token);
        await ClearCapacityResourcesAsync(fixture, token);

        CapacityRequest New(ProjectWriteAdmission project) => new(new(new ResourceMetadataExpectation.Absent(context, Guid.NewGuid()),
            project, ResourceMetadataValues.Capture(CapacityEditor(project, CapacityShape.Small, 0, 0))), null, null, null);

        async Task WaitControlAsync(bool cancel) {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            ResourcesDbContext? holder = null;
            ResourcesDbContext? observer = null;
            IDbContextTransaction? held = null;
            Task<CapacityOutcome>? pending = null;
            Task<CapacityOutcome>? separate = null;
            Exception? primary = null;
            var released = false;
            var call = new CapacityCall(0);
            var waiting = New(projects[0]);
            var independent = New(projects[1]);
            try {
                holder = await fixture.OwnerAsync();
                observer = await fixture.OwnerAsync();
                held = await holder.Database.BeginTransactionAsync(token);
                await SerializableMutationScope.AcquireRelationalScopeLocksAsync(holder, [ProjectMutationScopeKeys.ForProject(projects[0].ProjectId)], token);
                var holderPid = ((NpgsqlConnection)holder.Database.GetDbConnection()).ProcessID;
                pending = RunCapacityCallAsync(workers[0].ServiceProvider, waiting, call, Task.CompletedTask, cancellation.Token);
                var waiterPid = await call.Waiting.Task.WaitAsync(token);
                var observed = false;
                while (!observed) {
                    observed = await observer.Database.SqlQuery<bool>($"SELECT {holderPid} = ANY(pg_blocking_pids({waiterPid})) AS \"Value\"").SingleAsync(token);
                    if (!observed) {
                        Assert.False(pending.IsCompleted, "The owner ended before its exact Project wait was observed.");
                        await Task.Delay(TimeSpan.FromMilliseconds(10), token);
                    }
                }
                separate = RunCapacityCallAsync(workers[1].ServiceProvider, independent, new(1), Task.CompletedTask, token);
                var independentOutcome = await separate;
                Assert.True(independentOutcome.Accepted);
                Assert.False(pending.IsCompleted);
                if (cancel) {
                    cancellation.Cancel();
                }
                released = true;
                await held.DisposeAsync();
                var waitingOutcome = await pending;
                WriteCapacityOutcomes(() => new { Kind = "control", Control = cancel ? "cancel-exact-project-wait" : "exclusive-project-versus-independent",
                    ExactHolderObserved = observed, Waiting = waitingOutcome.Label, Independent = independentOutcome.Label, Observation = call.Report() },
                    waitingOutcome, independentOutcome);
                Assert.Equal(!cancel, waitingOutcome.Accepted);
                if (cancel) {
                    Assert.Equal(ResourceMetadataRejectionReason.Cancelled, waitingOutcome.Rejection);
                }
                await VerifyCapacityBatchAsync(fixture, [waiting, independent], [waitingOutcome, independentOutcome], token);
            } catch (Exception failure) {
                primary = failure;
                throw;
            } finally {
                var failures = new List<Exception>();
                try {
                    cancellation.Cancel();
                } catch (Exception failure) {
                    failures.Add(failure);
                }
                if (!released && held is not null) {
                    released = true;
                    try {
                        await held.DisposeAsync();
                    } catch (Exception failure) {
                        failures.Add(failure);
                    }
                }
                foreach (var task in new[] { pending, separate }.OfType<Task<CapacityOutcome>>()) {
                    try {
                        var terminal = await task;
                        if (terminal.Failure is not null && terminal.Failure is not OperationCanceledException) {
                            failures.Add(terminal.Failure);
                        }
                    } catch (Exception failure) {
                        failures.Add(failure);
                    }
                }
                foreach (var owned in new[] { observer, holder }.OfType<ResourcesDbContext>()) {
                    try {
                        await owned.DisposeAsync();
                    } catch (Exception failure) {
                        failures.Add(failure);
                    }
                }
                if (failures.Count > 0) {
                    if (primary is not null) {
                        failures.Insert(0, primary);
                    }
                    throw new AggregateException("Resource capacity wait control or terminal cleanup failed.", failures);
                }
            }
        }
    }

    private sealed class CapacityUnpooledFactory(DbContextOptions<ResourcesDbContext> options) : IDbContextFactory<ResourcesDbContext> {
        public ResourcesDbContext CreateDbContext() => new(options);
        public Task<ResourcesDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ResourcesDbContext(options));
        }
    }

    private async Task WriteCapacityEnvironmentAsync(Fixture fixture, CancellationToken token) {
        var owner = await fixture.OwnerAsync();
        Exception? primary = null;
        try {
            var connection = (NpgsqlConnection)owner.Database.GetDbConnection();
            await owner.Database.OpenConnectionAsync(token);
            var settings = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
            WriteCapacity(new { Kind = "environment", Workload = "resource-exclusive-baseline-v1", Runtime = RuntimeInformation.FrameworkDescription,
                OperatingSystem = RuntimeInformation.OSDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                LogicalProcessors = Environment.ProcessorCount, PgVersion = connection.PostgreSqlVersion.ToString(),
                TotalAvailableGcMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
                ProviderVersion = typeof(NpgsqlConnection).Assembly.GetName().Version?.ToString(),
                SourceModule = typeof(ResourceMetadataWriter).Assembly.ManifestModule.ModuleVersionId,
                settings.Pooling, settings.MinPoolSize, settings.MaxPoolSize, settings.Timeout, settings.CommandTimeout,
                HostedServices = "TestApplication builds and initializes a service provider; it does not StartAsync an IHost",
                AllocationScope = "precise process-wide dispatch batch delta, including instrumentation/background; no per-worker attribution",
                SqlScope = "Npgsql provider spans and EF command/transaction events reported separately, never summed as network round trips",
                LockScope = "client-observed wait-plus-command and acquisition-to-ack/connection-close intervals, not pure server lock duration",
                Measurement = "126 finite batches, 2178 matrix calls, 3 samples including first, no discarded warmup or forced GC",
                Controls = "separately labelled and excluded from matrix; final process/resource reconciliation remains parent-owned" });
        } catch (Exception failure) {
            primary = failure;
            throw;
        } finally {
            var failures = new List<Exception>();
            try {
                await owner.Database.CloseConnectionAsync();
            } catch (Exception failure) {
                failures.Add(failure);
            }
            try {
                await owner.DisposeAsync();
            } catch (Exception failure) {
                failures.Add(failure);
            }
            if (failures.Count > 0) {
                if (primary is not null) {
                    failures.Insert(0, primary);
                }
                throw new AggregateException("Resource capacity environment observation or terminal cleanup failed.", failures);
            }
        }
    }

    private static async Task<CapacityStorage> CapacityStorageAsync(Fixture fixture, CancellationToken token) {
        await using var owner = await fixture.OwnerAsync();
        return await owner.Database.SqlQuery<CapacityStorage>($"""
            SELECT (SELECT count(*) FROM "Resources_ProjectResources") AS "Resources",
                (SELECT count(*) FROM "Infrastructure_SearchDocuments" WHERE "SourceType"='resource') AS "SearchRows",
                (SELECT coalesce(sum(octet_length("ConfigJson")),0)::bigint FROM "Resources_ProjectResources") AS "ConfigBytes",
                pg_table_size('"Resources_ProjectResources"') AS "ResourceTableAndToastBytes",
                pg_indexes_size('"Resources_ProjectResources"') AS "ResourceIndexBytes",
                pg_table_size('"Infrastructure_SearchDocuments"') AS "SearchTableAndToastBytes",
                pg_indexes_size('"Infrastructure_SearchDocuments"') AS "SearchIndexBytes"
            """).SingleAsync(token);
    }

    private sealed class CapacityStorage {
        public long Resources { get; set; }
        public long SearchRows { get; set; }
        public long ConfigBytes { get; set; }
        public long ResourceTableAndToastBytes { get; set; }
        public long ResourceIndexBytes { get; set; }
        public long SearchTableAndToastBytes { get; set; }
        public long SearchIndexBytes { get; set; }
    }
}
