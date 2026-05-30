# Proof Manifest SB09

Status: `Completed`

Subbundle: `09-email-processed-marker-unattended-policy`

Semantic invariant contract: `bundle://proof/SB09/semantic-invariants.md`

## Owned Requirements

- R11: Approval/preapproval semantics for scheduled Office365 category mutation are explicit and auditable.
- Reopened live feedback: missed approval later reported success but did not change the email category.

## Root Cause

- `Office365MarkProcessedWorkflowExecutor` previously required `RequiredForExternalEffect` approval because it writes the Outlook message category.
- `WorkflowExecutorInvoker` stopped before executing the category mutation when approval was required.
- `WorkflowRuntimeManager.RespondToExternalRequestAsync` completed the waiting workflow response after approval instead of resuming execution at the skipped mark-processed node.
- The practical result was a false successful completion with no email category mutation.

## Changed File Hashes

- Source/test hash transcript: `bundle://proof/SB09/transcripts/file-hashes-email-marker-policy.txt`
- `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowExecutorModels.cs` SHA-256 `28a9c8a5cec2e47d36b0bf5132a98551240dda2b001a1da452ddf84e273e7126`
- `repo://src/CanDoItAll.Plugins.Abstractions/PluginManifestValidation.cs` SHA-256 `11e602deec56975a81d521c6a54cd16ba08533ddc75df62d6ddce1352170c633`
- `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs` SHA-256 `eccc0c1ede36dcc1fcf3d249f58c020dc72912b6c1497cf7e801665e8db22593`
- `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365BundledPlugin.cs` SHA-256 `149fe8cf28f76e359b99eb1c44bc9dacd4c5cc1e57746c5959162fdda8419387`
- `repo://src/plugins/CanDoItAll.Plugin.Gmail/GmailWorkflowExecutor.cs` SHA-256 `9dc3c435318d66a90924d653362ce16b2af5c526af3cd8be87347ae9f18541fb`
- `repo://src/plugins/CanDoItAll.Plugin.Gmail/GmailBundledPlugin.cs` SHA-256 `a23d4f1ef2308f0afdd6b749bed75fd0e0579b04082a9738b33094b167bb44a0`
- `repo://tests/CanDoItAll.Tests.Integration/SchedulerPlannerIntegrationTests.cs` SHA-256 `f0c21a345ce55ca16febf717d9970dc0d75321d5ffeec36f61697f5349a1beed`
- `repo://tests/CanDoItAll.Tests.Integration/PluginCatalogIntegrationTests.cs` SHA-256 `4aff7fb3d8c6ee0e5f682a149ebdb22e2599b3075955b8371fc25e9ad2a8ebd1`
- `repo://tests/CanDoItAll.Tests.Unit/PluginManifestTests.cs` SHA-256 `a04c6844b55a18c745b0f0c39f97098c4424da5565715eaf95c2b4f216634921`

## Production Behavior Artifact Matrix

| Behavior | Producer | Consumer | Proof |
| --- | --- | --- | --- |
| Idempotent email marker capability exists as a typed flag. | `WorkflowExecutorCapabilityFlags.IdempotentExternalMarker` | Plugin descriptors and manifest validator. | `bundle://proof/SB09/transcripts/source-assertions-email-marker-policy.txt` |
| Generic external writes still require approval. | `PluginManifestValidator` external-write rule. | Plugin catalog validation. | `bundle://proof/SB09/transcripts/passing-plugin-manifest-tests.txt` |
| Office365 processed category mutation runs without human approval. | Office365 real and bundled mark-processed descriptors. | Workflow executor invoker policy check. | `bundle://proof/SB09/transcripts/passing-office365-processed-marker-policy.txt` |
| Gmail processed label mutation follows the same email policy. | Gmail real and bundled mark-processed descriptors. | Plugin preview/catalog validation. | `bundle://proof/SB09/transcripts/passing-plugin-simulation-tests.txt` |

## Command Transcripts

- Failing-first approval-policy regression: `bundle://proof/SB09/transcripts/failing-first-office365-processed-marker-approval-policy.txt`
- Passing Office365 marker policy integration test: `bundle://proof/SB09/transcripts/passing-office365-processed-marker-policy.txt`
- Passing plugin manifest validator unit tests: `bundle://proof/SB09/transcripts/passing-plugin-manifest-tests.txt`
- Passing bundled plugin preview simulation tests: `bundle://proof/SB09/transcripts/passing-plugin-simulation-tests.txt`
- Broad plugin catalog run with unrelated package-install failures: `bundle://proof/SB09/transcripts/plugin-catalog-broad-run-unrelated-failures.txt`
- Source assertions: `bundle://proof/SB09/transcripts/source-assertions-email-marker-policy.txt`
- Anti-stub audit transcript: `bundle://proof/SB09/transcripts/anti-stub-audit-email-marker-policy.txt`
- Completed-stage validator transcript: `bundle://proof/SB09/transcripts/validate-bundle-completed-after-sb09.txt`

## Result

- The repair keeps the approval model strict for generic external effects.
- Office365/Gmail processed-marker executors run unattended because they are narrow idempotent markers.
- No live Office365 credentials or Microsoft Graph calls were used during automated proof.
