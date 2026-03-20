# Validation Checklists

## Architecture checklist

- [ ] Grouping is no longer destructive delete-and-recreate
- [ ] Many-to-many memberships exist
- [ ] Dry-run preview exists
- [ ] Apply flow exists
- [ ] Manual locks survive reruns
- [ ] Evidence is persisted and visible
- [ ] Tags are not canonical truth
- [ ] Existing catalog/detail routes still function

## Normalization checklist

- [ ] strict vs loose forms exist
- [ ] composer surname handling exists
- [ ] catalog parsing exists
- [ ] movement extraction exists
- [ ] arrangement/editorial detection exists
- [ ] risky rules are not auto-treated as hard truth

## Embedding checklist

- [ ] model availability check exists
- [ ] pull-if-missing behavior exists or is documented
- [ ] vectors are cached by content hash
- [ ] embeddings are not computed all-vs-all
- [ ] descriptions are auxiliary only in phase 1

## UI checklist

- [ ] score detail shows memberships
- [ ] evidence panel exists
- [ ] groups page supports review-oriented filtering
- [ ] group detail supports curation actions
- [ ] dry-run review path exists

## Safety checklist

- [ ] original real DB is not mutated during validation
- [ ] copied-DB flow is documented
- [ ] apply requires explicit action
- [ ] suspicious groups are surfaced
- [ ] manual corrections are sticky

## Test checklist

- [ ] false-positive tests exist
- [ ] false-negative tests exist
- [ ] rerun/idempotency tests exist
- [ ] merge/split tests exist
- [ ] lock/override tests exist
