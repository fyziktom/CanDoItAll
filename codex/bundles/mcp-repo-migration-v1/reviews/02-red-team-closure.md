# Red-Team Closure Review

## Fake-Proof Resistance

- A docs-only move would fail `bundle://proof/SB01/transcripts/build-release.txt` because the MCP solution must compile from the sibling repo.
- Leaving migrated MCP projects in the main solution would fail `bundle://proof/SB01/transcripts/source-assertions.txt`.
- Replacing component package references with main-repo project references would fail `bundle://proof/SB01/transcripts/source-assertions.txt`.
- Adding `-McpRepoRoot` without using it for build paths would fail `bundle://proof/SB02/transcripts/source-and-config-assertions.txt`.
- Syncing skills from the MCP repo instead of `repo://codex/skills` would fail `bundle://proof/SB02/transcripts/source-and-config-assertions.txt`.
- Leaving retired Processes or ProjectStructure MCP references in active installs/config would fail `bundle://proof/SB02/transcripts/artifact-cleanup.txt`.

## Manual Verdict

The proof is command-backed and rejects the main shallow implementations: empty repo, stale main-solution ownership, wrong build root, wrong skill root, and stale generated MCP artifacts.
