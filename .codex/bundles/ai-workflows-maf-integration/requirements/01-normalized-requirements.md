# Normalized Requirements

| ID | Requirement | Source |
| --- | --- | --- |
| RQ-001 | Workflows must be represented as first-class executable definitions in CanDoItAll. | Main goal |
| RQ-002 | Microsoft Agent Framework workflow SDK must be analyzed from local source and used where it provides suitable execution primitives. | Main goal |
| RQ-003 | Processes must remain above workflows and agents; process runs choose an executor, they are not replaced by workflows. | Human architect note 1 |
| RQ-004 | A process role must be fillable by an AI agent or by a workflow. | Human architect note 1 |
| RQ-005 | Phase 1 must improve MAF wrapper libraries with workflow models, helpers, and wrappers before UI/process integration. | Human architect note 2 |
| RQ-006 | A detailed architecture review must run after phase 1 and gate downstream phases. | Human architect note 2 |
| RQ-007 | Each phase must include an architecture review and capture changes required by that review. | Human architect note 2 |
| RQ-008 | Workflows must have their own settings system, similar in API/UI usability to processes but not the same canonical model. | Human architect note 3 |
| RQ-009 | Workflows must have a testing system covering APIs and UI flows. | Human architect note 3 |
| RQ-010 | Workflows must have their own canvas editor similar to the process canvas. | Human architect note 3 |
| RQ-011 | Workflow runs must support artifacts. | Human architect note 3 |
| RQ-012 | Workflow runs must support human-in-loop requests and responses. | Human architect note 3 |
| RQ-013 | Workflow steps must be able to use agents. | Human architect note 3 |
| RQ-014 | Workflow steps must support common LLM call patterns: strict instructions, result capture, handoff of result to another step, LLM triage, and strict logic. | Human architect note 3 |
| RQ-015 | The system must include prepared LLM Call Components usable as workflow building blocks. | Human architect note 4 |
| RQ-016 | LLM Call Components must capture provider/model, modality, model settings, instructions, and result shape. | Human architect note 4 |
| RQ-017 | The implementation must decide, with evidence, whether MAF provides enough runtime core for CanDoItAll workflow execution. | Human architect note 5 |
| RQ-018 | If MAF runtime core is insufficient by itself, CanDoItAll must add a wrapper or workflow runtime library for clean parallelism, observations, checkpoints, and management. | Human architect note 5 |
| RQ-019 | Workflow UI must stay in the existing Agents module but have its own page. | Human architect note 2 |
| RQ-020 | Strongly typed models must be used for identifiers, executor kinds, component kinds, states, and commands instead of magic strings. | AGENTS.md and architecture requirements |
| RQ-021 | Implementation agents must not skip architecture reviews, validation proof, or execution report updates. | User planning instruction |
| RQ-022 | Durable production/long-running workflow execution must evaluate and prefer `Microsoft.Agents.AI.DurableTask` and DTS instead of reimplementing durable orchestration. | .NET Blog article |
| RQ-023 | In-process MAF execution must be limited to local development, tests, previews, or explicitly non-durable short runs unless architecture review accepts another use. | .NET Blog article |
| RQ-024 | Hosting must evaluate `ConfigureDurableOptions` when workflows and agents are registered together, and `ConfigureDurableWorkflows` for workflow-only hosts. | .NET Blog article and MAF source |
| RQ-025 | Azure Functions hosting, generated workflow endpoints, RequestPort response/status endpoints, and MCP tool exposure must be evaluated explicitly as hosting/integration options. | .NET Blog article |
| RQ-026 | Workflow runtime implementation must include performance review gates for async, serialization, event streaming, polling/status endpoints, graph validation, and collection-heavy hot paths. | analyzing-dotnet-performance skill |
