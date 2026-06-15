# Project Structure E2E Source Request

## User Instruction Preserved

The running instance on `http://localhost:5032/` contains a `TetrisGame` project structure. This project structure was source information for previous E2E validation and must become part of the v3 bundle as final E2E testing source information.

Future Codex implementation agents must be able to load this kind of source data into the application through APIs of the new Process module and related project-structure APIs. The Process module must therefore expose typed APIs and a complementary Codex skill so agents can create/load definitions, templates, launch plans, runs, assignments, artifacts, project-scoped process links, and E2E scenario inputs without direct database edits.

The architecture must remain generic:

- No `TetrisGame`, game-specific, or app-specific logic may appear in generic Process Core, Runtime, Dispatcher, Builder, Manager, Artifact, Monitoring, Template, or Projection contracts.
- Software-development and .NET domain drivers may model software delivery, app types, validation, browser proof, build/test proof, and static hosting generically.
- Tetris-specific concepts may appear only in scenario data, evidence, tests, and final E2E source packs.
- Add at least three additional app scenarios so final E2E validation proves the implementation is generic and not shaped around one source project.

