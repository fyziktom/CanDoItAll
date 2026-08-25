# SB07 proof artifacts

State: `BLOCKED`

This directory currently preserves partial, non-closing SB07 evidence:

- `proof-manifest.json` records the blocked state and current exact counts.
- `transcripts/35-e2e-tool-build-release-final.txt` and
  `transcripts/36-focused-build-release-final.txt` are clean current Release builds.
- `transcripts/37-focused-list-release-final.txt` and
  `transcripts/38-focused-test-release-final.txt` prove the frozen local 10/10 checkpoint.
- `behavior/attempt-24-scenario-results.sanitized.json` preserves the bounded partial 19-scenario
  result: 10 passed, 5 failed, and 4 pending.
- `test-budget-exception.md` is the authoritative seven-attempt/seven-build ledger and explains why
  no further Docker work is currently allowed.

Architecture/security closure, final changed-file/hash packaging, and a passing Docker lifecycle
remain pending. CP-05 and SB08 are not unlocked.

Do not store credentials, prompt/response content, binary model outputs, or unredacted logs.
Every artifact referenced by the manifest must exist and have a SHA-256 at completion.
