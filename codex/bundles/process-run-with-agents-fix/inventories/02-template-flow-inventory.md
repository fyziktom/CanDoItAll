# Template Flow Inventory

## `software-delivery`

| Step | Kind | Dependencies | Branches | Primary automated fit |
| --- | --- | --- | --- | --- |
| `feature-intake` | Start | none | none | Product owner mock can produce scope. |
| `architecture-review` | Review | `feature-intake` | none | Architect mock role key does not match `solution-architect`. |
| `implementation` | Work | `feature-intake`, `architecture-review` | none | Developer mock role key does not match `lead-engineer`. |
| `peer-review` | Review | `architecture-review`, `implementation` | none | No dedicated mock peer reviewer. |
| `qa-validation` | Review | `implementation`, `architecture-review`, `peer-review` | none | QA mock role key does not match `qa-lead`; no reject branch. |
| `security-review` | Approval | `implementation`, `architecture-review`, `peer-review` | none | No current mock security reviewer. |
| `release-approval` | Approval | implementation, architecture, QA, security | none | No explicit mock approval branch. |
| `execute-release-rollout` | Delivery | `release-approval` | none | Release manager mock can write release notes but artifact names must align. |
| `post-release-learning` | End | `execute-release-rollout` | none | No current mock retrospective role. |

## `ai-assisted-change-delivery`

| Step | Kind | Dependencies | Branches | Fit for requested calculator loop |
| --- | --- | --- | --- |
| `task-intake` | Start | none | none | Product owner mock can fit conceptually. |
| `delegation-design` | Decision | `task-intake` | `delegate`, `human-only` | Requires AI-safety/model-risk roles not present in current mock catalog. |
| `agent-execution` | Work | `delegation-design:delegate` | none | Developer mock role key does not match `software-engineer`. |
| `evaluation-and-benchmarking` | Review | `agent-execution`, `delegation-design:delegate` | none | QA mock can fit conceptually, but role key differs. |
| `safety-and-security-review` | Approval | `evaluation-and-benchmarking`, `delegation-design:delegate` | `approved`, `rework` | `approved` matches QA approval key, but `rework` does not match `repairs-required`; rework does not loop back to repair. |
| `controlled-merge-and-learning` | End | `safety-and-security-review:approved` | none | Release/closure equivalent, but no repair loop. |
| `manual-delivery-handoff` | End | `delegation-design:human-only` | none | Not part of mock calculator E2E. |
| `capture-rework-decision` | End | `safety-and-security-review:rework` | none | Stops at rework capture instead of repair/recheck. |

## Conclusion

Neither inspected template is currently suitable as-is for the requested deterministic QA rejection and repair iteration. The implementation bundle should introduce a dedicated calculator mock process fixture/template first, then decide whether to generalize the pattern into the public template pack.
