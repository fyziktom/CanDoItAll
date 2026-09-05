# Lifecycle follow-up and provider sequencing

Accepted owner input: review of d3ba280a431bfe74ce03a72638ac06dff47de660, followed by authorization to repair the two lifecycle gaps and prepare/implement/test the next state/read provider child.

Shared changes: distinguish task-slot ownership from snapshot generation/loading; propagate actual session tokens to every direct nested dialog; allow one per-panel typed session without a duplicate page store; fence removed targets after catalog refresh. The program sequence now follows the owner's Providers-01, Providers-02, small catalog sandbox, then standalone history order.

Evidence: Agents SB09 eight failing-first cases pass; provider session/read adapter 27 direct Unit cases pass; all newly added provider rendering cases and existing bounded behavior coverage pass after documented reruns. Final portability and real browser checks pass. The broader stable checkpoint executed 9462 cases; one workflow timeout passed unchanged on rerun. The 118-log historical documentation gate remains blocked. Current counts, source hashes, limitations and invalidation are in [the child closure](../../UI_Providers_01_Component_Seams_Bundle/reviews/closure.md). This reference itself executes no product work.
