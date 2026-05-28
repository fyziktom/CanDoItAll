# CanDoItAll Main Tailwind

This workspace builds only main CanDoItAll app styles. Shared component styles are built in `C:\repositories\CanDoItAll.Components\Tailwind` and are delivered through the `CanDoItAll.Components.BaseLib` NuGet package.

Install dependencies:

```powershell
npm install
```

Build once:

```powershell
npm run build
```

Watch mode:

```powershell
npm run watch
```

This workspace compiles to `src/CanDoItAll.Web/wwwroot/css/output.css`.

The web app loads styles in this order:

1. `_content/CanDoItAll.Components.BaseLib/css/output.css`
2. `css/output.css`

From the repo root you can also run:

```powershell
npm run tailwind:build
npm run tailwind:watch
```
