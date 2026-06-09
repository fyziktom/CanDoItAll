# SB051 Red-Team: Release-Candidate Shallow Proof Rejected

## Rejected Claim
"Gate Q passes because earlier subbundles passed and the UI smoke test opened a page."

## Why That Is Insufficient
- Earlier subbundle proof can go stale after later edits.
- A page-open smoke does not prove process launch execution, run detail recovery readback, project-structure output navigation, source scans, build health, or focused integration health.
- Browser screenshots alone do not prove no runtime-host drift or no transient bundle-path leakage.
- Build-only proof does not prove runtime execution, operator readback, or browser-visible release flow.

## Required Proof Shape
- Clean solution build.
- Full unit test pass.
- Focused process runtime integration matrix across lifecycle, dispatch, execution, trigger-origin, read-only manager diagnostics, boundary, and observability.
- Large-desktop Playwright proof with route, viewport, actions, assertions, and screenshots.
- Source assertions and clean scans for transient bundle paths and forbidden runtime-host/driver-host surfaces.
- Critical manifest and semantic invariants that cite artifact-backed transcripts.

## Accepted Evidence
- `bundle://proof/SB049/build-unit-focused-integration-matrix.md`
- `bundle://proof/SB050/large-desktop-playwright-matrix.md`
- `bundle://proof/SB051/transcripts/release-candidate-source-assertions.txt`
- `bundle://proof/SB051/transcripts/no-transient-bundle-path-scan.txt`
- `bundle://proof/SB051/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- `bundle://proof/SB051/transcripts/production-driver-runtime-host-scan.txt`
