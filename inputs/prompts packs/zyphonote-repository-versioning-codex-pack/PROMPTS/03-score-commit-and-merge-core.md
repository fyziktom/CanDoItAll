Implement score repository integration next.

Required work:
1. Backfill score histories into repositories.
2. Dual-write new score saves to repository commits.
3. Add commit hash bridge fields to score versions.
4. Implement score compare / merge-preview service contracts.
5. Implement a first structured score diff output with semantic hunks.
6. Keep current score create/edit/download behavior working.

Important:
- current score file storage is version-id-addressed; the repository path must fix that for new writes
- store both source MusicXML and canonical score JSON if possible
- even if the full merge UI is not built yet, the API contract for score hunks/conflicts must be real

Update checklists after completion.
