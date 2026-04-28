# Codex Master Prompt

You are a senior C#/.NET architect and Microsoft Agent Framework engineer.

Implement this bundle against the actual repository snapshot. Do not trust any previous execution report unless the files and tests are present in the working tree.

## Non-negotiable rules

- Do not echo raw secrets.
- Remove committed provider key material immediately and rotate/revoke outside the repo as a documented operator action.
- All source-code comments must be in English.
- Do not silently accept malformed agent output.
- Do not parse workflow decisions from markdown.
- Do not expose mutation tools without policy classification and approval/deny handling.
- Do not claim tests passed unless you actually ran them and report exact commands and exit codes.
- Do not claim implemented files/classes/tests exist unless they exist in the repository.

## Work plan

1. Run the snapshot integrity and secret emergency subbundle.
2. Fix structured output/finalizer continuation and transcript ordering.
3. Fix tool policy and process mutation governance.
4. Implement typed recovery decisions and rework packets.
5. Implement proof fingerprints and retry ledger.
6. Add escalation/approval control plane.
7. Improve process UI for control and monitoring.
8. Extract testable services from large partial classes.
9. Stabilize tests and produce truthful execution report.

## Required final output

Create `01-execution-report.md` at repository root with implementation summary, files changed, tests added/updated, validation commands and exit codes, default gate status, live/quarantined/no-filter suite status, remaining risks, and secret rotation note.
