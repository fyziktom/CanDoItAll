# Runtime Smoke Checks

Run these on a temp DB or copied DB only.

## Smoke 1 — app startup
- app starts
- DB initializes/migrates
- dashboard loads
- tasks page loads

## Smoke 2 — profile refresh
- queue profile refresh mode
- task completes
- some `ScoreGroupingProfile` rows exist
- no unexpected failures logged

## Smoke 3 — missing embeddings
- queue embedding refresh mode
- vectors are created
- rerun skips unchanged rows

## Smoke 4 — dry run
- queue grouping dry run
- run preview rows appear
- suspicious/review counts visible

## Smoke 5 — apply
- apply a small reviewed run
- groups/memberships created
- primary `SongGroupId` cache updated
- catalog and score detail show the result

## Smoke 6 — manual correction
- remove wrong membership or set manual primary
- rerun targeted grouping
- manual change survives

## Smoke 7 — UI review path
- open group detail
- open score detail grouping panel
- verify evidence text visible
