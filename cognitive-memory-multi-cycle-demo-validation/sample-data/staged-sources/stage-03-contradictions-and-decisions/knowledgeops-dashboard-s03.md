# Contradictions and decisions: KnowledgeOps Dashboard

Source package: knowledgeops-dashboard-s03
Project domain: support engineering incident knowledge dashboard
Named owner: Irena Malik, Support Engineering Manager
Intended ingestion: conflict/decision Markdown file, then forced consolidation and review.
Expected consolidation behavior: create reviewable contradiction or decision candidates and keep obsolete claims distinguishable from accepted decisions.

## Conflicts Introduced

- One team wants generated summaries to appear above official runbooks; support leadership rejected that ordering for high severity incidents.
- An old design treated repeated customer reports as duplicates, but the current rule keeps them separate when regions or product tiers differ.
- A proposed auto-close action conflicts with the requirement that incident ownership stays human-controlled.

## Resolution Decision

Rank official current-version runbooks first for high severity incidents, keep generated summaries advisory, and record postmortem learning as review-gated proposals.

## Review Expectations

- The contradiction candidates must show the old claim, the new conflicting claim, and the deciding source.
- The review queue should not silently overwrite earlier memory.
- After approval, recall should prefer the resolved decision while still being able to explain that an older source was superseded.
- If the system produces near-duplicate candidates for the same contradiction, record them in the duplicate analysis sheet and approve only the best source-backed candidate.

## Mindmap

```mermaid
mindmap
  root((KnowledgeOps Dashboard))
    Contradictions and decisions
      Domain: support engineering incident knowledge dashboard
      Owner: Irena Malik, Support Engineering Manager
      Durable facts
        The dashboard combines incident intake, customer impact, runbook lookup, timeline notes, action tracking, and postmortem learning proposals.
        Incident status is canonical; runbook recommendations and memory-backed suggestions are advisory until an operator acts.
        Operators need dense lists, keyboard navigation, status badges, source citations, and visible staleness warnings.
      Updates
        The on-call group added a severity downgrade path for incidents where customer impact is confirmed lower than initially reported.
        Runbook rank must penalize stale runbooks when the service version in the incident differs from the version mentioned in the runbook.
      Decisions
        Rank official current-version runbooks first for high severity incidents, keep generated summaries advisory, and record postmortem learning as review-gated proposals.
```
