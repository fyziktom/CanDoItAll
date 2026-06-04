# Proof Manifest SB03

Status: `Completed`

Subbundle: `03-office365-email-summary-and-task-template-workflows`

## Owned Requirements

- R5: Summary workflow stores Markdown summary asset under configured project/node and then marks the message processed.
- R6: Task workflow creates project task nodes under configured project/node and then marks the message processed.
- R12: Templates are file-backed under `Templates/Workflows` and loaded through the manifest.

Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`

## Changed File Manifest

Source hash transcript: `bundle://proof/SB03/transcripts/changed-file-hashes-sb03.txt`

| Path | Before marker | Current SHA-256 |
| --- | --- | --- |
| `repo://Templates/Workflows/manifest.yaml` | HEAD blob `759b937375a476ed3e2f11c73c0cf6696670e7f7` | `9db4d9de6152a26c6da6f36cdae3aecd2e649f6ce967174a5374d7ef9a99ce8e` |
| `repo://Templates/Workflows/workflows/office365-email-watch-workflows.yaml` | `NEW_IN_WORKTREE` | `d4d688e946bfd0a316dad1667599e2a784a0c6aab4d59385d11fd65a3be10efb` |
| `repo://src/plugins/CanDoItAll.Plugin.Email/EmailWorkflowPayloadResolver.cs` | HEAD blob `2543841c0ad9a178be52648235429d6a371069e2` | `90e3f1b29d5fba20c3b29d46024cc2acee0dfbb56f3f888d33319fccc8497f99` |
| `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365PluginModels.cs` | HEAD blob `7aacbb33b2e8ab38f46f47b73d04d9957daf1138` | `b6ea52a415e9495636118d649833a916795789c8e60e3db992635209d58badfb` |
| `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs` | HEAD blob `cd6d91c3e5de53d5b64459e99d4ce842525565f5` | `eee79697f64b8405ae0f17cf9b7a91bfacf89ebb68287937c875cb8d296bb94f` |
| `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365BundledPlugin.cs` | HEAD blob `305fc13344f0c53211e7a9f6244e17dcb3d59544` | `179d954c5b027507fb80b01987d9221e3c8f85e45428b720ee5559744fd0ecd2` |
| `repo://tests/CanDoItAll.Tests.Unit/ProjectStructureWorkflowPreviewSimulationSupportTests.cs` | HEAD blob `f955650b7c743a8a02cfa22fa9d965e692c562f7` | `5a189f44f05bf319a3429c76dd2634eca3d0cd10de1bc3ad57733a82f343c59b` |
| `repo://tests/CanDoItAll.Tests.Integration/EmailPluginClientTests.cs` | HEAD blob `06adf82229644a7989b6ba7a7f02d1d7818ee7f1` | `14aa511d1e89cac95a349b542dee8d57f07c1c9a8b513d443bba73c8510a7eae` |

## Command Transcripts

- Failing-first: `bundle://proof/SB03/transcripts/failing-first-office365-watch-templates-before-implementation.txt`
- Passing template proof: `bundle://proof/SB03/transcripts/unit-template-loader-after-sb03.txt`
- Build: `bundle://proof/SB03/transcripts/build-after-sb03.txt`
- Template loader and graph tests: `bundle://proof/SB03/transcripts/unit-template-loader-after-sb03.txt`
- Office365 dynamic input path integration tests: `bundle://proof/SB03/transcripts/integration-office365-scheduler-input-paths-after-sb03.txt`
- Source assertions: `bundle://proof/SB03/transcripts/source-assertions-office365-watch-templates.txt`
- Anti-stub audit: `bundle://proof/SB03/transcripts/anti-stub-audit-office365-watch-templates.txt`
- Semantic invariant labels: `bundle://proof/SB03/transcripts/semantic-invariant-evidence.txt`

## Failing-First And Passing Proof

- Failing-first proof uses `git grep` against `HEAD` to prove the Office365 email-watch summary/task templates were absent before this subbundle.
- Passing unit proof loads the default workflow template pack and asserts the new summary/task graphs, no-message switch branches, dynamic Scheduler input paths, and write-before-mark edges.
- Passing integration proof resolves Scheduler input JSON paths for Office365 address polling and keeps bundled preview simulation deterministic without live Office365 calls.

## Source Assertions

- `repo://Templates/Workflows/workflows/office365-email-watch-workflows.yaml` defines the summary and task email-watch workflows, no-message branches, compact Scheduler result nodes, and write-before-mark ordering.
- `repo://Templates/Workflows/manifest.yaml` registers the new workflow file and bumps template seed/version metadata.
- `repo://src/plugins/CanDoItAll.Plugin.Email/EmailWorkflowPayloadResolver.cs` supports typed optional JSON-path resolution used by Scheduler-supplied inputs.
- `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs` consumes dynamic Office365 connection/category/lookback settings and carries the selected message context into mark-processed.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| File-backed Office365 watch templates | `repo://Templates/Workflows/workflows/office365-email-watch-workflows.yaml` | `bundle://proof/SB03/transcripts/unit-template-loader-after-sb03.txt` | manifest registration in `repo://Templates/Workflows/manifest.yaml` | `bundle://proof/SB03/transcripts/failing-first-office365-watch-templates-before-implementation.txt` |
| No-message side-effect skip | template switch route `no_messages` to compact end nodes | `AssertNoMessageBranch` in unit proof | compact no-message result preserves Scheduler context | source assertions show no edge from no-message branch to LLM/project/mark nodes |
| Summary write-before-mark | summary graph edge from `store-office365-watch-summary` to `mark-office365-watch-summary-processed` | unit graph assertions | project write includes input payload before category mutation | no mark-processed edge exists before summary project write |
| Task write-before-mark | task graph edges from `create-office365-watch-task-nodes` and `store-office365-watch-no-task-summary` to mark processed | unit graph assertions | project writes include input payload before category mutation | no mark-processed edge exists before task/no-task project writes |
| Scheduler dynamic input paths | template settings plus Office365 resolver support | `bundle://proof/SB03/transcripts/integration-office365-scheduler-input-paths-after-sb03.txt` | SB04 can surface the same parameters as typed Scheduler fields | integration test proves configured empty values resolve from input JSON paths |

## Browser, Host, And External Service Proof

- Browser proof for the Workflows page template visibility is deferred to SB08 final browser proof. SB03 did not change UI component code; automated loader tests prove the templates are discoverable by the same file-backed catalog consumed by the Workflows UI.
- Live Office365 proof is intentionally not used; all automated proof uses fake Graph handlers, source assertions, and deterministic preview simulation.

## Anti-Stub Audit

`bundle://proof/SB03/transcripts/anti-stub-audit-office365-watch-templates.txt` reports no `TODO`, `NotImplemented`, `throw new NotImplementedException`, or fixture-specific branches in scoped Office365/template production files.
