# HR agent governance boundary

Status: Accepted  
Date: 2026-07-14  
CodeAnalytics final snapshot: `snap-20260714124927-00d1a37b`

## Context

The managed agent pack already contains `hr-staffing-manager`. That agent chooses process-role assignments and must remain narrowly focused. The requested HR agent is a different responsibility: it administers technical agents, reviews their usage and process evidence, generates avatars, and reads privacy-safe CRM/HR summaries.

The existing agent runtime composes `IAgentRuntimeToolProvider` implementations. AgentFramework may depend on CRM/HR, while Processes already depends on AgentFramework. Reversing that dependency would create a project cycle. CRM/HR services also expose private contact and confidential workforce data that must not be returned directly to a model.

## Decision

Add a distinct managed agent with stable key `agents/hr-agent`, template key `hr-agent`, and its deterministic agent ID published by `HrAgentIdentity`. Privileged tool attachment requires that immutable ID, the expected template key, Active lifecycle state, a non-template agent, tool permission, and an exact assigned capability-to-catalog mapping. Workload, tags, names, and observation permission are not authorization inputs. Every tool invocation reloads the managed identity and capability catalog before executing, so a stale runtime closure cannot bypass lifecycle suspension or capability revocation. The HR tools cannot update the HR agent itself or create another privileged HR identity.

Add a dedicated HR runtime-tool provider and cohesive application services. Do not add another partial file to the existing catalog or execution monoliths. Tools are described through the existing provider metadata/catalog seam and registered at module composition.

The initial tool surface is:

- agent search and settings inspection;
- technical-agent creation and an allowlisted settings patch;
- AI avatar generation and assignment as one approval-gated operation;
- usage and cost aggregation with explicit work-scope and date filters;
- process participation and run-review evidence;
- an approval-gated review request to an explicitly selected manager who participated in the run;
- bounded, redacted CRM/HR search and item summaries.

Agent creation uses the current-profile `IAgentFrameworkWorkspaceService`. Its existing projection bridge synchronizes a CRM AI-agent party and technical binding after the catalog save, but it does not create an editable `AiAgentProfile`. Full CRM/HR AI-resource profile creation remains owned by `AiAgentService`. Because catalog save precedes projection synchronization, the HR tool re-reads the catalog after a synchronization exception and reports whether the technical agent was created instead of implying that the whole operation rolled back.

All mutations are added to `AgentToolInvocationPolicyMetadata`, `ToolContractCatalog`, and `ToolCapabilityRegistry`, so the existing approval wrapper and execution receipts remain the enforcement and audit path. Service/application logs include actor, target, and run identifiers but no prompts, contact data, secrets, or raw CRM content. HR tools with free text use tool-aware redaction in runtime progress and receipt signatures. Pending approval state retains raw mutation arguments only while replay is required. At decision time, persisted HR approval rows replace those arguments with a deterministic redacted audit representation and a canonical SHA-256 hash. Agent-package export applies the same protection to legacy rows and pending approval projections, while non-HR approval behavior remains unchanged.

Settings updates use an allowlisted typed patch. They never accept raw configuration JSON, secret references, template identity, or HR authority. The caller supplies the target's observed `UpdatedAtUtc`; stale updates fail predictably. Avatar generation uses only the HR seed's explicitly configured image provider/model, requires the requested JPEG result, validates marker structure, dimensions, square shape, and the shared avatar byte limit, and preserves the old avatar on every pre-save failure.

Usage aggregation reads stored observations through the workspace-store contract. It reports observed/estimated/missing counts separately, sums known cost only, and states whether the result is incomplete. Basic chat, process, workflow, and other work are disjoint typed scopes.

CRM/HR access is implemented behind a narrow query contract owned by the CRM/HR module. Results are bounded and redact private contact values, confidential notes, rates, and sensitive-party detail. Returned text is labelled as untrusted business data, not instructions.

Agent-authored catalog/settings text and peer-manager responses are also labelled with typed untrusted-text markers. Tool descriptions and the HR governance instructions require those fields to be treated as attributed data and never as instructions, including when they contain prompt-injection-shaped commands.

Process insight uses the authoritative AgentFramework execution-history and usage-store contracts. Process-originated runs already persist typed agent, process-run, process-step, outcome, error, activity, tool, and usage lineage there. Dependency direction therefore remains:

```text
AgentFramework HR provider -> AgentFramework execution-history/store contracts
Processes runtime          -> persists AgentFramework execution lineage
```

This avoids a new cross-module adapter and the inverse AgentFramework-to-Processes reference. Repeated work is reported as multiple execution attempts for the same persisted process-step ID. Manager outreach never guesses a canonical manager: the tool requires an explicit manager agent ID, verifies that the agent participated in the selected run and has permission to observe other agents, and labels the conversation as a requested review.

Manager review runs use a one-shot execution with runtime tools, workspace tools, context capabilities, and memory disabled. No chat session or transcript is created. The service returns a response only after verifying the exact process lineage, a terminal successful run, no pending approvals, and successful redaction of retained request/response summaries, runtime state, and log messages. Failed or cancelled reviews remain failed even if their best-effort cleanup cannot persist. Agent-package export independently replaces legacy request, response, serialized-session, and execution-log content for this typed lineage. The selected provider's own retention policy remains an external constraint and must not be described as application-controlled confidentiality.

The Agents tab opens only the managed HR identity in an OverlayLib `OverlayWindow`. The existing chat panel gains a strongly typed focused-floating display mode; the full-page mode remains unchanged.

## Responsibility map

| Responsibility | Owner |
| --- | --- |
| Stable privileged identity | AgentFramework Models |
| HR tool contracts, access policy, orchestration, usage aggregation, avatar mutation | AgentFramework module |
| CRM/HR safe search and summary | CRM/HR module |
| Process evidence and participant validation | AgentFramework HR process-review service over persisted execution lineage |
| Approval classification and receipts | Existing AgentFramework tool policy/runtime |
| Floating HR chat presentation | AgentFramework Blazor UI + OverlayLib |

## Rejected alternatives

- Expanding `hr-staffing-manager`: conflates staffing selection with privileged agent administration and changes existing process behavior.
- Gating by workload, tag, name, template key alone, or `CanObserveOtherAgents`: these values are editable or cloneable and are not a privilege boundary.
- Adding HR behavior to a runtime partial class: future tools would continue growing the monolith and isolated tests would still require it.
- Making AgentFramework reference Processes: creates the wrong dependency direction and a cycle.
- Adding a process adapter for evidence already persisted in AgentFramework execution history: adds another boundary without new behavior.
- Returning direct CRM/HR service models: leaks PII and confidential workforce data.
- Treating missing cost as zero: produces a false exact total.
- Heuristically selecting an agent whose name contains `manager`: invents a run-manager relationship that is not persisted.
- Exposing an agent-delete tool: deletion was not requested and has a substantially larger recovery/audit surface. Suspension remains available through settings.

## Test seams and proof

- Provider unit tests prove tools attach only to the managed HR identity and only for interactive chat.
- Administration tests prove allowlisted patch preservation, stale-update rejection, self-update rejection, and no authority/secret mutation.
- Usage tests prove scope separation and known-versus-unknown cost reporting.
- Avatar tests fake image generation and prove invalid MIME, oversized output, and provider failure preserve the old avatar.
- CRM/HR query tests prove bounds and redaction.
- Process-review tests prove attempt/error evidence and explicit participating-manager validation.
- Composition smoke tests prove provider and process adapter registration through contracts.
- Blazor tests prove the HR card action and focused floating-chat mode without changing full-page chat.

The architecture review gate must recheck changed project references, partial-class growth, provider registration, negative tests, and the final dependency graph before closure.
