# Next Bundle Decision Template

## Decision Options
- [ ] Approve production verification host registration with static allow-list.
- [ ] Continue read-only adapters without registration.
- [ ] Prepare manager-visible read-only verification UI/process results.
- [ ] Defer and harden Core/driver contracts further.

## Approval Preconditions
- Full unit debt is resolved or explicitly quarantined.
- Gateway tests prove no runtime discovery, DI, registry, selector, or mutation.
- All read-only domain lanes have denial tests for their risky operations.
- Core has no driver references.
- Driver packages have no forbidden dependencies.
- Semantic adequacy proof is artifact-backed.

## Default
Defer runtime host registration unless all gates are green.
