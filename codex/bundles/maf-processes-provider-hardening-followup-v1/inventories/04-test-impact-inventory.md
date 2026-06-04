# Test Impact Inventory

| Test area | Why impacted | Required proof |
| --- | --- | --- |
| Runtime provider composition | Provider metadata and product migrations change tool attachment flow | Unit tests for ordering, duplicate names, provider failure, no-provider behavior. |
| Tool invocation policy | Approval wrapping must stay correct | Policy/capability registry tests for process/project/image tools. |
| Process provider access | Process provider split and purpose hardening can weaken access checks | Read/write/definition-scope denial tests. |
| Project-structure tools | Migration out of MAF can drop tools | Exact tool inventory before/after and integration test. |
| Image-generation tools | Migration out of MAF can drop approval/access behavior | Exact inventory before/after and policy test. |
| Process evidence semantics | Provider refactors can affect receipts/artifact lineage indirectly | Process outbox, receipt semantics, artifact-lineage smoke. |
| Architecture guards | Decoupling can regress silently | Source/project reference scans and static architecture tests. |
| Full solution build | Cross-project references change | `dotnet build CanDoItAll.slnx`. |
