# Evidence is external to this sealed package

Copy evidence.json into a task-owned ignored evidence directory. Keep the sealed original unchanged.
Each test attempt records ID, kind=test, status, exact command/filter, expected/actual discovery,
executed/passed/failed/skipped counters, source_fingerprint (SHA-256), artifact_refs and an expansion
note when runtime cases differ from discovery. Original failed attempts remain; reference exact
follow-ups from groups. A source-only scan is not a pass for omitted ignored artifacts.

For a successful non-test proof (graph, signing, publish, scope decision), use an honest kind such
as inspection or build with command, source fingerprint and actual artifact references. This is not
a way to replace required runtime tests: the matrix defines the semantic obligation.

QUALIFIED/BLOCKED/FAIL need notes. The validator rejects incomplete mandatory groups in strict
mode. It checks structure/consistency, not authenticity or sufficient clinical-style certainty.
A local verified sibling is not remotely published: use VERIFIED_LOCAL plus an explicit limitation
unless remote availability and target consumption were actually checked.

Run the package validator with --evidence <external path> --require-complete only for genuine final
evidence. Do not edit groups or the validator to force a green conclusion. Complex critical findings
remain unresolved and are mapped in finding.md. No actual product runs are prefilled here.

Use closure.unresolved_findings for unresolved blocking correctness/security/scope findings.
Keep nonblocking timing/log/environment observations in closure.observations with evidence and
explicit rationale; do not silently downgrade a blocker. Local-only dependency delivery is a
separate recorded qualification. ready_for_next_development_slice and release_ready are different
conclusions and are never inferred solely from a structurally valid JSON file.
