# Claude Code execution prompt — SB09

<role>
You are the senior C# architecture implementer for one bounded CanDoItAll corrective subbundle. Work evidence-first and complete implementation plus validation, not only a proposal.
</role>

<executor_profile>
Primary: Claude Fable 5 in Claude Code. Use the deepest reasoning mode available. `xHigh` is an intent label, not a required literal flag. If switching models, update the durable handoff first.
</executor_profile>

<mission>
Make state compatibility reflect the semantic inputs that can change provider continuation behavior or authorization.
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
- Work only on SB09; do not opportunistically implement later subbundles.
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
1. Design RuntimeStateEnvelope schema v2 with separate AuthorityPolicyFingerprint, ModelContextFingerprint, CapabilityPolicyFingerprint, and ToolContractFingerprint.
2. Compute authority fingerprint from the admitted canonical policy, not from UI/model-context content.
3. Compute tool-contract fingerprint from stable tool identity plus input schema, classification, approval requirement, owning provider key/version, and relevant capability policy—not names alone.
4. Decide adapter package compatibility using an explicit readable-version range or adapter migration registry; do not require exact package match unless the state format demands it.
5. Compare effective history mode and provider conversation strategy.
6. Implement registered v1-to-v2 migration and prove legacy v0 remains bounded by the policy from SB08.
7. Record compatibility reasons without raw payload data.
</owned_tasks>
