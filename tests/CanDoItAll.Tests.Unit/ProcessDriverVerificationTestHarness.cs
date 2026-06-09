using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Tests.Unit;

internal static class ProcessDriverVerificationTestHarness
{
    public static readonly IReadOnlyList<ProcessDriverOperation> TranscriptReadonlyOperations =
    [
        ProcessDriverOperation.InspectExistingEvidence,
        ProcessDriverOperation.ReturnDiagnostics
    ];

    public static readonly IReadOnlyList<ProcessDriverOperation> RuntimeReadonlyOperations =
    [
        ProcessDriverOperation.ReadProcessFacts,
        ProcessDriverOperation.ReturnDiagnostics
    ];

    public static readonly IReadOnlyList<ProcessDriverOperation> OfficeReadonlyOperations =
    [
        ProcessDriverOperation.InspectExistingEvidence,
        ProcessDriverOperation.ReturnDiagnostics
    ];

    public static readonly IReadOnlyList<ProcessDriverOperation> BusinessAnalysisReadonlyOperations =
    [
        ProcessDriverOperation.InspectExistingEvidence,
        ProcessDriverOperation.ReturnDiagnostics
    ];

    public static readonly IReadOnlyList<ProcessDriverOperation> ArtifactEvidenceReadonlyOperations =
    [
        ProcessDriverOperation.InspectExistingEvidence,
        ProcessDriverOperation.ReturnDiagnostics
    ];

    public static readonly IReadOnlyList<ProcessDriverOperation> SideEffectOperations =
    [
        ProcessDriverOperation.MutateProcessState,
        ProcessDriverOperation.ExecuteCommand,
        ProcessDriverOperation.RestorePackage,
        ProcessDriverOperation.WriteArtifact,
        ProcessDriverOperation.WriteWorkspaceStorage,
        ProcessDriverOperation.CallOfficeGraph,
        ProcessDriverOperation.MutateEmailCategory,
        ProcessDriverOperation.CreateTask,
        ProcessDriverOperation.MutateBusinessRecord,
        ProcessDriverOperation.ApplyTransition,
        ProcessDriverOperation.ClaimDispatch,
        ProcessDriverOperation.ApplyFinalizer,
        ProcessDriverOperation.ScheduleRetry
    ];

    public static ProcessDriverCapabilityScope CreateReadonlyScope(
        ProcessDriverCapabilityScopeKind scopeKind,
        ProcessDriverPermissionMode permissionMode)
    {
        return new ProcessDriverCapabilityScope(
            scopeKind,
            permissionMode,
            AllowsProcessMutation: false,
            AllowsExternalCalls: false,
            AllowsWorkspaceWrites: false,
            AllowsStorageWrites: false);
    }

    public static ProcessDriverEvidenceReference CreateEvidenceReference(
        ProcessDriverEvidenceReferenceKind kind,
        string uri,
        string contentSeed,
        ProcessDriverCoreDescriptorFamily? coreDescriptorFamily,
        string? contentHash = null)
    {
        return new ProcessDriverEvidenceReference(
            kind,
            uri,
            contentHash ?? ProcessDriverEvidencePolicy.ComputeSha256(contentSeed),
            coreDescriptorFamily);
    }

    public static ProcessDriverVerificationRequest CreateVerificationRequest(
        ProcessDriverPermissionMode permissionMode,
        ProcessDriverCapabilityScope scope,
        IReadOnlyList<ProcessDriverEvidenceReference> evidenceReferences,
        IReadOnlyList<ProcessDriverOperation> requestedOperations,
        string callerContext)
    {
        return new ProcessDriverVerificationRequest(
            permissionMode,
            scope,
            evidenceReferences,
            requestedOperations,
            callerContext,
            ProcessDriverContractVersion.Current);
    }

    public static void AssertMutationFreeDenial(
        ProcessDriverVerificationResponse response,
        ProcessDriverDenialReason expectedDenialReason,
        ProcessDriverDiagnosticCategory expectedDiagnosticCategory)
    {
        Assert.False(response.Accepted);
        AssertNoMutation(response);
        Assert.Equal(expectedDenialReason, response.DenialReason);
        Assert.Contains(response.Diagnostics, diagnostic => diagnostic.Category == expectedDiagnosticCategory);
    }

    public static void AssertSideEffectDenied(
        ProcessDriverVerificationResponse response,
        ProcessDriverOperation requestedOperation)
    {
        Assert.False(response.Accepted);
        AssertNoMutation(response);
        Assert.NotEqual(ProcessDriverDenialReason.None, response.DenialReason);
        Assert.True(ProcessDriverOperationRules.IsSideEffectOperation(requestedOperation));
        Assert.Contains(
            response.Diagnostics,
            diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.MutationAttemptDenied);
        Assert.Contains(
            response.AuditFacts,
            fact => fact.RequestedOperation == requestedOperation && fact.DenialReason == response.DenialReason);
    }

    public static void AssertNoMutation(ProcessDriverVerificationResponse response)
    {
        Assert.True(response.NoMutationPerformed);
    }

    public static void AssertReadonlyAuditFacts(
        ProcessDriverVerificationResponse response,
        ProcessDriverPermissionMode expectedPermissionMode,
        ProcessDriverCapabilityScopeKind expectedScopeKind)
    {
        AssertNoMutation(response);
        Assert.NotEmpty(response.AuditFacts);
        Assert.All(response.AuditFacts, fact =>
        {
            Assert.Equal(expectedPermissionMode, fact.PermissionMode);
            Assert.Equal(expectedScopeKind, fact.Scope.Kind);
            Assert.Equal(expectedScopeKind, fact.Lane);
            Assert.NotEmpty(fact.EvidenceReferences);
            Assert.All(fact.EvidenceReferences, evidenceReference =>
                Assert.Matches("^[A-F0-9]{64}$", evidenceReference.ContentHash));
            Assert.True(fact.DiagnosticSummary.Length <= ProcessDriverRedactionPolicy.DefaultMaxAuditSummaryLength);
            Assert.Matches("^[A-F0-9]{64}$", fact.OutputHash);
            Assert.Equal(response.Redaction.Status, fact.Redaction.Status);
        });
    }

    public static void AssertNormalizedAuditFacts(
        ProcessDriverVerificationResponse response,
        string expectedCallerContext,
        ProcessDriverCapabilityScopeKind expectedLane,
        IReadOnlyList<ProcessDriverOperation> expectedOperations,
        ProcessDriverDenialReason expectedDenialReason)
    {
        Assert.NotEmpty(response.AuditFacts);
        Assert.Equal(
            expectedOperations.OrderBy(operation => operation).ToArray(),
            response.AuditFacts.Select(fact => fact.RequestedOperation).OrderBy(operation => operation).ToArray());
        Assert.All(response.AuditFacts, fact =>
        {
            Assert.Equal(expectedCallerContext, fact.CallerContext);
            Assert.Equal(expectedLane, fact.Lane);
            Assert.Equal(expectedLane, fact.Scope.Kind);
            Assert.Equal(expectedDenialReason, fact.DenialReason);
            Assert.NotEmpty(fact.EvidenceReferences);
            Assert.False(string.IsNullOrWhiteSpace(fact.DiagnosticSummary));
            Assert.True(fact.DiagnosticSummary.Length <= ProcessDriverRedactionPolicy.DefaultMaxAuditSummaryLength);
            Assert.Matches("^[A-F0-9]{64}$", fact.OutputHash);
        });
    }

    public static void AssertSealedReadonlyResponse(
        ProcessDriverVerificationResponse response,
        bool expectedAccepted,
        ProcessDriverDenialReason expectedDenialReason,
        string expectedCallerContext,
        ProcessDriverPermissionMode expectedPermissionMode,
        ProcessDriverCapabilityScopeKind expectedLane,
        IReadOnlyList<ProcessDriverOperation> expectedOperations,
        ProcessDriverRedactionStatus? expectedRedactionStatus = null,
        params string[] forbiddenFragments)
    {
        Assert.Equal(expectedAccepted, response.Accepted);
        Assert.Equal(expectedDenialReason, response.DenialReason);
        Assert.Equal(ProcessDriverContractVersion.Current, response.ContractVersion);
        Assert.NotEmpty(response.Diagnostics);
        Assert.NotEmpty(response.EvidenceReferences);
        Assert.All(response.EvidenceReferences, evidenceReference =>
            Assert.Matches("^[A-F0-9]{64}$", evidenceReference.ContentHash));
        Assert.Matches("^[A-F0-9]{64}$", response.Redaction.RedactedTextHash);
        if (expectedRedactionStatus.HasValue)
        {
            Assert.Equal(expectedRedactionStatus.Value, response.Redaction.Status);
        }

        if (response.Redaction.Status == ProcessDriverRedactionStatus.None)
        {
            Assert.Empty(response.Redaction.AppliedKinds);
        }
        else
        {
            Assert.NotEmpty(response.Redaction.AppliedKinds);
        }

        AssertReadonlyAuditFacts(response, expectedPermissionMode, expectedLane);
        AssertNormalizedAuditFacts(
            response,
            expectedCallerContext,
            expectedLane,
            expectedOperations,
            expectedDenialReason);
        Assert.All(response.AuditFacts, fact =>
        {
            Assert.False(fact.Scope.AllowsProcessMutation);
            Assert.False(fact.Scope.AllowsExternalCalls);
            Assert.False(fact.Scope.AllowsWorkspaceWrites);
            Assert.False(fact.Scope.AllowsStorageWrites);
            Assert.Equal(response.Redaction.RedactedTextHash, fact.Redaction.RedactedTextHash);
        });
        AssertDiagnosticsAndAuditDoNotContain(response, forbiddenFragments);
    }

    public static void AssertEvidenceHashMismatchDenied(
        ProcessDriverVerificationResponse response,
        string expectedCallerContext,
        ProcessDriverPermissionMode expectedPermissionMode,
        ProcessDriverCapabilityScopeKind expectedLane,
        IReadOnlyList<ProcessDriverOperation> expectedOperations,
        params string[] forbiddenFragments)
    {
        AssertSealedReadonlyResponse(
            response,
            expectedAccepted: false,
            ProcessDriverDenialReason.MissingEvidence,
            expectedCallerContext,
            expectedPermissionMode,
            expectedLane,
            expectedOperations,
            expectedRedactionStatus: null,
            forbiddenFragments: forbiddenFragments);
        Assert.Contains(
            response.Diagnostics,
            diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.EvidenceHashMismatch);
    }

    public static void AssertRedaction(
        ProcessDriverVerificationResponse response,
        ProcessDriverRedactionStatus expectedStatus,
        params ProcessDriverRedactionKind[] expectedKinds)
    {
        Assert.Equal(expectedStatus, response.Redaction.Status);
        foreach (var expectedKind in expectedKinds)
        {
            Assert.Contains(expectedKind, response.Redaction.AppliedKinds);
        }
    }

    public static void AssertDiagnosticsAndAuditDoNotContain(
        ProcessDriverVerificationResponse response,
        params string[] forbiddenFragments)
    {
        var diagnosticAndAuditText = response.Diagnostics
            .Select(diagnostic => diagnostic.Message)
            .Concat(response.AuditFacts.Select(fact => fact.DiagnosticSummary))
            .ToArray();

        foreach (var forbiddenFragment in forbiddenFragments.Where(static fragment => !string.IsNullOrWhiteSpace(fragment)))
        {
            Assert.All(diagnosticAndAuditText, value =>
                Assert.DoesNotContain(forbiddenFragment, value, StringComparison.Ordinal));
        }
    }
}
