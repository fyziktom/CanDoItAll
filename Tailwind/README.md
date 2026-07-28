# CanDoItAll Application Tailwind

This workspace builds application-specific CanDoItAll styles. Shared component styles are owned by the sibling [CanDoItAll.Components](https://github.com/fyziktom/CanDoItAll.Components) repository and arrive through the `CanDoItAll.Components.BaseLib` package.

From the repository root, install this workspace's dependencies:

```powershell
npm install --prefix .\Tailwind
```

Build once:

```powershell
npm run tailwind:build
```

Watch for changes:

```powershell
npm run tailwind:watch
```

The root commands delegate to the scripts in `Tailwind/package.json`. From this directory, the equivalent commands are `npm run build` and `npm run watch`.

The generated application stylesheet is:

```text
src/App/CanDoItAll.Web/wwwroot/css/output.css
```

The web host loads shared component CSS before this application stylesheet:

1. `_content/CanDoItAll.Components.BaseLib/css/output.css`
2. `css/output.css`

Keep reusable component structure and styling in the owning Components package. Keep only application-specific composition and overrides here.
