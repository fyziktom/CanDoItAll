# Proposed project and asset plan

| Type | Target path | Role |
| --- | --- | --- |
| New project | src/CanDoItAll.Components.WebGlLib | Universal RCL with typed scene contracts and JS runtime. |
| New project | src/CanDoItAll.Components.WebGlSandbox | Dedicated concept host for template-backed WebGL review. |
| Potential new tests | tests/CanDoItAll.Tests.Components | Wrapper, adapter, and sandbox state tests. |
| Potential new tests | tests/CanDoItAll.Tests.Playwright | Semantic WebGL proof and screenshot capture. |
| Potential new tooling | tools/webgllib | Deterministic asset bundling for Three.js and runtime modules. |

## Asset strategy recommendation

- Keep built assets committed in the repository.
- Use repository-local tooling under `tools/webgllib` or equivalent.
- Avoid CDN references.
- Expose runtime through a single global namespace entry for Playwright parity with the current canvas runtime.
