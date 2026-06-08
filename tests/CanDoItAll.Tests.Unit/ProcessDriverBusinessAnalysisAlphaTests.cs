using System.Runtime.CompilerServices;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;
using CanDoItAll.Processes.Drivers.BusinessAnalysis;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDriverBusinessAnalysisAlphaTests
{
    [Fact]
    public void Business_analysis_alpha_SB031_INV_001_verifies_supplied_deliverable_and_evidence_text_only()
    {
        var verifier = new BusinessAnalysisAlphaVerifier();
        var result = verifier.Verify(CreateRequest(
        [
            new BusinessAnalysisEvidenceItem(
                BusinessAnalysisEvidenceItemKind.Deliverable,
                "analysis-1",
                "Customer churn analysis",
                "Requirement: explain churn risk. Deliverable text cites supplied evidence.",
                DateTimeOffset.Parse("2026-06-08T13:00:00Z")),
            new BusinessAnalysisEvidenceItem(
                BusinessAnalysisEvidenceItemKind.SupportingEvidence,
                "evidence-1",
                "Interview summary",
                "Evidence: supplied customer feedback supports the churn analysis.",
                DateTimeOffset.Parse("2026-06-08T13:05:00Z"))
        ]));

        Assert.True(result.Accepted);
        Assert.Equal(ProcessDriverDenialReason.None, result.DenialReason);
        Assert.Equal(ProcessDriverContractVersion.Current, result.ContractVersion);
        ProcessDriverVerificationTestHarness.AssertNoMutation(result);
        ProcessDriverVerificationTestHarness.AssertReadonlyAuditFacts(
            result,
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverCapabilityScopeKind.BusinessAnalysisRead);
        ProcessDriverVerificationTestHarness.AssertNormalizedAuditFacts(
            result,
            "manager:business-readonly",
            ProcessDriverCapabilityScopeKind.BusinessAnalysisRead,
            ProcessDriverVerificationTestHarness.BusinessAnalysisReadonlyOperations,
            ProcessDriverDenialReason.None);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Category == ProcessDriverDiagnosticCategory.NoIssueDetected);
        Assert.Contains(result.EvidenceReferences, evidenceReference =>
            evidenceReference.Kind == ProcessDriverEvidenceReferenceKind.BusinessReadonlyArtifact);
    }

    [Fact]
    public void Business_analysis_alpha_SB032_INV_001_reports_missing_requirements_unsupported_assumptions_contradictions_and_evidence_gaps()
    {
        var verifier = new BusinessAnalysisAlphaVerifier();
        var result = verifier.Verify(CreateRequest(
        [
            new BusinessAnalysisEvidenceItem(
                BusinessAnalysisEvidenceItemKind.Deliverable,
                "analysis-4",
                "Risk summary",
                "Assumption: fixture-secret reviewer@example.invalid. Contradiction: supplied evidence conflicts with the conclusion.",
                DateTimeOffset.Parse("2026-06-08T13:20:00Z"))
        ]));
        var categories = result.Diagnostics.Select(diagnostic => diagnostic.Category).ToHashSet();

        Assert.True(result.Accepted);
        ProcessDriverVerificationTestHarness.AssertNoMutation(result);
        Assert.Contains(ProcessDriverDiagnosticCategory.BusinessRequirementMissing, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.BusinessUnsupportedAssumption, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.BusinessContradictionMarker, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.BusinessEvidenceGap, categories);
        ProcessDriverVerificationTestHarness.AssertDiagnosticsAndAuditDoNotContain(
            result,
            "fixture-secret",
            "reviewer@example.invalid",
            "conflicts with the conclusion");
    }

    [Fact]
    public void Business_analysis_alpha_SB031_INV_002_reports_missing_supplied_metadata_without_raw_text_leakage()
    {
        var verifier = new BusinessAnalysisAlphaVerifier();
        var result = verifier.Verify(CreateRequest(
        [
            new BusinessAnalysisEvidenceItem(
                BusinessAnalysisEvidenceItemKind.SupportingEvidence,
                "evidence-2",
                string.Empty,
                "fixture-secret reviewer@example.invalid",
                null)
        ]));

        Assert.True(result.Accepted);
        ProcessDriverVerificationTestHarness.AssertNoMutation(result);
        ProcessDriverVerificationTestHarness.AssertNormalizedAuditFacts(
            result,
            "manager:business-readonly",
            ProcessDriverCapabilityScopeKind.BusinessAnalysisRead,
            ProcessDriverVerificationTestHarness.BusinessAnalysisReadonlyOperations,
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
    public void Business_analysis_alpha_SB031_INV_003_rejects_invalid_envelopes_and_business_record_mutation()
    {
        var verifier = new BusinessAnalysisAlphaVerifier();
        var item = CreateCompleteDeliverableItem();
        var wrongContentType = verifier.Verify(CreateRequest(
            [item],
            suppliedContentFactory: reference => new ProcessDriverSuppliedEvidenceContent(
                ProcessDriverSuppliedEvidenceContentKind.OfficeEvidencePayload,
                reference,
                ProcessDriverSuppliedEvidenceContentRules.JsonContentType,
                SizeBytes: 32,
                reference.ContentHash)));
        var untrustedUri = verifier.Verify(CreateRequest(
            [item],
            evidenceUri: "https://example.invalid/business-analysis.json"));
        var mismatchedEnvelope = verifier.Verify(CreateRequest(
            [item],
            suppliedContentFactory: reference => ProcessDriverSuppliedEvidenceContentRules.CreateBusinessAnalysisPayload(
                reference with { Uri = "bundle://proof/SB031/different-business-analysis.json" },
                BusinessAnalysisPayload)));
        var emptyItems = verifier.Verify(CreateRequest([]));
        var businessMutation = verifier.Verify(CreateRequest(
            [item],
            requestedOperations: [ProcessDriverOperation.MutateBusinessRecord]));

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
        ProcessDriverVerificationTestHarness.AssertSideEffectDenied(
            businessMutation,
            ProcessDriverOperation.MutateBusinessRecord);
        Assert.Equal(ProcessDriverDenialReason.MutationDenied, businessMutation.DenialReason);
        Assert.DoesNotContain(untrustedUri.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("example.invalid", StringComparison.Ordinal));
        Assert.DoesNotContain(businessMutation.Diagnostics, diagnostic =>
            diagnostic.Category == ProcessDriverDiagnosticCategory.NoIssueDetected);
    }

    [Fact]
    public void Business_analysis_alpha_SB031_INV_004_package_is_solution_bound_dependency_clean_and_record_mutation_free()
    {
        var root = FindRepositoryRoot();
        var solution = ReadRepositoryFile("CanDoItAll.slnx");
        var project = ReadRepositoryFile(
            "src",
            "CanDoItAll.Processes.Drivers.BusinessAnalysis",
            "CanDoItAll.Processes.Drivers.BusinessAnalysis.csproj");
        var source = ReadProjectSource(root);

        Assert.Contains(
            "src/CanDoItAll.Processes.Drivers.BusinessAnalysis/CanDoItAll.Processes.Drivers.BusinessAnalysis.csproj",
            solution,
            StringComparison.Ordinal);
        Assert.Contains("CanDoItAll.Processes.Drivers.Abstractions.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("<PackageReference", project, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", project, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Modules.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Infrastructure", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.AgentFramework", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CRM", source, StringComparison.OrdinalIgnoreCase);
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
        Assert.DoesNotContain("ProcessDriverProvider", source, StringComparison.Ordinal);
        Assert.Contains("ProcessDriverSuppliedEvidenceContentKind.BusinessAnalysisPayload", source, StringComparison.Ordinal);
        Assert.Contains("ProcessDriverCapabilityScopeRules.IsBusinessAnalysisReadScope", source, StringComparison.Ordinal);
    }

    private const string BusinessAnalysisPayload = """{"items":[{"kind":"deliverable","id":"analysis-1"},{"kind":"evidence","id":"evidence-1"}]}""";

    private static BusinessAnalysisVerificationRequest CreateRequest(
        IReadOnlyList<BusinessAnalysisEvidenceItem> items,
        IReadOnlyList<ProcessDriverOperation>? requestedOperations = null,
        string evidenceUri = "bundle://proof/SB031/business-analysis.json",
        Func<ProcessDriverEvidenceReference, ProcessDriverSuppliedEvidenceContent>? suppliedContentFactory = null)
    {
        var evidenceReference = ProcessDriverVerificationTestHarness.CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.BusinessReadonlyArtifact,
            evidenceUri,
            BusinessAnalysisPayload,
            coreDescriptorFamily: null);
        var suppliedContent = suppliedContentFactory?.Invoke(evidenceReference) ??
            ProcessDriverSuppliedEvidenceContentRules.CreateBusinessAnalysisPayload(
                evidenceReference,
                BusinessAnalysisPayload);
        var verificationRequest = ProcessDriverVerificationTestHarness.CreateVerificationRequest(
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverVerificationTestHarness.CreateReadonlyScope(
                ProcessDriverCapabilityScopeKind.BusinessAnalysisRead,
                ProcessDriverPermissionMode.VerificationOnly),
            [evidenceReference],
            requestedOperations ?? ProcessDriverVerificationTestHarness.BusinessAnalysisReadonlyOperations,
            "manager:business-readonly");

        return new BusinessAnalysisVerificationRequest(
            verificationRequest,
            suppliedContent,
            items,
            DateTimeOffset.Parse("2026-06-08T13:10:00Z"));
    }

    private static BusinessAnalysisEvidenceItem CreateCompleteDeliverableItem()
    {
        return new BusinessAnalysisEvidenceItem(
            BusinessAnalysisEvidenceItemKind.Deliverable,
            "analysis-3",
            "Evidence review",
            "Business analysis evidence review text was supplied by the caller.",
            DateTimeOffset.Parse("2026-06-08T13:15:00Z"));
    }

    private static string ReadProjectSource(string repositoryRoot)
    {
        return string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(
                    Path.Combine(repositoryRoot, "src", "CanDoItAll.Processes.Drivers.BusinessAnalysis"),
                    "*.cs",
                    SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                    !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
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
