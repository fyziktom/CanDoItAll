# Semantic preservation and command coverage

Treat distributed templates as immutable inputs. A complete authored document is not the
sum of visible form fields. Use a rich fixture with non-default values in every supported
semantic family, then serialize/reload and execute each patch. Assert that only the intended
owned fields and deliberate dependent fields changed. Make newly supported semantic fields
visible in the coverage tests so later model expansion cannot silently discard them.
Unsupported/future import schema must be explicitly rejected or handled by an intentional
lossless extension policy, never silently dropped by deserialization.

## Required preservation map

| Family | Preserve or deliberately update |
| --- | --- |
| Definition identity and provenance | Key/stable definition identity, base pack/version/content, names/summaries/owner/customer/value, change history |
| Governance | Operating/autonomy modes, policy/constitution summaries, interface/approval requirements, simulation metadata; existing runtime enforcement remains authoritative |
| Role semantics | Resource defaults, template provenance, staffing/executor preferences, workflow binding, required/fallback/approval flags, allocation and notes |
| Step identity and scheduling | Stable key/order/kind, all dependency forms and target references, lead time and execution class |
| Decision rights | Actual edited step's DecisionRoleKey, approval/decision/manual-skip/refusal flags, completion-with-open-issues discipline |
| Execution contract | ExecutionContract, CompletionPolicy, CapabilityScope, allowed operations, target scope and required tool/host contracts |
| Execution guidance | ExecutionGuidanceRefs plus immutable resolved content/hash/provenance; ResolvedExecutionGuidance is JsonIgnore in the current template model |
| Driver activation | LaunchDriverActivations, settings and typed artifact bindings, including a deliberately resolved empty activation list |
| Assignment | RoleAssignments, responsibility/fallback/rebind semantics, ExecutorPreferredSpecializationTags and workflow version bindings |
| Subprocess | Typed SubprocessContract, child definition reference, output/no-go/skip mappings, forwarded context contracts and pinned executable dependencies |
| Branches | Outcome identity, route/target/artifact, loop budget/fingerprint/escalation and open-issues semantics |
| Artifacts | Expectations, payload/schema/trust/sensitivity/retention, workflow/child bindings, ArtifactInputs and producer/consumer relations |
| Authored layout | Step/role/branch/reference positions and supported node/edge semantics; no accidental recompose of unrelated nodes |
| Import | Materialized canonical content, source identity/hash, explicit target and stable collision remapping, not only a display record |
| View-only state | Keep zoom/pan, editor selection, raw invalid text, dialog lifetime and request generations local and out of native documents |

The current kernel fingerprints serialized definition content plus resolved guidance hashes.
A JSON-only copy of the current template class drops resolved guidance. Pin its materialized
content/resources or use an equivalently immutable resource envelope; do not repair the hash
by excluding guidance. Test unchanged default identity/hash/plan compatibility before and
after introducing the resolver. Do not rewrite the distributed pack or use mutable current
files as the authoritative content of an old publication.

## Existing command families

**Definition:** SaveDraft, Publish, Archive, Delete. Preserve validation versus publish
validation distinctions: incomplete drafts may remain editable where currently supported;
publication must validate executable relationships. Default templates cannot be edited in
place. Delete's reviewed meaning restores inherited/template availability after deleting the
local authored entry; do not silently reinterpret it as permanent global template deletion.
Retain immutable history needed by earlier publications/preparations/runs.

**Roles:** enumerate current command enum and enabled actions, including Add, Save, Delete
and template application. Preserve owner-selected identity after Add/Delete and do not hijack
a selection changed while awaiting a result. Deleting a referenced role must refuse with an
explicit repair route or apply an intentional all-reference update transaction; never leave
DecisionRoleKey/RoleAssignments dangling. Adapt unrealistic tests using an unreferenced role
or explicit unbinding, not by weakening canonical integrity or dropping last-role coverage.

**Steps:** patch the actual selected step and every supported editable section. Keep hidden
fields above unless the command explicitly owns them. Ensure a change visible in a form is
also visible in the canvas and compiler. A role/step key rewrite must update all references
atomically or be rejected; no index-based pairing.

**Canvas:** current commands are MoveNodes, AddStep, AddBranchRouter, AddRoleBinding,
AddArtifactExpectation, AddSubprocessBoundary, CloneArtifactReference, CloneRoleReference
and Recompose. Inspect their current partial implementations and toolbox defaults before
mapping them. Semantic actions update the shared canonical structure. Reference-node clones
and placements do not arbitrarily duplicate semantic roles/artifacts; retain distinct authored
layout/reference identities where the original action means a second visual reference.
Recompose preserves execution meaning and unrelated user edits. Coalesce drag completion,
not intentful add/import actions; don't commit on every pointer frame or discard final geometry.

**Templates/import:** ImportProcess, ImportRole and ImportArtifact must materialize what their
UI promises. Preserve target definition identity/scope. Default process import is a non-
destructive merge of the selected source structure using stable remapping of colliding
role/step/artifact/branch references; never silently replace the entire target draft. Role
import clones effective role meaning; artifact import attaches to the captured target step.
Record source/target/remap and the actual result. Current source UI saying 'imported into'
is not authorization to erase the target. If a present, explicit replace mode already exists
on a newer checkout, preserve its separately confirmed semantics instead of inventing one.

Replaying the same operation must not allocate again. A new explicit import is a new intent;
handle collision/no-op policy deliberately and visibly, not a deduplication rule that loses
same-source imports into different steps. Resolve all imported links/resources or reject the
whole command with diagnostics. Do not keep a successful import count for absent content.

**Catalog/defaults:** FeedDefaults stays repeatable and never overwrites authored rows. Query
search/scope/selection/counts must reflect committed state, draft plus published state and
archived visibility accurately. Unloaded/unavailable data is not a zero count. Keep bounded
queries and legitimate globally inherited defaults visible under the existing scope rules.
