# Agent configuration concurrency

Agent configuration, Team replacement and capability editing use their native owners.
Existing Agents and complete Teams require the original `ExpectedUpdatedAtUtc`;
capabilities require the original `ExpectedFingerprint`. A missing original cannot turn
an update into a create. Deletion uses the same original observation. Team metadata has
its narrower original metadata expectation and preserves independently changed members.
Membership replacement requires the original Team version; adding a member is a separate
intent guarded by the Team lifetime.

The main editors keep their original actor and dataset through protected reads and native
publication. Metadata hints invalidate review without loading complete configuration.
Explicit original/local/current comparison changes the local draft; Save is separate.
An acknowledgment uses submitted/local/accepted values and preserves input received while
the command was in flight. Source equality does not establish an earlier unknown outcome.

The file owner records its accepted catalog at primary publication. A later observer or
index failure carries that catalog in `SandboxWorkspaceCatalogCommittedException`.
Configuration writers select the result from that exact catalog and expose
`AgentCatalogCommittedException<T>`, without reading a later peer version. Agent directory
projection is still attempted and preserves the accepted definition if a secondary update
also fails. Team and capability editors retain their draft and exact accepted original
and show a saved warning. Conditional Team/capability deletion preserves its committed
identity and reconciles through reads. The separate durable Agent workspace deletion
journal keeps its existing ownership and recovery protocol.

HTTP configuration saves retain their ordinary successful identifier or membership
payload after a known catalog commit. The response includes
`CDA-Agent-Catalog-Outcome: committed-reconciliation-pending` when a secondary update needs
attention. This includes conditional Team/capability deletion. Read current state to
reconcile; do not repeat the command because of the warning. Unknown I/O failures keep
their separate recovery path and do not receive this committed header.

Clone and template creation copy the current source under the catalog's coordinated
owner into a new identity. They preserve the source configuration and history. The
accepted new identity survives secondary publication or directory warnings and uses the
same HTTP committed header. These copy intents do not replace the source editor's draft.

Catalogs whose native reads materialize complete definitions use targeted hints and
visible explicit refresh. This deferred list policy does not weaken conditional writes
or replace the original of an open editor. Runtime execution, append-only conversation
history and external setup retain their own command and recovery protocols.
