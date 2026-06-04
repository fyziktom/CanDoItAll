# Semantic Invariants SB03

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| File-backed Office365 watch templates | `repo://Templates/Workflows/workflows/office365-email-watch-workflows.yaml` | `bundle://proof/SB03/transcripts/unit-template-loader-after-sb03.txt` | registered in `repo://Templates/Workflows/manifest.yaml` | `bundle://proof/SB03/transcripts/failing-first-office365-watch-templates-before-implementation.txt` |
| No-message side-effect skip | no-message switch routes to compact end nodes | `AssertNoMessageBranch` in unit proof | compact result preserves Scheduler context | source assertions show no no-message edge to LLM/project/mark nodes |
| Summary write-before-mark | `store-office365-watch-summary` precedes `mark-office365-watch-summary-processed` | unit graph assertions | mark input carries project write output | no mark-processed edge exists before summary write |
| Task write-before-mark | `create-office365-watch-task-nodes` and `store-office365-watch-no-task-summary` precede `mark-office365-watch-tasks-processed` | unit graph assertions | mark input carries project write output | no mark-processed edge exists before task/no-task writes |
| Scheduler input path resolution | template JSON-path fields plus resolver support | integration path-resolution proof | SB04 typed parameters can bind to the same keys | empty configured values are resolved from Scheduler input JSON paths |

## SB03-INV-FILE-BACKED-TEMPLATES

- Invariant ID: `SB03-INV-FILE-BACKED-TEMPLATES`
- Source raw note: R12.
- Expected behavior: Office365 email-watch summary and task templates are stored under `Templates/Workflows`, registered through the manifest, and loaded by the default template pack.
- Disallowed shallow implementation: hard-coding templates in tests, leaving them unregistered, or adding only documentation without loader visibility.
- Failing-first test: `bundle://proof/SB03/transcripts/failing-first-office365-watch-templates-before-implementation.txt`
- Passing test: `Default_template_pack_loads_file_backed_workflow_examples` in `bundle://proof/SB03/transcripts/unit-template-loader-after-sb03.txt`
- Changed source files: `repo://Templates/Workflows/manifest.yaml` current SHA-256 `9db4d9de6152a26c6da6f36cdae3aecd2e649f6ce967174a5374d7ef9a99ce8e`; `repo://Templates/Workflows/workflows/office365-email-watch-workflows.yaml` current SHA-256 `d4d688e946bfd0a316dad1667599e2a784a0c6aab4d59385d11fd65a3be10efb`
- Production assertions: `bundle://proof/SB03/transcripts/source-assertions-office365-watch-templates.txt`
- Red-team negative case: the failing-first transcript proves the template keys did not exist in `HEAD` before implementation.
- Downstream dependency check: SB04 can inspect the file-backed template settings and add parameter schema without relying on in-memory fixtures.

## SB03-INV-NO-MESSAGE-SKIPS-SIDE-EFFECTS

- Invariant ID: `SB03-INV-NO-MESSAGE-SKIPS-SIDE-EFFECTS`
- Source raw note: R3, R5, and R6.
- Expected behavior: when the Office365 address executor returns `route = no_messages`, both templates finish through compact no-message result nodes without calling LLM, project writes, or mark-processed.
- Disallowed shallow implementation: treating no-message as failure, fabricating message content, or routing no-message through write/category mutation nodes.
- Failing-first test: `bundle://proof/SB03/transcripts/failing-first-office365-watch-templates-before-implementation.txt`
- Passing test: `AssertNoMessageBranch` coverage in `bundle://proof/SB03/transcripts/unit-template-loader-after-sb03.txt`
- Changed source files: `repo://Templates/Workflows/workflows/office365-email-watch-workflows.yaml` current SHA-256 `d4d688e946bfd0a316dad1667599e2a784a0c6aab4d59385d11fd65a3be10efb`
- Production assertions: `bundle://proof/SB03/transcripts/source-assertions-office365-watch-templates.txt`
- Red-team negative case: graph assertions require the default branch to reach LLM while the `no_messages` branch reaches only compact no-op result nodes.
- Downstream dependency check: SB06 can classify no-message Scheduler runs separately from failures without side effects.

## SB03-INV-SUMMARY-WRITE-BEFORE-MARK

- Invariant ID: `SB03-INV-SUMMARY-WRITE-BEFORE-MARK`
- Source raw note: R5.
- Expected behavior: the summary workflow writes the generated Markdown asset under the configured project/node before it marks the Office365 message processed.
- Disallowed shallow implementation: marking the message processed before project write success or omitting the project write output from mark-processed input.
- Failing-first test: `bundle://proof/SB03/transcripts/failing-first-office365-watch-templates-before-implementation.txt`
- Passing test: summary edge assertions in `bundle://proof/SB03/transcripts/unit-template-loader-after-sb03.txt`
- Changed source files: `repo://Templates/Workflows/workflows/office365-email-watch-workflows.yaml` current SHA-256 `d4d688e946bfd0a316dad1667599e2a784a0c6aab4d59385d11fd65a3be10efb`
- Production assertions: `bundle://proof/SB03/transcripts/source-assertions-office365-watch-templates.txt`
- Red-team negative case: source assertions and graph tests require `store-office365-watch-summary -> mark-office365-watch-summary-processed`.
- Downstream dependency check: SB06 and SB08 can prove retries do not lose messages after a failed project write.

## SB03-INV-TASK-WRITE-BEFORE-MARK

- Invariant ID: `SB03-INV-TASK-WRITE-BEFORE-MARK`
- Source raw note: R6.
- Expected behavior: the task workflow writes either task nodes or a no-task summary asset before it marks the Office365 message processed.
- Disallowed shallow implementation: marking processed immediately after LLM extraction or skipping project output for informative messages.
- Failing-first test: `bundle://proof/SB03/transcripts/failing-first-office365-watch-templates-before-implementation.txt`
- Passing test: task edge assertions in `bundle://proof/SB03/transcripts/unit-template-loader-after-sb03.txt`
- Changed source files: `repo://Templates/Workflows/workflows/office365-email-watch-workflows.yaml` current SHA-256 `d4d688e946bfd0a316dad1667599e2a784a0c6aab4d59385d11fd65a3be10efb`
- Production assertions: `bundle://proof/SB03/transcripts/source-assertions-office365-watch-templates.txt`
- Red-team negative case: graph tests require both `create-office365-watch-task-nodes` and `store-office365-watch-no-task-summary` to feed `mark-office365-watch-tasks-processed`.
- Downstream dependency check: SB06 can add idempotency around concrete project writes without changing template ordering.

## SB03-INV-SCHEDULER-INPUT-PATHS

- Invariant ID: `SB03-INV-SCHEDULER-INPUT-PATHS`
- Source raw note: R8 and R12.
- Expected behavior: templates accept Scheduler input JSON keys for connection id, email address, processed category, lookback hours, project id, and node id through explicit JSON-path settings.
- Disallowed shallow implementation: requiring users to edit raw executor JSON or baking static project/email/category values into templates.
- Failing-first test: `bundle://proof/SB03/transcripts/failing-first-office365-watch-templates-before-implementation.txt`
- Passing test: `Office365_address_filter_settings_resolve_scheduler_input_paths` in `bundle://proof/SB03/transcripts/integration-office365-scheduler-input-paths-after-sb03.txt`
- Changed source files: `repo://src/plugins/CanDoItAll.Plugin.Email/EmailWorkflowPayloadResolver.cs` current SHA-256 `90e3f1b29d5fba20c3b29d46024cc2acee0dfbb56f3f888d33319fccc8497f99`; `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs` current SHA-256 `eee79697f64b8405ae0f17cf9b7a91bfacf89ebb68287937c875cb8d296bb94f`
- Production assertions: `bundle://proof/SB03/transcripts/source-assertions-office365-watch-templates.txt`
- Red-team negative case: the integration test leaves configured values empty and proves runtime settings come from input JSON paths.
- Downstream dependency check: SB04 can expose typed Scheduler fields for the same keys instead of leaving this as raw JSON.
