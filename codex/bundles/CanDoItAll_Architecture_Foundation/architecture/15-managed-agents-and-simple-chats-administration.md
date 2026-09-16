# 15. Managed agents and Simple Chats administration

## Current observed surface

The targeted audit at 504c47d8f3fa085085b3fdbf955b69b9fdfb8856 found:

| Area | Observation | Consequence |
|---|---|---|
| HR agent | Actual typed search/settings/options, draft create, settings update, avatar, usage, process history/review, safe CRM search/detail, CRM party creation, affiliation list/upsert. | Preserve existing owner-backed paths; do not call CRM contact creation wholly missing. |
| HR authority | Managed identity/purpose gates; CRM scope additionally checked; invocation authorization repeated. | Do not expose the same privileged pack to every agent or background run. |
| Scheduler agent | Interactive workflow target/schedule search and workflow schedule creation; descriptor excludes process scheduling pending refactoring. | Preserve that exact supported surface; process-scheduling support is separate. |
| Simple Chats | Owner definition API already offers create/update/status/get/paging; change commands carry expected concurrency token. | Reuse it; new HR adapter must not duplicate the product. |
| HR -> Simple Chats | Not present in the completely read HR provider; user explicitly requests the missing capability. | Required new integration, not proof the product API is missing. |
| Processes | General direct Process provider is not documented; UI/HTTP/governed/Structure bridges exist. | A process-related agent or template alone does not prove attached process tools. |

Sources: SRC-028 through SRC-035. None of these observations is an executed authorization or integration test. Complete tool inventories must be discovered from current code/host; this table is a targeted baseline, not a forever-fixed count.

## Ownership of managed presets

Agents owns the technical preset/definition and identity. The target module owns its business operations, schemas, and policy. The preset/host configuration selects an approved tool pack. Technical name, localized label, or system prompt “I am HR” is not authority.

Preserve protected-capability rules and the existing HR administration allowlist. Creation stays draft where current policy requires it. Agent configuration, provider selection, capability assignment, project/file access, external-call rules, and activation are different risk levels. Never grant secret values or protected capabilities through a generic settings patch. Verify complete current self/protected-target guards before changing them; the service audit was targeted, not exhaustive [SRC-030].

A successful agent save with CRM/team synchronization failure returns committed technical identity plus pending/warning effects. Do not create another agent on retry. CRM enrichment remains local and must survive technical refresh.

## HR and optional CRM-specialist scope

HR may use safe agent administration, independently authorized Simple Chat definition management, and explicitly granted CRM party/affiliation/staffing operations. A CRM specialist may focus on CRM without acquiring technical agent administration. Both call CRM's canonical services. Selection of a future specialist is a product decision; the architecture supports either without new ownership.

For staffing edits, field allowlists distinguish organizational role, skills, availability, capacity, confidential HR records, and task assignments. Work Management still owns task assignment; Providers still owns AI prices. Broad CON-011 does not authorize modifying every CRM entity or field. Existing affiliation editing preserves restricted HR fields [SRC-029].

## Simple Chats management scope

**Allowed target operations:** bounded definition search, authorized editable settings retrieval, create definition, update definition with expected token, and change status under owner policy. Fields can include name, summary, avatar reference, system prompt, provider/model reference, bounded model settings, timeout/response format, tags, and revision reason where currently supported [SRC-034]. Prefer safe DTOs over returning entire product entities to the model.

**Not implied:** enumerate/read/write conversations or transcripts, inject messages, start ordinary turns, delete retention history, alter deployments/channels, reveal provider secrets, attach tools/skills/MCP, add Memory, or silently inject Project Structure context. Definition administration is a control-plane action performed *on* a non-agent product, not permission *for that product* to execute tools.

Use a separate external adapter depending on the published Simple Chats API and neutral tooling. Core product/persistence must remain free of Agents/MAF/runtime/UI/domain dependencies prohibited by SRC-033. UI selection is not needed; exact definition ID/scope is explicit. Missing provider or disallowed settings is owner validation, not an automatic replacement.

## Revision and running-operation behavior

Retrieve an authorized current definition/token, propose an allowlisted change, bind approval to exact intent and expected revision, then invoke owner update/status. A concurrent user update produces conflict; do not silently reread and overwrite with a full stale form. Preserve revision history and the owner-defined pinning semantics for already admitted turns. Do not mutate historical messages or rerun a conversation to “apply” a definition change.

Archive/status effects on new admissions and existing turns must use the actual product policy and tests; do not infer cancellation from status change. A cached definition-settings response must not leak system prompts to an actor who only has summary search.

## Retry-safe creation is new work to prove

CreateLlmChatDefinitionCommand in the audited API has no OperationId [SRC-034]. That is not proof of a safe retry contract. A new machine-create adapter must add/reuse an owner-controlled atomic dedupe/receipt boundary, or a proven shared transaction enclosing the owner effect. Recording a receipt afterward leaves a crash gap and is not enough. Reusing the name as a dedupe key is also invalid because names need not be unique or immutable.

Until that boundary is proven, expose uncertainty/reconciliation rather than automatically retry a possibly successful create. This requirement concerns definition administration; it does not secretly change the separately documented non-idempotent HTTP conversation-create API or its future deployment namespace [SRC-033].

## Scheduler and curator packs

Scheduler owner validates exact target/version, schema, cron/timezone/misfire, and plan lifecycle. Future automation update/disable/delete needs separate policy and concurrency; current managed tool only establishes creation. Each firing revalidates target execution under an explicit durable principal, not a browser session or stale HR identity. Recurring approval is either an explicitly approved bounded plan policy or per-effect approval, never inferred unlimited consent.

Prompts curator uses Prompts commands, workflow curator uses Workflows definition commands, and capability curator uses Agents catalog policy. None obtains arbitrary run-store access. Capability curation does not itself grant execution, and editing a referenced workflow/prompt cannot change the meaning of an already-approved action without revalidation.
