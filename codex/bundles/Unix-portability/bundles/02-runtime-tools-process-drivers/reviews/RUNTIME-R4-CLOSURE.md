# Final Runtime Gate R4 closure

## Status

- `Deferred — local Windows/Linux candidate preparation in progress; genuine macOS evidence pending`

## Exact anchor

- Commit: `386d8beb6038035f89a9a6961ec017d8213879a5`
- Working tree: reviewed M00-M07 changes; immutable fingerprint is assigned only after M08
- CI: active three-host workflow configured, not claimed as executed for this candidate
- SDK/runtime: Windows SDK `10.0.303`; Linux SDK `10.0.302`
- Core C4 revalidation: local Windows/Linux C2 green; C4 hosted/macOS boundary remains deferred

## Supported profiles

| Profile | OS/arch | Runtime nodes | Manager | MCP/tools | Docker/FileTools | Processes | Evidence |
|---|---|---|---|---|---|---|---|
| Windows local | Windows x64 | Locally green | Locally green | Locally green | Docker green; package FileTools capability unavailable by policy | Locally green | C2 422 Unit + 45 Integration + 1 Browser |
| Linux local | Docker Linux amd64 | Locally green including Chromium | Locally green | Locally green | Docker green; desktop FileTools unavailable by profile/package policy | Locally green | C2 422 Unit + 45 Integration + 1 Browser |
| macOS candidate | macOS 15 arm64 | Unverified | Unverified | Unverified | Capability-sensitive and unverified | Unverified | M09 colleague handoff pending |

## Invariants

- [x] one execution primitive/lifecycle owner
- [x] OS-correct executable/environment behavior on Windows/Linux
- [x] process-tree cleanup on Windows/Linux
- [x] typed Workbench plans and optional terminal/elevation
- [x] registry-first Manager ownership and no foreign kill
- [x] governed MCP/external tools with redaction
- [x] external dependency compatibility/disable behavior
- [x] Processes semantic ownership and authority preservation
- [ ] active actual-host macOS evidence for the frozen candidate
- [x] local Windows/Linux failure injection and rollback contracts

## Known limitations

- M08 has not yet frozen the complete Windows/Linux candidate fingerprint or run the scheduled stable suites.
- Genuine macOS arm64 validation is not available in this checkout and cannot be inferred from Docker, cross-publish, or deterministic fixtures.
- Hosted workflow configuration is not a hosted execution result.
- Package-mode FileTools desktop launching remains unavailable for the alpha; explicit source mode requires exact clean anchors and the contract-v2 marker.

## Decision

- Result: `DEFERRED — NOT R4 COMPLETE`
- Reviewers: local executor; genuine macOS colleague and final independent review pending
- Evidence: follow-up bundle `proof/C2`; M08/M09 evidence pending
