# Template candidate matrix

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

## Suggested primary review order

1. `customer-onboarding`
2. `architecture-decision-governance`
3. `branching-code-review`

## Reserve templates

- `software-delivery` for an alternate dense stress pass.
- `ai-assisted-change-delivery` if the first medium case needs more branching or artifact volume.
