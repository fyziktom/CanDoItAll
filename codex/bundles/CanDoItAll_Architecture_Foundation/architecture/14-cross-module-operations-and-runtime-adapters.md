# 14. Cross-module operations and runtime adapters

## The missing general rule

Agents, workflows, and processes may **read from and request mutations in every appropriate domain through that domain's canonical application boundary**. Project Structure contributions are a specialized case, not a central bus through which CRM contacts, Simple Chats definitions, or schedules must pass.

An HR agent is a managed caller. It is not the owner of CRM tables, agent definitions, or chat definitions merely because it administers them. The same applies to a CRM specialist, scheduler assistant, process designer, or workflow step. All can be legitimate callers without becoming competing writers.

A module operation has a typed semantic action, owning domain, query/command classification, bounded input/result, identity/revision rules, actual authorization, and truthful outcome. Reuse existing services where these rules can be preserved. This does **not** require a universal ModuleOperation class or a new gateway package.

## Four distinct surfaces

| Surface | Trust and invocation route | Important limitation |
|---|---|---|
| UI / HTTP | Transport/session authentication -> application adapter -> owner operation | A route being reachable does not grant agent access. |
| Interactive agent tool | Registered provider -> purpose/capability policy -> invocation authorization/approval -> typed owner adapter | A capability template, preset name, or HTTP endpoint is not an attached tool. |
| Workflow executor | Registered executor/schema -> admitted run authority -> owner operation -> durable step result | Does not impersonate interactive HR or simulate model tool calls. |
| Process step | Registered step/execution adapter -> process authority -> owner operation -> process checkpoint | Does not mutate a foreign DbContext or require a Razor page. |

A first-party operation may be available on one surface and absent on another. Availability requires the real handler, host registration, compatible version/purpose, current actor grants, owner scope, and any approval mechanism. The runtime-surface catalog records observations separately from targets [SRC-028].

## Reuse existing extension machinery

The repository already has IAgentRuntimeToolProvider and RuntimeToolProviderComposer [SRC-028]. Prefer product-owned providers/adapters using this mechanism. Keep generic composition, invocation, serialization, and metadata contracts neutral. The destination owner determines command meaning and policy; runtime does not import all modules' business enums into Core.

A possible dependency layout is:

```text
Crm.Application              -> Crm.Contracts + CRM persistence ports
Crm.AgentTools               -> Crm.Contracts + neutral Tooling
Workflow.CrmOperations       -> Workflow executor contracts + Crm.Contracts
Processes.CrmOperations      -> Process step contracts + Crm.Contracts
Hr.SimpleChatsAdministration -> SimpleChats published application API + Tooling
Composition                  -> selected concrete adapters
```

Do not add references from Simple Chats product/persistence to Agents Core, MAF, Tools, or Processes. Do not add reciprocal references from CRM application to concrete workflow/process executors. Integration code may know both sets of contracts; each product application remains independent of the other's implementation.

A small shared admission/receipt utility may reduce duplication, but cannot decide all domain authorization, define arbitrary CRUD, or hold an all-module DbContext. Handler resolution is infrastructure; the permitted typed action is still fixed before dispatch.

## Effective authority

The trusted adapter resolves initiating principal, actual executing actor, delegation chain, installed purpose, database profile/history, project/domain scope, and relevant policy. A user ID/agent ID/run ID in model input is a claim, never a grant. Anonymous/unverifiable background calls do not inherit ambient admin or HttpContext state.

Effective rights are bounded by the initiating/delegating principal, run admission ceiling, current policy, explicit operation grants, destination scope, provider-disclosure policy, and host capability. Newly added grants do not silently widen an already-admitted run. Revocation may narrow it. Destination owner validates the resource and field-level operation at use.

Managed HR/Scheduler providers are currently InteractiveChat-only and identity-bound [SRC-029, SRC-031]. Preserve that behavior during extraction. Supporting a CRM query for an ordinary task agent or a workflow is a separate authorized adapter/grant path, not weakening HR identity checks or broadening its supported purposes by default. A deterministic step uses an explicit run/service identity with bounded delegation.

Approval requirements travel with the operation's policy, not the existence of a browser. If headless execution needs approval and no valid continuation channel exists, return blocked/approval-required or reject admission. Do not silently autoapprove. Human approval can approve exact bounded intent but cannot confer rights the approver/delegator lacks.

## Read/write separation and allowed capabilities

Use separate grants for safe search, confidential details, create, update, archive/delete, execution, and administration. CRM lookup does not imply contact creation; contact creation does not imply workforce/confidential fields. Reading agent options does not imply granting privileged tools. Listing schedules does not expose stored input JSON or grant target execution.

Capabilities are not all-or-nothing module access. A target policy pack describes the allowlisted operation/fields and supported actor purposes. The default is no new grant. Future optional CRM-specialist identity reuses the same owner APIs with its own approved capability set; no new CRM master store.

## Definition administration versus execution

Changing an agent/workflow/chat definition creates a new validated revision for the owner's future-use rules. It is not a rewrite of admitted run instructions, checkpoints, transcript, or pricing history. Runtime config changes affecting safety require an explicitly defined cancellation/revalidation strategy. In particular, an actor cannot edit its definition or a child capability to acquire authority unavailable to its current run.

## Result adaptation

Preserve typed owner validation, conflict, authorization, availability, committed-with-warning, and unknown outcomes. Tool output must not flatten them into a plausible success sentence. Unknown commit triggers status lookup with the same intent; a retry must not create a second contact/chat/schedule. Stable error codes and safe summaries should survive HTTP and tool serialization without raw exceptions, secrets, or confidential content.

## Rollout boundary

The operation matrix is an architectural requirement inventory, **not a permission manifest or implementation task list**. Current supported paths must be retained. Missing required paths receive separately scoped implementation work. Optional future APIs are not refactoring release gates until included in the chosen scope. Required security/receipt guarantees must be implemented before a new mutation is advertised as safe.
