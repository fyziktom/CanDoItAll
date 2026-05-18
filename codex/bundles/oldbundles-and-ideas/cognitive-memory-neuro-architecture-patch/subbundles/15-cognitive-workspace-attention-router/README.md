# 15 Cognitive Workspace And Attention Router

## Objective

Add architecture for active working memory frames and explainable attention routing.

## Inputs

- `architecture/17-neuro-cognitive-integration-layer.md`
- `architecture/18-cognitive-workspace-and-attention-router.md`
- Existing recall, probing, and MAF integration docs.

## Deliverables

- Workspace frame model.
- Focus slot and inhibited candidate model.
- Attention decision model.
- Recall trace extensions.
- Probe-session workspace rules.
- MAF context contribution guidance.

## Required Architecture Updates

- Recall starts from attention/workspace context.
- Probe sessions attach to a workspace frame.
- Context packs are rendered outputs, not working memory itself.
- Inhibited candidates are stored with reasons.
- Attention decisions are auditable.

## Acceptance Criteria

- Trace shows why a candidate was selected or inhibited.
- Ambiguous query can route to clarification instead of forced recall.
- Weak topic can route to probe before learning.
- High-risk unsupported procedure can route to review/abstention.

## Tests To Add Later

- workspace lifecycle,
- focus/inhibition,
- attention decision explanation,
- context budget handling,
- probe-workspace integration.
