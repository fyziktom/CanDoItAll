# Findings register

| ID | Severity | Finding | Consequence |
|---|---|---|---|
| F-001 | High | Three feature projects live under src/Modules and CanDoItAll.Modules.LlmChats despite representing reusable MAF LLM capability. | Solution grouping and namespace imply the wrong owner. |
| F-002 | High | The current core project mixes domain, application orchestration, ports, state machines, and DI across about 49 source files. | Renaming it alone would preserve a mixed boundary. |
| F-003 | Critical | LlmChats.Persistence combines EF/database concerns with provider resolution, audited invocation, conversation engine construction, and runtime execution. | Persistence is an accidental composition/runtime owner. |
| F-004 | High | LlmChats.Ui combines reusable components/gateways with /chats routing, shell navigation, and host registration. | Reuse from the Agent page would duplicate page chrome and navigation. |
| F-005 | Positive | Existing AgentFramework.Llm.Abstractions, Llm.Conversations, Llm.ProviderRuntime, Conversations.Components, and Conversations.Shell already provide generic seams. | Reuse them; do not add duplicate basic-chat projects. |
| F-006 | High | Web, App.Composition, PostgreSQL migrations, tests, and the solution directly reference old LlmChats projects/namespaces. | Cutover needs caller inventory and a bounded no-new-caller migration. |
| F-007 | High | /agents already uses SecondaryTabs, but tab keys are scattered string literals and there is no Simple Chats item. | Add a centralized typed tab catalog before query/redirect integration. |
| F-008 | Medium | /chats owns a full PageScaffold with nested Conversations/Definitions tabs and has a separate shell navigation contributor. | Canonical placement must render a reusable body, not nest the routed page. |
| F-009 | Positive | Simple Chats already resolves Chat-purpose profiles from canonical AgentFramework provider sources. | Provider setup belongs in the existing Providers tab. |
| F-010 | Critical | Agent usage observations and projections have no typed workload/producer dimension. | Chats cannot be filtered reliably and ChatSessionId is not a valid discriminator. |
| F-011 | Critical | Agent usage is a file-backed execution projection while Simple Chat usage is relational invocation evidence. | Dual writes would be non-atomic and retry-prone; use source adapters. |
| F-012 | Critical | Simple Chat invocation rows lack usage status, reasoning/cache-write detail, immutable cost, and pricing provenance. | Current dashboard cannot report trustworthy chat cost or unknown completeness. |
| F-013 | High | Transcript usage is duplicated success evidence and excludes billable failed/retried attempts. | Invocation OperationId + Ordinal must be the only chat cost identity. |
| F-014 | High | Agent usage assembly/pricing lives in AgentFrameworkWorkspaceExecutionService.Usage partial and file-store projection code. | Extract top-level reusable/testable usage collaborators; do not add partials. |
| F-015 | High | Large files include the 788-line workspace controller, 517-line transfer document, 441-line shell contributor, 393-line engine, and 356-line state machine. | Ownership must move with behavior and large classes should shrink through collaborators, not new projects per file. |
| F-016 | High | Existing historical chat rows cannot prove the price that applied at execution time. | Backfill tokens/status deterministically and mark price unknown; never reprice. |
| F-017 | High | No dedicated repeatable Playwright Simple Chat/Agent chat consolidation scenario exists. | Add named scenarios and require Playwright MCP closure. |
| F-018 | Medium | Components MCP was unavailable and CodeAnalytics reports unrelated pre-existing AgentFramework cycles. | Retry component discovery at SB07 and gate only no-new-cycle/no-enlargement. |
| F-019 | High | LlmChatDefinitionEditorDialog is a single stacked form without internal settings tabs and exposes AvatarImageUrl as a raw textbox. | The integrated editor would remain less usable and inconsistent with Agent settings unless SB07 adds typed modal tabs and replaces the raw field. |
| F-020 | High | AgentDetailsDialog contains the complete avatar catalog/upload/AI-generation selector inline instead of a reusable component. | Copying it into Simple Chats would duplicate policy and UI; extract one shared AgentFramework component and shrink the Agent dialog. |
