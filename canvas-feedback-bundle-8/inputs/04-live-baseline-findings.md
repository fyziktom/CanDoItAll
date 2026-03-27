# Live Baseline Findings

## Runtime

- App route validated at `http://127.0.0.1:5188`
- Project structure baseline route: `http://127.0.0.1:5188/projects/10a2d1ce-ca8e-4c29-b56d-8483b60955f0/structure`
- Runtime readiness endpoint confirmed the app was serving the page.

## Observations

- Initial click attempts on toolbox group headers failed because the health floating window overlapped the toolbox and intercepted pointer input.
- After hiding the health window, the `Planning` toolbox group expanded correctly, which proves the accordion logic exists but the default desktop layout is wrong.
- A browser-created Excel node titled `Customers with fake emails` exposed repeated subtype and upload signals in the selection panel.

## Evidence

- `C:\repositories\CanDoItAll\output\playwright\feedback8\baseline-desktop.png`
- `C:\repositories\CanDoItAll\output\playwright\feedback8\excel-selection-desktop.png`
