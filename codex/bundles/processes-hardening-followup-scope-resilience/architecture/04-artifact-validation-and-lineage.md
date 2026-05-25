# Artifact Validation And Lineage

## Required Improvements

### Explicit artifact contract

Prefer a typed artifact contract over string heuristics:

```csharp
public sealed record ProcessArtifactContract(
    ProcessArtifactExpectationMode Mode,
    string? RequiredFormat,
    bool RequiresManagedStoragePath,
    bool RequiresCurrentExecutorEvidence,
    bool AllowsAssistantResponseProjection,
    bool AllowsExistingManagedFile,
    bool AllowsManualRecord,
    bool AllowsWorkflowArtifact,
    bool AllowsManagerRecovery,
    IReadOnlyList<string> RequiredSections,
    IReadOnlyList<string> RequiredEvidenceKinds);
```

Keep existing `ValidationRequirementSummary` as human-readable text, but do not rely only on it for runtime classification.

### Conservative fallback

When no explicit contract exists:

- do not classify `decision log` as runtime proof just because it contains `log`;
- do not classify a valid planning `TODO register` as placeholder solely because it contains `todo`;
- do not reject a legal finding that says `data not available`;
- do not accept invalid JSON just because the file extension is `.json`.

### Lineage

For all producer kinds, validate current-run lineage:

| Producer | Required lineage |
| --- | --- |
| AgentExecutionArtifact | current execution run id in external key or provenance |
| WorkspaceWrite | current execution run id in external key |
| AssistantResponse | current execution run id in external key |
| ProviderNativeBrowser | current execution run id and current browser output file |
| WorkflowArtifact | current workflow run id |
| ManagerRecovery | recovery execution run id and recovery decision id |
| Manual | manual actor, timestamp, and explicit trust status |
| ExistingManagedFile | allowed only when the expectation allows carry-forward and the file hash/provenance matches the current dependency |

## Diagnostic vs Satisfying Records

A required artifact expectation is satisfied only by a satisfying record.

Diagnostic records must not set the same `ArtifactExpectationId` as if they completed the contract unless they carry:

```text
SatisfiesExpectation = false
```

If the schema does not support that yet, store diagnostics in journal entries or use a separate artifact kind/title that cannot be matched as satisfied.
