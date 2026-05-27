# SB02: 02-maf16-feature-adoption-truth-table-v2

## Goal

Create an adoption truth table for every MAF 1.6 feature discussed.

## Required work

- Use official docs/release notes and local symbol tests.
- Columns: feature, official source, local symbol available, production adoption, tests, decision, reason.
- Do not mark `IChatMessageInjector`, `AgentSessionFiles`, `SkillFrontmatter`, or `OpenTelemetryChatClient` as adopted unless symbols exist and code uses them.
- Record if MAF 1.7 is available but out of scope.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package / MAF adapter / process runtime / API / UI / template.
- Explicit note whether this subbundle is behavior-changing or proof-only.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB02` are filled and the downstream dependency is safe.
