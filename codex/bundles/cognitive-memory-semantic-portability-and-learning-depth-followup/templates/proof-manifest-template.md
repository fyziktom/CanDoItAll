# Proof Manifest Template

## Changed Files

| File | SHA-256 | Invariants |
|---|---|---|
| repo://path/to/file.cs | `<hash>` | INV-SBxx-01 |

## Semantic Invariant Contract

- Contract artifact: `bundle://proof/SBxx/semantic-invariants.json`

## Failing-first Evidence

- Transcript: `bundle://proof/SBxx/transcripts/failing-first.txt`
- Test name: `<test name>`
- Expected exit code: non-zero before implementation.

## Passing Evidence

- Transcript: `bundle://proof/SBxx/transcripts/passing-targeted.txt`
- Test name: `<test name>`
- Expected exit code: zero after implementation.

## Anti-stub Audit

- Transcript: `bundle://proof/SBxx/transcripts/anti-stub-audit.txt`
- Must search for TODO, NotImplemented, fixture/test-name-specific branches, and narrow example phrase branches.

## Red-team Negative Proof

- Transcript or report: `bundle://proof/SBxx/red-team.md`
