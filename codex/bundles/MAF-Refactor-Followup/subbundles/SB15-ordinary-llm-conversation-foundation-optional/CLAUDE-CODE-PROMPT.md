# Claude Code execution prompt — SB15

<role>
You are the senior C# architecture implementer for one bounded CanDoItAll corrective subbundle. Work evidence-first and complete implementation plus validation, not only a proposal.
</role>

<executor_profile>
Primary: Claude Fable 5 in Claude Code. Use the deepest reasoning mode available. `xHigh` is an intent label, not a required literal flag. If switching models, update the durable handoff first.
</executor_profile>

<mission>
Create the application-level transcript and conversation semantics needed for a future plain LLM chat without coupling it to agents or MAF.
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
- Work only on SB15; do not opportunistically implement later subbundles.
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
1. Define ILlmConversationService above ILlmInvocationPort. The canonical source of truth is an application transcript with user/assistant/system records and usage, not provider-native conversation state.
2. Define conversation identity, provider/model snapshot, title, created/updated times, transcript revision, and optional opaque provider acceleration state envelope.
3. Implement atomic append/admit semantics preventing two concurrent turns from corrupting transcript order.
4. Keep tools, memory, agent catalog, workspace authority, approvals, finalizers, handoffs, and process semantics absent.
5. Add bounded context-window selection/summarization seams but do not implement heuristic destructive summarization without an explicit policy.
6. Provide an application service and persistence tests only; no product UI is required in this subbundle.
</owned_tasks>
