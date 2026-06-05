# Target Solution

## Architecture Direction

- Keep the process dispatcher as the public orchestration boundary.
- Extract execution-attempt, retry, no-progress, and provider-recovery families into internal module-local helpers under `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/`.
- Preserve existing public contracts and persistence behavior.
- Keep explicit side-effect boundaries for provider repair, journal writes, execution run adoption, and scheduling decisions.

## Forbidden Boundaries

- Do not create `CanDoItAll.Processes.Core`.
- Do not introduce `IProcessDriverPack`, `IProcessDriverRegistry`, `IProcessHelperDriver`, driver packages, or production driver discovery.
- Do not move EF writes, external provider calls, recovery journal writes, or process scheduling into helpers that appear pure.

## Proof Direction

- Critical gates require source assertions, focused build or test proof, anti-stub scan, no-core/no-driver scan, no prohibited viewport proof scan, and artifact-backed proof manifests.
- Runtime/service refactor keeps browser validation N/A unless UI files unexpectedly change.
