# SB02 Proof Manifest

## Changed File Hashes

- `repo://NuGet.config`: 34893EF22ADA088462F3A9FCF5327D2A5546DFE470AD6D70AC76E677F3BA16C4

## Source Proof

- `repo://NuGet.config` defines the local `ExternalPackages` source.
- `repo://ExternalPackages` contains the eight version `0.1.0` component packages.
- Representative consumers now use package references: `repo://src/CanDoItAll.Components/CanDoItAll.Components.csproj`, `repo://src/CanDoItAll.Components.WebGlSandbox/CanDoItAll.Components.WebGlSandbox.csproj`, `repo://src/CanDoItAll.Web/CanDoItAll.Web.csproj`, and `repo://tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj`.
- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`.

## Command Proof

- Failing-first transcript: `bundle://proof/SB02/transcripts/failing-first-direct-project-references.txt`
- Passing transcript: `bundle://proof/SB02/transcripts/sb02-closure-proof.txt`
- Semantic positive proof: `bundle://proof/SB02/transcripts/sb02-closure-proof.txt`
- Anti-stub audit: `bundle://proof/SB02/transcripts/sb02-closure-proof.txt`

## Package Inventory

- `repo://ExternalPackages/CanDoItAll.Components.BaseLib.0.1.0.nupkg`
- `repo://ExternalPackages/CanDoItAll.Components.CanvasLib.0.1.0.nupkg`
- `repo://ExternalPackages/CanDoItAll.Components.Common.0.1.0.nupkg`
- `repo://ExternalPackages/CanDoItAll.Components.Charts.0.1.0.nupkg`
- `repo://ExternalPackages/CanDoItAll.Components.Mermaid.0.1.0.nupkg`
- `repo://ExternalPackages/CanDoItAll.Components.OverlayLib.0.1.0.nupkg`
- `repo://ExternalPackages/CanDoItAll.Components.Sandbox.0.1.0.nupkg`
- `repo://ExternalPackages/CanDoItAll.Components.WebGlLib.0.1.0.nupkg`
