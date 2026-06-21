# SB02 Proof Manifest

## Scope

- Inline simple-note cards use more available width before wrapping.
- CanvasLib package consumption is updated consistently through `CanDoItAll.Components.CanvasLib` `0.1.1`.
- Workbench placement estimation mirrors the CanvasLib note width change.

## Evidence

- Failing-first browser width transcript: `bundle://proof/SB02/transcripts/failing-first-browser-width.txt`
- Passing browser width transcript: `bundle://proof/SB02/transcripts/passing-browser-width.txt`
- Passing component placement transcript: `bundle://proof/SB02/transcripts/passing-component-placement-tests.txt`
- CanvasLib pack transcript: `bundle://proof/SB02/transcripts/canvaslib-pack.txt`
- CanvasLib nuspec proof: `bundle://proof/SB02/transcripts/canvaslib-0.1.1-nuspec.txt`
- Inline note screenshot: `bundle://proof/SB02/screenshots/04-inline-note-width.png`
- Converted node stability screenshot: `bundle://proof/SB02/screenshots/06-mutation-results.png`
- Source assertions: `bundle://proof/source-assertions.txt`
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`
- Changed-file hashes: `bundle://proof/changed-file-hashes.txt`

## SHA-256 Changed-File Hashes

- `B51375695EC2D3F10044ED328D62FFECEDB51C8F05197CD67559C964C502DB94` `repo://src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructurePlacementPolicy.cs`
- `E85CF8F69ADD574B9C65E55712CB5F9F7AAB33A808EF55C7FAD6BE08F98DB241` `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureCanvasFeedbackBundle.cs`
- `D3B9626E71D55C357E5892E1E6427895D76857C76426E9D3E67FECCA3A3A5362` `repo://ExternalPackages/CanDoItAll.Components.CanvasLib.0.1.1.nupkg`
- `CE4C479A3EBCB8ECEFE173792295BE01FFFB8652C6A04EE23EE08E1DF11DDDB7` `CanDoItAll.Components src/CanDoItAll.Components.CanvasLib/CanDoItAll.Components.CanvasLib.csproj`
- `1088BDE8502686EA08ABDA426157A8C1CC6F281F3D946EE64B2F0E17E650BB54` `CanDoItAll.Components src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/01-foundation.js`
- `CE51FAEDD8C1D437C3723BB45933C68EAAA5F4E229F89E7F66DA8A45F4287446` `CanDoItAll.Components src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/02-layout-and-legacy-render.js`
- `354F7E425659E8711F7F692F6C3D369F22D6B487D5D6BEE5A62F158EA8A8E514` `CanDoItAll.Components src/CanDoItAll.Components.CanvasLib/wwwroot/css/workbench/scene/04-scene-and-nodes.css`

## Changed Sources

- `repo://src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructurePlacementPolicy.cs`
- `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureCanvasFeedbackBundle.cs`
- `repo://ExternalPackages/CanDoItAll.Components.CanvasLib.0.1.1.nupkg`
- `CanDoItAll.Components src/CanDoItAll.Components.CanvasLib/CanDoItAll.Components.CanvasLib.csproj`
- `CanDoItAll.Components src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/01-foundation.js`
- `CanDoItAll.Components src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/02-layout-and-legacy-render.js`
- `CanDoItAll.Components src/CanDoItAll.Components.CanvasLib/wwwroot/css/workbench/scene/04-scene-and-nodes.css`

## Result

- SB02 status: `Completed`
- Closure gate: `Passed`
