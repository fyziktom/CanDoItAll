using System.Runtime.CompilerServices;
using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;
using CanDoItAll.Processes.Drivers.OfficeEvidence;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDriverOfficeEvidenceAlphaTests
{
    [Fact]
    public void Office_evidence_alpha_verifies_supplied_email_and_document_metadata_text_only()
    {
        var verifier = new OfficeEvidenceAlphaVerifier();
        var result = verifier.Verify(CreateRequest(
        [
            new OfficeEvidenceItem(
                OfficeEvidenceItemKind.EmailMessage,
                "message-1",
                "Customer escalation follow-up",
                "manager@example.invalid",
                ["owner@example.invalid"],
                DateTimeOffset.Parse("2026-06-08T12:00:00Z"),
                "Follow-up message confirms the action item was assigned."),
            new OfficeEvidenceItem(
                OfficeEvidenceItemKind.Document,
                "document-1",
                "Escalation notes",
                "manager@example.invalid",
                [],
                DateTimeOffset.Parse("2026-06-08T12:05:00Z"),
                "Document body records the same assigned action item.")
        ]));

        Assert.True(result.Accepted);
        Assert.Equal(ProcessDriverDenialReason.None, result.DenialReason);
        Assert.Equal(ProcessDriverContractVersion.Current, result.ContractVersion);
        ProcessDriverVerificationTestHarness.AssertNoMutation(result);
        ProcessDriverVerificationTestHarness.AssertReadonlyAuditFacts(
            result,
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverCapabilityScopeKind.OfficeEvidenceRead);
        ProcessDriverVerificationTestHarness.AssertNormalizedAuditFacts(
            result,
            "manager:office-readonly",
            ProcessDriverCapabilityScopeKind.OfficeEvidenceRead,
            ProcessDriverVerificationTestHarness.OfficeReadonlyOperations,
            ProcessDriverDenialReason.None);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Category == ProcessDriverDiagnosticCategory.NoIssueDetected);
        Assert.Contains(result.EvidenceReferences, evidenceReference =>
            evidenceReference.Kind == ProcessDriverEvidenceReferenceKind.OfficeReadonlyArtifact);
    }

    [Fact]
    public void Office_evidence_alpha_reports_missing_supplied_metadata_without_connector_calls()
    {
        var verifier = new OfficeEvidenceAlphaVerifier();
        var result = verifier.Verify(CreateRequest(
        [
            new OfficeEvidenceItem(
                OfficeEvidenceItemKind.EmailMessage,
                "message-2",
                string.Empty,
                string.Empty,
                [],
                null,
                "fixture-secret reviewer@example.invalid")
        ]));

        Assert.True(result.Accepted);
        ProcessDriverVerificationTestHarness.AssertNoMutation(result);
        ProcessDriverVerificationTestHarness.AssertNormalizedAuditFacts(
            result,
            "manager:office-readonly",
            ProcessDriverCapabilityScopeKind.OfficeEvidenceRead,
            ProcessDriverVerificationTestHarness.OfficeReadonlyOperations,
            ProcessDriverDenialReason.None);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Severity == ProcessDriverDiagnosticSeverity.Warning &&
            diagnostic.Category == ProcessDriverDiagnosticCategory.InsufficientProof);
        ProcessDriverVerificationTestHarness.AssertDiagnosticsAndAuditDoNotContain(
            result,
            "fixture-secret",
            "reviewer@example.invalid");
    }

    [Fact]
    public void Office_evidence_alpha_rejects_untrusted_mismatched_and_wrong_envelopes_before_analysis()
    {
        var verifier = new OfficeEvidenceAlphaVerifier();
        var item = CreateCompleteEmailItem();
        var wrongContentType = verifier.Verify(CreateRequest(
            [item],
            suppliedContentFactory: reference => new ProcessDriverSuppliedEvidenceContent(
                ProcessDriverSuppliedEvidenceContentKind.TranscriptText,
                reference,
                ProcessDriverSuppliedEvidenceContentRules.PlainTextContentType,
                SizeBytes: 32,
                reference.ContentHash)));
        var untrustedUri = verifier.Verify(CreateRequest(
            [item],
            evidenceUri: "https://example.invalid/office-evidence.json"));
        var mismatchedEnvelope = verifier.Verify(CreateRequest(
            [item],
            suppliedContentFactory: reference => ProcessDriverSuppliedEvidenceContentRules.CreateOfficeEvidencePayload(
                reference with { Uri = "artifact://proof/scenario028/different-office-evidence.json" },
                OfficePayload)));
        var emptyItems = verifier.Verify(CreateRequest([]));

        ProcessDriverVerificationTestHarness.AssertMutationFreeDenial(
            wrongContentType,
            ProcessDriverDenialReason.MissingEvidence,
            ProcessDriverDiagnosticCategory.InsufficientProof);
        ProcessDriverVerificationTestHarness.AssertMutationFreeDenial(
            untrustedUri,
            ProcessDriverDenialReason.MissingEvidence,
            ProcessDriverDiagnosticCategory.TranscriptUntrusted);
        ProcessDriverVerificationTestHarness.AssertMutationFreeDenial(
            mismatchedEnvelope,
            ProcessDriverDenialReason.MissingEvidence,
            ProcessDriverDiagnosticCategory.EvidenceHashMismatch);
        ProcessDriverVerificationTestHarness.AssertMutationFreeDenial(
            emptyItems,
            ProcessDriverDenialReason.MissingEvidence,
            ProcessDriverDiagnosticCategory.InsufficientProof);
        Assert.DoesNotContain(wrongContentType.Diagnostics, diagnostic =>
            diagnostic.Category == ProcessDriverDiagnosticCategory.NoIssueDetected);
        Assert.DoesNotContain(untrustedUri.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("example.invalid", StringComparison.Ordinal));
    }

    [Fact]
    public void Office_evidence_alpha_package_is_solution_bound_dependency_clean_and_connector_free()
    {
        var root = FindRepositoryRoot();
        var solution = ReadRepositoryFile("CanDoItAll.slnx");
        var project = ReadRepositoryFile(
            "src",
            "CanDoItAll.Processes.Drivers.OfficeEvidence",
            "CanDoItAll.Processes.Drivers.OfficeEvidence.csproj");
        var source = ReadProjectSource(root);

        Assert.Contains(
            "src/CanDoItAll.Processes.Drivers.OfficeEvidence/CanDoItAll.Processes.Drivers.OfficeEvidence.csproj",
            solution,
            StringComparison.Ordinal);
        Assert.Contains("CanDoItAll.Processes.Drivers.Abstractions.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("<PackageReference", project, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", project, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Modules.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Infrastructure", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.AgentFramework", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Office365", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Gmail", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Graph", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HttpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IServiceCollection", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddScoped", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Diagnostics.Process", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.Start", source, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverRegistry", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverRuntimeSelector", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverManagerCommand", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverHost", source, StringComparison.Ordinal);
        Assert.Contains("ProcessDriverSuppliedEvidenceContentKind.OfficeEvidencePayload", source, StringComparison.Ordinal);
        Assert.Contains("ProcessDriverCapabilityScopeRules.IsOfficeEvidenceReadScope", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Office_evidence_alpha_denies_category_mutation_task_creation_document_write_graph_call_and_attachment_fetch()
    {
        var verifier = new OfficeEvidenceAlphaVerifier();
        var item = CreateCompleteEmailItem();
        var deniedAttempts = new[]
        {
            new DeniedOfficeAttempt(
                "category-mutation",
                ProcessDriverOperation.MutateEmailCategory,
                ProcessDriverDenialReason.MutationDenied),
            new DeniedOfficeAttempt(
                "task-creation",
                ProcessDriverOperation.CreateTask,
                ProcessDriverDenialReason.MutationDenied),
            new DeniedOfficeAttempt(
                "document-write",
                ProcessDriverOperation.WriteArtifact,
                ProcessDriverDenialReason.MutationDenied),
            new DeniedOfficeAttempt(
                "graph-call",
                ProcessDriverOperation.CallOfficeGraph,
                ProcessDriverDenialReason.ExternalCallDenied),
            new DeniedOfficeAttempt(
                "attachment-fetch",
                ProcessDriverOperation.CallOfficeGraph,
                ProcessDriverDenialReason.ExternalCallDenied)
        };

        foreach (var attempt in deniedAttempts)
        {
            var result = verifier.Verify(CreateRequest(
                [item],
                requestedOperations: [attempt.Operation],
                evidenceUri: $"artifact://proof/scenario029/{attempt.Name}.json"));

            ProcessDriverVerificationTestHarness.AssertSideEffectDenied(result, attempt.Operation);
            Assert.Equal(attempt.ExpectedDenialReason, result.DenialReason);
            Assert.DoesNotContain(result.Diagnostics, diagnostic =>
                diagnostic.Category == ProcessDriverDiagnosticCategory.NoIssueDetected);
            Assert.All(result.AuditFacts, fact =>
            {
                Assert.Equal(ProcessDriverAuditFactKind.OperationDenied, fact.Kind);
                Assert.Equal(ProcessDriverCapabilityScopeKind.OfficeEvidenceRead, fact.Lane);
                Assert.Equal(attempt.Operation, fact.RequestedOperation);
                Assert.Equal(attempt.ExpectedDenialReason, fact.DenialReason);
            });
        }
    }

    private const string OfficePayload = """{"items":[{"kind":"email","id":"message-1"},{"kind":"document","id":"document-1"}]}""";

    private sealed record DeniedOfficeAttempt(
        string Name,
        ProcessDriverOperation Operation,
        ProcessDriverDenialReason ExpectedDenialReason);

    private static OfficeEvidenceVerificationRequest CreateRequest(
        IReadOnlyList<OfficeEvidenceItem> items,
        IReadOnlyList<ProcessDriverOperation>? requestedOperations = null,
        string evidenceUri = "artifact://proof/scenario028/office-evidence.json",
        Func<ProcessDriverEvidenceReference, ProcessDriverSuppliedEvidenceContent>? suppliedContentFactory = null)
    {
        var evidenceReference = ProcessDriverVerificationTestHarness.CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.OfficeReadonlyArtifact,
            evidenceUri,
            OfficePayload,
            coreDescriptorFamily: null);
        var suppliedContent = suppliedContentFactory?.Invoke(evidenceReference) ??
            ProcessDriverSuppliedEvidenceContentRules.CreateOfficeEvidencePayload(
                evidenceReference,
                OfficePayload);
        var verificationRequest = ProcessDriverVerificationTestHarness.CreateVerificationRequest(
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverVerificationTestHarness.CreateReadonlyScope(
                ProcessDriverCapabilityScopeKind.OfficeEvidenceRead,
                ProcessDriverPermissionMode.VerificationOnly),
            [evidenceReference],
            requestedOperations ?? ProcessDriverVerificationTestHarness.OfficeReadonlyOperations,
            "manager:office-readonly");

        return new OfficeEvidenceVerificationRequest(
            verificationRequest,
            suppliedContent,
            items,
            DateTimeOffset.Parse("2026-06-08T12:10:00Z"));
    }

    private static OfficeEvidenceItem CreateCompleteEmailItem()
    {
        return new OfficeEvidenceItem(
            OfficeEvidenceItemKind.EmailMessage,
            "message-3",
            "Evidence review",
            "manager@example.invalid",
            ["owner@example.invalid"],
            DateTimeOffset.Parse("2026-06-08T12:15:00Z"),
            "Evidence review text was supplied by the caller.");
    }

    private static string ReadProjectSource(string repositoryRoot)
    {
        return string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(
                    Path.Combine(repositoryRoot, "src", "CanDoItAll.Processes.Drivers.OfficeEvidence"),
                    "*.cs",
                    SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static string ReadRepositoryFile(params string[] pathParts)
    {
        return File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. pathParts]));
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        foreach (var startPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory(), Path.GetDirectoryName(sourceFilePath) ?? string.Empty })
        {
            if (string.IsNullOrWhiteSpace(startPath))
            {
                continue;
            }

            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
