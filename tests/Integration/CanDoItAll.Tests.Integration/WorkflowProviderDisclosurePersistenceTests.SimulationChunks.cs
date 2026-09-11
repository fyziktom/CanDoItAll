using System.Text;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CanDoItAll.Tests.Integration;

public sealed partial class WorkflowProviderDisclosurePersistenceTests {
    [Fact]
    public async Task Large_exact_simulation_step_survives_owner_restart_without_entering_public_events() {
        await using var environment = CanDoItAllTestEnvironment.Create("workflow-simulation-chunks");
        var profile = environment.CreatePostgreSqlProfile("retained");
        var harness = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile };
        var run = Run();
        var simulation = LargeSimulation();
        var declaration = Declaration(run) with { Simulations = [simulation] };
        Assert.True(Encoding.UTF8.GetByteCount(Json(simulation)) > 65_536);
        var completed = Completion(run, declaration, parts: 0);
        completed = completed with { CompletionProof = completed.CompletionProof! with { SimulationHash = simulation.Hash } };
        await using (var application = await TestApplication.CreateAsync(harness)) {
            var store = Store(application);
            await store.CreateRunWithStartedEventAsync(run, Started(run, declaration));
            await store.SaveEventAsync(completed);
        }
        await using var restarted = await TestApplication.CreateAsync(harness);
        var restored = await Store(restarted).ReadProviderDisclosureAsync(run.RunId);
        var original = Assert.Single(restored.Declaration!.Simulations);
        Assert.Equal(simulation.Step, original.Step);
        Assert.Equal(simulation.Hash, original.Hash);
        Assert.Equal(simulation.Hash, Assert.Single(restored.Completions).Proof.SimulationHash);
        var visible = await Store(restarted).ListEventsAsync(run.RunId);
        Assert.DoesNotContain("private-preview-output", Json(visible), StringComparison.Ordinal);
        await using var database = await Factory(restarted).CreateDbContextAsync();
        var privateRows = await database.Set<WorkflowEventRecordEntity>().AsNoTracking()
            .Where(row => row.RunId == run.RunId.Value && row.Kind == WorkflowEventKind.ProviderReadEvidence).ToArrayAsync();
        Assert.True(privateRows.Count(row => JsonNode.Parse(row.PayloadJson)!["$disclosure"]!.GetValue<string>() == "simulation-chunk-v2") > 2);
        Assert.All(privateRows, row => Assert.True(Encoding.UTF8.GetByteCount(row.PayloadJson) <= 16_384));
        var bytes = await Rows(restarted, run.RunId);
        _ = await new PersistentWorkflowRunStore(Factory(restarted)).ReadProviderDisclosureAsync(run.RunId);
        Assert.Equal(bytes, await Rows(restarted, run.RunId));
    }

    [Theory]
    [InlineData(SimulationCorruption.MissingChunk)]
    [InlineData(SimulationCorruption.SwappedContent)]
    [InlineData(SimulationCorruption.WrongIndex)]
    [InlineData(SimulationCorruption.WrongByteLength)]
    public async Task Missing_or_logically_reordered_simulation_chunks_refuse_restoration_without_rewriting_evidence(SimulationCorruption corruption) {
        await using var application = await TestApplication.CreateAsync();
        var run = Run();
        var declaration = Declaration(run) with { Simulations = [LargeSimulation()] };
        await Store(application).CreateRunWithStartedEventAsync(run, Started(run, declaration));
        await using (var database = await Factory(application).CreateDbContextAsync()) {
            var rows = await database.Set<WorkflowEventRecordEntity>().Where(row => row.RunId == run.RunId.Value &&
                row.Kind == WorkflowEventKind.ProviderReadEvidence).ToArrayAsync();
            var chunks = rows.Select(row => (Row: row, Json: JsonNode.Parse(row.PayloadJson)!.AsObject()))
                .Where(value => value.Json["$disclosure"]!.GetValue<string>() == "simulation-chunk-v2")
                .OrderBy(value => value.Json["partIndex"]!.GetValue<int>()).ToArray();
            Assert.True(chunks.Length > 2);
            switch (corruption) {
                case SimulationCorruption.MissingChunk:
                    database.Remove(chunks[1].Row);
                    break;
                case SimulationCorruption.SwappedContent:
                    var first = chunks[0].Json["contentBase64"]!.GetValue<string>();
                    chunks[0].Json["contentBase64"] = chunks[1].Json["contentBase64"]!.GetValue<string>();
                    chunks[1].Json["contentBase64"] = first;
                    chunks[0].Row.PayloadJson = chunks[0].Json.ToJsonString();
                    chunks[1].Row.PayloadJson = chunks[1].Json.ToJsonString();
                    break;
                case SimulationCorruption.WrongIndex:
                    chunks[0].Json["partIndex"] = 1;
                    chunks[0].Row.PayloadJson = chunks[0].Json.ToJsonString();
                    break;
                case SimulationCorruption.WrongByteLength:
                    var header = rows.Select(row => (Row: row, Json: JsonNode.Parse(row.PayloadJson)!.AsObject()))
                        .Single(value => value.Json["$disclosure"]!.GetValue<string>() == "simulation-header-v2");
                    header.Json["byteLength"] = header.Json["byteLength"]!.GetValue<int>() + 1;
                    header.Row.PayloadJson = header.Json.ToJsonString();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(corruption));
            }
            await database.SaveChangesAsync();
        }
        var originalBytes = await Rows(application, run.RunId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new PersistentWorkflowRunStore(Factory(application)).ReadProviderDisclosureAsync(run.RunId));
        Assert.Equal(originalBytes, await Rows(application, run.RunId));
    }

    [Fact]
    public async Task Missing_exact_simulation_step_refuses_admission_and_corrupt_restart_without_rewriting_evidence() {
        await using var application = await TestApplication.CreateAsync();
        var store = Store(application);
        var missingRun = Run();
        var missing = new WorkflowNodeSimulationAdmission(ReadNode, Hash("missing original step"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.CreateRunWithStartedEventAsync(missingRun,
            Started(missingRun, Declaration(missingRun) with { Simulations = [missing] })));
        Assert.Null(await store.GetRunAsync(missingRun.RunId));
        Assert.Empty(await Rows(application, missingRun.RunId));
        var run = Run();
        var original = Simulation(ReadNode, "{\"preview\":\"exact original\"}");
        var declaration = Declaration(run) with { Simulations = [original] };
        await store.CreateRunWithStartedEventAsync(run, Started(run, declaration));
        await using (var database = await Factory(application).CreateDbContextAsync()) {
            var rows = await database.Set<WorkflowEventRecordEntity>().Where(row => row.RunId == run.RunId.Value &&
                row.Kind == WorkflowEventKind.ProviderReadEvidence).ToArrayAsync();
            var typed = rows.Select(row => (Row: row, Json: JsonNode.Parse(row.PayloadJson)!.AsObject())).ToArray();
            var incomplete = original with { Step = null };
            var incompleteJson = Json(incomplete);
            var incompleteBytes = Encoding.UTF8.GetBytes(incompleteJson);
            Assert.True(incompleteBytes.Length < 8_192);
            var chunk = typed.Single(value => value.Json["$disclosure"]!.GetValue<string>() == "simulation-chunk-v2");
            chunk.Json["contentBase64"] = Convert.ToBase64String(incompleteBytes);
            chunk.Row.PayloadJson = chunk.Json.ToJsonString();
            var simulationHeader = typed.Single(value => value.Json["$disclosure"]!.GetValue<string>() == "simulation-header-v2");
            simulationHeader.Json["byteLength"] = incompleteBytes.Length;
            simulationHeader.Json["hash"] = JsonNode.Parse(Json(Hash(incompleteJson)));
            simulationHeader.Row.PayloadJson = simulationHeader.Json.ToJsonString();
            var header = typed
                .Single(value => value.Json["$disclosure"]!.GetValue<string>() == "declaration-v1");
            header.Json["simulationManifest"] = JsonNode.Parse(Json(new WorkflowReadEvidenceManifest(1, Hash(Json(new[] { incomplete })))));
            header.Row.PayloadJson = header.Json.ToJsonString();
            await database.SaveChangesAsync();
        }
        var bytes = await Rows(application, run.RunId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new PersistentWorkflowRunStore(Factory(application)).ReadProviderDisclosureAsync(run.RunId));
        Assert.Equal(bytes, await Rows(application, run.RunId));
    }

    [Fact]
    public async Task Disclosure_reader_excludes_diagnostic_payloads_and_refuses_same_or_foreign_run_event_retrofits() {
        await using var application = await TestApplication.CreateAsync();
        var store = Store(application);
        var run = Run();
        var declaration = Declaration(run);
        await store.CreateRunWithStartedEventAsync(run, Started(run, declaration));
        var completion = Completion(run, declaration);
        await store.SaveEventAsync(completion);
        var diagnosticIds = Enumerable.Range(0, 12).Select(_ => Guid.NewGuid()).ToArray();
        foreach (var id in diagnosticIds) {
            await store.SaveEventAsync(new(id, run.RunId, WorkflowEventKind.Output, null, "Diagnostic",
                Json(new { diagnostic = new string('x', 32_000) }), Timestamp.AddSeconds(2)));
        }
        await using var database = await Factory(application).CreateDbContextAsync();
        var probe = new EventMaterializationProbe();
        var options = new DbContextOptionsBuilder<WorkflowDbContext>().UseNpgsql(database.Database.GetConnectionString())
            .AddInterceptors(probe).Options;
        var observed = new PersistentWorkflowRunStore(new OwnerFactory(options));
        Assert.Single((await observed.ReadProviderDisclosureAsync(run.RunId)).Completions);
        Assert.NotEmpty(probe.EventIds);
        Assert.DoesNotContain(probe.EventIds, id => diagnosticIds.Contains(id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => observed.SaveEventAsync(completion with {
            Id = diagnosticIds[0], CompletionProof = completion.CompletionProof! with { CompletionId = Guid.NewGuid() }
        }));
        var foreign = Run();
        await store.CreateRunWithStartedEventAsync(foreign, Started(foreign, null));
        var foreignEvent = new WorkflowEventRecord(Guid.NewGuid(), foreign.RunId, WorkflowEventKind.Warning, null, "Original foreign display", "{}", Timestamp);
        await store.SaveEventAsync(foreignEvent);
        var originalForeignRows = await Rows(application, foreign.RunId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => observed.SaveEventAsync(completion with {
            Id = foreignEvent.Id, CompletionProof = completion.CompletionProof! with { CompletionId = Guid.NewGuid() }
        }));
        Assert.Equal(originalForeignRows, await Rows(application, foreign.RunId));
        Assert.Single((await store.ReadProviderDisclosureAsync(run.RunId)).Completions);
        Assert.Equal(14, (await store.ListEventsAsync(run.RunId)).Count);
    }

    private static WorkflowNodeSimulationAdmission LargeSimulation() {
        var step = new WorkflowPreviewSimulationStep(ReadNode, new("simulated.original"), "Original reason " + new string('r', 17_000),
            Json(new { prefix = "private-preview-output", text = string.Concat(Enumerable.Repeat("Ž🙂", 9_000)) }));
        return new(ReadNode, WorkflowProviderDisclosureContent.Simulation(step)) { Step = step };
    }

    private sealed class EventMaterializationProbe : IMaterializationInterceptor {
        public List<Guid> EventIds { get; } = [];
        public object InitializedInstance(MaterializationInterceptionData materializationData, object entity) {
            if (entity is WorkflowEventRecordEntity value) {
                EventIds.Add(value.Id);
            }
            return entity;
        }
    }

    public enum SimulationCorruption {
        MissingChunk,
        SwappedContent,
        WrongIndex,
        WrongByteLength
    }
}
