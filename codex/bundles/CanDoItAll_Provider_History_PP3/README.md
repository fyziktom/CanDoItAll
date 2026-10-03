# Provider History PP3 — complete the existing history UI boundary

**Sequence:** bounded Tooltip follow-up → complete Request History → native and multi-instance validation.
**Target:** Codex GPT-6 Astra Max. This is an implementation handoff, not a request for another plan.

Read [prompt.md](prompt.md), then its required documents. Preserve the completed PP1/PP2,
Agent Editor, Projects and Workspace boundaries. Do not redo shared-provider publication or
move runtime ownership. The review found no additional blocker in the inspected PP2 paths;
its final report is qualified, not an application-wide release certificate.

The next slice covers the global and single-provider History workspace, all filters, applied-query
results/pagination, metadata/details and explicitly authorized bounded content. Reuse the existing
History renderers in AgentFramework.UI. Prefer completing that family rather than creating a
parallel implementation or an unnecessary project. A representative independent sandbox is required.

[PP2 review](PP2_IMPLEMENTATION_REVIEW.md) · [Tooltip prerequisite](S0_TOOLTIP_LIFETIME.md) ·
[PP3 scope](PP3_SCOPE_AND_ARCHITECTURE.md) · [Source review](HISTORY_SOURCE_REVIEW.md) ·
[Validation](VALIDATION_MATRIX.md) · [Application journeys](APPLICATION_JOURNEYS.md) ·
[Multi-instance runbook](MULTI_INSTANCE_HISTORY_RUNBOOK.md) · [Roadmap](ROADMAP.md) ·
[Česká revize](REVIEW.cs.md).

The source review uses main `components-decoupling` at `643a295e112ca29835323501907bf1d8920a5945`
and Components `development` at `4a858412d2c2a3f6123bf23d8c4584f05b47627d`.
These are provenance, never a reset/checkout instruction. Operate on the supplied current checkout.

All 22 files of [shared v3](shared/README.md) are retained verbatim. Its dated audit is historical.
Current repository instructions win; this handoff adds the authorized slice, desktop restriction,
signed checkpoints and proof requirements. No product build/test was run while preparing it.

Validate the extracted handoff with `python tools/validate_handoff.py`.
That validates guidance/evidence structure, not the product. All product groups start `NOT_RUN`.
