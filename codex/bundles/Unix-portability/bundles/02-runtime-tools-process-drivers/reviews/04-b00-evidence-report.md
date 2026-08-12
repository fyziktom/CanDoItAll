# B00 evidence report

## Outcome

B00 is ready for independent Gate R0 review. The runtime plan is re-anchored to the exact pushed main and sibling commits, all prepared references resolve, every discovered P0/P1 execution surface has a named owner and target subbundle, and no production source was changed.

The proof tier is `Behavioral`. Existing unchanged full-suite evidence is reused, while a class-named process/runtime slice was rerun on Windows and actual Linux to keep the validation loop safe and fast.

## Immutable anchors

| Repository | Branch | Commit |
|---|---|---|
| CanDoItAll | `unix-adoption` | `dd78ffa9769ba1d125b8be81a4b303df37c32505` |
| CanDoItAll.Components | `development` | `8372c1d55f21b349f8e859470b02eeb4421e96ca` |
| CanDoItAll.FileTools | `development` | `f31e20d054003348c7557b9634e0838fc5996ae0` |

The core handoff is implementation-only under `HOSTED-PORTABILITY-VALIDATION-001`. C4, hosted validation, genuine macOS proof, R4, and final support claims remain deferred.

## Source and architecture evidence

- Prepared source: `62ea8ee0cc42c1c06da934d126a5c18f8237a89f`.
- Execution source: `dd78ffa9769ba1d125b8be81a4b303df37c32505`.
- Prepared-reference check: all original 33 references still exist. The execution manifest adds four newly discovered current surfaces and resolves 37/37 total references with 0 missing.
- Static scan: 4,826 tracked files; 27,261 non-truncated candidate findings. The raw scan is discovery input and is not misrepresented as a zero-finding policy gate.
- Runtime inventory: 17 classified surfaces; 0 unclassified P0/P1 runtime surfaces.
- Process ownership inventory: 12 launch/recovery surfaces with current ambiguity and target correction explicitly recorded.
- Executable capability inventory: 13 typed capability families with host candidates, policy source, availability states, and owners.
- CodeAnalytics: unchanged snapshot `snap-20260810211432-d225a84b`; no Error findings and no project-level dependency cycle. Existing module/type cycles remain inputs to later bounded reviews.

The authoritative boundary remains: generic hosts execute and report facts; Workbench, Manager, MCP, tools, plugins, and Security retain their domain adapters; Processes alone owns process-strategy eligibility, recovery meaning, evidence, escalation, and domain failure semantics.

## Behavioral characterization

| Host | Slice | Result | Artifact |
|---|---|---:|---|
| Windows 10 / .NET SDK 10.0.302 | Ten named unit classes covering workspace host/command, MCP, Workbench launcher, Manager helpers, plugins, tuning, and process drivers | 165/165 | `artifacts/unix-portability/B00/windows/b00-runtime-slice-windows.trx` |
| Windows 10 / .NET SDK 10.0.302 | `WatchSupervisorServiceIntegrationTests` | 4/4 | `artifacts/unix-portability/B00/windows/b00-watch-supervisor-windows.trx` |
| Linux Docker Engine 29.6.2 / `mcr.microsoft.com/dotnet/sdk:10.0` image `sha256:ed034a8bf0b24ded0cbbac07e17825d8e9ebfe21e308191d0f7421eaf5ad4664` | Same ten named unit classes | 165/165 | `artifacts/unix-portability/B00/linux/b00-runtime-slice-linux.trx` |
| Same Linux container | `WatchSupervisorServiceIntegrationTests` | 4/4 | `artifacts/unix-portability/B00/linux/b00-watch-supervisor-linux.trx` |

The Linux runs execute the existing platform-neutral Release test assemblies with the Linux SDK test host. This avoids a redundant solution build while still exercising OS branches under Linux. No production C# or dependency graph changed after the accepted A07 candidate, so the authoritative Windows 7,459/7,459 and Linux 7,459/7,459 aggregate evidence remains applicable.

The Integration project’s `dotnet test --no-build` path returned success without discovering or running the filtered tests. B00 therefore invoked the exact existing Integration test assembly through `dotnet vstest`; both TRXs prove four tests were discovered and passed. This tooling behavior is explicit and is not counted as a product test pass by the skipped command.

## External and native dependency review

- Components and FileTools are connected through the committed `Directory.Build.targets` local-project-reference switch and pinned to the sibling commits above. Package mode remains explicit through `UseLocalCanDoItAllLibraries=false` for reproducible tests.
- Docker is available as a Linux engine and is classified under B05. B00 does not claim Docker recipe compatibility beyond the recorded probe.
- node/npm/npx and the managed Playwright MCP root belong to B03/B04. Global cache selection is an open B04 P1 defect rather than accepted support.
- PowerShell/runas is a Windows-only Workbench presentation path owned by B02. No Unix elevation fallback is allowed.
- Python/Conda are runtime-node intents currently serialized through the Workbench shell path; B02 owns typed profile adaptation. Presence is not treated as capability.
- WMI is a Windows Manager recovery adapter. The current Unix name-only fallback is an explicit B03 P0/P1 defect; procfs/libproc/ps support is not claimed before B03 proof.
- Keychain/Secret Service and native vault helper execution remain Security-owned. Genuine macOS Keychain proof is deferred exactly as recorded by the operator and is not made a runtime R0 prerequisite.
- FileTools desktop launch is an external B05 capability. The local project reference enables development but does not by itself prove a supported host profile.

## Redaction and artifact integrity

The schema-3 artifact scanner accounted for all seven B00 evidence files, scanned all seven as text, skipped no oversized/non-text/unreadable file, and reported 0 findings. Artifact: `artifacts/unix-portability/B00/b00-secret-scan.json`.

## Split decision

The program crosses more than eight project owners and is expected to exceed 60 production files. The prepared B01–B07 subbundles already separate those owners and preserve independent gates. B90 and B91 remain conditional and are not invoked. No additional split is required at R0.

## Gate recommendation

`GO` for Gate R0, subject to independent review. If accepted, B01 alone becomes eligible. B01 must begin with dependency-direction confirmation and named failing-first characterization before production edits.
