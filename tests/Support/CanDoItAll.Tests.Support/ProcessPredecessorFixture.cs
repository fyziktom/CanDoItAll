using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Support;

public static class ProcessPredecessorFixture {
    public const string DefinitionKey = "dotnet-runtime-command-writeback";
    public const string ProbeKey = "Pc4LegacyProbe";
    public const string ProbeValue = "predecessor-retained-run";
    private const string ResourcePrefix = "CanDoItAll.Tests.Support.Fixtures.Processes.PrePc3.";

    public static string PlanJson => Read("plan.json");
    public static string InitialCommitJson => Read("initial-commit.json");
    public static string ProvenanceJson => Read("provenance.json");
    public static string ParentPlanJson => Read("parent-plan.json");
    public static string ParentProvenanceJson => Read("parent-provenance.json");

    public static ProcessRuntimeCommitRequest ReadInitialCommit(bool parent = false)
        => JsonSerializer.Deserialize<ProcessRuntimeCommitRequest>(parent ? Read("parent-initial-commit.json") : InitialCommitJson, ProcessInstancePlanPersistenceMapper.CreateSerializerOptions())
            ?? throw new InvalidOperationException("The authentic predecessor initial commit could not be decoded.");

    public static async Task<ProcessRuntimeStateSnapshot> SeedRetainedAsync(ProcessPersistenceDbContext database, bool parent,
        ProcessProjectAdmission? admission, ProcessRuntimeStatus status, IReadOnlyDictionary<string, string> variables,
        ProcessRunId? rootRunId = null) {
        var initial = ReadInitialCommit(parent);
        var plan = ProcessInstancePlanPersistenceMapper.ToEntity(initial.InitialPlan!);
        if (plan.PayloadJson != (parent ? ParentPlanJson : PlanJson)) {
            throw new InvalidOperationException("Importing the predecessor fixture would change its immutable plan bytes.");
        }
        var state = initial.Mutation.State with { ProjectAdmission = admission, Status = status,
            RootRunId = rootRunId ?? initial.Mutation.State.RootRunId };
        database.InstancePlans.Add(plan);
        database.RuntimeStates.Add(ProcessPersistenceMappers.ToEntity(state));
        foreach (var original in initial.InitialAssignments!) {
            var captured = original.LaunchVariables.ToDictionary();
            foreach (var pair in variables) {
                captured[pair.Key] = pair.Value;
            }
            database.RuntimeStepAssignments.Add(EfProcessRuntimeStepAssignmentStore.ToEntity(original with { LaunchVariables = captured }));
        }
        await database.SaveChangesAsync();
        return state;
    }

    private static string Read(string name) {
        using var stream = typeof(ProcessPredecessorFixture).Assembly.GetManifestResourceStream(ResourcePrefix + name)
            ?? throw new InvalidOperationException("The authentic predecessor process fixture is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
