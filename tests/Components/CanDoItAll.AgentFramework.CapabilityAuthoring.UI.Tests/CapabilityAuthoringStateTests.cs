using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.CapabilityAuthoring.UI;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using Microsoft.Extensions.Logging.Abstractions;
using CapabilityKind = CanDoItAll.AgentFramework.Models.CapabilityKind;
using AccessKind = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityKind;

namespace CanDoItAll.AgentFramework.CapabilityAuthoring.UI.Tests;

public sealed class CapabilityAuthoringStateTests {
    [Fact]
    public void Extension_property_names_preserve_their_JSON_case() {
        using var draft = new CapabilityAuthoringDraft(new() {
            Kind = CapabilityKind.McpServer, Key = "fixture", Name = "Fixture",
            ConfigurationJson = """{"transport":"logical","future":1,"Future":2}"""
        }, false);
        var submission = Assert.IsType<CapabilityAuthoringSubmission>(draft.Prepare(StringComparer.Ordinal));
        using var json = JsonDocument.Parse(submission.ConfigurationJson);
        Assert.Equal(1, json.RootElement.GetProperty("future").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("Future").GetInt32());
    }

    [Theory]
    [InlineData(CapabilityKind.McpServer)]
    [InlineData(CapabilityKind.Skill)]
    [InlineData(CapabilityKind.Tool)]
    public void Malformed_configuration_retains_raw_until_explicit_repair(CapabilityKind kind) {
        using var draft = new CapabilityAuthoringDraft(new() { Kind = kind, Key = "fixture", Name = "Fixture", ConfigurationJson = "{bad" }, false);
        Assert.NotNull(draft.ConfigurationError);
        Assert.False(draft.RawLocked);
        Assert.Null(draft.Prepare(StringComparer.Ordinal));
        Assert.Equal("{bad", draft.RawConfigurationJson);
        draft.RawConfigurationJson = "{}";
        Assert.NotNull(draft.ConfigurationError);
        Assert.True(draft.ApplyRawConfiguration());
        Assert.Null(draft.ConfigurationError);
    }

    [Fact]
    public void Skill_round_trip_preserves_extensions_resources_and_exact_authority_paths() {
        var model = new CapabilityEditorModel {
            Kind = CapabilityKind.Skill, Key = "fixture", Name = "Fixture", ConfigurationJson = """
            {"skillSource":"inline","allowedExternalRoots":[" /allowed/root ","/A","/a"],"extension":{"v":9},
             "scriptExecution":{"approvalRequired":true,"trustLevel":"InlineSkill","future":"kept"},
             "inlineSkill":{"name":"fixture","instructions":"Original","future":7,"resources":[{"name":"a.txt","content":"Unicode Ω","description":"Fixture","future":8}]}}
            """
        };
        using var draft = new CapabilityAuthoringDraft(model, false);
        draft.Skill.InlineInstructions = "Edited";
        var submitted = Assert.IsType<CapabilityAuthoringSubmission>(draft.Prepare(StringComparer.Ordinal));
        using var document = JsonDocument.Parse(submitted.ConfigurationJson);
        var root = document.RootElement;
        Assert.Equal(9, root.GetProperty("extension").GetProperty("v").GetInt32());
        Assert.Equal("kept", root.GetProperty("scriptExecution").GetProperty("future").GetString());
        Assert.Equal(7, root.GetProperty("inlineSkill").GetProperty("future").GetInt32());
        var resource = root.GetProperty("inlineSkill").GetProperty("resources")[0];
        Assert.Equal("Unicode Ω", resource.GetProperty("content").GetString());
        Assert.Equal(8, resource.GetProperty("future").GetInt32());
        Assert.Equal(new[] { " /allowed/root ", "/A", "/a" }, root.GetProperty("allowedExternalRoots").EnumerateArray().Select(value => value.GetString()));
        Assert.Contains("Original", model.ConfigurationJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Tool_validation_does_not_clamp_limits_or_mutate_the_draft() {
        using var draft = new CapabilityAuthoringDraft(ToolModel(), false);
        draft.Tool.HttpTimeoutSecondsText = "0";
        draft.Tool.MaxResponseBytesText = "2";
        Assert.Null(draft.Prepare(StringComparer.Ordinal));
        Assert.Equal("0", draft.Tool.HttpTimeoutSecondsText);
        Assert.Equal("2", draft.Tool.MaxResponseBytesText);
        draft.Tool.HttpTimeoutSecondsText = "6";
        draft.Tool.MaxResponseBytesText = "128";
        var submitted = Assert.IsType<CapabilityAuthoringSubmission>(draft.Prepare(StringComparer.Ordinal));
        using var json = JsonDocument.Parse(submitted.ConfigurationJson);
        Assert.Equal(99, json.RootElement.GetProperty("externalHttp").GetProperty("future").GetInt32());
        Assert.Equal(7, json.RootElement.GetProperty("sideEffects").GetProperty("future").GetInt32());
    }

    [Fact]
    public void Forbidden_plaintext_headers_are_not_retained_as_extension_data() {
        var model = ToolModel();
        model.ConfigurationJson = model.ConfigurationJson.Replace("\"future\":99", "\"future\":99,\"headers\":{\"X-Fixture\":\"forbidden-plaintext\"}", StringComparison.Ordinal);
        using var draft = new CapabilityAuthoringDraft(model, false);
        var submitted = Assert.IsType<CapabilityAuthoringSubmission>(draft.Prepare(StringComparer.Ordinal));
        Assert.DoesNotContain("forbidden-plaintext", submitted.ConfigurationJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Setup_captures_immutable_definition_and_input_and_never_publishes_late_success_as_current() {
        var pending = new TaskCompletionSource<CapabilitySetupTestResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CapabilityAuthoringSubmission? captured = null;
        string? input = null;
        var calls = 0;
        var operations = Operations() with { TestTool = (submission, json, _) => {
            calls++;
            captured = submission;
            input = json;
            return pending.Task;
        } };
        using var session = Session(operations, ToolModel().Id);
        await session.LoadAsync();
        session.Draft.Tool.TestInputJson = "{\"original\":true}";
        var test = session.TestSetupAsync();
        session.Draft.Model.Name = "Later";
        session.Draft.Tool.TestInputJson = "{}";
        session.Draft.Changed();
        pending.SetResult(new(true, new(AccessKind.Tool, new("fixture")), "fixture", []));
        await test;
        Assert.Equal("Fixture", captured!.Name);
        Assert.Equal("{\"original\":true}", input);
        Assert.True(session.SetupHistorical);
        Assert.Null(session.SetupSucceeded);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Save_retains_accepted_identity_and_later_edits_and_the_next_save_updates() {
        var pending = new TaskCompletionSource<CapabilityAuthoringSaveOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new List<CapabilityAuthoringSubmission>();
        var operations = Operations() with { Save = (submission, _) => {
            calls.Add(submission);
            return pending.Task;
        } };
        using var session = Session(operations);
        session.Draft.Model.Name = "Fixture";
        session.Draft.Mcp.Transport = "logical";
        var save = session.SaveAsync();
        session.Draft.Model.Name = "Later edit";
        session.Draft.Changed();
        var id = Guid.NewGuid();
        pending.SetResult(new CapabilityAuthoringSaveOutcome.Accepted(calls[0] with { Id = id, ExpectedFingerprint = "accepted-fingerprint" }));
        Assert.False(await save);
        Assert.Equal(id, session.AcceptedId);
        Assert.Equal("Later edit", session.Draft.Model.Name);
        Assert.Equal("accepted-fingerprint", session.Draft.Model.ExpectedFingerprint);
        await session.SaveAsync();
        Assert.Null(calls[0].Id);
        Assert.Equal(id, calls[1].Id);
        Assert.Equal("Later edit", calls[1].Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Known_rejection_allows_review_but_unknown_acknowledgement_blocks_replay(bool unknown) {
        var calls = 0;
        var operations = Operations() with { Save = (_, _) => {
            calls++;
            return Task.FromResult<CapabilityAuthoringSaveOutcome>(unknown ? new CapabilityAuthoringSaveOutcome.Unknown() : new CapabilityAuthoringSaveOutcome.Rejected(true));
        } };
        using var session = Session(operations);
        session.Draft.Model.Name = "Fixture";
        session.Draft.Mcp.Transport = "logical";
        await session.SaveAsync();
        await session.SaveAsync();
        Assert.Equal(unknown ? 1 : 2, calls);
        Assert.Equal(unknown, session.SaveUnknown);
        Assert.Equal("Fixture", session.Draft.Model.Name);
    }

    private static readonly Guid ToolId = Guid.NewGuid();
    private static CapabilityEditorModel ToolModel() => new() {
        Id = ToolId, Name = "Fixture", Key = "fixture", Kind = CapabilityKind.Tool,
        ConfigurationJson = """{"toolKind":"externalHttp","runtimeToolName":"fixture","implementationKey":"external.fixture","sideEffects":{"future":7},"externalHttp":{"endpoint":"https://fixture.invalid/","future":99}}"""
    };
    private static CapabilityAuthoringOperations Operations() => new(
        (_, _) => Task.FromResult(ToolModel()),
        (_, _) => throw new InvalidOperationException("Save must be explicitly arranged."),
        (_, _, _) => throw new InvalidOperationException("Tool setup must be explicitly arranged."),
        (_, _) => throw new InvalidOperationException("MCP setup must be explicitly arranged."),
        StringComparer.Ordinal);
    private static CapabilityAuthoringSession Session(CapabilityAuthoringOperations operations, Guid? id = null)
        => new(operations, id, CapabilityKind.McpServer, default, new NotificationService(), NullLogger.Instance);
}
