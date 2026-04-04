
# Feature-block architecture review — CRM/HR wave

## Scoped block

The reviewed block is the CRM/HR wave as it now touches:

- party directory / staffing / AI-agent identity
- project party assignment bridge
- workbench participant / meeting / work-item party integration
- cross-module responsible-party surfaces

## What the block added well

The block added real value:

- a real party directory and assignment model
- project-level portfolio context enrichment
- meeting/work-item/participant party flows
- positive integration/component tests for the new UI paths
- a path toward real people, partners, and AI agents inside the project graph

That is strategically strong and aligned with the product story.

## Block classification

| Area | Classification | Comment |
| --- | --- | --- |
| Party directory identity | Canonical entity | A strong new source of actor/party identity. |
| ProjectPartyAssignment | Canonical-ish relation but under-scoped | Good direction, but scope integrity and ownership are not finished. |
| Party integration page flows | UI workflow / boundary layer | Currently writes both metadata and assignment rows. |
| Participant / meeting / work-item metadata | Typed node payload | Useful typed context, but currently stores duplicated party truth. |
| Project party bridge contracts | Cross-module seam | Good seam; needs stronger scope semantics than raw NodeKey. |
| Cross-module responsible-party fields | Module-native canon | Still authoritative in their own modules unless/until migrated. |

## Main boundary violations introduced or amplified

### 1. UI writes duplicated truth

`ProjectStructurePage.PartyIntegration.cs`:

- reads current metadata-linked party IDs,
- rewrites metadata,
- deletes and recreates assignment rows.

That means the UI is acting as a **truth synchronizer**, not merely as an editor.

### 2. Assignment scope is too soft

`ProjectPartyAssignmentUpsertRequest.NodeKey` and `ProjectPartyAssignment.NodeKey` are plain strings.

`CrmHrServices.SaveAssignmentAsync` validates:

- project exists
- party exists

but not:

- node exists
- node belongs to the same project
- node kind allows the requested role

### 3. Node semantics are still owned partly by the UI

Participant/work-item subtype semantics still live heavily in `ProjectStructureCanvasCatalog.RichDefinitions.cs`.

That means a UI authoring catalog still decides part of the meaning of the canonical graph.

### 4. The block does not yet support the real lifecycle

The actual user workflow is:

- quick note
- refine later
- maybe assign later
- maybe promote to task / decision / other typed block

The current block lands on top of a lifecycle that still only supports note→block and block→block mutation.

## What must stabilize before the next wave

- one canonical owner for node-scoped actor truth
- explicit node kind registry and transition policy
- node-scoped assignment integrity checks
- transition model for note→task/decision evolution
- removal of projection-as-truth read patterns before more overlays land

## Recommended ADRs triggered by this block

- ADR-001 node remains canonical carrier
- ADR-003 node kind registry owns semantics
- ADR-005 canonical actor-assignment ownership matrix
- ADR-007 stable node identity with typed facet history

## Timing recommendation

### Must stabilize now

- ACR-005
- ACR-011
- ACR-013

### Before the next feature wave

- ACR-003
- ACR-004
- ACR-012
- ACR-014

### Can wait until later, but not forever

- ACR-002
- ACR-007
- ACR-008
- ACR-009
- ACR-010
- ACR-015
