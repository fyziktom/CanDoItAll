# SB01 Proof Manifest

## Subbundle

- Subbundle: `01-proof-claim-to-code-semantic-verifier`
- Status: `Completed`
- Owned requirements: R02 claim-to-code proof, supporting R01 portable proof.
- Test name: `CapabilityProof.ValidatesSourceBackedCapabilityClaims`
- Test name: `CapabilityProof.RejectsFakeCapabilityClaims`

## Changed Files And Hashes

| File | SHA-256 |
|---|---:|
| `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` | `E05FAC8476996CAD28EF8071252F9263E0E5439ED6019ABC2AFFAC868DB6172A` |
| `repo://codex/skills/bundles/candoitall-bundle-workflow/SKILL.md` | `25853549C6DA675B79B227665894DAB6939A925CF54B9D6562208F92C43F9923` |
| `repo://codex/skills/bundles/candoitall-bundle-execution/SKILL.md` | `C09D618C20B41942BB3A18EC75DD8BF5F111490A677371EDB2CB3D52778A54A5` |
| `repo://codex/skills/bundles/candoitall-bundle-validator/SKILL.md` | `5ABC9A95D033F55A70A0DE15E38316BC7A188BFD1294E748C2EFF663F5874FD2` |
| `repo://codex/skills/bundles/candoitall-bundle-preparation/SKILL.md` | `677081967FE2D90492A24E31F5B56A1DA839BEE8E533FF2095513F19CA1E7C2B` |
| `repo://codex/bundles/cognitive-memory-semantic-proof-and-real-learning-closure/templates/proof-claim-to-code-matrix-template.md` | `E92EA3D30841E2BFB575986970361D3888059AD0A99D6518751A50E850F32380` |
| `repo://codex/skills/bundles/candoitall-bundle-preparation/tests/fixtures/capability-proof-complete/proof/SB01/manifest.md` | `4F040E6FA5186947C7E2CE83783D7ACF8CC2FC7B198266144993A6FCD0B1502A` |
| `repo://codex/skills/bundles/candoitall-bundle-preparation/tests/fixtures/capability-proof-complete/source/multilingual-professor-extraction.cs` | `F4E164799A8BCB22C30DD0AE352A2DCFC854D144CFC1A58F28500E69CE626F79` |
| `repo://codex/skills/bundles/candoitall-bundle-preparation/tests/fixtures/capability-proof-complete/source/embedding-cluster-provider.cs` | `C9F478C9153F7EA565792CF10AA173DCD0AA8E18A3FEAC9A59C259AA19560F6E` |
| `repo://codex/skills/bundles/candoitall-bundle-preparation/tests/fixtures/capability-proof-fake-czech-diacritic/proof/SB01/manifest.md` | `875BCD2393D54678C807BE7BB78ABBFDC75A2C0C4F99C1E1878D7498E1745A3A` |
| `repo://codex/skills/bundles/candoitall-bundle-preparation/tests/fixtures/capability-proof-fake-czech-diacritic/source/english-only-professor-extraction.cs` | `7DDFA1D9AAA2E6394896F7537DBFA7893AEACA77C2DB7B90EB31924EBB82A864` |
| `repo://codex/skills/bundles/candoitall-bundle-preparation/tests/fixtures/capability-proof-fake-embedding-backed/proof/SB01/manifest.md` | `7FDCEB802E90CB6BD0453BA0464FDE1D8C2F339B3B67034C01DC1D762BBBA6BE` |
| `repo://codex/skills/bundles/candoitall-bundle-preparation/tests/fixtures/capability-proof-fake-embedding-backed/source/lexical-only-cluster-provider.cs` | `44839477A28DB3F9FBE6188FD0F6DF6EF8C028994AAF32FA00A58FF82BA37A4B` |

## Proof Artifacts

- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`
- Failing-first transcript: `bundle://proof/SB01/transcripts/failing-first.txt`
- Passing transcript: `bundle://proof/SB01/transcripts/passing.txt`
- Source assertion transcript: `bundle://proof/SB01/transcripts/source-assertions.txt`
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub.txt`
- Source assertion: `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py`

## Proof Claim To Code Matrix

| Capability claim | Required production source proof | Required test proof | Required negative fixture | Result |
|---|---|---|---|---|
| `Czech/diacritic` | `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` | `bundle://proof/SB01/transcripts/passing.txt` runs `CapabilityProof.ValidatesSourceBackedCapabilityClaims` | `bundle://proof/SB01/transcripts/failing-first.txt` rejects negative English-only source proof | Verified pass |
| `embedding-backed` | `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` | `bundle://proof/SB01/transcripts/passing.txt` runs `CapabilityProof.ValidatesSourceBackedCapabilityClaims` | `bundle://proof/SB01/transcripts/failing-first.txt` rejects negative lexical-only source proof | Verified pass |
| `provider-backed` | `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` | `bundle://proof/SB01/transcripts/source-assertions.txt` cites provider-backed source requirements | `bundle://proof/SB01/transcripts/failing-first.txt` rejects negative class-name-only proof patterns | Verified pass |
| `automatic` | `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` | `bundle://proof/SB01/transcripts/source-assertions.txt` cites automatic accepted-use source requirements | `bundle://proof/SB01/transcripts/failing-first.txt` rejects negative producer-free proof patterns | Verified pass |
| `scheduled` | `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` | `bundle://proof/SB01/transcripts/source-assertions.txt` cites scheduled lifecycle source requirements | `bundle://proof/SB01/transcripts/failing-first.txt` rejects negative manual-only proof patterns | Verified pass |
| `claim-specific` | `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` | `bundle://proof/SB01/transcripts/source-assertions.txt` cites claim-specific source requirements | `bundle://proof/SB01/transcripts/failing-first.txt` rejects negative broad-source proof patterns | Verified pass |
| `line-level` | `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` | `bundle://proof/SB01/transcripts/source-assertions.txt` cites line-level source requirements | `bundle://proof/SB01/transcripts/failing-first.txt` rejects negative broad-lineage proof patterns | Verified pass |
| `domain synthesis` | `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` | `bundle://proof/SB01/transcripts/source-assertions.txt` cites domain synthesis source requirements | `bundle://proof/SB01/transcripts/failing-first.txt` rejects negative diagnostic-template proof patterns | Verified pass |
| `portable proof` | `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` and `bundle://proof/SB02/transcripts/passing.txt` | `bundle://proof/SB02/transcripts/passing.txt` runs moved-path validation | `bundle://proof/SB02/transcripts/failing-first.txt` rejects negative machine-specific proof paths | Verified pass |

## Closure

- Failing-first: `bundle://proof/SB01/transcripts/failing-first.txt`.
- Semantic positive proof: `bundle://proof/SB01/transcripts/passing.txt`.
- Source assertions: `bundle://proof/SB01/transcripts/source-assertions.txt`.
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub.txt`.

