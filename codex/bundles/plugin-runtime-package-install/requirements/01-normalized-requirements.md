# Normalized Requirements

| Requirement | Statement | Owning subbundle | Proof |
| --- | --- | --- | --- |
| `R001` | Preserve the bundle-backed workflow and record implementation proof. | SB04 | Bundle validators and execution report updates. |
| `R002` | Move concrete Docker, Gmail, Office365, and shared email plugin implementation code out of `CanDoItAll.Modules.Plugins` into `src/plugins` projects. | SB01 | Build plus catalog tests proving existing plugins still register. |
| `R003` | Add the new plugin implementation projects to `CanDoItAll.slnx`. | SB01 | Solution file diff and build. |
| `R004` | Keep `CanDoItAll.Modules.Plugins` focused on plugin runtime services, catalog, grants, settings, OAuth, package services, persistence, and UI. | SB01 | Source review plus build. |
| `R005` | Provide a runtime package install service that can install a package zip without recompiling the application. | SB02 | Unit/integration tests with generated package zips. |
| `R006` | Support plugin zips containing manifest, libraries, and optional icon metadata. | SB02 | Package install tests inspect extracted files and manifest result. |
| `R007` | Validate uploaded/catalog package manifests through the existing strongly typed manifest validator. | SB02 | Invalid manifest test fails predictably. |
| `R008` | Reject unsafe zip entries such as path traversal. | SB02 | Path traversal package test fails. |
| `R009` | Make installed package manifests visible through the plugin catalog immediately after install. | SB02 | Catalog service test after package install. |
| `R010` | Load installed package assemblies at startup for workflow executor/service discovery when packages contain libraries. | SB02 | Registration test or startup scan proof. |
| `R011` | Persist restart-required state when package assemblies need startup registration. | SB02 | Package install result and restart status test. |
| `R012` | Add `/plugins` UI support for downloading/installing a package from a configured plugin catalogue. | SB03 | Component test and browser proof. |
| `R013` | Add `/plugins` UI support for uploading a plugin zip. | SB03 | Component test and browser proof. |
| `R014` | Add a restart-required UI banner/action that triggers graceful app restart through host lifetime. | SB03 | Component/API test and browser proof. |
| `R015` | Validate existing plugin behavior still works after the move. | SB04 | Existing targeted plugin tests pass. |

## Raw Note Coverage

| Raw note | Requirement mapping | Status before execution |
| --- | --- | --- |
| `N001` | `R001` | Planned |
| `N002` | `R002`, `R004` | Planned |
| `N003` | `R005`, `R009`, `R012` | Planned |
| `N004` | `R005`, `R010`, `R011` | Planned |
| `N005` | `R011`, `R014` | Planned |
| `N006` | `R014` | Planned |
| `N007` | `R002`, `R003` | Planned |
| `N008` | `R015` | Planned |
| `N009` | `R012` | Planned |
| `N010` | `R006`, `R013` | Planned |
