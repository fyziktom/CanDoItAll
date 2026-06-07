# SB014 Proof Manifest

## Status
- Completed.

## Scope
- Add Core projection evidence descriptor models and a module adapter while preserving current projection and provider-native browser behavior.

## Evidence
- Core descriptor implementation: `repo://src/CanDoItAll.Processes.Core/Artifacts/ProcessArtifactProjectionEvidenceDescriptors.cs`.
- Module adapter: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionEvidenceDescriptorAdapter.cs`.
- Build proof: `bundle://proof/SB015/transcripts/projection-validation-descriptor-build.txt`.
- Source assertions: `bundle://proof/SB015/transcripts/source-assertions.txt`.
- Public API generated surface: `bundle://proof/SB015/transcripts/current-core-public-api-surface-after-projection-evidence.txt`.

## Hashes
- SHA-256 `4308E00E1350BA17087FCE9D9C36BD30478BBF68582AF936912AA1C1EBC3D321` for `repo://src/CanDoItAll.Processes.Core/Artifacts/ProcessArtifactProjectionEvidenceDescriptors.cs`.
- SHA-256 `C3095BF143885AB46F4CD8F47A36E07BF0D1D3293262F740BFE82F972651CB47` for `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionEvidenceDescriptorAdapter.cs`.

## Result
- SB014 passed with a zero-warning solution build and adapter-owned descriptor conversion.
