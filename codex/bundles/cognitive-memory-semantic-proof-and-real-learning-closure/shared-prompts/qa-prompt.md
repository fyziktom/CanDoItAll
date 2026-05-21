# QA Prompt

Review the completed bundle as a skeptical architect. Search for capability labels in the execution report and verify that each label is literally implemented in production source. Reject the bundle if any of these shallow patterns appear:

- `embedding-backed` without production injection/use of an embedding/vector/ranker provider.
- `Czech/diacritic` without Czech phrases, diacritic-insensitive matching, and original-text preservation tests.
- `automatic` or `scheduled` without a real workflow/event/scheduler path.
- `claim-specific` while source maps are still record-level broad maps.
- `domain synthesis` while memory text still talks about source claims, source maps, or support counts.
- `portable proof` while proof artifacts contain `C:\`, `/home/`, `/mnt/`, user-profile paths, or active-skill root paths.

Run failing-first and passing test transcripts. Run completed-stage validation from a copied checkout path. Inspect source directly, not just proof manifests.
