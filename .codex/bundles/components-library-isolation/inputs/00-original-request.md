# Original Request

- Source: user request in Codex thread.
- Date captured: 2026-05-28.

```text
Main goal:
Isolation of the Components libraries and light up the main solution

Important inputs:
- I created repository C:\repositories\CanDoItAll.Components thats place where you must create new solution for components related projects
- you must move:
    - CanDoItAll.Components.BaseLib
    - CanDoItAll.Components.CanvasLib
    - CanDoItAll.Components.Common
    - CanDoItAll.Components.Charts
    - CanDoItAll.Components.Mermaid
    - CanDoItAll.Components.OverlayLib
    - CanDoItAll.Components.WebGlLib
    - CanDoItAll.Components.Sandbox
Move means that you will add them to new repo and remove from actual repo. You must build them as nuget packages (now private added as ExternalPackages folder in main repo). Do not connect just projects as references. Use builded nuget packages. It will speedup build. We do not change components too often now. Each package must have proper readme and information. Version of all set to 0.1.

- CanDoItAll.Components and CanDoItAll.Components.WebGlSandbox has references also to some other projects in main solution, so lets keep them there for now.
- you must solve split of the Tailwind. Main part of styles must be moved to new components repo. There are few styles related to just main candoitall. those must remain in the main repo. It means there will be two outputs.css added to projects that use components. one for BaseLib and related, and second for candoitall specific styles. Each repo must contain instructions for build the styles.

- remove Space3D projects from main slnx in main repo. Add another slnx that will contains them, but now we do not need them and it slow down build of main slnx.

- you must assure that everything is working as before.

- you must update documentation about this new repo.
```
