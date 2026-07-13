# SB13 Semantic Invariants

## INV-SB13-001 Domain Policy Isolation

- Raw note: process runtime and dispatcher must remain generic while domain drivers own domain-related rules.
- Expected behavior: generic completion services compose policy contributions without naming .NET/software-delivery terms.
- Disallowed shallow implementation: move strings to a constants/helper class still called by generic logic.
- Failing-first proof: `bundle://proof/SB13/transcripts/failing-first.txt`.
- Passing proof: `bundle://proof/SB13/transcripts/passing-tests.txt`.
- Production assertions: `bundle://proof/SB13/transcripts/source-assertions.txt`.
- Red-team negative case: unrelated tool/app families do not match .NET policy.
- Downstream dependency: satisfied; SB14 package review and production E2E were allowed to proceed.

## Closure Result

- Result: `Passed`.
- Generic runtime composition names contracts and catalogs, not .NET tool names or software-delivery step keys.
- .NET lifecycle, setup, tool-receipt, runtime-plan, and subprocess-contract policy is physically grouped under `Drivers/DotNet`.
- Negative tests prove unrelated tool families do not match and generic-only subprocess resolution does not invent a domain default.

## Production Behavior Artifact Matrix

No new persisted production signal is planned.
