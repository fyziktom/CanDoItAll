# B06 — Template hardening

## Goal

Move hard process contracts from prose into typed template metadata while preserving existing behavior.

## Changes for `dotnet-development-slice`

### `prepare-solution-skeleton`

Add typed subprocess metadata:

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
  ]
}
```

Then decide one of:

- set `AllowsManualSkip` to `false`, or
- add a typed `AlreadySatisfiedOutput` that still produces `solution-skeleton-evidence`.

Recommended first-stage choice: set manual skip to false for this controlled subprocess step.

## Template loader validation

Add validation rules:

- A `StepKind=Subprocess` with `SubprocessProcessKey` must have a typed subprocess contract or a backward-compatible compiled contract.
- If markdown/prose names an accepted child output, it should also exist in metadata. If automatic prose scanning is too brittle, enforce metadata for new/modified templates only.
- `AllowsManualSkip=true` on required output steps must define a typed skip output contract.
- Accepted child output and no-go child output must not overlap.
- Parent artifact expectation key must exist in the step’s `ArtifactExpectations`.

## Reduce prose load

Keep markdown docs for human explanation. Hard gates should be typed. This is especially important for setup and validation steps where agents have been repeatedly missing one rule in long notes.

## Tests

- `TemplateValidation_SubprocessContractRequiredForSubprocessStep`
- `TemplateValidation_AcceptedChildOutputMustMapToKnownChildArtifact`
- `TemplateValidation_ManualSkipOnRequiredOutputRequiresSkipArtifactContract`
- `PrepareSolutionSkeleton_TemplateContainsRepairHandoffAcceptedOutput`

## Acceptance criteria

- `prepare-solution-skeleton` accepted/no-go child outputs are machine-readable.
- Runtime bridge can complete parent from either initial or repaired setup handoff without relying on prose.
