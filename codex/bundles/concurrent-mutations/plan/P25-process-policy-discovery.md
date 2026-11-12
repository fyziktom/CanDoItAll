# P25: discover supported Process planning contracts

Status: qualified, 2026-10-23. [Validation](../validation/2026-10-23-process-policy-discovery.md)
records 56 distinct backend cases across a verified test/baseline repair, 103 SPA tests,
full portability, source/output identities and resource closure. Prerequisites are signed P23 (`2e130acbf`) and P24
(`c067f766a`); their bounded proof remains trusted. Entry gate: Pass for the
Behavioral tier below. This does not close Process launch UI or whole-engine scope.

## Outcome and ownership

An operator client must discover the exact registered planning contract and its
qualified read/change/publication choices before requesting a launch. Hard-coding a
Workbench GUID in either UI would couple presentation to one business implementation.
The existing SPA project Processes page is a placeholder; the native shell has a
durable launch flow, but its renderer cannot reference Runtime through Projections.
Do not introduce that reference or move serialized Runtime types for presentation.

The existing Application admission-provider boundary owns immutable descriptive
metadata: exact execution contract, operator name/description and complete supported
policy combinations with their maximum budgets. Workbench supplies its own descriptor
using the same owner ceiling as preparation. Its resolved policy must match a described
combination and remain within its ceiling. Metadata is not an authorization grant,
Workspace default, saved review or assertion that a selected definition is eligible.

The Application coordinator snapshots and validates descriptors at construction and
returns a deterministic read-only catalog. Duplicate execution identities, empty or
invalid metadata and duplicate policies fail explicitly. HTTP maps the existing
Processes read permission to this catalog; it never runs preparation or loads settings,
Agents, Projects or history. Actual launch/check retains all current admission checks.
No new project reference, runtime DI registration/lifetime, migration, partial class or generic
catalog framework is needed. Existing provider-boundary metadata is sufficient.

The first actual OpenAPI export exposed an array without `items` for the collection of
custom-serialized policies. A Web-only schema transformer supplies that property's item
schema through the existing complete policy schema owner; it does not duplicate the
policy serializer or add a Runtime dependency. Its registration is schema customization,
not a new runtime service. The failed export and HTTP check are retained before repair.
The permission fixture also now distinguishes a restricted unrelated grant from the
existing broad legacy `api` grant, whose access is preserved.

## Cost and compatibility

Catalog construction and reads are bounded by registered implementations and supported
choices, with no external IO and no held transaction. Snapshot mutable provider lists
so a later mutation cannot alter a client-visible contract. Sort by exact identity, not
localized display text. This catalog is advisory and may change after deployment;
prepared launches still replay their saved original contract and policy. Do not add
catalog metadata to persisted preparations, fingerprints or the runtime dispatch path.
Null selection and legacy launch behavior remain unchanged.

Expose the real host OpenAPI response, regenerate the SPA SDK using its installed
generator, and test serialization against the export. SPA remains exclusively on
`codex/concurrent-mutations`. A later UI slice will explicitly retain the original
request before sending, show the effective saved review, and accept/retry that exact
intent. Native renderer work additionally needs the mandatory Components MCP, whose
tools are currently unavailable; this backend slice does not modify native UI.

## Proof and acceptance

- Direct coordinator tests prove deterministic independent snapshots, invalid/duplicate
  registration rejection, empty catalog behavior and zero preparation calls.
- Existing `ProcessLaunchDecisionTests` prove legacy fingerprints, exact bindings,
  supported policy ceilings and refusals; add descriptor/validation agreement coverage.
- Real HTTP catalog and OpenAPI tests prove exact public contracts and unchanged launch
  routes. Run both actual P23 producer cases as the dependent-flow smoke if the shared
  provider validation changes, covering both read modes through publication/accounting.
- Build changed Application, Workbench and Web plus owning Unit/Integration assemblies
  serially in Release. Confirm exact build-backed test discovery before execution.
  Select only the above topics; no new broad Stable trigger is introduced.
- Export OpenAPI from an isolated owned test host, regenerate/test the SDK and typecheck
  the app. No rendered UI is changed in this slice, so browser evidence is not borrowed.
- Freeze source/output identities, reconcile owned resources, run full portability and
  maintained-doc checks, inspect all deltas, and sign both checkpoints.

Closure requires these observations and updated root/requirements/architecture records.
Missing proof keeps the slice open. New runtime, policy or contract findings reopen the
affected P23 prerequisite before dependent UI work proceeds.
