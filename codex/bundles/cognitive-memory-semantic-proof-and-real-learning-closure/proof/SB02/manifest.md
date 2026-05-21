# SB02 Proof Manifest

## Subbundle

- Subbundle: `02-portable-proof-and-installed-skill-sync-closure`
- Status: `Completed`
- Owned requirements: R01 portable proof, R02 active proof-rule synchronization.
- Test name: `PortableProof.RejectsMachineSpecificArtifactPaths`
- Test name: `PortableProof.ValidatesMovedPortableFixtureAndActiveSkillSync`

## Changed Files And Hashes

| File | SHA-256 |
|---|---:|
| `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` | `E05FAC8476996CAD28EF8071252F9263E0E5439ED6019ABC2AFFAC868DB6172A` |
| `repo://codex/skills/bundles/candoitall-bundle-workflow/SKILL.md` | `25853549C6DA675B79B227665894DAB6939A925CF54B9D6562208F92C43F9923` |
| `repo://codex/skills/bundles/candoitall-bundle-execution/SKILL.md` | `C09D618C20B41942BB3A18EC75DD8BF5F111490A677371EDB2CB3D52778A54A5` |
| `repo://codex/skills/bundles/candoitall-bundle-validator/SKILL.md` | `5ABC9A95D033F55A70A0DE15E38316BC7A188BFD1294E748C2EFF663F5874FD2` |
| `repo://codex/skills/bundles/candoitall-bundle-preparation/SKILL.md` | `677081967FE2D90492A24E31F5B56A1DA839BEE8E533FF2095513F19CA1E7C2B` |

## Proof Artifacts

- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`
- Failing-first transcript: `bundle://proof/SB02/transcripts/failing-first.txt`
- Passing transcript: `bundle://proof/SB02/transcripts/passing.txt`
- Source assertion transcript: `bundle://proof/SB02/transcripts/source-assertions.txt`
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/anti-stub.txt`
- Source assertion: `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py`
- Active skill hash proof: `bundle://proof/SB02/transcripts/passing.txt`

## Proof Claim To Code Matrix

| Capability claim | Required production source proof | Required test proof | Required negative fixture | Result |
|---|---|---|---|---|
| `portable proof` | `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` and `bundle://proof/SB02/transcripts/passing.txt` | `bundle://proof/SB02/transcripts/passing.txt` runs moved-checkout validation and active hash comparison | `bundle://proof/SB02/transcripts/failing-first.txt` rejects negative machine-specific artifact proof | Verified pass |

## Closure

- Failing-first: `bundle://proof/SB02/transcripts/failing-first.txt`.
- Semantic positive proof: `bundle://proof/SB02/transcripts/passing.txt`.
- Source assertions: `bundle://proof/SB02/transcripts/source-assertions.txt`.
- Anti-stub audit: `bundle://proof/SB02/transcripts/anti-stub.txt`.

