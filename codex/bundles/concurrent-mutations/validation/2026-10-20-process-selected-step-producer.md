# Selected Process source, snapshot and child producer

Status: PASS for selected preparation/enrollment; actual Agent activation remains gated.

The [accepted design](../plan/P07-process-selected-step-producer.md) adds bounded
selected original-source reads, claim-fenced preparation and exact first-child
enrollment. Application owns sequencing, Persistence owns original/current source
comparison and atomic attachment, and the existing Workbench capture now serves both
root and step scopes. No new project reference, runtime partial or service locator.

The initial candidate executed all 186 discovered cases: 185 passed and one failed.
Its claim-expiry fixture jumped two hours, also expiring the ten-minute material.
Production correctly settled closed work and raised material expiry. The correction
uses a five-minute claim and six-minute observation, and adds a separate material
expiry case. Only the fixture, its producer test and this documentation change.
Both affected classes repeat and pass all 23 cases. The other 164 passes are retained
under an exact five-file test/documentation comparison and 2,324 identical production
outputs. This yields 187 distinct passing behaviors.

Initial source: 9,770 files, SHA-256
`EC67CB80077657505780F5F35A129A922489161C6F9F52E4F658976D365441ED`.
Runtime: 4,575 files, SHA-256
`CF199AF410A2B89BDE3B038DD0942955E331F1423454583BA2FD7042C7587F21`.
Complete before/after identities match. All 12 test commands are covered by the
726.691-second observer. No observed process or database-name delta remains;
42 unowned TEMP paths are preserved. The isolated PG18 fixture remains in use.

Initial proof:
`proof/2026-10-19-planning-authority/c4-process-step-material.json`,
420,654 bytes, SHA-256
`C5FEB18D65BD598CA71795E5C3D0ECA315E68082E63A35BEFE11F2EEE3ADB393`.
It retains all build/static/test commands and the failed test's diagnostics.
The timestamp-property compile error and extra blank EOF were corrected before
tests. The final builds and documentation/rendering/whitespace checks pass.

The full portability gate passes with 15,652 unchanged reviewed findings. One
intentional ordinal-insensitive operation-identifier ordering finding was added
after review; the final scan and enforcement ran without writing the baseline.
The corrected test candidate repeats the full scan and passes enforcement without
writing the baseline. Documentation, rendering register and whitespace checks pass.

Final source: 9,771 files, SHA-256
`F4BC4EC48CC061A5ECC9992908CF8760196B4A9DF36E9DDEECB72D71243FD994`.
Runtime: 4,575 files, SHA-256
`A7507F6D4B4CC12257602EE711C9C9620767D581E968D06171D78C2BFD02C5A6`.
Both remain unchanged throughout execution. Current method names and counts match
discovery. The 164.720-second observer covers both commands, with no remaining observed
process or database-name delta; nine unowned TEMP entries remain untouched.

Final proof: `proof/2026-10-19-planning-authority/c4-process-step-material-final.json`,
67,125 bytes, SHA-256
`79CE21736A464E6DC432874B43F79484B238D5DAC85894F7B38400CC8BAE0D38`.
It is secret-reviewed and retains the exact bridge to the failed candidate. The
before-build helper now skips unnecessary runtime hashing for source-only build
checks; the final freeze and verification still hash every runtime input.

CodeAnalytics `snap-20261020235359-77361fd7` has five projects, seven project edges,
no project cycle and the same six existing Workbench module/type cycles. Four new
informational member-count findings concern the small deterministic identity value
and enum, immutable selected-source value, and cohesive preparation lifecycle owner.
The existing 208 diagnostics remain. Actual composition is tested independently of
the analyzer's incomplete DI recognition.

Remaining activation gates include measured initial Core preparation, actual
Workbench child binding, ordinary Process preflight, exact manager approval and
waiting-step completion, allocation capacity, Workflow occurrence ownership and
settings/UI integration. Broad Stable remains unqualified with 17 historical failures.
SPA stays on its separate branch. Historical cleanup denials remain explicit.
