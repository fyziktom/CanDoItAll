# Subprocess Contract Inventory

This inventory is the minimum typed-contract coverage SB04 and SB08 must achieve.

Implementation-time verification: `bundle://proof/SB01/transcripts/template-inventory.txt` confirmed all current subprocess mappings are untyped and all `SubprocessChildArtifactExpectationId` values are blank. SB04/SB08 must therefore add a first-class contract instead of relying on backward-compatible legacy fields for hard completion decisions.

## Parent Contract Matrix

| Contract id | Parent step | Parent expectation | Accepted child outputs | No-go child outputs | Special gates |
| --- | --- | --- | --- | --- | --- |
| `SP01` | `dotnet-development-slice.prepare-solution-skeleton` | `solution-skeleton-evidence` | `dotnet-solution-setup.setup-handoff/setup-handoff-packet`; `dotnet-solution-setup.setup-handoff-after-repair/setup-handoff-packet-after-repair` | `dotnet-solution-setup.setup-repair-escalation/setup-repair-escalation-packet` | Manual skip disabled or typed `AlreadySatisfiedOutput` that still materializes parent proof. |
| `SP02` | `dotnet-development-slice.implement-code-change` | `slice-change-set` | `dotnet-feature-function-implementation.feature-handoff/feature-handoff-packet`; `feature-handoff-after-repair/feature-handoff-packet-after-repair` | `feature-repair-escalation/feature-repair-escalation-packet` | Child request must name one observable product behavior; scaffold-only child is invalid. |
| `SP03` | `dotnet-development-slice.slice-repair-code-change` | `slice-repair-change-set` | `feature-handoff/feature-handoff-packet`; `feature-handoff-after-repair/feature-handoff-packet-after-repair` | `feature-repair-escalation/feature-repair-escalation-packet` | Child scope must inherit exact repair target and failing proof. |
| `SP04` | `software-delivery.architecture-review` | `architecture-decision-record`; `project-structure-context-brief` | `dotnet-architecture-design-review.architecture-handoff/architecture-design-review-handoff`; `classify-dotnet-application/dotnet-application-classification` | Child blocked/failed/missing required architecture artifacts | Parent must not mutate product files. |
| `SP05` | `software-delivery.implementation` | `implementation-change-set`; `migration-rollout-preparation-checklist` | `dotnet-development-slice.slice-handoff/slice-handoff-packet`; `slice-handoff-after-repair/slice-handoff-packet-after-repair` | `slice-repair-escalation/slice-repair-escalation-packet` | Full app scope must launch first reviewable MVP slice, not scaffold-only. |
| `SP06` | `software-delivery.capture-ui-screenshots` | `ui-screenshot-writeback` | `dotnet-ui-screenshot-writeback.screenshot-handoff/ui-screenshot-writeback-handoff` | Child blocker with missing screenshots/no-UI proof/image-analysis receipts | Requires runtime command compatibility and visual-target comparison when source images exist. |
| `SP07` | `software-delivery.capture-ui-screenshots-after-repair` | `ui-screenshot-writeback-after-repair` | `dotnet-ui-screenshot-writeback.screenshot-handoff/ui-screenshot-writeback-handoff` | Child blocker with missing repaired screenshots/no-UI proof/image-analysis receipts | Same as SP06, using repaired evidence. |
| `SP08` | `software-delivery.record-runtime-commands` | `runtime-command-writeback` | `dotnet-runtime-command-writeback.runtime-command-handoff/runtime-command-handoff` | Child blocker with missing launcher-compatible command-node receipts | Requires `write-run-command-nodes` receipts. |
| `SP09` | `software-delivery.record-runtime-commands-after-repair` | `runtime-command-writeback-after-repair` | `dotnet-runtime-command-writeback.runtime-command-handoff/runtime-command-handoff` | Child blocker with missing repaired launcher-compatible command-node receipts | Same as SP08, using repaired evidence. |

## Required Typed Shape

```json
{
  "SubprocessContract": {
    "DefinitionKey": "child-process-key",
    "LaunchMode": "RuntimeOwned",
    "ParentProducedArtifactExpectationKey": "parent-artifact-key",
    "AcceptedChildOutputs": [],
    "NoGoChildOutputs": [],
    "RequiredChildReceipts": [],
    "AlreadySatisfiedOutput": null,
    "MaterializationMode": "RuntimeSynthesizedParentHandoff"
  }
}
```

Use strongly typed C# records/options for this shape. Do not pass opaque dictionaries or stringly typed branch classifiers through runtime behavior.
