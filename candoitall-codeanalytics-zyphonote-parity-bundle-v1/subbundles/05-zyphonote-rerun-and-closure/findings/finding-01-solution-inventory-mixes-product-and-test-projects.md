# Finding 01: Solution Inventory mixes product and non-product projects

## Trigger

- Zyphonote scenario 1 rerun on installed server `snap-20260408215645-36a986a3`

## Observation

`code_analytics_solution_inventory_get` correctly returns direct project references, but the raw answer for `Zyphonote.MusicTheory.Core` also includes `Zyphonote.MusicTheory.Tests` and `Zyphonote.MusicTheory.Benchmarks`.

## Why this matters

For architecture questions about where a core library sits in the product, tests and benchmarks are usually noise. The current inventory is factually correct, but it costs precision and forces the caller or skill to apply a project-name heuristic.

## Evidence

- Raw reference set: `Zyphonote.AI.TranscriptionLab`, `Zyphonote.API`, `Zyphonote.App`, `Zyphonote.App.PdmxTool`, `Zyphonote.Components`, `Zyphonote.MusicNotation.Editor`, `Zyphonote.MusicTheory.Benchmarks`, `Zyphonote.MusicTheory.Tests`
- Product-only answer key omits the benchmark and test projects

## Improvement options

- Add project classification such as `IsTestProject` / `IsBenchmarkProject` to the project inventory surface.
- Or add optional filtering flags on solution/project inventory tools so callers can ask for product projects only.
- Keep the current repo skill filtering names containing `Test` and `Benchmark` until the tool surface grows a first-class classifier.
