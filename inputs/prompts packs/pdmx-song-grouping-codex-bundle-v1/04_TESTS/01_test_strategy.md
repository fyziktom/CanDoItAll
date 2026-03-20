# Test Strategy

## Philosophy

The grouping subsystem needs both:
- correctness tests,
- safety tests.

Correctness:
- obvious duplicates should group.

Safety:
- obvious non-duplicates should not group.

## Test layers

## 1. Unit tests

Focus:
- normalization helpers
- structured extraction
- composer alias handling
- score fusion logic
- confidence-band classification
- canonical selection logic

## 2. Integration tests

Focus:
- DB persistence
- profile refresh
- run generation
- apply flow
- lock/override persistence
- rerun idempotency

## 3. UI/component tests

Focus:
- grouping badges render
- evidence panel renders
- validation messages
- lock state UI

## 4. Playwright tests

Focus:
- end-to-end review flows
- group detail workflows
- manual correction survival

## 5. Copied real-DB benchmark tests

Focus:
- runtime performance
- false-positive sampling
- suspicious cluster discovery
- top-composer hot-block behavior

## Minimum required scenario matrix

### Positive cases
- exact duplicate title/composer formatting variant
- opus/number abbreviation variant
- initials vs full composer name
- accent-insensitive composer variant
- punctuation variant
- title order variant

### Negative cases
- same title, different composer
- same composer, different catalog number
- movement vs whole work
- arrangement vs original
- generic hymn title collision
- excerpt vs full work

### Operational cases
- rerun with unchanged profiles
- rerun with changed normalization version
- missing embedding generation
- manual lock prevents overwrite
- dry run does not mutate canonical groups
- apply run updates primary group cache

## Suggested naming organization

- `Grouping/Normalization/*`
- `Grouping/Profiles/*`
- `Grouping/CandidateGeneration/*`
- `Grouping/Scoring/*`
- `Grouping/Application/*`
- `Grouping/Ui/*`
