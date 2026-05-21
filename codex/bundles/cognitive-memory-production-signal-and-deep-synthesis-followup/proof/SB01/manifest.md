# SB01 Proof Manifest

## Status

- Subbundle: `SB01 - Proof gates for production behavior closure`
- Status: `Completed`
- Owned requirements: `R01`
- Raw notes: Codex must not close production behavior from consumer-only code, seeded tests, or prose proof.
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`

## Changed File Hashes

| File | SHA-256 |
|---|---:|
| `repo://codex/skills/bundles/candoitall-bundle-workflow/SKILL.md` | `C86FC0E2D611F2A9983EF7B118FFD7582A19EF8EBCABAB4E44D5109C83577F0B` |
| `repo://codex/skills/bundles/candoitall-bundle-execution/SKILL.md` | `770A94BA24374CC280B942933D184EA7F050F15809FDE160AF57104982F47D38` |
| `repo://codex/skills/bundles/candoitall-bundle-execution/references/artifact-backed-proof-manifest.md` | `CF310CE08F37C9C8EF0C3ACFDD90E77C377F0B35B8942939CBEA70643728DDAE` |
| `repo://codex/skills/bundles/candoitall-bundle-execution/references/semantic-adequacy-proof.md` | `970BD9698F300FB0B4F5D73DDD8164413E9E02CE4BE2C82C39235AE49E805B39` |
| `repo://codex/skills/bundles/candoitall-bundle-validator/SKILL.md` | `DDDD654804B4244EB0612A49492A37F692286FF768EC644C350B85FFA29A19E8` |
| `repo://codex/skills/bundles/candoitall-bundle-preparation/SKILL.md` | `EE2F4DA123A89F4973F61A68F92E70C32B1FD4531FFA6E05207A56400CAC5C39` |
| `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` | `7D082597C99E690DB4C7152368BF4A128CAC754B085AF69CE1343E56845CB077` |
| `repo://codex/bundles/cognitive-memory-production-signal-and-deep-synthesis-followup/templates/proof-manifest-template.md` | `FCBB3EF566EDF47D659DE99D1216EE49E10F2B5CABEF4329365BA1FCCBBEC01C` |
| `repo://codex/bundles/cognitive-memory-production-signal-and-deep-synthesis-followup/templates/semantic-invariant-template.md` | `7CF63579884770107DA9CB0CC19D5A6DF72BBF657507D349DB43BADBECCDAD90` |
| `C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py` | `7D082597C99E690DB4C7152368BF4A128CAC754B085AF69CE1343E56845CB077` |

Full hash transcript: `bundle://proof/SB01/transcripts/changed-file-hashes.txt`.

## Command Transcripts

- Failing-first and red-team negative proof: `bundle://proof/SB01/transcripts/failing-first.txt`
- Passing proof: `bundle://proof/SB01/transcripts/passing.txt`
- Source assertions: `bundle://proof/SB01/transcripts/source-assertions.txt`
- Active skill sync hashes: `bundle://proof/SB01/transcripts/active-skill-sync-hashes.txt`
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub.txt`
- Changed-file hashes: `bundle://proof/SB01/transcripts/changed-file-hashes.txt`

## Tests And Invariants

- Test name: `FakeProof.AcceptedUseConsumerOnly`
- Test name: `FakeProof.TemplateDreamMetaText`
- Test name: `ValidatorProof.PositiveFixtureStillPasses`
- Invariant ID: `SB01-PRODUCTION-MATRIX-01`
- Invariant ID: `SB01-DREAM-META-TEXT-02`

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `ProfessorAnchorAcceptedUse` proof requirement | `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` plus `bundle://proof/SB01/transcripts/source-assertions.txt` validates producer-proof matrix entries | `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` validates consumer-proof matrix entries | `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` validates lifecycle-proof matrix entries | `bundle://proof/SB01/transcripts/failing-first.txt` proves the consumer-only accepted-use fixture now fails completed validation |

## Source Assertions

`bundle://proof/SB01/transcripts/source-assertions.txt` proves the validator detects production artifact terms, requires a production behavior artifact matrix in both critical proof files, checks producer/consumer/lifecycle/negative matrix columns, rejects weak or uncited matrix cells, and rejects dream evidence-count template text when it appears as shipped or expected positive synthesis behavior.

## Red-Team Negative Proof

`bundle://proof/SB01/transcripts/failing-first.txt` records that the two fake fixtures passed before hardening and now fail after hardening:

- `repo://codex/skills/bundles/candoitall-bundle-preparation/tests/fixtures/production-artifact-consumer-only-accepted-use`
- `repo://codex/skills/bundles/candoitall-bundle-preparation/tests/fixtures/production-artifact-template-dream-meta-text`

## Browser And Host Proof

Browser validation: N/A. SB01 changes backend validator scripts, bundle skill instructions, references, fixture bundles, active skill files, and proof templates only; no UI routes, components, browser-visible behavior, host startup behavior, or OS shell behavior changed.

## Downstream Dependency Check

`bundle://proof/SB01/transcripts/passing.txt` proves the prepared-stage validator still accepts this bundle and the positive proof-depth fixture still passes. `bundle://proof/SB01/transcripts/active-skill-sync-hashes.txt` proves the active skill-root validator matches the repo-local validator before downstream subbundles start.
