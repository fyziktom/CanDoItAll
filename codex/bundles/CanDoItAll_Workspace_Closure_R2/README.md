# Workspace closure R2: deletion, navigation and reproducible delivery

Read [prompt.md](prompt.md) first. This is an executable repair and verification handoff for
Codex GPT-6 Astra Max, not the next feature extraction. It uses the unchanged
[shared v3 foundation](shared/README.md).

Workspace's rendering boundary is structurally complete. Two reproduced application
acceptance failures remain: WCL-DEL1 (Agent deletion after completed runs) and WCL-NAV1
(navigation acknowledgement/circuit retirement). Their exact causes are not yet established.
They are not safely classifiable as minor cosmetic carry-over. The repaired Components
revision was not retrievable remotely during this review. Do not restart completed extractions.

## Read order

1. [Review](REVIEW.md), [Workspace status](WORKSPACE_STATUS.md), and [test evidence](TEST_RESULTS.md).
2. [Agent deletion repair](FIX_AGENT_DELETION.md), [navigation repair](FIX_NAVIGATION.md),
   and [dependency delivery](DEPENDENCY_DELIVERY.md).
3. [Validation matrix](VALIDATION_MATRIX.md), [application journeys](REGRESSION_JOURNEYS.md),
   [safe execution](SAFE_EXECUTION.md), and [closure semantics](CLOSURE.md).
4. [Remaining module roadmap](REMAINING_MODULES.md) is planning only, not execution scope.

The reviewed application is `15eadc18932e77a20ae5be2d7f2b207700a15d25`. This identifies
source evidence; it never instructs a reset, checkout, rebase or cherry-pick.

The previous 35 campaign groups remain present. Three new groups cover DEL1, NAV1 and
current module documentation. Blank result templates intentionally contain no passes.
Local package tests validate bookkeeping, not product behavior, source authenticity or
permission to spend money. No new real-model request is authorized by this package.
