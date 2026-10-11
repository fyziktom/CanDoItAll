# CD1 — Application surfaces and components-decoupling closure

One implementation assignment to finish the current components-decoupling wave, not another
Processes rewrite. Reviewed branch: `components-decoupling`; review source:
`fd9e43547457eb18901185da47de123efdfd8108`. This identifies the review, not an execution pin.

## Install and execute

Extract only `CanDoItAll_Components_Decoupling_Closure_CD1` into `codex/bundles/`.
Do not replace the existing `UI_Decoupling_Shared_Bundle`. Preserve any local edits to an
already installed CD1 directory before replacing it.

Give the implementation agent [prompt.md](prompt.md). Use the shared bundle already present
in the repository and the current repository/SharedInfo instructions. This archive neither
contains shared content nor requires a new shared version or manifest. A current progress
register belongs in maintained product documentation, not in a new shared-bundle release.

## Decision

PC4's reviewed corrections are wired into the real producers and native owners. Its maintained
report records complete local Stable and native browser campaigns. Preserve that work. The
review found no new blocker in the PC4 paths examined; that is not an independent execution
of all tests or an exhaustive proof of the repository.

There is a concrete remaining application-level slice: Home/Dashboard, Runtime Capabilities
and CapabilityFact, the database profile dialog, MainLayout's utilities/database flyout,
and its status top bar. They still render in Web; two families expose Web/Composition or
Infrastructure-owned types. Finish their presentation seams and then close every remaining
active rendering boundary identified by a current repository-wide census in this same task.

## Reading order

1. [Review and evidence limits](REVIEW_PC4.md), [remaining scope](REMAINING_SCOPE.md).
2. [Application surface design](APPLICATION_SURFACES.md), [census and guards](CENSUS_AND_GUARDS.md).
3. [Execution plan](EXECUTION_PLAN.md), [validation and evidence reuse](VALIDATION_AND_REUSE.md),
   [browser journeys](BROWSER_JOURNEYS.md).
4. [Signing and resources](OPERATIONS_AND_SIGNING.md), [completion and handoff](COMPLETION_AND_HANDOFF.md).

The machine-readable [acceptance plan](acceptance-plan.json), [family register](family-register.json),
[seed inventory](inventory-seed.json) and [source register](review-sources.json) accompany the task.
The inventory is a seed, not a certificate. The [evidence template](evidence-template.json)
starts with no product test passes. Use [the progress template](progress-template.md) for resume.

Run the archive check independently of the application:

```powershell
python codex/bundles/CanDoItAll_Components_Decoupling_Closure_CD1/tools/verify_bundle.py
python -m unittest discover -s codex/bundles/CanDoItAll_Components_Decoupling_Closure_CD1/tools -p "test_*.py"
```

Package checks do not certify the C# implementation, the evaluated graph, browser behavior,
PGP signatures, or the semantic completeness of a renderer census.
