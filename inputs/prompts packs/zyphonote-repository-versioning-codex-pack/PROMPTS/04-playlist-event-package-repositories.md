Implement playlist, event, and learning-package repository integration.

Required work:
1. Backfill playlist history into repositories.
2. Create event repositories with initial immutable commits.
3. Bridge learning package versions into repositories.
4. Ensure playlist/package manifests pin exact score commit hashes going forward.
5. Add structured compare/merge-preview behavior for:
- playlist
- event
- learning_package

Rules:
- keep legacy screens working
- keep current package content-addressed manifest storage as the reference style
- make event history honest: only current state can be backfilled if no older snapshots existed

Update checklists after completion.
