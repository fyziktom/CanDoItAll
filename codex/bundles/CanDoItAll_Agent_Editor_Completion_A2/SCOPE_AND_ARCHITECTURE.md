# A2 scope and architecture

## Target: the complete technical editor, not all AgentFramework

A1 already owns shell, Identity, Runtime, Images and Voice. A2 moves the other six sections and
small editor confirmations. At closure each of the ten sections has real leaf markup, a native
host binding, deterministic scenario coverage and tests. No second full-agent draft or per-tab Save.

| Section | Rendering to complete | Remains with its actual owner |
|---|---|---|
| Memory | CRM source switch, invocation mode, required/automatic settings, provider eligibility display, binding add/remove/order/requirement and validation | Profile/driver reads, runtime Memory policy, canonical whole-agent write |
| Project Structure Access | Granular flags, lazy list, select/clear, stored/missing selections and load/retry states | Project reference query, acquired lifetimes, admission at save/runtime |
| Workspace Tools | Profiles/flags, risk guidance, external-root entry/list, Storage permissions and actual picker composition | Host path registry, protected bindings, risk confirmation policy, real Storage query/access |
| Secrets | Available/saved/missing reference metadata and selection | Picker metadata query; secret values never loaded here |
| Process Access | Existing read/write/AllowAll state and preserved stored IDs | Existing unavailable definition-picker behavior; no new Process capability |
| Capabilities | Summary/filter/list/assignment/Verify states, new tool/MCP/skill launch controls and exact result stages | Capability definition wizard, verification publication, actual assignment/save owner |

Small confirmation rendering includes `AgentDeleteConfirmationDialog`,
`AgentAutoApprovalConfirmationDialog` and `AgentWorkspaceRiskConfirmationDialog`. Keep actual typed
results and acknowledgement requirements. Origin/version/policy lives with the opener; generic
Dialog internals are not part of this extraction.

## Prefer reuse over project multiplication

Extend existing Editor.UI and its sandbox for cohesive editor sections. A separate leaf is allowed
when necessary to prevent an evaluated dependency inversion or share a genuine reusable family.
Explain that decision using concrete references/callers, not symmetry. No new .UI project quota.

A2 can use neutral records plus typed intents, a cohesive view contract or a blend at real sub-
boundaries. Read ports return limited immutable catalog projections. Production host/adapter calls
remain in-process. No HTTP-only redesign. Existing HTTP control planes elsewhere are unchanged.

## Current source responsibilities

R03/R04: `AgentDetailsDialog` is still the complete effect host. Its current large code-behind is
not proof of failure by itself: it owns commands, reads, confirmations, events and policies.
After moving six markup blocks, extract coherent state/effect responsibilities only where needed;
do not split the same coupled implementation into cosmetic partial files.
R06/R07: Memory helper holds both policy and application store/driver reads; those need a real
boundary, not just moving the Razor sibling. R08/R09: root selector holds a host registry and
protected bindings; the renderer must not inherit that infrastructure dependency.

## Reused and intentionally retained integrations

The existing `AgentCapabilityList` and `StorageCatalogSelectionField` are already extracted real
renderers. Use them. Their owner registration is supplied by production or fixture composition.
The overall editor renderer must not become a new owner of the capability catalog or Storage.

The full capability-definition wizard, Avatar provider-backed generation and shared-provider sync
remain explicit production integrations. A2 verifies their launch/result lifetime; it does not lift
their entire authoring/runtime families. The final census names exact retained component/owner
paths and callers. A vague 'deferred content' slot containing all six sections is NOT acceptable.

## Assets and compatibility

Move corresponding scoped CSS, Tailwind sources and component registration needs. Preserve public
component entry points used by catalog, route, embedded and template callers; use a small real host
or deliberate compatibility forwarder as needed. Existing section token maps and enums retain
semantics. No persisted JSON/enum renames or route rewrites. Component identity/context and invalid
raw input must survive ordinary section changes; preserve the old lazy read behavior intentionally.
