# SB039 Red-Team: Shallow Launch API Proof Rejected

## Rejected Shallow Pass
A shallow Gate M pass could cite a launch-plan read model or process route string without proving that launch APIs still start real process runs and preserve project/global compatibility.

## Why It Is Rejected
- Launch-plan execution must delegate to `StartRunAsync` with `LaunchPlanId`.
- Direct run start must persist runtime rows and outbox records.
- Project-structure launch-plan creation must preserve bridge context and route identity.
- Project-structure execution must return a real run id and persist project/node context.
- Retried project-structure launch-plan creation for the same context must be idempotent.
- Launch read models must surface generated runtime run failure instead of stale planning status.
- No runtime driver host or execution-capable driver hook may be introduced.

## Positive Proof Required Instead
- `bundle://proof/SB039/transcripts/launch-api-compatibility-tests.txt`
- `bundle://proof/SB039/transcripts/source-assertions.txt`
- `bundle://proof/SB039/transcripts/no-transient-bundle-path-scan.txt`
- `bundle://proof/SB039/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
