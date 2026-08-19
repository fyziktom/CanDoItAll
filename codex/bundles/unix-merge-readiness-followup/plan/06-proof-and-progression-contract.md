# Proof and progression contract

This table supplies the status, prerequisite, proof-tier, dependency-impact, and progression semantics shared by the compact subbundle files. Each subbundle's `tasks.md`, `validation.md`, and `exit-criteria.md` remain its implementation boundary and acceptance checklist. Source ownership is in `inventories/source-hotspots.csv`.

| Unit | Status | Prerequisite | Proof tier | Downstream progression | Reopen trigger |
|---|---|---|---|---|---|
| M00 | Completed | Clean inspected checkout | Standard | `GO`; M01 eligible | New source commit or sibling head/dirty-state change |
| M01 | Completed | M00 GO | Governed | `GO`; M02 eligible | Persistence schema, plan canonicalization, capability sealing, or migration change |
| M02 | Completed | M01 GO | Governed | `GO`; M03 eligible | Package version, sibling anchor, build property, or capability claim change |
| M03 | Completed | M02 GO | Governed | `GO`; C1 eligible | Process start/control/identity/lifecycle change |
| C1 | Completed | M01-M03 GO | Behavioral | `GO`; M04 eligible | Any M01-M03 invalidation key changes |
| M04 | Completed | C1 GO | Behavioral | `GO`; M05 eligible | MCP transport, framing, process lifecycle, or capability advertisement change |
| M05 | Completed | M04 GO | Governed | `GO`; M06 eligible | Docker recipe, Compose, secret file, image, or workflow change |
| M06 | Completed | M05 GO | Governed | `GO`; C2 eligible | Workspace/safe-path, executable lookup, or host permission change |
| C2 | Completed | M04-M06 GO | Behavioral | `GO`; M07 eligible | Any M04-M06 invalidation key changes |
| M07 | Completed | C2 GO | Behavioral | `GO`; M08 eligible | Validation script, test catalog, dependency-mode stamp, or anchor change |
| M08 | In progress | M07 GO | Governed | M09 after frozen Windows/Linux candidate proof | Production/test/build/dependency/runtime configuration change |
| M09 | Pending | M08 GO | Governed | M10 after colleague records actual-host result; preparation may complete locally | Candidate hash change or macOS evidence contradiction |
| M10 | Pending | M09 handoff result | Standard | Final decision and closure | Any candidate/evidence mismatch |

## Covered input and closure path

The literal operator request in `ORIGINAL-REQUEST.md` maps to MR-001 through MR-010. M01-M08 own implementation and local proof; M09 owns the explicit macOS deferral/handoff; M10 classifies the request as `Solved`, `Partially solved`, or `Not solved` using exact evidence.

## Global constraints

- No UI composition work is introduced by this bundle; the pre-anchor secret-provider UX delta is preserved and revalidated at M08.
- No new project reference, partial-class expansion, service locator, shell invocation, or silent security fallback is allowed.
- Host-visible process, Docker, path, and executable behavior requires actual-host proof.
