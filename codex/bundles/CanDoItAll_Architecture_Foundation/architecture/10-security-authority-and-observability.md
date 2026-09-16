# 10. Authority, security, and operational traceability

## Trust boundary

User, model, plugin, and external client supply intent. Trusted transport/host authenticates identity/scope; owner checks resource-specific authorization, lifecycle, and revision. Agent capabilities/purpose and HTTP authorization are different gates [SRC-028]. Tool-list visibility is not the security boundary. Resource authorization must evaluate the actual target/operation [EXT-007].

Files, prompts, notes, retrieved Memory, and workflow input are untrusted business data. They cannot change owner, root, grants, or execution purpose. A model's request to skip validation is not a control instruction. Automation uses admitted authority as an upper bound and current policy as a further restriction.

## Continuation and delegation

Generic source-authority interfaces are implemented by domain-owned conditions [SRC-009]. Restoration validates scope/profile/incarnation/revision/current privileges. Missing or malformed governed context fails closed, not as an ungoverned fallback. Child workflow/process receives no wider rights and has an explicit purpose. Repeat checks before mutation, secret resolution, content use, and approval continuation after role/plugin/source changes.

Approval binds exact action, normalized payload, target revisions, and deciding identity. Changed material payload invalidates approval. Confirmation cannot bypass filesystem provenance, schema, target existence, or current scope. Preserve serial calls and barriers. Administrative agent editing cannot self-escalate, grant protected capabilities, or replace its current execution authority; technical identity remains Agents-owned.

## Least data and disclosure

Picklists omit confidential Party notes, provider secrets, and unrelated agent instructions. Different purposes receive different DTOs, not one enormous DetailDto. Apply access partitions, field purpose, and destination audience. Permission to read into UI is not necessarily permission to disclose to the selected LLM, attachment, export, or wider project/thread; evaluate both source and destination policy.

Secrets remain opaque references or short-lived transport-only values. Never cache/publish/report resolved values. Sanitize exceptions, credential URLs, connection strings, PII, and attachments before logging. Synthetic fixtures use no live credentials. Approval text must show a safe meaningful action, not sensitive complete internals.

## Identity and observability

Keep OperationId, RunId, InvocationId, ContributionId, ParentRunId, step/iteration, binding, correlation, and causation distinct. Receipts allow owner status lookup without foreign raw-table access. Operator views expose pending delivery, blocked target, stale projection, retry/dead-letter/reconciliation, quota, invalid scope, and safe retry class. Log safe identifiers/fingerprints rather than all content.

Validation/Denied/Unsupported/Conflict are not automatically retried with a different target. Bounded transient retries preserve stable identity when safe. Unknown commits are looked up; uncertain remote effects reconcile first. Poison messages are traceable rather than silently dropping required effects or blocking a stream forever.

Operator repair is a bounded audited command with reason, not universal force=true. Quarantine/NeedsReconciliation is a legitimate safety outcome. Receipt replay also rechecks access and cannot become an information oracle.

## Hosts

Label test doubles as fake-only. Missing production security/writer/executor means invalid configuration or explicit unavailability, not empty success. Optional readers may be absent only with truthful capabilities/health. Preserve currently supported PC/large-screen and headless/desktop scope; no incidental mobile redesign or new security platform. Reverify supported OS/storage behavior when an affected boundary changes.
