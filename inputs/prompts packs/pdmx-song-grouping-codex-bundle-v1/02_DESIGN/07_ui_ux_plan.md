# UI / UX Change Plan

## UX goal

The grouping feature must support two user modes:

1. **fast browsing**
   - see whether a score belongs to a group
   - jump across alternate versions quickly

2. **curation / repair**
   - understand why grouping happened
   - correct it without SQL or scripts
   - safely review ambiguous results from dry runs

## Page-by-page plan

## 1. Dashboard (`Home.razor`)

Current:
- maintenance has a single `Queue grouping` button.

Add:
- `Build grouping profiles`
- `Build missing work embeddings`
- `Run grouping dry run`
- `Apply reviewed grouping run`
- `Refresh groups for filtered scope`
- summary stats:
  - profiles stale
  - embeddings missing
  - groups needing review
  - suspicious large groups

Data needed:
- grouping health summary
- latest run status
- counts of missing/outdated profiles and embeddings

## 2. Catalog (`Catalog.razor`)

Add filters:
- has primary group
- has any group
- needs group review
- low-confidence membership
- manual grouping lock
- group key / group title search

Add row badges:
- primary group chip
- secondary group count
- confidence badge
- review-required badge

Add quick actions:
- open group detail
- open grouping evidence drawer (optional later)

## 3. Score detail (`ScoreDetail.razor`)

Add a dedicated **Grouping** panel or sub-section on the Review tab.

Show:
- primary group
- secondary/related groups
- confidence per membership
- group type
- evidence summary
- canonical group values
- manual override state
- manual lock state

Actions:
- set/create primary group
- add secondary/related group
- remove membership
- mark primary membership
- open group detail
- lock against auto regroup
- inspect “why grouped” evidence

Avoid:
- forcing the user to edit opaque raw keys only

## 4. Groups page (`Groups.razor`)

Replace the current simple table with:
- search box
- filters:
  - group type
  - review state
  - member count range
  - suspicious only
  - curated only
- columns:
  - title
  - composer
  - type
  - members
  - review state
  - confidence summary
  - updated time
  - source (`Auto` / `Manual` / `Hybrid`)

Add tabs or segmented modes:
- all groups
- needs review
- suspicious large groups
- recent dry-run proposals

## 5. Group detail (`GroupDetail.razor`)

Expand to include:

### Header
- canonical title/composer
- group type
- key
- review state
- member count
- source
- last updated

### Actions
- edit canonical values
- merge with another group
- split selected members into new group
- mark curated / lock
- sync derived tags
- re-evaluate this group

### Members table
- score title/composer
- membership role
- confidence
- source
- review status
- representative flag
- evidence preview

### Evidence / diagnostics section
- common signals shared by cluster
- warnings:
  - conflicting catalogs
  - mixed arrangement markers
  - mixed movement/full-work members

## 6. New review surface

Recommended new route:
- `/groups/review`

Purpose:
- review dry-run proposals before apply

View:
- proposed clusters
- confidence bands
- accept / reject / split / merge
- bulk accept high-confidence
- open score detail side by side

## Interaction rules

### Manual creation
- user may create a group with canonical title/composer
- then add one or more scores manually

### Manual correction
- removing a member should create a sticky manual rule so the same bad edge is not re-applied silently

### Merge
- group merge should preserve member evidence history and choose a final canonical display

### Split
- group split should create a new `GroupKey` and preserve source audit

## Suggested validation messages

- `Group key already exists.`
- `Primary membership is required before saving.`
- `This membership is locked and cannot be auto-overwritten.`
- `The selected merge would combine conflicting catalog identities. Review required.`
- `This proposed cluster mixes arrangements and originals. Choose a group type or split it.`

## Failure states to design for

- no grouping profile exists yet
- embeddings missing
- latest dry run failed
- group deleted/deprecated while page open
- optimistic concurrency conflict on edit
- apply run blocked because review-required proposals remain

## UI testing expectations

New UI should be covered by:
- group badges on catalog rows
- grouping panel on score detail
- group detail evidence visibility
- dry-run review actions
- merge/split flow smoke tests
- lock/manual override persistence
