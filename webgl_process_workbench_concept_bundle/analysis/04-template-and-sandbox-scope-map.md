# Template and sandbox scope map

## Template complexity sample

| Template key | Steps | Branch steps | Dependencies | Artifacts | Recommended role |
| --- | --- | --- | --- | --- | --- |
| branching-code-review | 13 | 1 | 16 | 13 | Dense |
| software-delivery | 9 | 0 | 17 | 11 | Dense |
| ai-assisted-change-delivery | 8 | 2 | 9 | 14 | Medium |
| hotfix-rollout | 7 | 0 | 7 | 7 | Simple |
| release-readiness-and-deployment | 7 | 1 | 6 | 12 | Medium |
| architecture-decision-governance | 6 | 1 | 5 | 9 | Medium |
| oss-intake-supply-chain-governance | 6 | 1 | 5 | 11 | Medium |
| customer-onboarding | 3 | 0 | 2 | 3 | Simple |
| incident-response | 3 | 0 | 2 | 3 | Simple |

## Recommended representative concept set

| Template | Role in proof | Reason |
| --- | --- | --- |
| `customer-onboarding` | Simple | Tiny sanity case that exposes camera and label defaults quickly. |
| `architecture-decision-governance` | Medium | Moderate branching and governance semantics without maximum density. |
| `branching-code-review` | Dense | Best stress case for routing, overlap, and authoring proof. |

## Why not use only one template

A single small template could make the concept look artificially successful. The concept must show:

- a sparse scene,
- a moderate branched scene,
- a dense scene where clutter is currently a real problem.

## Sandbox-scope implication

The dedicated sandbox route should allow:

- template switching,
- camera preset switching,
- reset to original projection,
- screenshot/export capture,
- move/connect proof on at least the medium or dense template.
