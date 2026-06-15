# Runtime History Migration And Readonly Compatibility

## Design Intent

v2 left runtime history migration as a red-team risk. v3 turns it into a plan and future gate. Existing runtime records may need migration, archive, or read-only compatibility projections. The old runtime implementation must not stay alive only to display history.

## Inventory Plan

Before final compatibility decisions, inventory:

- current process definitions and versions,
- current process runs,
- step runs,
- assignments,
- decisions,
- artifact records,
- journal entries,
- work briefs,
- workflow links,
- launch plans,
- conformance observations,
- improvement candidates,
- observation/cache derived data,
- template import/projection metadata,
- process-related EF migrations,
- production/customer data retention requirements.

## Compatibility Options

| Option | Use when | Tradeoff |
| --- | --- | --- |
| Full migration | Historical runs must participate in new projections and queries. | Highest cost and risk; requires mapping old runtime state to event/projection model. |
| Archive export | Old runs must be retained for audit but not interactive runtime views. | Lower runtime risk; requires searchable archive and retrieval UI. |
| Read-only legacy projection adapter | Users need old history visible in UI, but old records cannot become new runtime state. | Good compromise; adapter reads legacy data and emits read-only projections. |
| Drop after explicit approval | Data has no retention value and users approve deletion. | Requires strong governance and audit signoff. |

Recommended default: read-only legacy projection adapter plus archive export, unless product requirements demand full migration.

## Read-Only Legacy Projection Adapter

The adapter:

- reads archived or legacy runtime records,
- maps them to `LegacyRunProjection`,
- labels projections as legacy/read-only,
- blocks runtime actions,
- provides restricted links to archived diagnostics,
- does not call old runtime services,
- does not use old dispatcher,
- does not emit new runtime events as if old events happened in the new system.

## Compatibility Report Requirements

The final compatibility report must include:

- counts by legacy entity type,
- unmapped fields,
- sensitivity/retention classification,
- selected compatibility option,
- known data loss or transformation limits,
- user-visible UI behavior,
- search/index behavior,
- rollback plan,
- validation queries,
- signoff owner.

## Final Closure Gate

The rewrite cannot close until:

- runtime history inventory exists,
- compatibility option is selected,
- template migration report exists,
- legacy projection/archive behavior is implemented or explicitly rejected,
- tests prove old runtime code is not referenced outside archive/migration adapter,
- UI clearly labels legacy read-only history,
- product owner accepts any data not migrated.

## Invariants

- Old runtime code is not kept alive only for history.
- Legacy history is read-only unless fully migrated through explicit migration tooling.
- Legacy records do not become authoritative new runtime state by accident.
- Compatibility adapters do not reference old dispatcher behavior.
- Retention and sensitivity policies apply to legacy data.

## Failure Behavior

| Failure | Required response |
| --- | --- |
| Legacy data cannot be mapped | Record unmapped field and require product decision. |
| Retention policy unknown | Escalate before deletion or migration. |
| UI requests action on legacy run | Return read-only action denied projection. |
| Old code reference needed for display | Stop and implement projection adapter; do not keep old runtime alive. |

## Test Implications

- Inventory tests verify counts and unmapped fields.
- Compatibility adapter tests verify read-only projections and action denial.
- Search tests verify legacy data discoverability if selected.
- Old-symbol leak tests verify old dispatcher/runtime services are not active dependencies.
