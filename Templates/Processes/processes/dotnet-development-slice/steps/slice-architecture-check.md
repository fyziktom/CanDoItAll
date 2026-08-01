# Check architecture and source-of-truth impact

Confirm canonical models, integration boundaries, persistence ownership, and UI/application/domain responsibilities before code starts.

Use the current project-structure mindmap and upstream slice scope packet as the source of truth. Copy explicit product root, app archetype, solution/project names, target framework, test framework, argument meanings, feature list, exclusions, and validation hooks exactly; treat explicit facts as resolved decisions rather than unresolved questions, and do not add optional behavior or substitute preferred defaults.

This is not a build, startup, or browser validation step. Read the upstream `ProductTargetState` record before producing any topology or solution context. `greenfield` authorizes creation of the declared baseline and requires `provisioningMode: "initialize"`; `existing` authorizes preservation and modification of the authoritative baseline and requires `provisioningMode: "verify-existing"`. `ProductTargetFilesystemState` is a read-only physical observation, not provisioning intent: a missing, empty, or preliminary target must not override the upstream decision, and a populated directory does not prove that an authoritative baseline exists. For an `existing` state, use read-only current-run evidence to ground the exact solution and project topology before emitting `verify-existing`. If the target-state decision conflicts with project structure, current-run evidence, or a non-directory/unavailable target, record the concrete contradiction and return `Blocked`; do not guess, silently switch modes, or retry an unchanged classification.

When launch variables contain `DotNetProductBaselineContract`, treat `status: "discovered"` with `discoveryComplete: true` as current-run proof that an existing .NET baseline must be preserved. When `topologySampleComplete: true`, preserve its relative solution and project paths exactly; do not replace them with the example layout below. When `topologySampleComplete: false`, the arrays are samples and the counts describe the bounded discovery, so complete the exact topology with bounded read-only workspace inspection before emitting `verify-existing`. Trust target-framework and test-project metadata only when `metadataInspectionComplete: true`; otherwise reread the affected project files. Treat `partial`, `unavailable`, duplicate-name, incomplete-sample, or incomplete-metadata findings as explicit evidence requiring bounded clarification, not as permission to initialize over existing files. Treat `not-found` as proof of absence only when `discoveryComplete: true`.

## Solution-context contract

Alongside the architecture decision, produce the required `.NET solution context` artifact. Its body must contain exactly one fenced `json` block using this schema. Replace every placeholder with an explicit, source-grounded decision. Every solution, candidate, required-project, test-project, and initialization path must be a bare path relative to the grounded ProductRoot, resolved exactly once; never put a native absolute path, `external-target/...` alias, ProductRoot prefix, or managed-artifact path into this JSON. `verify-existing` records only topology that is proven present and must not carry templates or initialization details. `initialize` is required for an upstream `greenfield` target state and must carry a complete, explicit initialization plan. Make one bounded technical choice when the authoritative scope leaves a non-essential choice open; do not write a `preserve-existing` pseudo-plan or switch to verification merely because no baseline exists yet.

`application.templateOptions` is only for optional `dotnet new` option flags. Use an empty array when no option is required. Each non-empty entry must be one flag beginning with `--`; never place a feature label, template name, archetype, or bare option value in that array. The runtime independently checks the workspace-approved options, so do not invent switches.

`initialization.targetFramework` is the single source of truth for both the app and test projects. It is not a template option: never encode `--framework` or its value in `application.templateOptions`, and never rely on the installed SDK default. Choose a value supported by both selected templates. If no common supported target exists, return `Blocked` with the architecture conflict instead of emitting a partial initialize context.

`initialization.application.template` and `initialization.tests.template` are machine fields, not display labels. Each must be exactly one workspace-approved `dotnet new` short identifier with no spaces or inline flags: use values such as `blazorwasm`, `webapi`, `webapp`, `console`, `classlib`, `xunit`, `nunit`, or `mstest`; do not write labels such as "Blazor WebAssembly App" or "xUnit test project". Select the identifier from the accepted architecture and current delivery requirements, then put only optional approved flags in `application.templateOptions`.

```json
{
  "schema": "dotnet.solution-context/v1",
  "provisioningMode": "initialize",
  "solution": {
    "file": "ProductSolution.slnx"
  },
  "requiredProjectFiles": [
    "app/ProductApp/ProductApp.csproj",
    "verification/ProductApp.Tests/ProductApp.Tests.csproj"
  ],
  "testProjectFiles": ["verification/ProductApp.Tests/ProductApp.Tests.csproj"],
  "initialization": {
    "solutionName": "ProductSolution",
    "application": {
      "name": "ProductApp",
      "directory": "app/ProductApp",
      "file": "app/ProductApp/ProductApp.csproj",
      "template": "chosen-dotnet-template",
      "templateOptions": [],
      "archetype": "human-readable architecture label"
    },
    "tests": {
      "name": "ProductApp.Tests",
      "directory": "verification/ProductApp.Tests",
      "file": "verification/ProductApp.Tests/ProductApp.Tests.csproj",
      "template": "chosen-test-template",
      "frameworkPreference": "chosen test framework"
    },
    "targetFramework": "explicit target framework"
  }
}
```

## Whole-payload self-check before writing

Before the first write, construct the complete JSON object for the selected provisioning mode and verify it as one payload. Do not write a partial object and wait for a completion gate to identify the next missing field.

- Use exactly one fenced `json` block in the managed artifact.
- Verify that the root and `solution` are objects; `initialization` is also an object when `provisioningMode` is `initialize`.
- Verify that `requiredProjectFiles` is a non-empty array of strings, and that optional `testProjectFiles` and `solution.candidateFiles` are arrays of strings when present.
- The runtime derives the same-name `.sln` and `.slnx` alternatives from `solution.file`. Omit `solution.candidateFiles` unless the accepted architecture genuinely declares additional non-equivalent solution candidates.
- For `initialize`, verify that `initialization.solutionName`, `initialization.application`, `initialization.tests`, and `initialization.targetFramework` are present; `application.templateOptions` must be an array, even when it is empty. Every non-empty `templateOptions` item must be a single `--`-prefixed option flag, not a feature label or an argument value.
- For `verify-existing`, verify that `initialization` is omitted and the declared topology is grounded by current read-only evidence.

For `verify-existing`, use the same top-level `solution.file`, `requiredProjectFiles`, and optional `testProjectFiles`, omit `initialization`, and do not invent names, templates, framework, project-reference relationships, or a conventional layout. The example layout and labels above are placeholders, not defaults. Do not infer a template, framework, test framework, project name, or layout from familiar product words. Confirm the values against the accepted architecture and project structure. If they conflict or are incomplete, record the contradiction and route it for architecture decision rather than emitting a guessed contract.
