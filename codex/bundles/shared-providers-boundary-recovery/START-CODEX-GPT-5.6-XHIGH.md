# Start prompt for Codex GPT-5.6 xhigh

Use this prompt from the repository root after copying this bundle to:

`codex/bundles/shared-providers-boundary-recovery`

---

You are Codex GPT-5.6 xhigh acting as a senior C#/.NET architect and implementation engineer.

Work in repository `fyziktom/CanDoItAll`, branch `providers-shared`.

The audited starting HEAD is `fdf1ff9702c376ad0ffd101a34d6bf542c9857d2`. First verify the branch and inspect any commits after that SHA. Do not reset, rewrite, or discard user work.

Your task is to execute the Shared Providers Boundary Recovery bundle in order, BR00 through BR08. The primary objective is to remove provider and shared-provider ownership from Workspace, establish a dedicated ProviderManagement boundary in the AgentFramework module family, and converge all inference on the existing AgentFramework/MAF provider runtime while preserving behavior and persisted data.

Read only these documents before the first production edit:

1. `codex/bundles/shared-providers-boundary-recovery/DECISION-LOCK.md`
2. `codex/bundles/shared-providers-boundary-recovery/TARGET-BOUNDARY.md`
3. `codex/bundles/shared-providers-boundary-recovery/EXECUTION-CONTRACT.md`
4. `codex/bundles/shared-providers-boundary-recovery/subbundles/BR00-freeze-and-characterize/README.md`
5. `codex/bundles/shared-providers-boundary-recovery/ARCHITECTURE-ANALYSIS.md` once

For later subbundles, read only the current README and the previous RESULT in addition to the three root control documents. Do not repeatedly read or rewrite the original `codex/bundles/shared-providers` bundle. It is historical evidence and is read-only until BR08.

Locked constraints:

- `CanDoItAll.Modules.Workspace` is not the provider bounded context.
- The new ProviderManagement project must have zero Workspace dependency.
- Do not preserve a second direct OpenAI/Ollama/ComfyUI inference stack outside AgentFramework/MAF.
- Preserve existing public API routes, wire contracts, IDs, secrets, and physical table names.
- Preserve shared-provider publication, discovery, reconciliation, relay, audit, rate limiting, image routing, revision snapshots, and fail-closed behavior.
- Do not run Docker or Podman.
- Do not implement original SB07 feature scope.
- All source-code comments must be in English.

Efficiency rules:

- Make production changes first; write the current subbundle's single `RESULT.md` only after the code gate is complete.
- Do not generate proof manifests, hash inventories, duplicated status files, or narrative restatements.
- Use at most one restore, two affected builds, three targeted test commands, one architecture-guard command, and one EF command per subbundle unless a concrete compiler failure requires one additional repair run.
- Reuse `--no-restore` and `--no-build` outputs.
- Do not run the entire test estate before BR07.
- On an external infrastructure failure, record the exact command and error once. Do not retry lifecycle operations in a loop.
- Do not edit unrelated files or reformat broad areas.

Execute BR00 now, then proceed sequentially while every acceptance gate remains green. Commit once per completed subbundle with message `BRxx: <concise outcome>`. Do not commit a failed or partially accepted subbundle.

---
