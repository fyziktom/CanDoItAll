# Senior QA Review

## Verdict

Approve the branch as the base for the large connector and plugin wave, with guarded rollout conditions.

## Why

The repeated structural blockers are now closed in code and no longer survive the phase7 hard-gate search:

- persisted projection sync is gone from the active canonical model
- the node carrier is no longer the overloaded storage seam for bindings and artifact state
- `ProjectNodeKindRegistry` now exists as the central capability and assignment rule source
- reclassification has dedicated transition-history support
- editable hierarchy no longer depends on duplicate generic persisted hierarchy links
- metadata foreign-id leakage and marker dual truth were removed from the active model
- closed enum-based connector seams were replaced by manifest and plugin-platform seams
- hard guardrail enforcement now exists in both tests and the repo-level gate script

## Guarded Conditions

- treat `CrmHrServices.cs` hotspot growth as a regression signal
- keep the targeted Playwright regression pack green
- do not treat the full Playwright-project timeout as equivalent to a full-suite pass
- do not route new connectors around the registry, manifest, and binding seams introduced here
