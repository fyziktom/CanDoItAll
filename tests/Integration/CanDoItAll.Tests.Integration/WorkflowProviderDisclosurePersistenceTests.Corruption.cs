using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration;

public sealed partial class WorkflowProviderDisclosurePersistenceTests {
    [Theory]
    [InlineData(Corruption.MissingPart)]
    [InlineData(Corruption.ChangedPart)]
    [InlineData(Corruption.MissingOriginalLink)]
    [InlineData(Corruption.MissingDeclaration)]
    [InlineData(Corruption.ForeignNode)]
    [InlineData(Corruption.ForeignSource)]
    [InlineData(Corruption.UnknownPrivateSchema)]
    [InlineData(Corruption.MissingSimulationPart)]
    [InlineData(Corruption.ChangedSimulationPart)]
    public async Task Independent_reader_refuses_incomplete_or_forged_private_history_without_rewriting_it(Corruption corruption) {
        await using var application = await TestApplication.CreateAsync();
        var store = Store(application);
        var run = Run();
        var declaration = Declaration(run) with { Simulations = [Simulation(new("preview-only"), "{\"preview\":\"original\"}")] };
        var completion = Completion(run, declaration, 2);
        await store.CreateRunWithStartedEventAsync(run, Started(run, declaration));
        await store.SaveEventAsync(completion);
        await using (var database = await Factory(application).CreateDbContextAsync()) {
            var rows = await database.Set<WorkflowEventRecordEntity>()
                .Where(row => row.RunId == run.RunId.Value && row.Kind == WorkflowEventKind.ProviderReadEvidence).ToArrayAsync();
            var typed = rows.Select(row => (Row: row, Json: JsonNode.Parse(row.PayloadJson)!.AsObject())).ToArray();
            var part = typed.First(item => item.Json["$disclosure"]!.GetValue<string>() == "part-v1");
            var header = typed.Single(item => item.Json["$disclosure"]!.GetValue<string>() == "completion-v1");
            switch (corruption) {
                case Corruption.MissingPart:
                    database.Remove(part.Row);
                    break;
                case Corruption.ChangedPart:
                    part.Json["evidence"]!["payloadJson"] = "{\"forged\":true}";
                    part.Row.PayloadJson = part.Json.ToJsonString();
                    break;
                case Corruption.MissingOriginalLink:
                    database.Remove(rows.Single(row => row.Id == WorkflowProviderDisclosureJournal.LinkId(run.RunId, completion.Id)));
                    break;
                case Corruption.MissingDeclaration:
                    database.Remove(typed.Single(item => item.Json["$disclosure"]!.GetValue<string>() == "declaration-v1").Row);
                    break;
                case Corruption.ForeignNode:
                    header.Json["proof"] = JsonNode.Parse(Json(completion.CompletionProof! with { NodeId = new("other-node") }));
                    header.Row.PayloadJson = header.Json.ToJsonString();
                    break;
                case Corruption.ForeignSource:
                    header.Json["proof"] = JsonNode.Parse(Json(completion.CompletionProof! with { SourceHash = Hash("foreign-source") }));
                    header.Row.PayloadJson = header.Json.ToJsonString();
                    break;
                case Corruption.UnknownPrivateSchema:
                    header.Json["$disclosure"] = "unsupported-v9";
                    header.Row.PayloadJson = header.Json.ToJsonString();
                    break;
                case Corruption.MissingSimulationPart:
                    database.Remove(typed.Single(item => item.Json["$disclosure"]!.GetValue<string>() == "simulation-chunk-v2").Row);
                    break;
                case Corruption.ChangedSimulationPart:
                    var simulation = typed.Single(item => item.Json["$disclosure"]!.GetValue<string>() == "simulation-header-v2");
                    simulation.Json["hash"] = JsonNode.Parse(Json(Hash("different preview")));
                    simulation.Row.PayloadJson = simulation.Json.ToJsonString();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(corruption));
            }
            await database.SaveChangesAsync();
        }
        var corruptBytes = await Rows(application, run.RunId);
        var independent = new PersistentWorkflowRunStore(Factory(application));
        await Assert.ThrowsAsync<InvalidOperationException>(() => independent.ReadProviderDisclosureAsync(run.RunId));
        Assert.Equal(corruptBytes, await Rows(application, run.RunId));
    }

    [Theory]
    [InlineData(Forgery.Run)]
    [InlineData(Forgery.Version)]
    [InlineData(Forgery.Node)]
    [InlineData(Forgery.Source)]
    [InlineData(Forgery.Definition)]
    [InlineData(Forgery.EvidenceOccurrence)]
    public async Task Writer_rejects_mismatched_original_completion_identity_before_any_event_is_persisted(Forgery forgery) {
        await using var application = await TestApplication.CreateAsync();
        var store = Store(application);
        var run = Run();
        var declaration = Declaration(run);
        await store.CreateRunWithStartedEventAsync(run, Started(run, declaration));
        var completion = Completion(run, declaration);
        var original = completion.CompletionProof!;
        var forged = forgery switch {
            Forgery.Run => completion with { CompletionProof = original with { Occurrence = WorkflowExecutionOccurrence.Start(WorkflowRunId.New()) } },
            Forgery.Version => completion with { CompletionProof = original with { VersionId = WorkflowVersionId.New() } },
            Forgery.Node => completion with { CompletionProof = original with { NodeId = new("foreign-node") } },
            Forgery.Source => completion with { CompletionProof = original with { SourceHash = Hash("foreign-source") } },
            Forgery.Definition => completion with { CompletionProof = original with { DefinitionHash = Hash("different-definition") } },
            Forgery.EvidenceOccurrence => completion with {
                ProviderReadEvidence = [completion.ProviderReadEvidence[0] with { Occurrence = original.Occurrence.Advance(run.VersionId, ReadNode) }]
            },
            _ => throw new ArgumentOutOfRangeException(nameof(forgery))
        };
        var before = await Rows(application, run.RunId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveEventAsync(forged));
        Assert.Equal(before, await Rows(application, run.RunId));
        Assert.Empty((await store.ReadProviderDisclosureAsync(run.RunId)).Completions);
    }

    public enum Corruption {
        MissingPart,
        ChangedPart,
        MissingOriginalLink,
        MissingDeclaration,
        ForeignNode,
        ForeignSource,
        UnknownPrivateSchema,
        MissingSimulationPart,
        ChangedSimulationPart
    }

    public enum Forgery {
        Run,
        Version,
        Node,
        Source,
        Definition,
        EvidenceOccurrence
    }
}
