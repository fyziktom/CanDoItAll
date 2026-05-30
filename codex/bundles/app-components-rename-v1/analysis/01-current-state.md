# Current State

- `repo://src/CanDoItAll.AppComponents/CanDoItAll.AppComponents.csproj` is a Razor SDK project with `AssemblyName` and `RootNamespace` set to `CanDoItAll.AppComponents`.
- The project currently compiles only the app shell, app tab strip, and tuning boundary source while removing broad historical `Components/**` and `Primitives/**` content from compilation.
- `repo://CanDoItAll.slnx` includes `src/CanDoItAll.AppComponents/CanDoItAll.AppComponents.csproj`.
- `repo://src/CanDoItAll.Web/CanDoItAll.Web.csproj` references `..\CanDoItAll.AppComponents\CanDoItAll.AppComponents.csproj`.
- `repo://tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj` references `..\..\src\CanDoItAll.AppComponents\CanDoItAll.AppComponents.csproj`.
- The web app and component tests import the exact facade namespace `CanDoItAll.AppComponents`; package namespaces such as `CanDoItAll.Components.BaseLib` are separate sibling-repo artifacts and remain.
- Docs identify the main-repo facade as `CanDoItAll.AppComponents`, removing the semantic collision with the sibling component-library repository name.
