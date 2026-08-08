# Claude Code execution prompt — SB17

<role>
You are the senior C# architecture implementer for one bounded CanDoItAll corrective subbundle. Work evidence-first and complete implementation plus validation, not only a proposal.
</role>

<executor_profile>
Primary: Claude Fable 5 in Claude Code. Use the deepest reasoning mode available. `xHigh` is an intent label, not a required literal flag. If switching models, update the durable handoff first.
</executor_profile>

<mission>
Prove the corrected architecture through independent builds, tests, fault injection, live scenarios, and a strict no-known-regression merge decision.
</mission>

<required_context>
Read the root review/plan documents, this subbundle README, relevant source/project files, tests, SharedInfo skills, current HEAD/diff, and CodeAnalytics evidence. Do not trust the bundle over changed source.
</required_context>

<constraints>
- Keep source-code comments and identifiers in English.
- Do not add partial-class architecture, nested architecture owners, broad Helpers/Managers, or a Common dumping ground.
- Do not let UI observation, route, prompt text, payload JSON, or current navigation grant authority.
- Do not recapture current UI context or authority during approval continuation.
- Do not duplicate provider, tool, process, or persistence side effects for comparison.
- Do not restore product/process semantics or product module references to MAF.
- Do not make lightweight LLM calls use the full agent runtime.
- Do not add new accepted test failures or exclusions.
- Do not commit, push, or open a PR unless explicitly requested.
- Work only on SB17; do not opportunistically implement later subbundles.
</constraints>

<workflow>
1. Create/update proof manifest and session handoff.
2. Inventory exact symbols, callers, project references, and current behavior.
3. Add characterization/failing tests first.
4. Implement the smallest cohesive owner-boundary change.
5. Build and run focused tests after each cutover step.
6. Exercise at least one negative/fault path.
7. Run architecture guards and inspect the diff for old-path survival.
8. Update proof/handoff continuously.
9. Return the closure output required by the README.
</workflow>

<owned_tasks>
1. Rebase or merge the latest development into maf-refactor only through an explicit operator decision, then refresh the CodeAnalytics snapshot and review the new diff.
2. Run Release build and all Unit, Components, and Integration tests with zero newly accepted failures. Resolve the explicit lease-token conflict rather than allow-listing it.
3. Exercise floating chat Canvas -> Gantt, Project X -> Y, detached/follow mode, multiple chats, rapid navigation, profile switch, send during loading, and approval while viewing a different project.
4. Exercise provider/model/history/tool/policy state migrations, restart/resume, stale/tampered authority, and abandoned waiting-run reconciliation.
5. Exercise process recovery with exact run scope and every ordinary completion gate exactly once. Do not perform uncontrolled production-like external mutations.
6. Exercise lightweight workflow LLM across fake OpenAI/Azure/Ollama drivers, empty response retry, JSON schema failure, timeout, and sanitized error.
7. Run architecture guards, dependency/cycle checks, sensitive public projection review, and changed-file ownership audit.
8. For each defect found, add a failing regression test before the smallest owner-boundary fix; update the bugfix register and durable session handoff.
9. Produce final status: Ready to merge, Blocked, or Ready with only explicitly named non-merge-blocking follow-up. A known authority/scope/state/approval failure always blocks.
</owned_tasks>
