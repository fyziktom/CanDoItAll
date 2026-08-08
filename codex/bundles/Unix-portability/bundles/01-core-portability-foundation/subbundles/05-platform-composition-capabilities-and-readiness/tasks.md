# A05 tasks

## Entry checklist

- [ ] Verify exact checkout and preserve unrelated working-tree changes.
- [ ] Verify prerequisite gate evidence.
- [ ] Reproduce focused baseline/characterization.
- [ ] Confirm every source hotspot after materialization.

## A05-T01 — Define narrow platform facts and adapters

- [ ] Keep common code on portable .NET APIs. Add purpose-owned contracts only where behavior genuinely differs: root defaults, filesystem semantics, key/vault backend, native permission hardening, and optional capability probes.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A05-T02 — Select implementations at composition

- [ ] Register exactly one mandatory implementation per profile and zero-or-one optional adapters. Avoid conditional compilation unless a native reference cannot be isolated otherwise.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A05-T03 — Create capability/readiness descriptors

- [ ] Report availability, reason, remediation, support level, dependency version, and execution boundary without exposing secrets or full sensitive paths.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A05-T04 — Fail fast for mandatory security/path defects

- [ ] The host must not start in a production profile with an unsupported secret provider, unusable control-plane root, insecure key permissions, or ambiguous migration.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A05-T05 — Degrade optional features independently

- [ ] Desktop open, terminal presentation, native process discovery, FileTools, and other runtime capabilities can be unavailable without blocking headless core startup.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A05-T06 — Add architecture enforcement

- [ ] Add dependency/scan tests that prevent a broad IPlatformService, OS branching in domain/process semantics, and reverse MAF-to-product ownership.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A05-T07 — Prove profile matrix

- [ ] Test Windows interactive, Linux headless, Linux interactive-keyring, macOS interactive, macOS headless/service, and explicit test profiles.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A05-T08 — Issue composition gate C3a

- [ ] Require consistent capability UI/API/readiness snapshots and no misleading support claims.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [ ] Every owned requirement has evidence and status.
- [ ] Focused validation and required stable regression pass.
- [ ] Source references/findings/ADRs/traceability are current.
- [ ] Artifacts are redacted.
- [ ] Required independent reviewers record GO.
- [ ] Handoff identifies the next eligible subbundle or conditional stop.
