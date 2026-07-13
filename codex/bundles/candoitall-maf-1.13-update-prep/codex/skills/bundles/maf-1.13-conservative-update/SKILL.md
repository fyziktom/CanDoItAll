# MAF 1.13 Conservative Update Bundle

## Purpose

Guide Codex through a conservative first-stage update of Microsoft Agent Framework packages in CanDoItAll. The goal is to move the stable MAF package references to `1.13.0`, fix package-induced breaking changes, and prove current behavior still works.

## Scope

In scope:

- package version update,
- restore/build fixes,
- adapter compatibility fixes,
- focused regression tests,
- evidence documentation.

Out of scope:

- new MAF feature adoption,
- process runtime redesign,
- direct process runtime tools,
- process API expansion,
- memory architecture redesign,
- large class refactoring.

## Required subbundle order

1. `subbundles/00-inventory-and-freeze/SKILL.md`
2. `subbundles/01-package-version-update/SKILL.md`
3. `subbundles/02-compile-break-adapter-fixes/SKILL.md`
4. `subbundles/CHECKPOINT-after-update/SKILL.md`
5. `subbundles/03-focused-regression-validation/SKILL.md`
6. `subbundles/04-documentation-and-merge-evidence/SKILL.md`

## Global constraints

- Keep current runtime architecture.
- Make the smallest useful change for each break.
- Prefer adapter compatibility over product behavior changes.
- Preserve approvals, finalizers, tool policy, telemetry, provider gates, and process evidence.
- Do not introduce `ProcessAgentRuntimeToolProvider`.
- Do not expand `/api/processes`.
- Source-code comments must be in English.

## Required final evidence

Create or update `docs/maf-1.13-update-evidence.md` with:

- package before/after table,
- restore/build/test command outputs summarized,
- source scan outcomes,
- A2A/Mem0 preview package decision,
- known limitations,
- explicit statement that no broad feature adoption was done.
