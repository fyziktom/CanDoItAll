# Scoring, Confidence, And Audit Design

## Design goal

Every grouping decision should be:
- explainable,
- reproducible,
- triageable.

## Composite score model

Recommended score sources:

### Strong structured features
- same manual group override
- exact strict composer match
- exact primary catalog system/value match
- same work number
- same movement number
- same normalized key signature

### Medium textual features
- title token Jaccard / Dice similarity
- strict title equality
- loose title containment
- work-type match
- composer initials alignment

### Negative / conflict features
- composer conflict
- catalog conflict
- movement conflict
- arrangement/original conflict
- excerpt/full-work conflict
- key conflict when title is otherwise too generic

### Embedding features
- work embedding cosine similarity
- optional title-only embedding cosine for missing composer cases

## Confidence bands

Recommended bands:

- `Definite`
  - safe auto-apply
- `High`
  - auto-apply only if no cluster conflict
- `Review`
  - human validation required
- `Low`
  - do not apply
- `Rejected`
  - store only if useful for audit/debug

## Suggested first threshold profile

These numbers are placeholders to tune on real copied data.

- `Definite`
  - deterministic exact rule set satisfied
  - or composite >= 0.96 with no conflicts
- `High`
  - composite 0.90–0.959 with no major conflicts
- `Review`
  - composite 0.78–0.899 or any flagged ambiguity
- `Low`
  - composite 0.65–0.779
- `Rejected`
  - below 0.65 or hard conflict

Important:
- Codex should not bake these into invisible magic constants.
- Put threshold profiles into explicit config or well-named constants.

## Suggested weight pattern

Example starting weights:

- manual override exact match: +1.00 and short-circuit
- strict composer exact: +0.22
- loose composer alias: +0.12
- catalog exact: +0.28
- work number exact: +0.08
- movement exact: +0.10
- movement conflict: -0.18
- arrangement conflict: -0.22
- key exact: +0.05
- key conflict: -0.06
- strict title exact: +0.18
- loose token similarity: +0.10 to +0.20
- work embedding cosine contribution: +0.00 to +0.22 scaled
- description auxiliary contribution: 0 in phase 1 or very small

This is intentionally conservative.
Strong structure should dominate generic semantic similarity.

## Cluster-level guardrails

Even if pair scores are good, a cluster can still be bad.

Required cluster checks:
- does the cluster contain conflicting composers?
- does it contain conflicting primary catalog identities?
- does it mix arrangement and non-arrangement unexpectedly?
- does it mix movement and full-work records beyond allowed policy?
- is cluster diameter too large?
- is group size suspiciously large?

Recommended policy:
- if a cluster fails guardrails, downgrade the entire cluster or split it before application.

## Representative/canonical member selection

Use this order:

1. manual canonical group values if present
2. manual representative member
3. member with highest metadata quality
4. most common structured normalized form
5. stable lexical fallback

Metadata quality ranking signals:
- reviewed
- export-ready / selected
- has composer
- has full title
- has catalog tokens
- has MXL
- higher rating count / views only as weak tie-breakers

## Explanation model

Store both:
- short reason summary
- detailed JSON evidence

Example summary:
- `Exact composer + opus/number match + high title similarity`

Example evidence JSON sketch:

```json
{
  "pair": {
    "leftIndexedScoreId": 101,
    "rightIndexedScoreId": 245
  },
  "signals": {
    "composerStrictExact": true,
    "composerAliasMatch": false,
    "catalogSystem": "opus",
    "catalogValueMatch": true,
    "workNumberMatch": true,
    "movementNumberMatch": null,
    "keyMatch": true,
    "arrangementConflict": false,
    "titleTokenJaccard": 0.92,
    "embeddingCosine": 0.9481
  },
  "score": {
    "composite": 0.973,
    "band": "Definite"
  },
  "policyFlags": {
    "manualOverrideApplied": false,
    "needsHumanReview": false
  }
}
```

## Reasons should be visible in UI

Minimum display requirement:
- “Why is this in this group?” panel on score detail and group detail

Good concise evidence lines:
- exact canonical composer match
- same catalog identity `op. 27 no. 2`
- same normalized key `d_flat_major`
- embedding similarity `0.948`
- no arrangement conflict detected

## Manual corrections and stickiness

When a reviewer manually:
- adds a member,
- removes a member,
- changes canonical group title,
- merges or splits groups,

the system should:
- store the action source as manual,
- protect it from future silent overwrite,
- still allow future proposals against it in a review queue.

## Recommended group states

- `AutoDraft`
- `NeedsReview`
- `Curated`
- `Locked`
- `Deprecated`

This helps UI and rollout.

## Important anti-chaining rule

Do not treat “connected by any review-band edge” as enough to merge clusters.
Only strong edges should drive auto-clustering.
Review-band edges are for human review or cluster explanation, not blind merge.

## Phase 1 scoring philosophy

Bias phase 1 toward:
- lower false positives,
- higher manual-review volume.

False negatives are cheaper to fix than false positive group pollution.
