# SB33 Proof Manifest

## Scope

Repaired the 2026-06-20 follow-up issues from the running development instance on port `5032`:

- Provider quota, billing, and credit exhaustion failures now produce a clear operator-facing message instead of a generic runtime failure.
- `.NET runtime` project-structure nodes now use typed environment metadata for launch resolution, including `projectPath`, `workingDirectory`, protocol, and localhost URL when evidence is available.
- Legacy `.NET runtime` nodes with concrete note-only command evidence can be resolved safely without accepting vague notes as runnable proof.
- Runtime command writeback templates now require launcher-compatible metadata receipts instead of accepting note-only command writeback as complete.
- Contextual agent chat no longer blocks opening the window on full execution document load/update when split chat projections/session stores are available.

## Root Cause

Provider failures were persisted and surfaced through raw exception messages, so OpenAI `insufficient_quota` and billing failures were technically present but buried in runtime wording.

The runtime writeback path allowed `.NET runtime` command details to land in notes while the typed environment metadata stayed empty. The launcher and UI action capability resolver depend on metadata, so the node could look correct but had no safe run action.

The agent chat open path was backend-bound. On the unchanged live instance, `/api/agents/{agentId}/chat-sessions` and `/api/agents/{agentId}/chat-workspace` took about 15 seconds while project-structure reads were sub-100 ms. The service was still loading or rewriting the full execution workspace for chat session list/create/rename/latest-run bootstrap, and the Blazor component waited for that work before opening the chat shell.

## Proof Files

- `changed-file-hashes.txt`
- `semantic-invariants.md`
- `validation.md`
- `performance-analysis.md`
- `api/live-api-before-sb33.json`
- `api/runtime-node-selected-before-sb33-full.json`
- `transcripts/unit-tests.txt`
- `transcripts/integration-tests.txt`
- `transcripts/web-build.txt`
- `transcripts/git-diff-check.txt`

## Validation Result

Focused unit validation passed 18/18. Focused split-storage integration validation passed 1/1. The web project build passed serially with 0 warnings and 0 errors using an isolated output folder because the 5032 development instance was still running. `git diff --check` reported no whitespace errors; the transcript only contains normal line-ending warnings.

The live 5032 process was not restarted during this proof, so the API snapshot records the before-deploy bottleneck and malformed runtime-node state rather than post-deploy browser behavior.

