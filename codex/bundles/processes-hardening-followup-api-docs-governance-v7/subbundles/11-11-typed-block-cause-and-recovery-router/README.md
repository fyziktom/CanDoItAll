# SB11: 11-typed-block-cause-and-recovery-router

## Goal

Extend transition requests and runtime code to carry typed block reason code and recovery options; treat reason-text inference only as legacy fallback.

## Scope

- Work only on the generic process runtime/API/skill/documentation contract.
- Keep workflows below processes.
- Keep PostgreSQL-only.
- Do not add software-only assumptions unless the test is explicitly a software scenario.

## Required implementation tasks

1. Read reviewed source observations and current source.
2. Add failing-first or red-team tests before production code.
3. Implement production changes.
4. Add/adjust API, docs, skill, and template coverage if this subbundle touches public process semantics.
5. Update proof manifest.

## Required proof

- `proof/SB11/transcripts/failing-first.txt`
- `proof/SB11/transcripts/passing.txt`
- `proof/SB11/transcripts/source-assertions.txt`
- `proof/SB11/transcripts/anti-stub-audit.txt`
- `proof/SB11/transcripts/changed-file-hashes.txt`

## Closure criteria

- Focused tests pass.
- No stub-only implementation.
- No SQLite reintroduction.
- Public/API/tool/skill/docs surface is not behind production runtime for fields owned by this subbundle.
