# Source Artifacts

| Artifact ID | Source | Description | Workspace Location |
| --- | --- | --- | --- |
| `ART-01` | Chat request | Raw user request covering large-screen layout density, projects filter row compaction, modals, component flexibility, Tailwind-first styling, and bundle execution. | `inputs/00-original-request.md` |
| `ART-02` | Chat attachment | Embedded screenshot of `/projects` showing stacked filters and wasted first-screen space. | Chat-only artifact, summarized in `inputs/00-original-request.md` |
| `ART-03` | Playwright CLI baseline | Browser screenshot of `/projects` during bundle preparation at a large desktop viewport. | `C:\repositories\CanDoItAll\.playwright-cli\page-2026-04-01T21-06-11-094Z.png` |
| `ART-04` | Playwright CLI baseline | Accessibility snapshot of `/projects` confirming the stacked board controls and open startup database modal state. | `C:\repositories\CanDoItAll\.playwright-cli\page-2026-04-01T21-01-22-371Z.yml` |
| `ART-05` | Playwright CLI baseline | Accessibility snapshot of `/settings` confirming the repeated header plus summary plus tabs stack before the primary form surface. | Captured inline during bundle prep; route and observations logged in `analysis/01-current-state.md` |
| `ART-06` | Running workspace state | Managed watch session baseline for the web app used by the execution loop. | Managed app session `app_91afc34b68a3419d9c59e2fb860e41f2` |
| `ART-07` | Tailwind runtime baseline | Tailwind watch process started during bundle preparation to support nearby CSS validation. | `C:\repositories\CanDoItAll\output\tailwind\watch.stdout.log`, `C:\repositories\CanDoItAll\output\tailwind\watch.stderr.log` |

