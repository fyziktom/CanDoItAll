# Score merge strategy

This is the hardest part of the system and must be designed now, even if the rich conflict UI is implemented later.

## Why score merge is harder than playlist/package merge

A score is not just a flat text file.
Users can concurrently:

- add measures,
- split measures,
- change voices,
- insert notes inside existing measures,
- alter articulations/lyrics/directions,
- change metadata and layout settings.

A line-based diff is not enough.

## Required score model direction

The repository must preserve two parallel representations:

1. **source representation**
   - current practical input, often MusicXML
2. **canonical score representation**
   - normalized JSON with stable ids

## Stable ids required in canonical score JSON

At minimum:
- `partId`
- `staffId`
- `voiceId`
- `measureId`
- `noteId`
- `directionId`
- `lyricId`
- `chordSymbolId`

Without these, structured merge will stay unreliable.

## Measure ordering rule

Do not rely only on rendered measure numbers.

Use:
- stable `measureId`
- separate ordering field such as:
  - `orderKey`
  - or anchor-based insertion (`afterMeasureId`, `beforeMeasureId`)

This is needed because concurrent insertions into the middle of a score are common.

## Recommended diff granularity

### Level 1
- score metadata changes
- part-level changes
- measure insert/delete/move
- measure property changes
- note insert/delete/update within measure
- lyric/direction changes

### Level 2
- beam/slur/tie grouping changes
- tuplet grouping changes
- layout/page system changes

V1 core can focus on Level 1 if the API contract already leaves room for Level 2.

## Three-way merge rule

Use:
- `base`
- `ours`
- `theirs`

Algorithm outline:
1. diff `base -> ours`
2. diff `base -> theirs`
3. apply non-overlapping changes automatically
4. raise conflict hunks where both sides changed the same semantic node or incompatible anchors

## Auto-merge cases to support

Should merge cleanly when:
- one side edits metadata, the other edits notes
- one side adds measure A, the other adds measure B in another place
- one side edits measure 8, the other edits measure 22
- one side adds lyrics while the other changes dynamics elsewhere

## Conflict cases to expose

Must surface conflict when:
- both sides edit the same note differently
- both sides delete/modify the same measure incompatibly
- both sides insert into the same anchor position and ordering cannot be resolved deterministically
- one side restructures a voice while the other edits notes inside the old voice shape

## Current-phase implementation recommendation

### Phase 1 core
Implement:
- canonical score DTO
- measure-aware diff
- three-way merge preview service
- conflict JSON output
- exact API contracts for hunks and resolutions

### Phase 2 UI
Implement:
- visual diff in notation editor
- accept ours/theirs per hunk
- manual reorder of competing inserted measures
- regenerate merged canonical score and export source

## Required APIs for future merge UI

The core must expose something like:

- `compare(baseCommit, targetCommit)`
- `merge-preview(baseCommit, oursCommit, theirsCommit)`
- `apply-merge-resolution(...)`

## Minimal hunk payload shape

Each hunk should contain:
- `hunkId`
- `semanticKind`
- `path`
- `scope`
- `operation`
- `baseValue`
- `oursValue`
- `theirsValue`
- `isConflict`
- `recommendedResolution`

## Important implementation truth

If the current PHP stage cannot fully build a perfect score AST editor immediately, still do this now:

- define the canonical score format,
- store it beside MusicXML,
- create the merge service interfaces,
- implement a measure-aware first version,
- keep the API stable for the richer WASM merge UI later.

That prevents a second redesign.
