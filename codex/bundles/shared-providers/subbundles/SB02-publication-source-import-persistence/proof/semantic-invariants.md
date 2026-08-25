# SB02 semantic invariant contract

| ID | Expected behavior | Disallowed shallow implementation | Failing-first proof | Passing/source proof | Red-team negative case | Result |
| --- | --- | --- | --- | --- | --- | --- |
| SB02-INV-01 | Publication, source, import, service, and invocation identity are explicit relational state. | JSON-only identity, reused profile IDs as public IDs, process-static identity, or duplicate source/publication rows. | state/persistence red transcripts lack the production types/services | state 18/18; persistence 14/14; entity/migration inventory | public/profile equality, concurrent create, and real duplicate insert rejection | Pass |
| SB02-INV-02 | Transient and authoritative catalog outcomes are distinct and non-destructive. | Mark all imports missing on timeout/auth/network failure or delete rows on absence. | state/persistence red transcripts | pure transitions plus real coordinator tests | transient failure preserves state; identity mismatch blocks mutation; reappearance reuses identity | Pass |
| SB02-INV-03 | Local profile ID, alias, and enabled intent survive remote lifecycle changes. | Replace the profile, overwrite local fields, or create a second import during refresh. | state/persistence red transcripts | state/persistence stable-ID and local-intent tests | missing/unpublish/reappearance and repeated reconciliation | Pass |
| SB02-INV-04 | Source URI and one secret reference propagate atomically to every imported effective profile. | Copy secret values, update only some profiles, or notify observers before commit. | persistence red transcript | source service, one transaction, post-commit observer test | stale token conflicts; rollback/no early observer | Pass |
| SB02-INV-05 | Invocation audit is owner-consistent, metadata-only, retention-ready, and truthful about completeness. | Store content, pair a publication with another profile, fabricate missing usage as zero, or double-finalize differently. | state/persistence red transcripts | state/persistence audit tests; composite FK; schema scan | owner mismatch, incomplete usage, repeat completion, forbidden-property scan | Pass |
| SB02-INV-06 | Referenced provider profiles cannot be orphaned by either delete or transfer path. | Guard only one surface, rely on raw FK exceptions, or mutate before transfer validation. | deletion command red during incomplete implementation | deletion 6/6; transfer assertions; `Restrict` FKs | four production path/reference pairs, direct DB delete, transfer source/target collisions | Pass |
| SB02-INV-07 | Dependency direction and cohesion match CP-02. | Workspace-to-Http, Foundation-to-Workspace, inner reverse edge, service locator, reflection bridge, or monolithic partial. | SB02 before snapshots/reference table | after snapshot; 24 cohesive files; independent review | graph has exactly one authorized edge and zero project cycle | Pass |

After-state hashes are recorded in `hashes.sha256`; the complete worktree inventory is
`changed-files.md`. The deletion failing-first transcript is retained transparently: its command
exited during incomplete implementation on a reconciliation compile error, while the final
negative behavior is proven by the green production-path and database tests rather than by that
red transcript alone.

