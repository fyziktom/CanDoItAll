# SB006 Proof Manifest

## Result

Completed. The Core project builds as a narrow seed with only the Contracts project reference and no package references.

## Portable References

- Subbundle: bundle://subbundles/SB006/README.md
- Semantic invariants: bundle://proof/SB006/semantic-invariants.md
- Core project: repo://src/CanDoItAll.Processes.Core/CanDoItAll.Processes.Core.csproj
- Contracts enums: repo://src/CanDoItAll.Processes.Contracts/Runtime/ProcessRuntimeEnums.cs

## Changed File SHA-256

- repo://src/CanDoItAll.Processes.Core/CanDoItAll.Processes.Core.csproj: 434e2ba364b7d655cfdf52ede5d1b1b04d4a163ae2cc398ac1951031faa4a17a
- repo://src/CanDoItAll.Processes.Contracts/Runtime/ProcessRuntimeEnums.cs: ccfe91f2d889681f30efeda65df63a6a241ec1565aa93b632db7f3adc78cd13d
- repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs: d2bc3d1bb7d051eae31dd02d3f78df08ae2a5fe39a7766ca29843bae457fdc4b

## Command Transcripts

- Passing build: bundle://proof/common/transcripts/build-solution.txt
- Passing architecture test: bundle://proof/common/transcripts/unit-architecture.txt
- Core forbidden dependency scan: bundle://proof/common/transcripts/core-forbidden-scan.txt
- Anti-stub audit: bundle://proof/common/transcripts/anti-stub-scan.txt
- Adversarial negative proof: N/A process/no production behavior; the dependency scan rejects module, infrastructure, EF, storage, MAF, and driver tokens.

## Closure

The Core seed is minimal and dependency-clean.
