# 09-email-processed-marker-unattended-policy

## Status

- Status: `Completed`

## Objective

Repair the reopened live issue where an Office365 email workflow asked for approval before changing the processed category, then reported completion after delayed approval without changing the category.

## Covered Inputs

- R11: approval/preapproval semantics for scheduled Office365 category mutation are explicit and auditable.
- Reopened feedback: email workflows around Office365 category-to-summary-to-project should complete without human-in-the-loop approval for the processed marker.

## Prerequisites

- SB03 template flow proves project writes happen before mark-processed.
- SB06 idempotency proof protects project writes from duplicate nodes/assets.
- SB07 approval and Scheduler waiting behavior are understood as the failure source for delayed marker approval.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowExecutorModels.cs`
- `repo://src/CanDoItAll.Plugins.Abstractions/PluginManifestValidation.cs`
- `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs`
- `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365BundledPlugin.cs`
- `repo://src/plugins/CanDoItAll.Plugin.Gmail/GmailWorkflowExecutor.cs`
- `repo://src/plugins/CanDoItAll.Plugin.Gmail/GmailBundledPlugin.cs`
- `repo://tests/CanDoItAll.Tests.Integration/SchedulerPlannerIntegrationTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/PluginCatalogIntegrationTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/PluginManifestTests.cs`

## Scope

- Add a narrow `IdempotentExternalMarker` executor capability.
- Allow unattended external writes only when a descriptor explicitly declares the marker capability.
- Mark Office365 and Gmail processed-marker executors as unattended idempotent external markers.
- Keep generic external writes approval-required unless they declare the marker capability.
- Update scheduler/plugin catalog and manifest validation tests to prove the policy.

## Dependency Impact

- Workflow runtime approval continuation remains unchanged; this repair avoids entering that path for email marker mutations.
- Office365/Gmail marker descriptors change approval behavior, so plugin catalog and manifest validation tests must stay aligned.
- Generic external-write plugins retain the existing approval guard unless they explicitly opt into the new marker capability.

## Validation Depth

- Failing-first integration proof captures the previous Office365 approval-required policy.
- Focused integration proof confirms Office365 mark-processed now runs unattended as an idempotent marker.
- Unit proof confirms the validator allows only the narrow marker exception.
- Plugin preview simulation proof confirms bundled descriptors remain valid.

## Implementation Steps

1. Add the typed marker capability.
2. Update manifest validation to permit unattended external writes only for marker executors.
3. Update Office365 and Gmail mark-processed real and bundled descriptors.
4. Update focused scheduler, plugin catalog, and manifest tests.
5. Capture source assertions, anti-stub audit, hashes, and bundle validators.

## Do Not Do

- Do not make all scheduler-launched external writes unattended.
- Do not add a generic approval bypass.
- Do not hide Graph or label/category mutation failures.
- Do not perform live mailbox mutation in automated proof.

## Acceptance Checklist

- [x] Office365 mark-processed no longer requires human approval.
- [x] Gmail mark-processed follows the same email marker policy.
- [x] Plugin manifest validation still rejects generic unattended external writes.
- [x] Focused tests prove the policy and the bundle validator closes the repair.

## Closure Notes

- Root cause was an approval/resume mismatch: the workflow stopped before the category mutation, and delayed approval marked the run complete without executing the skipped node.
- The repair uses a narrow typed capability instead of weakening the general external-write approval model.
- No live Office365 credentials were used in proof; tests use descriptors, validators, and deterministic plugin preview simulation.

## Proof Required

- `bundle://proof/SB09/manifest.md`
- `bundle://proof/SB09/semantic-invariants.md`
- Failing-first approval-policy transcript.
- Passing focused test transcripts.
- Source assertion, anti-stub audit, hash, and bundle validator transcripts.

## Browser Validation Logging

- Not required. This subbundle changes backend/plugin approval policy only and does not change visible Scheduler or Workflows UI.

## Progression Gate

- Close the repair only after the focused Office365 marker policy test, manifest validator tests, plugin simulation tests, and completed-stage bundle validator pass.

## Suggested Agent Prompt

Repair the email processed-marker approval policy so Office365/Gmail mark-processed workflow executors run unattended as explicit idempotent external markers while generic external writes remain approval-protected.
