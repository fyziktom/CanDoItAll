# SB02 Semantic Invariants

## Invariant MAF-SB02-SCOPED-CAPABILITY

- Invariant ID: `MAF-SB02-SCOPED-CAPABILITY`
- Source raw note: a process step must be able to suppress tools, skills, and MCPs without changing the agent's baseline settings.
- Expected behavior: deny rules win, allow-only scopes use default-deny, required rules are preserved, and runtime provider tools receive provider-key tags that can be filtered.
- Disallowed shallow implementation: hiding a tool name in a prompt while still sending the denied capability descriptor to the agent context.
- Failing-first test: `bundle://proof/SB02/transcripts/adversarial-negative.txt` proves the implementation does not keep a hard-coded allow default in the scoped override path.
- Passing test: `Evaluate_DenyAllPolicy_AllowsOnlyExplicitAllowRuleMatches` in `bundle://proof/SB02/transcripts/passing.txt`.
- Changed source files: `repo://src/MAF/Capabilities/CanDoItAll.AgentFramework.Capabilities.Abstractions/CapabilityModels.cs` with hash `0EC96E005CB76110713B2DB9410738D2D9C7AE25A25CCAF0805E23F7117D4B41`.
- Production assertions: `CapabilityAccessPolicyEvaluator` applies deny precedence and `RuntimeToolProviderComposer` tags runtime provider tools before policy pruning.
- Red-team negative case: an allow-only provider-key policy must prune tools from other runtime providers.
- Downstream dependency check: SB04 consumes this as `AgentRuntimeCapabilityScopeOverride` without adding a process dependency to MAF.
