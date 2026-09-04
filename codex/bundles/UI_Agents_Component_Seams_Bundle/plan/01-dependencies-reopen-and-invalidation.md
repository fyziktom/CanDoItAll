# Dependencies, reopen triggers, and invalidation

## Invalidation keys

| Key | Owned by | Invalidates |
|---|---|---|
| `branch-head` | SB01 | all later proof |
| `shared-base-version` | SB01 | whole bundle |
| `agents-workspace-state-contract` | SB02 | SB03–SB07 |
| `overview-query-contract` | SB02 | page/component tests and closure |
| `catalog-state-intent-contract` | SB03 | SB04–SB07 page composition proof |
| `details-section-session-contract` | SB04 | SB05–SB07 |
| `editor-controller-contract` | SB05 | SB06–SB07 |
| `test-public-seam-baseline` | SB06 | SB07 |
| `agentframework-ui-di` | SB02/SB03/SB05 | final stable/host proof |

## Reopen triggers

Reopen the owning phase when:

- branch movement changes one of the primary components/tests;
- URL compatibility output changes;
- catalog or editor state is owned in both page and child;
- a controller starts storing UI presentation or navigation state;
- a fourth production interface becomes necessary;
- partial provider/secret/project error behavior changes;
- tests still require private reflection or full runtime construction;
- a later routing design requires child components to know query keys;
- physical extraction later reveals a hidden dependency or cycle.

## Downstream trust rule

Do not “patch around” an invalidated foundation in a later subbundle. Reopen the earliest
owner, repair its contract/proof, rerun every dependent focused gate, and record the
revalidation chain.
