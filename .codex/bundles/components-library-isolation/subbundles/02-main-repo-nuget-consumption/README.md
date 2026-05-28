# 02-main-repo-nuget-consumption

## Status

- `Completed`

## Objective

- Make the main repository consume moved component libraries from `ExternalPackages` through `PackageReference`, with no direct project references to moved component projects.

## Covered Inputs

- REQ-004, REQ-005, REQ-006.

## Prerequisites

- SB01 completed with built packages available.

## Exact Source References

- `repo://src/CanDoItAll.Components/CanDoItAll.Components.csproj`
- `repo://src/CanDoItAll.Components.WebGlSandbox/CanDoItAll.Components.WebGlSandbox.csproj`
- `repo://src/CanDoItAll.Web/CanDoItAll.Web.csproj`
- `repo://src`
- `repo://tests`
- `bundle://proof/SB02/manifest.md`
- `bundle://proof/SB02/semantic-invariants.md`

## Deliverables

- `ExternalPackages` populated with moved component `.nupkg` files.
- Main repo `NuGet.config` includes local package source.
- All main repo references to moved component projects become package references.
- Main-kept component projects still exist and compile against packages.

## Dependency Impact

- SB03 and SB04 depend on this isolation boundary. If any moved component is still project-referenced, the main solution is not actually lit up by packages.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Copy packages from the components repo staging output into `repo://ExternalPackages`.
2. Add or update `NuGet.config` with the repo-relative local package source.
3. Convert every main-repo `ProjectReference` to a moved component project into a `PackageReference` version `0.1.0`.
4. Keep project references to `CanDoItAll.Components` and `CanDoItAll.Components.WebGlSandbox` where appropriate because those projects remain in main.
5. Run a direct reference audit.
6. Restore/build representative main projects before moving to Tailwind work.

## Scope Exceptions

- `CanDoItAll.Components` and `CanDoItAll.Components.WebGlSandbox` are not packages for this split and remain as main repo projects.

## Do Not Do

- Do not add a project reference from main repo to `C:/repositories/CanDoItAll.Components`.
- Do not change package versions away from `0.1.0`.
- Do not weaken package dependencies by manually adding DLL references.

## Acceptance Checklist

- `rg "CanDoItAll.Components.(BaseLib|CanvasLib|Common|Charts|Mermaid|OverlayLib|WebGlLib|Sandbox).*csproj" -g "*.csproj"` has no main-repo project-reference matches.
- `dotnet restore` can resolve all moved component packages from `ExternalPackages`.
- Main-kept projects compile against packages.

## Proof Required

- `proof/SB02/manifest.md` with changed-file hashes, package inventory, restore/build transcripts, project-reference audit, source assertions, and anti-stub audit.
- `proof/SB02/semantic-invariants.md` naming package-consumption invariants and disallowed direct source coupling.
- Failing-first or before/after audit transcript showing direct project references existed before conversion and are absent after conversion.
- Passing restore/build transcript.

## Browser Validation Logging

- N/A. This phase is package consumption and build graph proof only.

## Progression Gate

- Pass only when the direct-reference audit is clean and main projects restore/build from `ExternalPackages`.

## Suggested Agent Prompt

```text
Implement SB02 only. Convert the main repo to local NuGet package consumption for moved components, prove no direct moved-component project references remain, and stop if restore cannot resolve packages.
```
