# Target Solution

## End State

- The app-specific facade project lives at `repo://src/CanDoItAll.AppComponents/CanDoItAll.AppComponents.csproj`.
- Its assembly and root namespace are `CanDoItAll.AppComponents`.
- The web app and component test project reference the renamed project path.
- App-shell consumers import `CanDoItAll.AppComponents`.
- Shared component package references remain under `CanDoItAll.Components.*`.

## Boundaries

- The sibling component-library repository remains untouched.
- The WebGL sandbox project remains `CanDoItAll.Components.WebGlSandbox`.
- This bundle does not move UI primitives into or out of component packages.
- This bundle does not change app-shell behavior, layout, services, or styling.

## Validation Shape

- Source assertions prove the old facade path and namespace were replaced only where appropriate.
- Targeted build proves the renamed project compiles.
- Component tests prove the direct test consumer still resolves app-shell types.
- Stale-reference search proves the old facade project reference is gone from source-controlled build inputs.
