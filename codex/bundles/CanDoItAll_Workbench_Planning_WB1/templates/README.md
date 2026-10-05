# External evidence ledger

Copy [evidence.json](evidence.json) into an owned private artifact directory. Do not fill
successes into this sealed package. Add immutable attempts and reference them by identity
from required groups. Preserve original failed and blocked runs; a subsequent pass is another
attempt. Use [finding.md](finding.md) for a new issue.

A passing attempt records command/inspection, exact source fingerprint and artifact references.
Tests additionally record expected/actual discovery and executed/pass/fail/skip counts. Any
runtime expansion needs its specific explanation. Non-test inspections must not invent counts.
A final group can reference multiple distinct checks; no single fake test proves all groups.

The optional validation script checks structure and internal consistency, not file authenticity,
privacy, native behavior or the truth of a claimed execution. Final confirmation requires
reviewing actual retained artifacts and their source relation. Historical qualifications stay.
