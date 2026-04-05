# Rollout and Rollback Notes

## Rollout posture

- Roll out the refactor in ordered phases only.
- Prefer compatibility adapters while the carrier/facet and registry changes are migrating.
- Reopen the plugin wave only after the final gate review says GO.

## Rollback posture

- Keep migration scripts reversible where practical.
- Preserve read compatibility for old metadata/facet payload during the transition period.
- If a phase reopens canonical drift, stop and revert before the next phase starts.
