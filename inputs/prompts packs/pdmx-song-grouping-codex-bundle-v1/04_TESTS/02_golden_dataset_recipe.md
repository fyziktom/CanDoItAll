# Golden Dataset Recipe

Build a gold-standard benchmark set that can be reused for threshold tuning and regression testing.

## Goal

The dataset should intentionally include:
- true duplicates
- hard near-duplicates
- false friends
- edge cases

## Recommended buckets

### Bucket A — obvious duplicates
Variants such as:
- punctuation changes
- `op.` vs `opus`
- `no.` vs `number`
- initials vs full composer name
- accent/no-accent composer

### Bucket B — title-order variants
- `Nocturne in D-flat major, Op. 27, No. 2`
- `Op. 27 No. 2, Nocturne in D-flat major`

### Bucket C — same work, movement references
- movement-only title
- whole-work title
- should usually become review or related-family, not silent exact-work merge

### Bucket D — arrangement boundary
- original score
- transcription
- orchestration
- piano reduction

### Bucket E — same title, different composers
- common hymn / folk / dance names
- `Ave Maria`
- `Prelude`
- `Waltz`

### Bucket F — same composer, different works
- same genre/work type
- same key family
- close opus numbers

### Bucket G — multilingual variants
- English / German / French / Italian title variants where they refer to the same work or same generic type

### Bucket H — incomplete metadata
- missing composer
- missing subtitle
- generic source title only

## Annotation format

For each benchmark row or pair, capture:
- `IndexedScoreId`
- raw title
- raw composer
- expected exact-work group ID or label
- expected work-family group ID or label if used
- notes
- confidence expectation
  - `auto`
  - `review`
  - `reject`

## How to create it from the copied DB

1. sample from top normalized title stems
2. sample from top composer buckets
3. sample rows with catalog tokens
4. sample rows with arrangement markers
5. hand-label representative pairs/clusters

## Minimum size

Recommended first benchmark:
- 200–500 labeled rows
- plus 500–1500 labeled candidate pairs

This is enough to tune early thresholds without overcommitting.

## Reuse rule

The golden dataset should be:
- versioned,
- deterministic,
- reused after every scoring or normalization change.
