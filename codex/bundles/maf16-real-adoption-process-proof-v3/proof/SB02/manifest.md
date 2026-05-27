# SB02 Proof Manifest

## Status

Completed.

## Goal

Prove MAF 1.6 symbol availability by compile/reflection tests.

## Semantic Invariant Contract

- `bundle://proof/SB02/semantic-invariants.md`

## Failing-first or adversarial proof

- `bundle://proof/SB02/transcripts/failing-first.txt`
- Invariant ID: `SB02-INV-001`
- Test name: `Maf16_symbols_are_classified_from_loaded_runtime_assemblies`

## Passing proof

- `bundle://proof/SB02/transcripts/passing.txt`
- Invariant ID: `SB02-INV-001`
- Test name: `Maf16_symbols_are_classified_from_loaded_runtime_assemblies`

## Source assertions

- `bundle://proof/SB02/transcripts/source-assertions.txt`
- `repo://tests/CanDoItAll.Tests.Unit/Maf16CapabilityReflectionTests.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`

## Anti-stub audit

- `bundle://proof/SB02/transcripts/anti-stub-audit.txt`

## Changed-file hashes

- `bundle://proof/SB02/transcripts/changed-file-hashes.txt`
- `9FD915784BE199B5B25AA8C898FF8C1912354D6317ECAF1873C5ED43E51024C3` `repo://tests/CanDoItAll.Tests.Unit/Maf16CapabilityReflectionTests.cs`
