# Original Request

Date received: 2026-07-12.

> Prepare a new detailed bundle for integration of the FileBrowser and FileInteraction components from `C:\repositories\CanDoItAll.FileTools` into the main `C:\repositories\CanDoItAll` app.
>
> The prior bundle at `C:\repositories\CanDoItAll.FileTools\codex\bundles\candoitall-filebrowserintegration`, especially `architecture\07-candoitall-integration.md` and the surrounding files, is an artifact from the transfer and improvements. It uses an older bundle structure. Prepare the new integration bundle using the updated `candoitall-bundle-workflow`.
>
> The first point is improvement of the Storage Driver in CanDoItAll. Test it properly. Only after it works may the UI phase start. Split UI into smaller parts; prove one case such as search of project files before continuing to more complex user stories.
>
> Every subbundle must contain all information required for a long-running implementation. Split subbundles into phases. After a phase, force a standard architecture review/refactor/cleanup so the next phase starts from a quality-tested base.
>
> Use the new C# architecture skills, including `csharp-architecture-governor` and related skills. The application targets large desktop screens only; do not spend time on small or medium screens.
>
> Prepare the bundle only. Do not start implementation.

## Binding Engineering Instructions

- Prefer the smallest correct change, typed contracts, explicit errors, masked actionable logs, and strict UI/Application/Domain/Infrastructure separation.
- Do not create magic-string identifiers, silent fallback mechanisms, trivial interfaces without a real boundary, or XML documentation comments.
- Keep Blazor components focused on rendering/orchestration; non-trivial behavior belongs in directly testable services.
- Use existing CanDoItAll Components wrappers and Radzen only if already present on the target surface. Tailwind may be used only where the existing project already uses it, and shared components take priority over raw structural markup.
- All implementation code uses fully cuddled Egyptian braces and one statement per line.
