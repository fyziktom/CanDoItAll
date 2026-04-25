# Technology decision notes

## Options considered

### Raw WebGL with custom engine

Pros:
- maximum control,
- minimal third-party abstraction.

Cons:
- too low-level for a concept branch,
- duplicates scene/camera/input infrastructure already solved in the JS ecosystem,
- slows down the experiment.

### Babylon.js

Pros:
- strong engine-level tooling,
- good documentation for broader 3D scenarios.

Cons for this concept:
- heavier abstraction layer than is necessary for a typed diagram/workbench proof,
- more engine surface than the concept currently needs.

### Three.js

Pros:
- flexible scene graph for custom diagram work,
- good fit for a thin host wrapper and custom DOM overlay labels,
- easier to keep the concept workbench-specific instead of engine-centric.

Cons:
- more manual work for higher-level authoring helpers than some full engines.

## Chosen direction

Use **Three.js behind a thin Blazor wrapper** for the concept, while keeping:

- scene/text/automation conventions repository-specific,
- labels in HTML/DOM overlay space,
- process projection outside the universal library.

## External references noted during bundle preparation

- https://learn.microsoft.com/en-us/aspnet/core/blazor/performance/javascript-interoperability?view=aspnetcore-10.0
- https://learn.microsoft.com/en-us/aspnet/core/blazor/javascript-interoperability/location-of-javascript?view=aspnetcore-10.0
- https://threejs.org/docs/pages/Renderer.html
- https://doc.babylonjs.com/features/introductionToFeatures/chap1/first_scene/
