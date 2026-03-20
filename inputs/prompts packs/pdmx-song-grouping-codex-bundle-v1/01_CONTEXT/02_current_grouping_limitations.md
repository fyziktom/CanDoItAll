# Current Grouping Limitations

This file is intentionally blunt. Codex should treat it as the list of things that must not be carried forward.

## 1. Current normalization is too shallow

Current `WorkKeyNormalizer`:

- lowercases,
- tokenizes with `[a-z0-9]+`,
- maps a few abbreviations and ordinals.

Missing today:

- Unicode decomposition and diacritic stripping
- composer surname-first parsing
- initials expansion / normalization
- key signature normalization
- catalog-system extraction
- movement boundary detection
- arrangement/excerpt markers
- multilingual work-token handling
- safe alias generation
- separate loose vs strict normalization forms

Result:
- easy wins are handled,
- real classical-title variation is not.

## 2. Current grouping is destructive

`PdmxGroupingService` currently:

- reads all scores,
- groups exact keys,
- removes all existing groups,
- nulls all `SongGroupId`,
- recreates groups.

Problems:
- manual curation can be lost or silently reshaped,
- there is no run history,
- there is no preview,
- there is no rollback,
- there is no conflict record,
- there is no “why did this change?” evidence.

## 3. Current model only supports one group per song

Current persistence is:

- `IndexedScore.SongGroupId`
- `SongGroup.Members`

That blocks several realistic needs:
- exact-work group + related work-family group
- primary group + alternate analytical grouping
- future arrangement/excerpt relationships
- preserving compatibility while adding richer grouping semantics

## 4. There is no confidence model

Current grouping has no:
- confidence score,
- confidence band,
- auto-accept threshold,
- review-required threshold,
- rejection reason,
- ambiguity state.

Result:
- all grouped results look equally “true”,
- reviewers cannot triage where attention is needed.

## 5. There is no explainability

Current group assignment stores no rationale such as:
- exact normalized title match,
- same composer alias,
- same opus and number,
- embedding similarity,
- movement conflict,
- arrangement flag.

Result:
- users can see the outcome,
- but cannot audit the reasoning.

## 6. There is no dry-run review mode

For a large dataset this is dangerous.

Before applying groups globally, the system needs to support:
- generate-only,
- review-only,
- apply-reviewed,
- compare-with-current,
- rollback using run history or backup copy.

## 7. There is no group administration workflow

Missing today:
- create group manually,
- merge groups,
- split group,
- remove incorrect member,
- choose canonical title/composer,
- mark representative version,
- mark “never auto-group with this cluster”.

## 8. Catalog and detail pages are too shallow for group curation

Current UI can display:
- a group summary,
- one group chip,
- a manual override text box.

Missing:
- multiple memberships,
- primary vs secondary group,
- confidence badge,
- evidence panel,
- related group browsing,
- review queue,
- bulk triage.

## 9. Current tests protect only the simple path

Current tests prove:
- exact-ish Moonlight normalization works,
- grouping page shows a duplicate cluster.

They do not protect:
- false positives,
- arrangements,
- movement vs full-work conflicts,
- same title / different composer,
- multilingual variants,
- same composer / different catalog numbers,
- incremental reruns,
- manual override survival,
- dry run behavior.

## Non-negotiable implementation rule

Codex must treat the current grouping code as a **prototype baseline**, not as the final architectural pattern.
