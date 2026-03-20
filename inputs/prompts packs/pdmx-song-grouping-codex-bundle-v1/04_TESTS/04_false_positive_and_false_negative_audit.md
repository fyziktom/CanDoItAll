# False Positive / False Negative Audit Workflow

## Why this matters

For this feature, false positives are costlier than false negatives.
A bad merge pollutes search and curation quickly.

## Audit buckets

### False-positive focused
Sample clusters that:
- are large
- contain mixed arrangement markers
- contain mixed composers
- contain mixed movement/full-work signals
- were auto-accepted near threshold

### False-negative focused
Sample pairs that:
- have strong structured similarity
- remain ungrouped
- or were routed to review but look obvious

## Output format

For each audited case record:
- score IDs
- current decision
- human decision
- root cause category
- fix recommendation

Root-cause categories:
- normalization miss
- composer alias miss
- catalog parser miss
- movement policy issue
- arrangement policy issue
- threshold too high
- threshold too low
- candidate generation miss
- embedding noise

## Post-audit action

Any recurring root cause should lead to:
- normalization improvement,
- threshold adjustment,
- new regression test,
- or explicit policy clarification.
