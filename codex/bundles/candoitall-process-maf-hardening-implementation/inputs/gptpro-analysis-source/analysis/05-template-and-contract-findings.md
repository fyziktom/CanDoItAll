# Template and contract findings

## Current strengths

The templates are not empty or careless. They already contain many good process requirements:

- explicit setup subprocess for .NET skeleton creation,
- child validation before parent implementation,
- repair branch and no-go escalation,
- strict distinction between setup, implementation and validation,
- capability scopes and required receipts in some validation steps,
- prevention of runtime/browser proof in scaffold steps.

The issue is not “templates are too simple” in the old sense. The issue is that the important parts are split across prose, launch variables and runtime slot validation.

## Main template weaknesses

### 1. Prose-only accepted child outputs

`prepare-solution-skeleton` text accepts both `setup-handoff` and `setup-handoff-after-repair`. The JSON artifact expectation contains only:

```json
"SubprocessChildStepKey": "setup-handoff",
"SubprocessChildArtifactTitle": "Setup handoff packet"
```

This cannot represent the repair accepted path or no-go path robustly.

### 2. Manual skip on a required subprocess step

`AllowsManualSkip: true` on `prepare-solution-skeleton` is risky. The template says skip only when equivalent skeleton already exists and evidence is recorded, but the runtime needs a typed branch/output for that case.

Recommended choices:

- set `AllowsManualSkip: false`, or
- add explicit output contract `already-existing-skeleton-proof` and a typed branch/outcome that still produces `solution-skeleton-evidence`.

### 3. Long notes are used as hard gates

Some setup steps contain long notes with many imperative rules. Agents will miss details. Runtime can only enforce what is typed.

Move hard requirements into typed fields:

- `CompletionGates.RequiredPaths`
- `CompletionGates.RequiredToolReceipts`
- `CompletionGates.RequiredFileContentChecks`
- `BranchRules`
- `SubprocessContract.AcceptedChildOutputs`
- `SubprocessContract.NoGoChildOutputs`
- `ArtifactMaterialization.PrimaryManagedRefPattern`

### 4. Tool names are indirect

`prepare-solution-skeleton` has allowed operation `ExecuteExternalAction`, but the exact launch tool is not a required receipt/capability scope item on that step. If the design remains agent-owned, the exact `project_structure_process_subprocess_launch` tool must be required and preflighted. If runtime-owned, the template should state `LaunchMode: RuntimeOwned` and the agent prompt should not demand a launch tool.

## Proposed typed contract shape

```json
"SubprocessContract": {
  "DefinitionKey": "dotnet-solution-setup",
  "LaunchMode": "RuntimeOwned",
  "ParentProducedArtifactExpectationKey": "solution-skeleton-evidence",
  "AcceptedChildOutputs": [
    {
      "StepKey": "setup-handoff",
      "ArtifactExpectationKey": "setup-handoff-packet",
      "ArtifactTitle": "Setup handoff packet"
    },
    {
      "StepKey": "setup-handoff-after-repair",
      "ArtifactExpectationKey": "setup-handoff-packet-after-repair",
      "ArtifactTitle": "Repaired setup handoff packet"
    }
  ],
  "NoGoChildOutputs": [
    {
      "StepKey": "setup-repair-escalation",
      "ArtifactExpectationKey": "setup-repair-escalation-packet",
      "ArtifactTitle": "Setup repair escalation packet"
    }
  ],
  "AlreadySatisfiedOutput": {
    "Allowed": false,
    "RequiredArtifactExpectationKey": "already-existing-skeleton-proof"
  }
}
```

This shape can be introduced as additive metadata first, while keeping existing fields for backward compatibility.
