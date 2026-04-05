# Rollout and Rollback Notes

## Rollout Bias

This bundle favors **incremental internal migration with adapters** over a big-bang rewrite.

## Rollback Principle

If any subbundle fails mid-flight:

- preserve the current user-facing structure DTO contracts
- keep CRM/HR canonical assignment ownership intact
- do not partially ship a plugin framework that still depends on old enums and scattered subtype switches
- do not mix old persisted SyncGraph truth with new assembled projections in the same canonical tables

## Safe Rollback Anchors

- old public surface DTOs/routes
- old node editing surface behavior
- old CRM/HR assignment flows
- a feature flag or temporary adapter around new projection assembly if needed
