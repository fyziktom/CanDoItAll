# SB01 semantic invariants

## SB01-I1 scope proof is current

- Source raw note: review and clean evidence noise before merge.
- Expected behavior: branch status, residue audit, and proof artifacts describe the current worktree.
- Disallowed shallow implementation: relying on stale preparation text or old audit output.
- Passing proof: `bundle://proof/SB01/transcripts/git-status-rerun.txt`, `bundle://proof/SB01/transcripts/branch-ancestry-rerun.txt`, and `bundle://proof/SB03/transcripts/residue-and-switch-audit-final.txt`.
- Changed source files and hashes: `bundle://proof/SB08/transcripts/changed-file-hashes.txt`.
- Production assertions: no runtime provider residue or switch/drain terms remain outside allowed quarantine.
- Red-team negative case: `bundle://proof/SB08/transcripts/anti-stub-audit.txt`.
- Downstream dependency check: SB02-SB08 proof cites this refreshed audit instead of stale preparation status.
