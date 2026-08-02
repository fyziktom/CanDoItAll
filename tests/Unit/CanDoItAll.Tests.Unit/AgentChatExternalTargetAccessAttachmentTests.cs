using System.Reflection;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentChatExternalTargetAccessAttachmentTests
{
    private static readonly DateTimeOffset CapturedAtUtc =
        new(2026, 8, 1, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Authority_attachment_payload_and_factory_are_not_public_API()
    {
        Assert.False(typeof(AgentChatExternalTargetAccessAttachment).IsPublic);
        Assert.True(typeof(AgentChatExternalTargetAccessAttachmentFactory).IsNotPublic);
        Assert.True(typeof(AgentChatExternalTargetAccessAttachmentPublisher).IsPublic);
        Assert.DoesNotContain(
            typeof(AgentChatContextInvocationFactory).Assembly
                .GetCustomAttributes<System.Runtime.CompilerServices.InternalsVisibleToAttribute>(),
            attribute => attribute.AssemblyName.StartsWith(
                "CanDoItAll.Modules.Workbench",
                StringComparison.Ordinal));
    }

    [Fact]
    public void CTX_AUTH_001_Create_applies_current_project_structure_access_as_read_only()
    {
        var attachmentDraft = Assert.IsType<AgentChatContextAttachmentDraft>(
            AgentChatExternalTargetAccessAttachmentFactory.CreateReadOnlyDraft(
                [@"C:\programovani\dotnet\calculator-e2e-test"],
                new DatabaseProfileGeneration(7),
                CapturedAtUtc,
                CapturedAtUtc.AddMinutes(5)));
        var context = CreateContext("project-structure", attachmentDraft);

        var invocation = AgentChatContextInvocationFactory.Create(
            context,
            Guid.NewGuid(),
            chatSessionId: null,
            "Repair the selected runtime node.",
            AgentExecutionOperationId.New(),
            new DatabaseProfileGeneration(7),
            CapturedAtUtc.AddMinutes(1));

        var executionContext = Assert.IsType<ExecutionInvocationContext>(
            invocation.Options.Context);
        using var metadata = JsonDocument.Parse(executionContext.MetadataJson);
        var readOnlyAliases = metadata.RootElement
            .GetProperty(ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey)
            .EnumerateArray()
            .Select(static item => item.GetString()!)
            .ToArray();

        Assert.Equal(
            ["external-target/C/programovani/dotnet/calculator-e2e-test"],
            readOnlyAliases);
        Assert.False(metadata.RootElement.TryGetProperty(
            ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey,
            out _));
        Assert.DoesNotContain(
            "external-target/C/programovani/dotnet/calculator-e2e-test",
            invocation.Options.TransientContext!.Content,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            @"C:\programovani\dotnet\calculator-e2e-test",
            invocation.Options.TransientContext.Content,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CTX_AUTH_001_Create_does_not_trust_the_attachment_outside_project_structure_context()
    {
        var attachmentDraft = Assert.IsType<AgentChatContextAttachmentDraft>(
            AgentChatExternalTargetAccessAttachmentFactory.CreateReadOnlyDraft(
                [@"C:\sensitive\unrelated"],
                new DatabaseProfileGeneration(7),
                CapturedAtUtc,
                CapturedAtUtc.AddMinutes(5)));
        var context = CreateContext("crm-account", attachmentDraft);

        var invocation = AgentChatContextInvocationFactory.Create(
            context,
            Guid.NewGuid(),
            chatSessionId: null,
            "Inspect the selected account.",
            AgentExecutionOperationId.New(),
            new DatabaseProfileGeneration(7),
            CapturedAtUtc.AddMinutes(1));

        var executionContext = Assert.IsType<ExecutionInvocationContext>(
            invocation.Options.Context);
        using var metadata = JsonDocument.Parse(executionContext.MetadataJson);

        Assert.False(metadata.RootElement.TryGetProperty(
            ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey,
            out _));
        Assert.False(metadata.RootElement.TryGetProperty(
            ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey,
            out _));
    }

    [Theory]
    [InlineData(@"C:\")]
    [InlineData("relative/project")]
    [InlineData("external-target/C/../sensitive")]
    public void CTX_AUTH_001_Factory_rejects_unbounded_or_invalid_roots(string candidate)
    {
        var attachmentDraft =
            AgentChatExternalTargetAccessAttachmentFactory.CreateReadOnlyDraft(
                [candidate],
                new DatabaseProfileGeneration(7),
                CapturedAtUtc,
                CapturedAtUtc.AddMinutes(5));

        Assert.Null(attachmentDraft);
    }

    [Theory]
    [InlineData(TamperedFingerprint.Content)]
    [InlineData(TamperedFingerprint.Coverage)]
    [InlineData(TamperedFingerprint.Freshness)]
    public void Invocation_does_not_mint_authority_from_a_tampered_attachment(
        TamperedFingerprint tamperedFingerprint)
    {
        const string root = @"C:\programovani\dotnet\calculator-e2e-test";
        var validDraft = Assert.IsType<AgentChatContextAttachmentDraft>(
            AgentChatExternalTargetAccessAttachmentFactory.CreateReadOnlyDraft(
                [root],
                new DatabaseProfileGeneration(7),
                CapturedAtUtc,
                CapturedAtUtc.AddMinutes(5)));
        var tamperedDraft = new AgentChatContextAttachmentDraft(
            validDraft.Kind,
            tamperedFingerprint == TamperedFingerprint.Content
                ? new SnapshotContentFingerprint("tampered-content")
                : validDraft.ContentFingerprint,
            tamperedFingerprint == TamperedFingerprint.Coverage
                ? new SnapshotCoverageFingerprint("tampered-coverage")
                : validDraft.CoverageFingerprint,
            validDraft.DatabaseProfileGeneration,
            tamperedFingerprint == TamperedFingerprint.Freshness
                ? new SnapshotFreshnessFingerprint("tampered-freshness")
                : validDraft.FreshnessFingerprint,
            validDraft.CapturedAtUtc,
            validDraft.FreshUntilUtc,
            new AgentChatExternalTargetAccessAttachment([root]));
        var context = CreateContext("project-structure", tamperedDraft);

        var invocation = AgentChatContextInvocationFactory.Create(
            context,
            Guid.NewGuid(),
            chatSessionId: null,
            "Repair the selected runtime node.",
            AgentExecutionOperationId.New(),
            new DatabaseProfileGeneration(7),
            CapturedAtUtc.AddMinutes(1));

        var executionContext = Assert.IsType<ExecutionInvocationContext>(
            invocation.Options.Context);
        using var metadata = JsonDocument.Parse(executionContext.MetadataJson);
        Assert.False(metadata.RootElement.TryGetProperty(
            ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey,
            out _));
    }

    [Fact]
    public void Invocation_rejects_external_authority_attachment_before_capture_time()
    {
        var futureCapturedAtUtc = CapturedAtUtc.AddMinutes(2);
        var attachmentDraft = Assert.IsType<AgentChatContextAttachmentDraft>(
            AgentChatExternalTargetAccessAttachmentFactory.CreateReadOnlyDraft(
                [@"C:\programovani\dotnet\calculator-e2e-test"],
                new DatabaseProfileGeneration(7),
                futureCapturedAtUtc,
                futureCapturedAtUtc.AddMinutes(5)));
        var context = CreateContext("project-structure", attachmentDraft);

        var exception = Assert.Throws<AgentChatContextAttachmentUnavailableException>(() =>
            AgentChatContextInvocationFactory.Create(
                context,
                Guid.NewGuid(),
                chatSessionId: null,
                "Repair the selected runtime node.",
                AgentExecutionOperationId.New(),
                new DatabaseProfileGeneration(7),
                CapturedAtUtc.AddMinutes(1)));

        Assert.Equal(
            AgentChatContextAttachmentFreshness.NotYetValid,
            exception.Freshness);
    }

    [Fact]
    public void Invocation_rejects_expired_external_authority_attachment()
    {
        var attachmentDraft = Assert.IsType<AgentChatContextAttachmentDraft>(
            AgentChatExternalTargetAccessAttachmentFactory.CreateReadOnlyDraft(
                [@"C:\programovani\dotnet\calculator-e2e-test"],
                new DatabaseProfileGeneration(7),
                CapturedAtUtc,
                CapturedAtUtc.AddMinutes(5)));
        var context = CreateContext("project-structure", attachmentDraft);

        var exception = Assert.Throws<AgentChatContextAttachmentUnavailableException>(() =>
            AgentChatContextInvocationFactory.Create(
                context,
                Guid.NewGuid(),
                chatSessionId: null,
                "Repair the selected runtime node.",
                AgentExecutionOperationId.New(),
                new DatabaseProfileGeneration(7),
                CapturedAtUtc.AddMinutes(5)));

        Assert.Equal(
            AgentChatContextAttachmentFreshness.Expired,
            exception.Freshness);
    }

    private static AgentChatContextSnapshot CreateContext(
        string sourceKind,
        AgentChatContextAttachmentDraft attachmentDraft)
    {
        var projectId = Guid.NewGuid();
        var publication = new AgentChatContextPublication(
            new AgentChatContextScope(
                AgentChatContextScopeId.Create(),
                new AgentChatContextSource(
                    new AgentChatContextSourceKind(sourceKind),
                    new AgentChatContextSourceId(projectId.ToString("D"))),
                "Selected context",
                WorkspaceScopeDescriptor.Project(projectId.ToString("D")),
                accessMode: AgentChatContextScopeAccessMode.Unrestricted),
            [
                new AgentChatContextContributorPublication(
                    new AgentChatContextFragment(
                        new AgentChatContextContributorId(
                            AgentChatExternalTargetAccessAttachmentFactory.TrustedContributorIdValue),
                        0,
                        "Selected node: runtime"),
                    [attachmentDraft])
            ]);
        var registry = new AgentChatContextRegistry(
            new FixedTimeProvider(CapturedAtUtc));
        using var lease = registry.PublishModuleContext(publication);
        return Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
            => utcNow;
    }

    public enum TamperedFingerprint
    {
        Content,
        Coverage,
        Freshness
    }
}
