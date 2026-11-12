# Target boundary map

The maintained [architecture](../../../../docs/architecture/concurrent-mutations/README.md)
and [owner design](../../../../docs/architecture/concurrent-mutations/01-owner-transaction-design.md)
define the semantic boundaries. Proposed concrete types are named here for implementation
planning; they are not asserted to exist.

| Target | Responsibility | Existing responsibility removed/retained |
| --- | --- | --- |
| `SerializableMutationScope` in Infrastructure | Existing physical gate/transaction lifecycle; finally-release repair | Retain all normal acquisition/isolation/key semantics |
| Projects.Contracts definition expectation/command/outcome records | Original content identity, full versus scalar intention, accepted acknowledgement | Keep lifecycle admission separate; no behavior/EF references |
| Top-level `ProjectDefinitionWriter` in Projects | Single atomic definition use case, CAS, bounded children, durable result and search participant | Remove save protocol from ProjectsService; leave thin public delegation |
| Top-level pure definition rules in Projects | Normalization, duplicate/ownership checks, linear child matching and semantic request identity | Remove duplicated loops/validation from facade; do not put product rules in Infrastructure |
| Projects-owned operation record/mapping | Exact accepted outcome and deduplication | Existing creation compensation receipt remains its distinct witness |
| Existing Projects coherent query seam | Definition snapshot and original expectation | No full hierarchy/portfolio read for edit admission |
| Existing Workbench scalar adapter | Translate original scalar intent and authority into Projects command | Stop fabricating empty whole-aggregate lists |
| Existing ProjectEditorDraft and host | Base/live/submitted/current distinction and explicit conflict/unknown recovery | Preserve field revisions, row correlation, generation fences; no new shared mutable state service |
| Workbench planning basis/publication services | Required material facts, membership and proposal publication | No new runtime partial or generic domain-discovery coordinator |
| Existing neutral execution models/Core | Validated policy vocabulary/admission, durable resolved settings/budget | No product queries, EF, MAF SDK dependency introduced |

Composition registers cohesive implementations through existing module registration.
No builder/factory framework is needed for a single stable definition writer. Keep
constructor dependencies concrete where only one local service exists and interfaces
at real cross-owner/persistence/test substitution boundaries. No temporary bridge may
silently attach a current token to old data. Existing wrappers can remain only as thin
typed forwarding methods, with all enabled callers upgraded together.

P01a introduces no new type or reference. The larger definition extraction must be
reviewed against its actual diff and prove removed behavior from the existing partial
cluster before downstream work closes.
