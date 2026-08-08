# Claude Code execution prompt — SB11

<role>
You are the senior C# architecture implementer for one bounded CanDoItAll corrective subbundle. Work evidence-first and complete implementation plus validation, not only a proposal.
</role>

<executor_profile>
Primary: Claude Fable 5 in Claude Code. Use the deepest reasoning mode available. `xHigh` is an intent label, not a required literal flag. If switching models, update the durable handoff first.
</executor_profile>

<mission>
Expose the application-owned proposal model to users and bound all in-memory continuation state without weakening fail-closed behavior.
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
- Work only on SB11; do not opportunistically implement later subbundles.
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
1. Render each pending proposal with tool name, classification, safe details, target scope/resource summary, and independent approve/reject choice.
2. Submit IReadOnlyList<PendingToolApprovalDecision> from UI and add a decision-list HTTP endpoint or request version. Keep the bool API only as a compatibility mapper that is clearly documented as all-proposals.
3. Require exact coverage through AgentApprovalDecisionMismatchException and preserve the original proposal arguments hash/binding.
4. Add bounded TTL/size and durable-run reconciliation to the MAF approval cache; prefer reconstruction from persisted compatible session state over process-lifetime cache authority.
5. Add an abandoned WaitingOnTool reconciliation/expiry policy that can release turn-context lease capacity without auto-approving, replaying, or losing audit evidence.
6. Resolve the explicit lease-token test conflict. Recommended default: AutoApprovedNonInteractive must not expose explicit project lease tokens; scripted harnesses should model GovernedProcessAutomation when tokens are required.
7. Add multi-proposal mixed-decision component and API integration tests.
</owned_tasks>
