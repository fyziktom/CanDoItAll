# Normalized Requirements

| Id | Requirement | Acceptance Signal |
|---|---|---|
| R01 | Add plugin-ready workflow executor contracts with typed ids, descriptor metadata, settings schema, setup renderer key, default policy, and execution interface. | Built-in descriptors are registered through the same public contracts that future plugins can use. |
| R02 | Add execution policy for timeouts, retry count/backoff, and non-happy-path behavior. | Validator and runtime reject invalid policy values and runtime records actionable failures. |
| R03 | Add a generic executor node model without one enum value per tool. | Workflow node settings persist executor id/settings while old node kinds continue to load. |
| R04 | Add `CanDoItAll.Tools.Documents` and wrap ClosedXML there. | No ClosedXML package reference or `using ClosedXML` appears outside the wrapper project and tests/reference code. |
| R05 | Add spreadsheet read/write executor capability for cell reads, range reads, cell writes, range writes, workbook summary, and Markdown table/report extraction. | A workflow scenario reads an `.xlsx`, writes a derived `.xlsx`, and produces a Markdown-friendly output payload. |
| R06 | Add storage/file executor capability using existing workspace file/storage services. | Workflow scenarios can list, stat, read, write, append, search, and diff bounded workspace paths with receipts. |
| R07 | Add project-structure executor capability for reading project/tree/subtree/node and creating asset nodes with artifact type selectors. | Workflow scenarios can read a subtree and create at least one typed asset node when services are available. |
| R08 | Add HTTP/HTTPS fetch executor with explicit URI validation, timeout, method/body/header settings, and size limits. | Workflow scenarios fetch JSON and text over HTTP/HTTPS and fail predictably for invalid URI/scheme/timeout. |
| R09 | Add AI image generation executor using existing image providers. | Workflow scenario attempts image generation and records either provider-backed success or explicit provider blocker. |
| R10 | Add obvious generic executor descriptors users will need next. | Catalog includes descriptor-only entries or follow-up records for JSON transform, Markdown render, delay/schedule, and approval/request nodes where implementation is not in this pass. |
| R11 | Wire executor nodes into MAF in-process execution. | Compiler invokes executor registry instead of pass-through behavior for executor nodes. |
| R12 | Capture executor outputs as workflow events/artifacts where policy says to capture. | Run history shows executor invocation/completion/failure and artifact records for artifact-producing executors. |
| R13 | Add workflow canvas right-click second-layer executor menu. | Browser proof shows grouped executor actions under a second-level menu. |
| R14 | Add workflow component toolbox for executors. | Browser proof shows grouped/searchable executor toolbox with storage, project, HTTP, image, spreadsheet categories. |
| R15 | Add setup UI hooks for built-in and future plugin executors. | Node inspector edits descriptor-backed settings and stores them in typed settings JSON. |
| R16 | Validate with at least 20 real-world workflow examples. | Execution report lists 20 scenario rows with result, provider, and proof artifact. |
| R17 | Test `gpt-5-mini` and Ollama `gptoss20b64k`. | Execution report records provider/model state, command/run result, and exact blocker if unavailable. |
