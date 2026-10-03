using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.CapabilityAuthoring.UI;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CapabilityKind = CanDoItAll.AgentFramework.Models.CapabilityKind;
using AccessKind = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityKind;

namespace CanDoItAll.AgentFramework.CapabilityAuthoring.UiSandbox;

public enum AuthoringScenario {
    NewMcp, NewSkill, NewTool, McpStdio, McpHttp, McpSse, McpLogical,
    SkillFile, SkillInline, SkillRegistered, ToolProcess, ToolHttp, RawPlugin,
    BuiltInTool, Malformed, Unavailable, LoadFailure, Rejected, Conflict, Unknown,
    HeldLoad, HeldSave, HeldSetup, Large
}

public sealed class CapabilityAuthoringScenario {
    private readonly Dictionary<Guid, CapabilityAuthoringSubmission> stored = [];
    private TaskCompletionSource? held;
    public CapabilityAuthoringScenario(AuthoringScenario scenario) {
        Scenario = scenario;
        var definition = CreateDefinition(scenario);
        InitialKind = definition.Kind;
        Mode = scenario is AuthoringScenario.NewMcp or AuthoringScenario.NewSkill or AuthoringScenario.NewTool
            ? CapabilityAuthoringMode.Wizard : CapabilityAuthoringMode.Details;
        if (Mode == CapabilityAuthoringMode.Details) {
            CapabilityId = Guid.NewGuid();
            definition = definition with { Id = CapabilityId };
            stored[CapabilityId.Value] = Stamp(definition);
        }
        Operations = new(LoadAsync, SaveAsync, TestToolAsync, TestMcpAsync, StringComparer.Ordinal);
    }

    public AuthoringScenario Scenario { get; }
    public CapabilityAuthoringMode Mode { get; }
    public CapabilityKind InitialKind { get; }
    public Guid? CapabilityId { get; }
    public CapabilityAuthoringOperations Operations { get; }
    public int Saves { get; private set; }
    public int Setups { get; private set; }
    public int DefinitionCount => stored.Count;
    public Guid? LastSavedId { get; private set; }
    public void Release() => held?.TrySetResult();
    public CapabilityAuthoringSubmission Read(Guid id) => stored[id];

    private async Task WaitAsync(CancellationToken token) {
        held = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await held.Task.WaitAsync(token);
    }

    private async Task<CapabilityEditorModel> LoadAsync(Guid id, CancellationToken token) {
        if (Scenario == AuthoringScenario.HeldLoad) {
            await WaitAsync(token);
        }
        if (Scenario == AuthoringScenario.LoadFailure) {
            throw new IOException("Controlled fixture read failure.");
        }
        return stored[id].ToEditorModel();
    }

    private async Task<CapabilityAuthoringSaveOutcome> SaveAsync(CapabilityAuthoringSubmission request, CancellationToken token) {
        Saves++;
        if (Scenario == AuthoringScenario.HeldSave) {
            await WaitAsync(token);
        }
        if (Scenario == AuthoringScenario.Unknown) {
            return new CapabilityAuthoringSaveOutcome.Unknown();
        }
        if (Scenario is AuthoringScenario.Rejected or AuthoringScenario.Conflict) {
            return new CapabilityAuthoringSaveOutcome.Rejected(Scenario == AuthoringScenario.Conflict);
        }
        if (request.Id is { } requested && (!stored.TryGetValue(requested, out var previous) || previous.ExpectedFingerprint != request.ExpectedFingerprint)) {
            return new CapabilityAuthoringSaveOutcome.Rejected(true);
        }
        var accepted = Stamp(request with { Id = request.Id ?? Guid.NewGuid() });
        stored[accepted.Id!.Value] = accepted;
        LastSavedId = accepted.Id;
        return new CapabilityAuthoringSaveOutcome.Accepted(accepted);
    }

    private async Task<CapabilitySetupTestResult> TestToolAsync(CapabilityAuthoringSubmission request, string input, CancellationToken token) {
        Setups++;
        if (Scenario == AuthoringScenario.HeldSetup) {
            await WaitAsync(token);
        }
        return new(true, new(AccessKind.Tool, new(request.Key)), "scenario-tool", []);
    }

    private async Task<McpSetupTestResult> TestMcpAsync(CapabilityAuthoringSubmission request, CancellationToken token) {
        Setups++;
        if (Scenario == AuthoringScenario.HeldSetup) {
            await WaitAsync(token);
        }
        return new(true, new(AccessKind.McpServer, new(request.Key)), new("scenario-server"), "scenario-mcp", [], [], [], true);
    }

    private static CapabilityAuthoringSubmission Stamp(CapabilityAuthoringSubmission value) => value with {
        ExpectedFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value with { ExpectedFingerprint = null }))))
    };

    private static CapabilityAuthoringSubmission CreateDefinition(AuthoringScenario scenario) {
        var kind = scenario switch {
            AuthoringScenario.NewSkill or AuthoringScenario.SkillFile or AuthoringScenario.SkillInline or AuthoringScenario.SkillRegistered => CapabilityKind.Skill,
            AuthoringScenario.NewTool or AuthoringScenario.ToolProcess or AuthoringScenario.ToolHttp or AuthoringScenario.BuiltInTool => CapabilityKind.Tool,
            AuthoringScenario.RawPlugin => CapabilityKind.Plugin,
            _ => CapabilityKind.McpServer
        };
        var json = scenario switch {
            AuthoringScenario.NewMcp or AuthoringScenario.NewSkill or AuthoringScenario.NewTool => "",
            AuthoringScenario.SkillFile => """{"skillSource":"file","skillRoot":"skills/review","allowedExternalRoots":["FixtureRoot","fixtureroot"],"scriptExecution":{"approvalRequired":true,"trustLevel":"WorkspaceSkillRoot"}}""",
            AuthoringScenario.SkillInline => """{"skillSource":"inline","inlineSkill":{"name":"fixture","description":"Inline fixture","instructions":"Review the supplied text.\nKeep Unicode: Ω.","resources":[{"name":"guide.txt","content":"Bounded fixture","description":"Guide"}],"future":"retained"}}""",
            AuthoringScenario.SkillRegistered => """{"skillSource":"registered","registeredSkillServiceType":"Fixture.RegisteredSkill"}""",
            AuthoringScenario.ToolHttp => """{"toolKind":"externalHttp","runtimeToolName":"fixture","implementationKey":"external.fixture","sideEffects":{"requiresApprovalByDefault":true,"isStateChanging":true,"kind":"ExternalAction"},"externalHttp":{"method":"POST","endpoint":"https://fixture.invalid/tool","headerBindings":{"X-Fixture":"secretref://fixture"},"timeoutSeconds":4,"maxResponseBytes":4096}}""",
            AuthoringScenario.ToolProcess or AuthoringScenario.BuiltInTool => """{"toolKind":"externalProcess","runtimeToolName":"fixture","implementationKey":"external.fixture","externalProcess":{"command":"fixture-helper","arguments":[""," value ","--bounded"],"workingDirectory":"fixtures","allowedExecutableNames":["fixture-helper"],"timeoutSeconds":4,"maxOutputBytes":4096}}""",
            AuthoringScenario.McpHttp => """{"transport":"http","serverName":"fixture-server","endpoint":"https://fixture.invalid/mcp","allowedTools":["fixture"],"headerBindings":{"X-Fixture":"secretref://fixture"}}""",
            AuthoringScenario.McpSse => """{"transport":"sse","serverName":"fixture-server","endpoint":"https://fixture.invalid/sse","allowedTools":["fixture"]}""",
            AuthoringScenario.McpStdio => """{"transport":"stdio","serverName":"fixture-server","command":"fixture-helper","arguments":["mcp",""," value "],"allowedTools":["FixtureTool","fixturetool"],"workingDirectory":"fixtures","allowedWorkingDirectories":["FixtureRoot","fixtureroot"],"environmentVariableBindings":{"FIXTURE":"secretref://fixture"}}""",
            AuthoringScenario.Malformed => "{invalid legacy configuration",
            AuthoringScenario.Unavailable => """{"transport":"unavailable-legacy","future":{"retained":true}}""",
            AuthoringScenario.RawPlugin => """{"future":{"retained":true}}""",
            _ => """{"transport":"logical","serverName":"fixture-server","allowedTools":["fixture"]}"""
        };
        return new(null, null, kind, "fixture", "Fixture capability", scenario == AuthoringScenario.Large ? new string('x', 12000) : "Independent authoring fixture", "", json,
            scenario == AuthoringScenario.BuiltInTool, ["fixture", "authoring"]);
    }
}
