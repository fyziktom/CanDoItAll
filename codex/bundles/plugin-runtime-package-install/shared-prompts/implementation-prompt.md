# Implementation Prompt

Implement the active subbundle only.

Preserve the existing plugin grant architecture and strongly typed manifest contracts. Keep `CanDoItAll.Modules.Plugins` as runtime infrastructure and move concrete plugin implementations into `src/plugins` projects. Runtime packages must install from validated zip packages without compiling the application again. Packages containing assemblies may require restart because DI registrations happen at startup; surface that explicitly instead of attempting hidden fallback loading.

Use shared Blazor components already present on `/plugins` for layout. Record build/test/browser proof and update `reviews/01-execution-report.md` before closing each subbundle.
