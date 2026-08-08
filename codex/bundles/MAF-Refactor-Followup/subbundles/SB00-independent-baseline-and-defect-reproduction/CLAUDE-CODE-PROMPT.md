# Claude Code execution prompt — SB00

<role>
You are the senior C# architecture implementer for one bounded CanDoItAll corrective subbundle. Work evidence-first and complete implementation plus validation, not only a proposal.
</role>

<executor_profile>
Primary: Claude Fable 5 in Claude Code. Use the deepest reasoning mode available. `xHigh` is an intent label, not a required literal flag. If switching models, update the durable handoff first.
</executor_profile>

<mission>
Independently establish the maf-refactor branch state and reproduce every merge-blocking review finding before production fixes begin.
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
- Work only on SB00; do not opportunistically implement later subbundles.
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
1. Verify the exact branch head and compare it with development; stop if HEAD differs from the bundle baseline until the evidence map is refreshed.
2. Build CanDoItAll.slnx in Release and run Unit, Components, and Integration projects without expanding any accepted-failure list.
3. Create a focused CodeAnalytics snapshot covering AgentFramework Core, Runtime.Abstractions, MAF, LLM, Workflows, Modules.AgentFramework, Modules.Processes, Workbench, Security, and tests.
4. Add failing characterization tests for FR-001 through FR-006: authority permissions not consumed, unknown-source scope grant, project-turn recovery using base scope, script inspection using base scope, envelope-wrapped conversationId, and fingerprint-policy mismatch.
5. Add lifetime probes that prove how many LocalWorkspaceProcessHost and WorkspaceRuntimeServices instances are created/disposed per profile workspace.
6. Reproduce the explicit project-lease test conflict and record which production purpose each test is actually modeling.
7. Produce a baseline test/failure inventory with each failure categorized as pre-existing, refactor regression, environment-only, or unresolved.
</owned_tasks>
