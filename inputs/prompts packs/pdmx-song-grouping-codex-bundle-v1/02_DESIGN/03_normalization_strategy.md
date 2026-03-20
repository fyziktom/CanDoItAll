# Normalization Strategy

## Main rule

Never replace original source title/composer text with normalized text.
Instead:

- keep raw display fields untouched,
- generate multiple derived normalization forms,
- use those derived forms for grouping, search, and candidate generation.

## Why one normalized string is not enough

One string cannot serve all goals simultaneously:

- strict exact grouping
- loose search recall
- structured catalog extraction
- composer alias matching
- explainability
- human readability

Therefore use multiple forms.

## Recommended normalization outputs

### Title-oriented

- `NormalizedTitleStrict`
  - conservative
  - preserves meaningful work-identity structure
  - suitable for exact or near-exact deterministic matching

- `NormalizedTitleLoose`
  - more recall-oriented
  - expands abbreviations
  - normalizes punctuation and token variants
  - useful for candidate generation and broad search

- `WorkSignatureStrict`
  - structured synthetic key built from:
    - composer strict
    - work type
    - catalog system/value
    - work number
    - movement number
    - key signature if present

- `WorkSignatureLoose`
  - same idea but more tolerant

### Composer-oriented

- `NormalizedComposerStrict`
  - conservative normalized full-name form

- `NormalizedComposerLoose`
  - accent-insensitive, alias-friendly form

- `ComposerSurnameKey`
  - especially useful for blocking

- `AliasComposersJson`
  - generated safe alternate forms for search/matching

## Title normalization pipeline

Recommended sequence:

1. trim and collapse whitespace
2. Unicode normalize
3. create accent-insensitive secondary form
4. normalize punctuation and separators
5. normalize apostrophes/quotes/dashes
6. expand safe abbreviations
7. normalize ordinal words and Roman numerals where safe
8. normalize catalog tokens
9. normalize key signature phrases
10. detect movement markers
11. detect arrangement/editorial markers
12. build strict and loose token lists
13. build structured signatures

## Composer normalization pipeline

Recommended sequence:

1. trim and collapse whitespace
2. Unicode normalize
3. accent-insensitive secondary form
4. normalize punctuation and commas
5. detect `Lastname, Firstname` and reorder
6. normalize initials spacing
7. strip honorifics only if clearly non-identity metadata
8. build strict and loose forms
9. derive surname key and forename key
10. generate safe aliases

## Safe rules vs risky rules

### Safe auto-normalization

These are generally safe to apply automatically:
- whitespace collapse
- lowercasing in derived keys
- accent-insensitive derived form
- punctuation folding
- `op` / `op.` / `opus` family normalization
- `no` / `no.` / `nr` / `number` normalization
- basic Roman numeral normalization for isolated numbering tokens
- `arr.` -> `arrangement`
- `orch.` -> `orchestration`
- `transcr.` -> `transcription`
- `ed.` -> `edition`
- key-format canonicalization such as:
  - `c sharp minor`
  - `c-sharp minor`
  - `C# minor`
  -> derived canonical key token

### Probably safe but validate on real data

- moving leading articles
- expanding multilingual movement words
- stripping “the” / “a” / “an” from loose forms
- composer initial expansion heuristics
- inferring missing `major` from bare uppercase key names
- title-order normalization when punctuation suggests container + movement form

### Risky: review-only or auxiliary-only

- aggressive aliasing of composer first names across languages
- merging arrangement markers into the same strict work key
- treating excerpts as the same as full works
- removing all editorial/version markers
- expanding ambiguous one-letter catalog prefixes
- mapping every Roman numeral to number in free text

## Stopword policy

Recommended:
- keep a small list only for the **loose** form,
- do not strip heavily in the strict form.

Candidate loose stopwords:
- the
- a
- an
- and
- for
- of
- in
- on
- from

But:
- do not remove catalog/key/movement tokens,
- do not remove genre-defining tokens like `sonata`, `concerto`, `suite`.

## Diacritics policy

Store both:
- diacritic-preserving canonical display
- accent-insensitive derived keys

Example:
- `Frédéric Chopin`
- derived loose key:
  - `frederic chopin`

This helps search recall without destroying display fidelity.

## Roman numeral policy

Use cautiously.

Safe contexts:
- isolated movement/work numbering tokens
- detected after `no`, `opus`, `book`, `part`, `movement`, `mv`

Risky contexts:
- words / names / phrases where Roman numerals may be part of a title or monarch name.

## Key signature policy

Normalize to a compact internal token, for example:
- `c_major`
- `d_flat_major`
- `c_sharp_minor`

Preserve raw display text separately.

Recommended parsing support:
- `C major`
- `C-major`
- `C dur`
- `Do majeur`
- `c`
- `c minor`
- `E-flat`
- `E♭`
- `Bb`
- `B-flat`

## Movement policy

Extract movement information into structured fields when present:
- movement number
- movement label

Example labels:
- allegro
- adagio
- andante
- scherzo
- finale
- presto

Important:
- movement detection should increase precision,
- but it should not silently collapse a whole work and a movement into the same exact-work group.

## Arrangement/editorial policy

Detect tokens such as:
- arrangement
- transcription
- orchestration
- revision
- edition
- urtext
- excerpt

Recommended semantics:
- keep them in strict signals,
- optionally downweight or separate them in loose grouping,
- surface them in evidence.

## Search strategy

Search should use:
- raw title/composer
- existing normalized fields for compatibility
- grouping profile loose fields
- group aliases
- derived `group:XYZ` compatibility tag if implemented

## Implementation recommendation

Replace the single `WorkKeyNormalizer` concept with a richer grouping-normalization layer, but keep a thin compatibility adapter so current indexing/search code does not break mid-refactor.
