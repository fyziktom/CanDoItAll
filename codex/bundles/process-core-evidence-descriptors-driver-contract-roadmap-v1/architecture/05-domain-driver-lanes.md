# Domain Driver Lane Map

## .NET / Rust verification lane
Allowed:
- Inspect existing build/test transcripts.
- Inspect declared project files and artifact metadata already produced by process steps.
- Return readonly diagnostics and suggested next proof.

Denied:
- Running `dotnet`, `cargo`, shell, PowerShell, package restore, publish, file writes, git mutations, process transitions.

## Office lane
Allowed:
- Inspect process-provided email/document summary facts and artifact metadata.
- Return readonly checklist/summary diagnostics.

Denied:
- Calling Graph/Office APIs, sending mail, tagging messages, creating tasks, mutating documents.

## Business-analysis lane
Allowed:
- Inspect deliverables, requirements snapshots, checklist facts and evidence links.
- Return gap analysis and traceability diagnostics.

Denied:
- Mutating CRM/project/business records, changing process state, writing artifacts, escalating to execution.

## Runtime verification lane
Allowed:
- Inspect Core evidence descriptors, proof summaries and process-owned snapshots.

Denied:
- AgentFramework execution, provider repair, retry scheduling, finalizer application, claim renewal, transition mutation.
