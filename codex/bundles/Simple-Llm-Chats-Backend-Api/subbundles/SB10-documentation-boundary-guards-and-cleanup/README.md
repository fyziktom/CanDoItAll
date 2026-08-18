# SB10 — Documentation, boundary guards, and cleanup

Proof tier: **Behavioral**

## Objective

Remove temporary compatibility code, lock architecture guards, and document the backend/API product without adding UI.

## Scope

- Update repository/module/API/testing docs.
- Document thinking-effort provider-default/explicit semantics and guard against a duplicate LLM Chat
  capability catalog or agent-execution dependency.
- Add architecture source guards for forbidden dependencies and global conversation activation.
- Add guard that this bundle changed no Razor/UI/floating-agent-chat files.
- Add test-policy validation to repository bundle tooling where appropriate.
- Remove dead adapters, duplicate models, placeholders, and broad partial classes introduced during work.
- Prepare handoff for shared-component and UI bundles.

## Expected change surface

- README/docs
- architecture guard tests/scripts
- no product behavior expansion

## Targeted validation

- documentation validator
- bundle validator
- test-policy validator
- architecture boundary focused tests

All test commands must comply with `test-budget.json`. Record exact commands and results in the proof
manifest.

## Acceptance criteria

- [ ] Source-truth documentation matches implementation.
- [ ] No forbidden dependency or UI diff.
- [ ] No unused dormant registrations.
- [ ] Deferred work is explicit.
- [ ] SB11 is the only remaining unlocked work.

## Forbidden work

- new feature behavior
- UI implementation
- broad tests

## Handoff

Complete `SESSION-HANDOFF.md` and `proof/proof-manifest.json`. Do not unlock the next subbundle until
all acceptance criteria and any checkpoint are satisfied.
