# Bundle intent and target direction

## The question this bundle is answering

The repository currently renders process structures through a 2D typed canvas workbench. The user wants a concept branch that explores whether **WebGL with depth** can make dense process diagrams easier to understand.

This bundle answers that question with a practical direction rather than abstract advice.

## Target direction

### 1. Thin Blazor wrapper, JS-owned runtime

The concept should follow the same broad pattern as the existing `CanvasWorkbench` surface:

- Blazor owns typed contracts, coarse-grained event handling, and hosting.
- JavaScript owns rendering, camera movement, hit-testing, drag preview, and animation timing.

### 2. Guided perspective 3D, not a free-fly graph editor

The concept should not become a free-fly 3D graph editor. That would add camera complexity and label occlusion faster than it adds value.

Instead:

- preserve a deterministic center lane for the main process path,
- spread role nodes around that lane instead of stacking them inside it,
- use semantic Z-depth to stage the route and reduce label collisions,
- default to a perspective camera with orbit, pan, and zoom conventions that feel like a standard 3D editor.

### 3. Universal library first, process-specific adapter second

The core library must remain generic. All process-specific projection belongs in the sandbox or in a small adapter layer outside the universal library.

### 4. Dedicated sandbox instead of production-route mutation

The concept should have its own executable sandbox project. That protects the production process editor from partial or misleading experiments and gives Playwright a focused proof route.

### 5. Proof must be semantic, not purely visual

Because WebGL and canvas are difficult to manipulate through raw browser automation alone, the concept must expose a semantic automation bridge and DOM mirror layer from day one.
