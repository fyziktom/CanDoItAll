# Drift signals

Treat these as signals, not proof.

## Naming drift
- similar concepts with slightly different names
- duplicated enums
- more catch-all managers/helpers

## Boundary drift
- UI talking directly to persistence
- infrastructure types inside domain projects
- policy concerns scattered across unrelated types

## Projection drift
- DTOs or view models used as write models
- export/import/snapshot types acting like live truth
- reporting models reused as canonical entities

## Source-of-truth drift
- same concern stored in multiple places
- multiple write paths without clear ownership
- derived fields becoming mutable

## Runtime drift
- tooling or watch helpers mixed into domain logic
- transient process state stored next to canonical truth

## Integration drift
- provider-specific fields spreading into the core model
- external schemas leaking into domain entities
