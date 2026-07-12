---
name: candoitall-bundle-validator
description: Validate a CanDoItAll bundle after preparation, when reopening a stale bundle, and before final closure. Use when Codex must prove the bundle covers every input, models dependencies correctly, and has strong enough proof to allow execution or final completion.
---

# CanDoItAll Bundle Validator

Use this skill when the bundle needs a gate, not more implementation. It exists to stop weak bundles from being treated as executable and to stop weak execution proof from being treated as completion.

## GPT-5.5 Gate Posture

- Decide the gate result from evidence, not from how much process text exists.
- Prefer a short `Pass`, `Fail`, or `Blocked` decision with concrete repairs over a long restatement of the bundle.
- If evidence is missing, name the missing artifact, command, row, source input, or prerequisite. Do not infer completion from intention.
- After compaction or resume, reread the bundle files and validator output before passing a gate from memory.

## Stages

- `Readiness gate` after preparation or bundle repair
- `Re-entry gate` when reopening an older bundle whose source references, dependency map, or proof rules may be stale
- `Final closure gate` before the bundle is marked finished

## Required Flow

1. Identify the bundle root and profile.
2. Read the raw inputs, root `README.md`, `plan/01-phase-plan.md`, `traceability`, `reviews/00-bundle-self-review.md`, and `reviews/01-execution-report.md`.
3. Run `scripts/validate_bundle.py --stage prepared` for readiness or re-entry validation.
4. Run `scripts/validate_bundle.py --stage completed` for final closure validation; this includes structural checks, proof manifest checks, and proof-depth checks for critical semantic adequacy evidence.
5. Audit input coverage:
   - every raw note or artifact is preserved
   - every raw note or artifact maps to a bundle destination and an owning subbundle, or has an explicit exception
   - literal language such as `all`, `every`, `must`, or `same flow` was not silently narrowed
6. Audit the dependency model:
   - `plan/01-phase-plan.md` has a usable mermaid dependency map
   - critical foundations are explicitly labeled
   - phase gates are stated clearly enough that another agent would know when to stop or reopen
7. Audit the proof contract:
   - every subbundle has prerequisites, dependency impact, validation depth, and progression gate sections
   - UI-relevant subbundles require real Playwright MCP proof plus screenshot review
   - critical foundations require deeper validation before dependent phases may continue
   - critical subbundles require a Semantic Adequacy Gate covering shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure
   - critical subbundles require `proof/SBxx/manifest.md` plus `proof/SBxx/semantic-invariants.md` or `.json` with existing transcript, hash, source-assertion, invariant, and anti-stub artifact paths
   - critical proof for a new production signal, state, record, or event includes a `## Production Behavior Artifact Matrix` in both the manifest and semantic invariant contract, with producer, consumer, lifecycle, and negative-test citations
   - critical manifests contain portable `repo://` or `bundle://` references and are not tied only to one machine's absolute path layout
8. At final closure, audit shipped proof:
   - no executed subbundle remains `Ready` or `In progress`
   - execution report gate rows and browser analytics rows are populated and no longer pending
   - raw note closure rows are no longer pending
   - weak proof is treated as a reopen condition, not a residual-risk paragraph
   - critical production-path process E2E proof contains automation-dispatch execution runs, tool receipts, current-run artifact lineage, and provider usage observations when provider calls occurred
   - completed critical subbundles have semantic proof that rejects template-only output, fixture-specific behavior, filled-table-only evidence, and status/count-only tests
   - completed critical subbundles have proof manifests whose referenced paths exist
   - behavior-changing critical subbundles have failing-first and passing transcripts
   - skill or validator changes include active Codex skill-root synchronization proof with portable repo-skill references and repo/active SHA-256 hashes
   - final closure has a red-team or verifier artifact that audits fake-proof resistance across all critical subbundles
9. If any gate fails, do not mark the bundle ready or complete. Repair the bundle or reopen the affected subbundle and rerun the gate.

## Rules

- This skill is a gatekeeper, not an implementation shortcut.
- Do not convert weak proof into `good enough`.
- Do not accept a bundle whose dependency map is decorative instead of operational.
- Do not pass the final closure gate when later proof already showed an earlier critical foundation is shaky.
- Do not replace UI proof with reasoning when the request depends on actual rendered behavior.
- Do not pass a readiness or final closure gate for a critical subbundle whose proof only demonstrates file existence, table completion, non-empty strings, diagnostic template markers, or happy-path fixture output.
- Do not pass a final closure gate from prose-only proof. Missing proof manifests, missing semantic invariant contracts, missing transcript files, missing changed-file hashes, machine-specific-only proof paths, or absent red-team closure artifacts are failures for critical work.
- Do not pass production-path process E2E proof that used manual transitions with automation dispatch suppressed, harness-generated product source, empty execution-run lists, detached tool receipts, or missing provider usage observations for provider-backed agent runs.

## Semantic Proof Failure Rule

For critical work, the gate fails when any of these are true:

- the shallow implementation that caused the bundle still passes the stated proof
- no adversarial negative case proves harmful behavior is rejected
- no semantic positive case proves realistic intended behavior
- a production-only signal, state, record, or event is proved only by enum/contract definitions, consumers, or manually seeded positive tests instead of a production producer and lifecycle path
- dream synthesis positive proof accepts diagnostic template text such as `Conclusion: ... supported by N source-backed observation(s)` as shipped memory text
- anti-stub audit is absent, incomplete, or admits production `TODO`, `NotImplemented`, template-only output, or fixture-specific branching without a blocker
- raw notes are marked closed without preserving literal scope words such as `all`, `every`, `must`, `exactly`, or `same flow`
- `proof/SBxx/manifest.md` is absent, incomplete, or cites missing artifact paths
- behavior-changing proof has no failing-first transcript or no passing transcript for the same intended behavior
- final bundle proof lacks a red-team or verifier artifact for fake-proof resistance

Use `../candoitall-bundle-execution/references/semantic-adequacy-proof.md` and `../candoitall-bundle-execution/references/artifact-backed-proof-manifest.md` as the audit rubric when evaluating these claims.

## C# Architecture Gate Checks

For C# architecture-heavy bundles, the readiness gate fails unless:

- `architecture/00-csharp-current-state-inventory.md` exists.
- `architecture/01-csharp-boundary-map.md` exists.
- `architecture/02-csharp-dependency-direction.md` exists.
- `architecture/03-csharp-pattern-selection-records.md` exists.
- `architecture/04-csharp-testability-plan.md` exists.
- `plan/architecture-checkpoints.md` exists.
- `reviews/csharp-architecture-gate.md` exists.
- every architecture-relevant subbundle has C# architecture sections
- critical foundation subbundles exist before dependent feature subbundles
- partial-class policy is explicitly stated
- testability proof is planned
- CodeAnalytics MCP evidence is recorded by snapshot id, or the bundle records an explicit MCP-unavailable validation gap

For completed C# architecture-heavy bundles, the final closure gate fails unless:

- architecture gate result is recorded
- changed project references have before/after proof
- old large class shrink or thin-facade proof is recorded
- no new partial class was added without policy justification
- extracted behavior has isolated unit tests
- composition smoke exists when registration changed
- pattern selection records match implementation
- unresolved bridges have follow-up subbundles
- CodeAnalytics dependency or findings proof was refreshed when project references, large classes, providers, tools, drivers, memory protocols, or runtime composition changed

## References

- Read [references/readiness-and-closure-checks.md](references/readiness-and-closure-checks.md) for the audit checklist.
- Read [../candoitall-bundle-execution/references/semantic-adequacy-proof.md](../candoitall-bundle-execution/references/semantic-adequacy-proof.md) before passing final closure for critical work.
- Read [../candoitall-bundle-execution/references/artifact-backed-proof-manifest.md](../candoitall-bundle-execution/references/artifact-backed-proof-manifest.md) before accepting critical proof manifests or red-team closure artifacts.
- Use `candoitall-csharp-architecture-bundle-guard`, `csharp-architecture-review-gate`, and `candoitall-codeanalytics-mcp` when validating C# architecture-heavy bundles.
- Use `candoitall-subbundle-validator` for per-subbundle entry and closure gates.
- Use `scripts/validate_bundle.py` as the automation-backed baseline for structural and proof-depth checks, then finish the manual audit before passing the gate.

## Exit Condition

The gate passes only when the bundle structure, dependency model, input coverage, and proof quality are strong enough that execution or closure can proceed without guesswork.
