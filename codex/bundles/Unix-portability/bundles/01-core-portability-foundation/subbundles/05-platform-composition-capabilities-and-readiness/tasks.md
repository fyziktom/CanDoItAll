# A05 tasks

## Entry checklist

- [x] Verify exact checkout and preserve unrelated working-tree changes.
- [x] Verify prerequisite gate evidence.
- [x] Reproduce focused baseline/characterization.
- [x] Confirm every source hotspot after materialization.

## A05-T01 — Define narrow platform facts and adapters

- [x] Keep common code on portable .NET APIs. Add purpose-owned contracts only where behavior genuinely differs: root defaults, filesystem semantics, key/vault backend, native permission hardening, and optional capability probes.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A05-T02 — Select implementations at composition

- [x] Register exactly one mandatory implementation per profile and zero-or-one optional adapters. Avoid conditional compilation unless a native reference cannot be isolated otherwise.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A05-T03 — Create capability/readiness descriptors

- [x] Report availability, reason, remediation, support level, implementation registration/identity/version, and execution boundary without exposing secrets or full sensitive paths.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A05-T04 — Fail fast for mandatory security/path defects

- [x] The host must not start in a production profile with an unsupported secret provider, unusable control-plane root, insecure key permissions, or ambiguous migration.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A05-T05 — Degrade optional features independently

- [x] Desktop open, terminal presentation, native process discovery, FileTools, and other runtime capabilities can be unavailable without blocking headless core startup.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A05-T06 — Add architecture enforcement

- [x] Add dependency/scan tests that prevent a broad IPlatformService, OS branching in domain/process semantics, and reverse MAF-to-product ownership.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A05-T07 — Prove profile matrix

- [x] Test Windows interactive, Linux headless, Linux interactive-keyring, macOS interactive, macOS headless/service, and explicit test profiles.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A05-T08 — Issue composition gate C3a

- [x] Require consistent capability UI/API/readiness snapshots and no misleading support claims.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

Initial independent review recorded NO-GO. The UI consumer, owner-produced path/filesystem readiness, and truthful implementation identity/version remediation are complete; bounded independent re-review recorded Gate C3a GO.

## Closure checklist

- [x] Every owned requirement has evidence and status.
- [x] Focused validation and required stable regression pass.
- [x] Source references/findings/ADRs/traceability are current.
- [x] Artifacts are redacted.
- [x] Required independent reviewers record GO.
- [x] Handoff identifies the next eligible subbundle or conditional stop.
