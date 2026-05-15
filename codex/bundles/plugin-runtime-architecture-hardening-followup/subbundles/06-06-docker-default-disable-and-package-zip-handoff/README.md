# SB06 Docker Default Disable And Package ZIP Handoff

## Status

- `Completed`

## Objective

Disable Docker as a default app plugin, build it as a runtime package ZIP, prove the ZIP installs and activates correctly, and leave the app running without Docker registered by default so the user can manually install it.

## Success Criteria

- Docker is not registered by default in app composition.
- The app builds and starts without Docker default registration.
- Docker runtime package ZIP is produced with manifest, icon, assemblies, and required runtime dependencies.
- Docker package install/activation is tested before handoff.
- Final running app state has Docker absent until the user installs the package.
- The ZIP path and checksum are recorded.

## Covered Inputs

- PRH-010 Docker Default Disable And Package ZIP Handoff
- PRH-011 Validation And Proof
- FIND-011, FIND-012

## Prerequisites

- SB01 passed real package assembly activation gate.
- SB04 supplied Docker icon metadata or fallback.
- SB05 performance gate passed or accepted with clear residual risk.
- SB02 is strongly recommended so Docker package install/activation failures can be diagnosed in the plugins page logs.
- Read the `Docker Package Handoff` rows in `inventories/plugin-runtime-architecture-hardening-checklist.xlsx`.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\RuntimeHostServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Docker\CanDoItAll.Plugin.Docker.csproj`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Docker\DockerPluginServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Docker\DockerBundledPlugin.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Docker\DockerPluginConstants.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Docker\DockerWorkflowExecutors.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginPackageServices.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs`

## Deliverables

- Docker removed from default composition registration.
- Docker package manifest adjusted for runtime package source/trust according to SB01 contract.
- Repeatable package build script or documented command that creates the Docker ZIP.
- Docker ZIP artifact under an agreed bundle or repo artifacts location.
- ZIP checksum recorded.
- Tests proving package install/activation and executor discovery.
- Final browser/app proof showing Docker absent by default and installable through runtime package flow.

## Dependency Impact

- This is the final closure subbundle. It validates that the entire follow-up architecture can run with a concrete default plugin removed and installed as a runtime package.

## Validation Depth

- `End-to-end package handoff and closure`

## Implementation Steps

1. Verify SB01, SB04, and SB05 gates are complete in `reviews/01-execution-report.md`.
2. Remove Docker from default app composition registration.
3. Ensure Docker package code uses the runtime package activation contract and does not emit bundled descriptor identity when installed.
4. Prepare Docker package manifest with package id, plugin id, source/trust, version, icon, assemblies, and dependencies.
5. Build Docker plugin output and assemble ZIP with deterministic contents where practical.
6. Compute and record ZIP checksum.
7. Run automated test that installs the Docker ZIP and proves Docker executors appear after activation.
8. Start the app without Docker default registration and prove Docker does not appear before package install.
9. Install the ZIP through the runtime package flow or equivalent end-to-end path and prove Docker appears after activation.
10. Return the app to the requested final state: running without Docker default module registered and ready for the user to manually install the ZIP.
11. Update execution report with artifact path, checksum, commands, browser proof, and residual risks.

## Scope Exceptions

- Do not remove Gmail or Office365 default registration.
- Do not require real Docker daemon access for catalog/activation proof unless an executor runtime test explicitly depends on it; executor runtime behavior can be validated with existing abstractions/mocks if Docker is unavailable.
- Do not leave the app running with Docker installed by default.

## Do Not Do

- Do not hard-code a Docker special case in package runtime.
- Do not hand off an untested ZIP.
- Do not claim success based on manifest-only install tests.
- Do not leave Docker registered in composition.

## Acceptance Checklist

- [x] Docker default composition registration removed.
- [x] App builds without default Docker.
- [x] Docker package ZIP created.
- [x] ZIP checksum recorded.
- [x] Package install/activation test uses real package assembly output.
- [x] Docker executors appear after package install/activation.
- [x] Docker is absent in app before manual install.
- [x] Final app run state and artifact path are recorded.

## Proof Required

- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.sln`
- Targeted package install/activation test for Docker ZIP.
- Browser proof on `/plugins` before install showing Docker absent.
- Browser proof of package upload/install path, or exact automated equivalent plus logs if browser install is impractical.
- Browser proof on workflow canvas after install showing Docker under plugin submenu.
- ZIP path and checksum.
- Execution report update.

## Browser Validation Logging

- Target routes: `/plugins` and workflow canvas/editor route.
- Required viewport passes: maximized desktop for package install and workflow menu; narrower only if layout changed during this subbundle.
- Required actions: confirm Docker absent before install, upload/install Docker ZIP, confirm log entry and catalog presence after activation, open workflow canvas plugin submenu and confirm Docker executors.
- Screenshot evidence: `artifacts/sb06-docker-absent-before-install.png`, `artifacts/sb06-docker-package-installed.png`, `artifacts/sb06-docker-canvas-menu-after-install.png`.
- Review questions: Is Docker truly absent before package install? Does install failure produce useful logs? Are Docker executors available only through package activation?

## Progression Gate

- Final closure may happen only after the app is left running without Docker default registration and the tested Docker ZIP path/checksum are recorded for the user.

## Suggested Agent Prompt

```text
Implement SB06 only from C:\repositories\CanDoItAll\codex\bundles\plugin-runtime-architecture-hardening-followup.
Remove Docker from default registration, build a runtime Docker plugin ZIP, prove real package install/activation and executor discovery, and leave the app running without Docker as a default module. Record ZIP path/checksum and all proof in reviews/01-execution-report.md.
```
