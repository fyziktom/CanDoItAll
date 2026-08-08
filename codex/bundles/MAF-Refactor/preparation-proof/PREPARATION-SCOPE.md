# Preparation scope — Claude/Fable 5 revision 2

## Reviewed baselines

- CanDoItAll repository: `fyziktom/CanDoItAll`
- Target branch: `development`
- Static source baseline: `51d9a2f071e9a5f295abac884c8c667328462cc4`
- SharedInfo repository baseline: `67a5e73a6f80ae3d7c8afcee56f9e7cde48b5939`
- Preparation date: 2026-08-06

## Revision work completed

The existing architecture bundle was re-read and revised for Claude Code with Claude Fable 5, conditional Claude Opus 5 fallback, and durable cross-model/session handoff. The revision adds:

- Claude-specific root and per-subbundle prompts;
- 19 subbundles with six blocking checkpoint/review decisions;
- explicit adaptation maps for runtime callers, DI/manual construction, floating UI context, approvals, provider state, process recovery, workflows, mocks/harnesses, diagnostics, API test hosts, and public projections;
- single-path cutover, rollback, observability, fault-injection, and owner-stage bugfix procedures;
- a provider-backed lightweight LLM boundary and a future ordinary-chat application/source-of-truth design;
- final architecture review and regression-triage prompts;
- structure, dependency, caller, architecture, and cutover guard scripts.

## Preparation-time validation performed

- bundle structure and dependency order validation;
- JSON parsing for every manifest/template;
- Python compilation and CLI help checks for every bundled Python script;
- semantic consistency of 19 subbundle IDs, titles, dependencies, prompts, proof manifests, and handoff templates;
- explicit relative bundle-reference resolution;
- stale executor/prompt-name and packaging-cache scans;
- deterministic ZIP integrity test and SHA-256 generation at packaging time.

## Validation not performed here

The preparation environment could inspect GitHub source through the connected GitHub integration but could not obtain a local repository checkout because direct network/DNS access from the artifact container was unavailable. It also did not have the target CodeAnalytics MCP or PowerShell runtime. Therefore this preparation did **not** run:

- `dotnet build` or target repository tests;
- target repository architecture/cutover scripts;
- CodeAnalytics snapshots or dependency queries;
- PowerShell execution of `scripts/run-validation.ps1`;
- live provider/process/UI scenarios.

SB00 must perform current-branch CodeAnalytics, build, test, persistence-fixture, provider-runtime, and caller characterization before any production cutover. No bundle statement should be interpreted as proof that the target solution currently builds or that the planned implementation has already succeeded.
