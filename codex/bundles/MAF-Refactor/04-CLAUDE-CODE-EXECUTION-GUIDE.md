# Claude Code execution guide

## Purpose

This bundle is optimized for Claude Code with Claude Fable 5 while remaining resumable by another high-capability Claude model. The architecture and proof requirements are model-independent; only the execution wrapper is Claude-specific.

## Model profile

- Prefer Claude Fable 5 for implementation-heavy subbundles.
- Use the deepest/maximal reasoning mode available in the installed Claude Code version.
- Treat `xHigh` as the requested reasoning intent, not as a required literal command-line setting.
- When the preferred model is unavailable or credits are exhausted, switch to the best configured high-capability Claude model only after writing a durable handoff.

## Repository memory

Claude Code automatically reads project `CLAUDE.md` instructions. Do not overwrite an existing repository file. Review `claude/CLAUDE.bundle.template.md` and merge only the bounded imports/rules that are missing from the repository's current instructions.

## MCP and skills

Before SB00:

1. Verify CodeAnalytics MCP is connected and can inspect the solution, projects, dependencies, symbols, references, and findings.
2. Verify installed SharedInfo architecture skills listed in `sharedinfo/required-skills.md`.
3. Record unavailable tools as validation gaps. Do not invent results.
4. Use MCP for orientation and dependency evidence, then inspect exact source and `.csproj` files before edits.

## Session discipline

- One implementation subbundle per Claude session and preferably per branch/commit series.
- Checkpoint subbundles are review sessions and must not smuggle feature work.
- Read only the root documents, relevant ADRs, selected README, and exact source required for the current slice; do not load the entire bundle indiscriminately.
- Keep `proof/proof-manifest.json` and `proof/SESSION-HANDOFF.md` current during work, not at the end.
- Use Plan Mode for orientation/review when useful, then return to an edit-capable mode for implementation. A plan is not completion.
- Inspect `git diff` and run a focused build/test after each risky cutover step.
- Never ask a fallback model to infer unfinished work from chat history alone.

## Prompt use

Start a subbundle by giving Claude Code the contents of its `CLAUDE-CODE-PROMPT.md`, or point it to that file and instruct it to execute it. The prompt intentionally separates role, context, constraints, workflow, stop conditions, and closure output.

## Regression and independent-review prompts

- Use `claude/REGRESSION-BUGFIX-PROMPT.md` for defects discovered after a cutover. It enforces owner-stage diagnosis and failing-first repair.
- Use `claude/FINAL-ARCHITECTURE-REVIEW-PROMPT.md` in a fresh Claude session/model for SB17/SB18. The reviewer should not inherit implementation assumptions from the coding session.

## Credit-aware handoff

Before a model/session switch:

1. Stop at a buildable or clearly documented failing state.
2. Write current HEAD and working-tree status.
3. List changed files and exact intent.
4. Record every command and test result.
5. Record CodeAnalytics snapshot/dependency evidence.
6. Record selected cutover path, active compatibility flag, and rollback action.
7. Record failures by stage and correlation IDs.
8. Name the next smallest safe action.

Use `claude/SESSION-HANDOFF.template.md` or the subbundle proof template.

## What Claude must not optimize away

- architecture checkpoints;
- failing-first characterization;
- per-concern source-of-truth boundaries;
- exact turn context through approval continuation;
- scope-bound service identity;
- provider/process/tool side-effect single-path rules;
- backward readers for persisted state;
- source/dependency guards;
- negative/fault testing;
- durable evidence for the next session.
