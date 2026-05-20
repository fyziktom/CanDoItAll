# Proof Manifest Template

Create one file per critical subbundle: `proof/SBxx/manifest.md` or `proof/SBxx/manifest.json`.

## Required Fields

- Subbundle: SBxx
- Requirements: R-xx list
- Raw notes covered: literal excerpts
- Changed production files: file path, before hash, after hash
- Changed test files: file path, before hash, after hash
- Failing-first commands: command, transcript path, expected failing tests
- Passing commands: command, transcript path, expected passing tests
- Source-level assertions: file path, method/class, invariant to inspect
- Anti-stub scan: command, transcript path, findings
- Red-team verdict: pass/fail, reviewer notes

## Minimum Transcript Content

Each transcript must include:

- command line,
- working directory,
- start/end timestamps,
- exit code,
- test names or validator fixture names,
- visible failure reason for failing-first runs.
