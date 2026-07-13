# SB18 Governed Proof Manifest

Date: 2026-07-13.

Decision: `Pass`.

## Scope

- Final package, architecture, security, performance, host, browser, raw-note, and validator closure for R001-R040 and N001-N018.
- Reopened repairs: explicit storage-placement composition at application roots and visible PDF object activation in FileTools.
- User-requested preservation: Projects retains its recursive project/subproject `TreeView`; no replacement was introduced.

## Evidence Index

- Semantic contract: `semantic-invariants.md`.
- Product and browser outcomes: `behavioral-proof.md`.
- Test/build/format results: `transcripts/test-results.txt`.
- Package payload and published assets: `transcripts/package-static-assets.txt`.
- Dependency/source/final architecture assertions: `transcripts/source-architecture-audit.txt` and `bundle://reviews/csharp-architecture-gate.md`.
- Security cases: `transcripts/security-red-team.txt`.
- Browser DOM/geometry/console/network evidence: `transcripts/browser-proof.txt` and `screenshots/*`.
- Failing-first repair record: `transcripts/failing-first-repairs.txt`.
- Performance delta and inherited envelope: `transcripts/performance-scan.txt`.
- Validators: `transcripts/completed-validator.txt` and `transcripts/manual-validator.txt`.
- Source and artifact hashes: `source-hashes.sha256`.

## Closure

- Code, accepted packages, published payload, tests, managed runtime, screenshots, status surfaces, raw notes, and validators agree.
- Fresh CodeAnalytics and Components MCP retries returned `Transport closed`; the deterministic checked graph/source/build/browser substitute is recorded without claiming an MCP result.
- An unrestricted repository-wide test attempt was stopped after it mixed unrelated database, Playwright, seed, prompt-artifact, and dependency-version failures into one unmanaged lane. The bundle-owned composition failure found early in that run was repaired and rerun directly. No unrelated failure is represented as a pass.

