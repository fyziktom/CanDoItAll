# Normalized Requirements

| Id | Requirement | Test |
| --- | --- | --- |
| R1 | New Office365 executor downloads at most one unprocessed email matching a configured address. | Unit fake Graph test + integration plugin descriptor test. |
| R2 | Matching excludes messages already carrying the processed category. | Graph URL assertion + fake response test. |
| R3 | No matching email returns no-op success by default, not exception/failure. | Scheduled workflow scenario test with empty fake Graph response. |
| R4 | Mark processed step can add processed category without requiring a source category. | Unit test for add-only category mutation. |
| R5 | Summary workflow template stores Markdown summary asset under configured project/node and then marks message processed. | Template loader + scenario harness test. |
| R6 | Task workflow template creates project task nodes under configured project/node and then marks message processed. | Template loader + fake project gateway test. |
| R7 | Project writes are idempotent by Office365 message id. | Retry test proving no duplicate task/asset creation. |
| R8 | Scheduler can select a workflow and configure typed input fields for email/contact, project, parent node, processed category, and interval. | Component/browser tests on `/scheduler`. |
| R9 | Scheduler can pick email from CRM when CRM contacts exist, while still allowing manual email entry. | Fake CRM option provider/component test. |
| R10 | Scheduler dispatch records `NoMessages` separately from failures. | Scheduler run history unit/integration test. |
| R11 | Approval/preapproval semantics for scheduled Office365 category mutation are explicit and auditable. | Approval policy tests. |
| R12 | All templates are file-backed under `Templates/Workflows` and loaded through the manifest. | Template pack loader test. |
