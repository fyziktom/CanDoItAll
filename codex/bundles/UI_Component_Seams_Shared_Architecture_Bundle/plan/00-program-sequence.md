# Program sequence and independent downstream work

## Dependency flow

~~~mermaid
flowchart TD
    Base[Shared decisions and behavior classification]
    Seams[One coherent in-place seam]
    Freeze[Frozen boundary and consumer checkpoint]
    Extract[Small physical UI extraction]
    Sandbox[Browser sandbox and measured iteration]
    Navigation[Feature location binding and history]
    Second[Different UI archetype]
    Patterns[Review shared boundary patterns]
    Rollout[Further module capability slices]
    Tooling[Direct watch tuning then Manager integration]
    Docs[Durable documentation and branch closure]

    Base --> Seams --> Freeze
    Freeze --> Extract --> Sandbox
    Freeze --> Navigation
    Freeze --> Second
    Sandbox --> Patterns
    Second --> Patterns
    Navigation -. navigation evidence when available .-> Patterns
    Patterns --> Rollout
    Sandbox --> Tooling
    Rollout --> Docs
    Tooling --> Docs
~~~

There is deliberately no Navigation -> Extract/Sandbox prerequisite. A second archetype can
begin independently. Navigation supplies evidence as it becomes available; it does not block
shared boundary review or further in-place module work. Validate navigation-specific rules
when their own implementation exists.

## First delivery path

1. Revise and characterize the Agents plan; no implementation in this revision.
2. Extract catalog/workspace and required editor seams in place through the Agents child.
3. At its first accepted catalog checkpoint, identify the exact small extraction candidate.
4. Schedule a catalog UI extraction + sandbox child as the next delivery after the relevant
   source is frozen, ordinarily after current Agents closure. Do not put provider refactoring,
   full bookmarkability, or application-wide untangling ahead of that first sandbox.
5. Bind the first navigation slice against the same semantic state and explicit host lifetime.
6. Validate the approach on another UI archetype before broad standardization.

An earlier sandbox delivery from SB03 is possible only through an explicit coordinated
scope handoff/frozen branch. Moving files concurrently with SB04–SB07 on the same checkout
would invalidate their references and proof. No such parallel implementation is authorized here.

## Broader rollout

| Candidate | Intended learning / boundary |
|---|---|
| Agents catalog/editor | Controlled selection, mutable editor session, cross-module pickers |
| Provider administration/history | Applied queries, partial data, independent read regions |
| Small Resources or Collaboration slice | Check that shared state/host rules work outside Agents |
| ProcessWorkspaceShell | Remove service location through explicit operational capabilities |
| ProjectFilesDialog | File/browser/native effects, session and cancellation |
| ProjectStructure | Capability-by-capability extraction of a long-lived workspace |
| Remaining CRM/HR, Workspace, Scheduler, Test Lab | Own state/behavior matrix per cluster |

The candidate register is not a complete application audit. Each child refreshes its
scope and all affected consumers. Do not impose an identical controller or project layout.

## Size, rollback, and coordination

Use coherent behavior boundaries. Separate logical ownership, physical movement, routing,
visual redesign, and defect fixes unless an explicit dependency requires combining them.
Every slice has a reversible source checkpoint, operation evidence, and revalidation keys.
A Git rollback is not a rollback of committed user data or external side effects.

The old independent development-test-repair preparation hold is superseded by the current
observed Agents child. Baseline freshness remains mandatory, without restarting historical work.

## Documentation

Transfer proven durable rules before removal of temporary bundles. Record active consumers,
approved version compatibility, and the destination of each rule. Cleanup/merge/remote writes
are separately authorized tasks, not effects of this reference.
