# Proof Portability And Invariant Gates

## Problem

The current artifact-backed validator is a major improvement, but the reviewed completed bundle still contains machine-specific `C:/repositories/...` references. In a Linux or CI verification context, completed-stage validation fails because Windows paths are not normalized in `validate_exact_source_references` and proof manifest references cannot be resolved after relocation.

## Target pattern

Introduce a proof reference model with these allowed path forms:

- `repo://relative/path` for repository source files.
- `bundle://proof/SBxx/...` for bundle-owned proof artifacts.
- Native absolute paths for local ad-hoc development, but never as the only durable reference.
- Optional `--repo-root` and `--bundle-root` validator arguments for path resolution.

## Semantic invariant contract

Every critical subbundle must include `proof/SBxx/semantic-invariants.json` or `proof/SBxx/semantic-invariants.md` with:

- invariant id,
- source raw note,
- expected behavior,
- disallowed shallow implementation,
- failing-first test name and transcript,
- passing test name and transcript,
- changed source files and hashes,
- production assertions,
- red-team negative case,
- downstream dependency check.

The completed-stage validator must fail if a critical completed subbundle lacks this contract or if the execution report does not cite it.
