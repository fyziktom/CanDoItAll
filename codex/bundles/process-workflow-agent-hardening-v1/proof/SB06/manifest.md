# SB06 Proof Manifest

## Status

Passed. SB06 canonicalized agent, process-template, and Codex API skill guidance around process operation contracts, current-run evidence, provider usage ledgers, workflow side-effect receipts, and active project-structure HTTP API behavior. The active Codex skill root was synchronized and verified with matching SHA-256 hashes.

## Delivered Changes

- Updated Blazor and .NET delivery agent instructions to treat `allowedOperations`, `operationTargetScope`, `MutateProductTarget`, and `CaptureRuntimeProof` as canonical process contracts rather than advisory text.
- Updated delivery manager instructions to preserve artifact expectation statuses, projection lineage, browser proof requirements, and HTTP API fallback for governed project-structure writeback when direct tools are unavailable.
- Updated screenshot review/storage instructions to treat screenshot asset storage as a governed external action with current-run browser proof receipts and project-structure HTTP API fallback.
- Updated the software-delivery process template JSON and markdown to state that operation contracts must stay source-aligned with the canonical `ProcessStepOperation` and `ProcessStepTargetScope` catalogs.
- Updated API skills for agents, processes, workflows, and project structure to document current HTTP API behavior, provider usage ledger fields, browser proof validator requirements, workflow side-effect descriptors, idempotency receipts, and direct-tool/HTTP API boundaries.
- Added API skill/template parity tests that assert canonical contract language, removed-MCP scan coverage, and current-run proof terminology.
- Extended process template governance tests to assert the software-delivery governance notes name the canonical operation and target-scope catalogs.
- Copied the four changed API skill files into the active Codex skill root and verified repo and active SHA-256 hashes match.

## Command Transcripts

- `proof/SB06/transcripts/api-docs-skills-parity-tests.txt`
- `proof/SB06/transcripts/process-template-governance-tests.txt`
- `proof/SB06/transcripts/failing-first-skill-canonical-contract-mutation.txt`
- `proof/SB06/transcripts/skill-canonical-contract-restored-test.txt`
- `proof/SB06/transcripts/failing-first-removed-mcp-assumption-mutation.txt`
- `proof/SB06/transcripts/removed-mcp-assumption-restored-test.txt`
- `proof/SB06/transcripts/active-skill-sync.txt`
- `proof/SB06/transcripts/source-assertions.txt`
- `proof/SB06/transcripts/removed-mcp-assumption-scan.txt`
- `proof/SB06/transcripts/anti-stub-audit.txt`
- `proof/SB06/transcripts/git-diff-check-after-sb06.txt`
- `proof/SB06/transcripts/prepared-validator-after-sb06.txt`

## Shallow-Pass Trap

The SB06 tests do not only check that files exist. They require exact canonical contract names, current-run evidence terminology, workflow side-effect descriptor names, idempotency receipt fields, project-structure direct-tool names, and stale removed-MCP rejection across skills and templates.

## Adversarial Negative Proof

`proof/SB06/transcripts/failing-first-skill-canonical-contract-mutation.txt` temporarily removed `WorkflowExecutorSideEffectDescriptor` from the workflows API skill. The targeted parity test failed because the canonical side-effect contract was no longer documented. The wording was restored and `proof/SB06/transcripts/skill-canonical-contract-restored-test.txt` passed afterward.

`proof/SB06/transcripts/failing-first-removed-mcp-assumption-mutation.txt` temporarily inserted `Use ProjectStructure MCP for writeback.` into the .NET application developer template. The removed-MCP scan test failed because the stale MCP-only assumption lacked a removed-server qualifier. The sentence was removed and `proof/SB06/transcripts/removed-mcp-assumption-restored-test.txt` passed afterward.

## Semantic Positive Proof

Passing targeted slices:

- API/docs/skills parity: 6 passed, covering canonical API skill governance, agent template governance, removed-MCP scans, existing API coverage, and dry-run terminology.
- Process template governance: 12 passed, covering the software-delivery process template, subprocess/writeback contracts, and the new canonical operation/scope governance notes.
- Restored side-effect contract test: 1 passed after the failing-first mutation was reverted.
- Restored removed-MCP scan test: 1 passed after the failing-first mutation was reverted.
- Active skill sync: all four changed API skill files matched between repository and `C:\Users\lucys\.codex\skills`.

## Source Assertions

`proof/SB06/transcripts/source-assertions.txt` confirms the changed agent templates, process templates, API skills, parity tests, governance tests, and active-sync transcript contain the canonical contract and current-run proof language expected by SB06.

## Active Sync Proof

`proof/SB06/transcripts/active-skill-sync.txt` records matching repository and active-root hashes for:

- `candoitall-api-agents`
- `candoitall-api-processes`
- `candoitall-api-workflows`
- `candoitall-api-project-structure`

## Anti-Stub Audit

`proof/SB06/transcripts/anti-stub-audit.txt` found no `TODO`, `HACK`, `NotImplementedException`, or `throw new NotImplementedException` markers in SB06 production skill/template files.

## Raw Note Literal Closure

- Agents/skills/tools/MCP coverage: closed for SB06 by canonicalizing active API skills, process templates, and role instructions, then proving active skill-root synchronization.
- Removed MCP assumptions: closed by the parity test, standalone source scan, and failing-first stale `ProjectStructure MCP` mutation.
- Stale evidence warnings: closed in the updated skills and templates by requiring current-run process-visible evidence, browser receipts, provider usage ledger fields, and projection lineage.
- Product root discipline and fake-shim avoidance: preserved in existing generic app-delivery instructions; SB06 did not introduce scenario-specific app rules.
- Browser proof language: closed at guidance level by tying browser proof to `CaptureRuntimeProof`, `ProcessBrowserProofValidator`, and current-run route/viewport/screenshot/console/startup/cleanup receipts.

## Additional Artifacts

- `proof/SB06/semantic-invariants.md`
- `proof/SB06/changed-file-hashes.md`
- `proof/SB06/production-behavior-artifact-matrix.md`
- `proof/SB06/browser-validation-analytics.md`
