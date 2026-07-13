# SB02 Proof Manifest

## Subbundle

- Subbundle: `SB02`
- Status: `Completed`
- Owned requirement: MAF runtime capability access must support scoped deny, allow-only, required capability, and provider-key filtering without moving process logic into MAF.
- Test name: `Evaluate_DenyAllPolicy_AllowsOnlyExplicitAllowRuleMatches`

## Changed Files And Hashes

| File | SHA-256 |
|---|---:|
| `repo://src/MAF/Capabilities/CanDoItAll.AgentFramework.Capabilities.Abstractions/CapabilityModels.cs` | `0EC96E005CB76110713B2DB9410738D2D9C7AE25A25CCAF0805E23F7117D4B41` |

## Proof Artifacts

- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`
- Failing-first transcript: `bundle://proof/SB02/transcripts/adversarial-negative.txt`
- Passing transcript: `bundle://proof/SB02/transcripts/passing.txt`
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/anti-stub.txt`
- Source assertion: `repo://src/MAF/Capabilities/CanDoItAll.AgentFramework.Capabilities.Abstractions/CapabilityModels.cs`
- Source assertion: `repo://src/MAF/Capabilities/CanDoItAll.AgentFramework.Capabilities.Access/CapabilityAccessPolicyEvaluator.cs`
- Source assertion: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeToolProviderComposer.cs`

## Closure

- Failing-first: `bundle://proof/SB02/transcripts/adversarial-negative.txt` records that the scoped override path does not hard-code allow as the default effect.
- Semantic positive proof: `bundle://proof/SB02/transcripts/passing.txt` records evaluator and runtime provider filtering tests.
- Anti-stub audit: `bundle://proof/SB02/transcripts/anti-stub.txt` records no placeholder implementation in the scoped capability path.
