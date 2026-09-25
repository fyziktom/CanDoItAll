# Codex execution prompt — MAF 1.22 and trustworthy process recovery

You are the senior C# architect and implementation owner for this change. Work at xhigh reasoning effort. Deliver working code and trustworthy validation, not just a proposal or a package-version edit.

## Mission and discretion

On the user's current branch derived from `fyziktom/CanDoItAll` development, upgrade the actual Microsoft Agent Framework dependency graph to **1.22.0** (matching published preview packages where already used). Resolve the applicable 1.21/1.22 breaking changes, repair the documented code defects and additional clearly related defects you uncover, and verify agents, project chats, workflows and processes end to end.

You may redesign local implementation details, improve this brief, choose commit granularity, and remove obsolete workarounds when replacement behavior is proven. Do not follow historical workflow bundles mechanically. The requirements and evidence gates below are mandatory; prescribed class names and micro-steps are intentionally absent. Keep scope focused: this is not a new provider, UI rewrite, authorization redesign, or broad architecture refactor.

Read all numbered documents in this pack, the source index as needed, and the repository's current `AGENTS.md`, `.github/copilot-instructions.md`, `docs/testing.md`, `.github/workflows/ci.yml`, and applicable shared standards/skills. If current authoritative instructions or code differ, reconcile them explicitly instead of copying stale assumptions.

## Establish the actual starting point

The audit SHA `ffa83cf903c305a7490a674f41a0d08498156db6` is provenance only. Do not check it out or reset to it. Record the current branch, starting HEAD, worktree status, dependency versions and sibling revisions. Preserve user changes. If the user already prepared a branch, use it. Otherwise create a suitable work branch from the current development baseline without changing shared branches.

The audited baseline is MAF 1.20.0, .NET SDK 10.0.302 / net10.0, PostgreSQL 18, with source dependencies on Components and FileTools. Recheck these facts. Use current test infrastructure rather than resurrecting pre-refactor paths, SQLite application fixtures, or old package-only builds.

## Required engineering outcomes

- Reconcile all used MAF package references and their transitive/direct dependency floors. Fix downgrade conflicts rather than suppressing restore warnings. Verify the evaluated graph in production and test projects, not only the central properties file.
- Treat **approval continuation and retained native session state as the highest-risk migration boundary**. Version 1.22 authorizes against requests actually surfaced and recorded by the framework. Preserve native checkpoints through supported serialization, the durable tool journal, exact authority, immutable tool intent, and at-most-once effect handling. Never invent native pending state or disable binding to make a regression pass.
- Assess every row in the migration matrix as Applicable / Not applicable with evidence. Existing imperative workflows and application-owned MCP clients are not automatically the declarative/Foundry APIs mentioned in release notes. Do not add unused integrations just to exercise them.
- Reproduce and fix findings F01–F03, or close any genuinely unreachable/non-defective case only with precise current-source reasoning and protective tests. Address F04's version-compatibility risk and F05's maintained-documentation drift. A passing compile is not proof of session or approval compatibility.
- Investigate unnecessary process escalation. Distinguish manager-controlled automatic recovery from an actual human escalation. Use typed cause and effect evidence, bounded recovery and authoritative artifacts; preserve cancellation, lease identity, child-work reuse and required finalization. A package upgrade alone is not evidence that escalation improved.
- Implement any minimal UI/API changes required to represent the corrected behavior: pending/consumed approval, incompatible restored state, automatic retry versus human intervention, cancellation, durable terminal results and recoverable errors. Preserve current module/component seams and existing API/SSR security behavior.

## Mandatory validation order

**First use `candoitall_codeanalytics` MCP.** Discover its actual schemas and read the current shared CodeAnalytics skill. Use scoped architecture/symbol/reference queries to identify the relevant test owners before editing. After the first real change, and as the diff grows, call `code_analytics_impacted_tests_get` with the actual changed paths/ranges and all relevant test workspaces. Do not present inspected-but-unchanged files as a diff. Keep returned required/conditional scopes, broadening reasons and promotion triggers as evidence.

During development, build affected production/test projects and run the selected regressions and required owner slices. Validate discovery counts; zero tests is not success. Start with conservative behavior intent. This dependency/serialization migration is not automatically behavior-preserving. Where analysis legitimately requires broad suites, keep that requirement pending for final closure rather than silently narrowing it or repeatedly running the entire suite while editing.

At a stable final checkpoint, run the required complete **Windows and actual Linux** suites. In this repository, a green filtered Stable CI lane is not a green full suite: the documented full gate requires unfiltered Stable and unfiltered Playwright runs. **Run standard non-browser proof first. Browser/UI execution is forbidden while required standard tests are failing or unavailable.** See the validation plan for the platform matrix, PostgreSQL 18, portability/static gates, and truthful quarantine reporting.

After standard proof is green, run browser coverage and the acceptance journeys: multi-turn agent chat over a real project, approval/denial/restart, a simple agent-backed process with an artifact, a recoverable process failure, a simple workflow, workflow-in-process where supported, and cancellation. Use the real application and persistence. Deterministic provider-boundary doubles are useful for regressions but do not replace the separately labeled live-provider journey. Never manipulate persisted terminal state to manufacture UI success.

If UI testing exposes a code defect, fix it, rerun impacted standard tests, and invalidate/repeat affected final proof before acceptance. Do not finish with stale platform results from an earlier source or dependency graph.

## Non-negotiable safeguards

Do not widen auto-approval, disable safety checks, silently replay effects, increase retry budgets to hide defects, accept prose as a process artifact, enable parallel mutable tools globally, or loosen timeout/assertion/filter logic just to turn tests green. Do not add per-user/machine settings or secrets to the repository. Use disposable test databases/workspaces and bounded provider budgets; never use a retained customer database as a fixture.

Keep implementation, UI strings, comments and maintained documentation in English. Follow current shared C# and UI conventions. Preserve signed-commit configuration; never disable signing or expose signing material. Use local commits when authorized by the repository/user workflow. Do not merge, push, open a PR, publish packages, or trigger a destructive deployment merely because this brief exists.

## Completion and reporting

Keep a compact decision/finding/evidence ledger rather than recreating a rigid bundle framework. Map each requirement and confirmed fix to a test or journey and its actual result. Record fresh source/dependency revisions, platforms, discovery counts, commands, durations, failures, skips, retries, and redacted logs/traces. Report the process scenario outcomes, not a speculative percentage improvement.

Finish with the delivery report in this pack. Clearly distinguish Passed, Failed, Blocked, Not run, and Not applicable. Missing Linux, live-provider, full-suite or UI proof blocks full acceptance; finish all independent work and report the exact missing proof without claiming completion. A justified false-positive finding may be closed with evidence, but a known reproducible defect in scope must be repaired.
