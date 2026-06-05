# Driver Readiness Position

This bundle prepares future driver work indirectly by naming candidate/evidence intent categories, but it must not introduce a production driver API.

Why before Process Core:

- It is useful to document what candidate facts future drivers will need: project id, route kind, step kind, expected artifacts, required evidence, technical-agent capabilities, project-structure context.
- It is too early to define `IProcessDriverPack` because the runtime/private candidate model is still changing.

Recommended stance:

- Add documentation-only driver-readiness map under `architecture/` or `inventories/`.
- Do not add interfaces, DI registrations, packages, tools, or public contracts.
- Make sure future driver terms are evidence-intent vocabulary only, not executable behavior.

Future driver categories to map later:

- software build/test/run helper drivers,
- browser proof helper drivers,
- document/spreadsheet/business-analysis helper drivers,
- manager verification helper drivers,
- project-structure inspection helpers.
