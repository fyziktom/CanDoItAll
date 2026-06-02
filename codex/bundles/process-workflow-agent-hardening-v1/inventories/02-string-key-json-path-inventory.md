# String-Key And JSON-Path Inventory

## Known Examples To Canonicalize Or Classify

| Literal / pattern | Area | Required classification |
| --- | --- | --- |
| `workspace_dotnet_run` | Workspace command tool | Internal canonical tool id. |
| `workspace_dotnet_new` | Workspace command tool | Internal canonical tool id. |
| `workspace_dotnet_build` | Workspace command tool | Internal canonical tool id. |
| `workspace_dotnet_test` | Workspace command tool | Internal canonical tool id. |
| `browser_take_screenshot` | Browser tool | Internal canonical browser tool id. |
| `browser_navigate` | Browser tool | Internal canonical browser tool id. |
| `browser_snapshot` | Browser tool | Internal canonical browser tool id. |
| `browser_console_messages` | Browser tool | Internal canonical browser tool id. |
| `office365.messages-by-category` | Workflow executor | External/catalog executor id, wrapped by descriptor. |
| `office365.mark-message-processed` | Workflow executor | External/catalog executor id, wrapped by descriptor. |
| `office365.message-by-address-unprocessed` | Workflow executor | External/catalog executor id, wrapped by descriptor. |
| `gmail.messages-by-label` | Workflow executor | External/catalog executor id, unavailable diagnostics required. |
| `gmail.mark-message-processed` | Workflow executor | External/catalog executor id, unavailable diagnostics required. |
| `workflow-executor:create:*` | Workflow canvas/action id | UI action id; should be generated from descriptor. |
| `$.status` | JSON path | Centralized JSON path descriptor or external template boundary. |
| `$.route` | JSON path | Centralized JSON path descriptor or external template boundary. |
| `$.inputPayload.runContext.office365Processing.messageIds[0]` | Workflow mapping | Centralized mapping descriptor with validation. |
| `external-target/...` | Workspace alias | Alias boundary descriptor and validator. |
| `CanDoItAllSummaryTest` | Workflow test category | Test fixture only; must not be reused for destructive tests. |
| `CanDoItAllSummaryTestProcessed` | Workflow processed category | Test fixture only; must not be reused without explicit side-effect gate. |

## Scanner Rules

- A string literal can remain if it is a UI label, markdown content, or test fixture and is classified.
- A string literal must be moved or wrapped if it controls runtime behavior, security boundary, tool availability, executor availability, artifact satisfaction, process status, or billing.
- JSON paths used for workflow mapping must be validated against sample payloads and owned by executor/mapping descriptors.
- Template JSON may contain ids, but import/validation must verify them against canonical descriptors.
