# Consumer Overrides

Use this path when a downstream app consumes `CanDoItAll.Components.BaseLib` and needs its own brand theme without rebuilding BaseLib Tailwind sources.

## Minimal Pattern

1. Wrap the app shell or page shell in `ThemeHost`.
2. Load an app-owned stylesheet after BaseLib styles.
3. Override semantic `--cad-*` variables inside the chosen `data-cad-theme` scope.

## Blazor Example

```razor
@using CanDoItAll.Components.BaseLib

<ThemeHost ThemeKey="@themeKey" ColorScheme="@colorScheme">
    @Body
</ThemeHost>

@code {
    private string themeKey = CadThemes.Light;
    private string colorScheme = "light";
}
```

For built-in light or dark, `ThemeKey="@CadThemes.Light"` or `ThemeKey="@CadThemes.Dark"` is enough.

For custom keys, keep the theme key descriptive and set `ColorScheme` explicitly when browser-native controls must follow the same light or dark intent.

## CSS Override Example

```css
[data-cad-theme="brand-light"] {
  color-scheme: light;
  --cad-color-page-bg: #f4f7fb;
  --cad-color-surface: rgba(255, 255, 255, 0.98);
  --cad-color-border: #bfd1e5;
  --cad-color-text-strong: #0b1b2b;
  --cad-tone-primary-solid-bg: #0d3b66;
  --cad-tone-primary-solid-hover: #124d86;
  --cad-tone-primary-solid-border: #0d3b66;
  --cad-tone-primary-solid-fg: #ffffff;
  --cad-tone-secondary-soft-bg: #e0f2fe;
  --cad-tone-secondary-soft-border: #7dd3fc;
  --cad-tone-secondary-soft-fg: #0c4a6e;
  --cad-radius-control: 0.75rem;
  --cad-radius-panel: 1.5rem;
}

[data-cad-theme="brand-dark"] {
  color-scheme: dark;
  --cad-color-page-bg: #06111d;
  --cad-color-surface: rgba(10, 22, 36, 0.92);
  --cad-color-border: rgba(120, 153, 186, 0.32);
  --cad-color-text-strong: #edf5ff;
  --cad-tone-primary-solid-bg: #9fd3ff;
  --cad-tone-primary-solid-hover: #c4e4ff;
  --cad-tone-primary-solid-border: #9fd3ff;
  --cad-tone-primary-solid-fg: #0b1b2b;
  --cad-tone-secondary-soft-bg: rgba(14, 116, 144, 0.28);
  --cad-tone-secondary-soft-border: rgba(103, 232, 249, 0.34);
  --cad-tone-secondary-soft-fg: #cffafe;
}
```

## Runtime Switching

The sandbox pattern is the current reference:

- state holder: `samples/CanDoItAll.Components.Sandbox/SandboxThemeState.cs`
- layout-level scope: `samples/CanDoItAll.Components.Sandbox/Components/Layout/MainLayout.razor`

The important rule is scope ownership. Put `ThemeHost` high enough that the entire shell switches, not only one inner content card.

## What Not To Do

- Do not edit BaseLib's generated `output.css` in the consumer app.
- Do not override individual button, badge, or alert selectors before trying the semantic tokens.
- Do not hard-code palette values on pages that should follow the app theme.
- Do not introduce public shorthand tone names.

## Validation Checklist

- Does one token change propagate to all expected shared surfaces?
- Does the shell switch theme, not only the inner content?
- Is the target large-screen desktop viewport readable? Check small and medium widths only for reusable basic BaseLib work or explicit user scope.
- If using a custom theme key, is `color-scheme` still correct for inputs and scrollbars?
