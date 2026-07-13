# Implementation Prompt

Implement the active subbundle only.

Start by reopening the subbundle README, `plan/01-phase-plan.md`, and `reviews/01-execution-report.md`. Confirm prerequisites and stop if a required upstream proof manifest is missing.

Keep changes small and aligned with the architecture:

- `CanDoItAll.Git` is the authoritative git command-spec layer.
- Workspace runtime tools consume typed specs and continue to use `WorkspaceCommandProcessRunner`.
- Tool names belong in `ToolContractCatalog`; do not duplicate string literals across policy and runtime code.
- Read-only git tools stay read-only; staging, commit, branch, and switch tools are mutation tools requiring the software-development/manage-paths capability path.
- Do not add remote or destructive git operations.

After code changes, run the focused validation named in the subbundle, write transcripts under `proof/SBxx/transcripts/`, create `proof/SBxx/manifest.md` and `proof/SBxx/semantic-invariants.md` for critical subbundles, and update `reviews/01-execution-report.md`.
