# CanDoItAll.Manager

## Purpose

Local development manager that supervises `dotnet watch`, runtime readiness probes,
the Tailwind workspace's npm watch script, capsule indexing, workspace-process cleanup,
and tuning endpoints.

## Project Type

- SDK: `Microsoft.NET.Sdk.Web`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build tools/App/CanDoItAll.Manager/CanDoItAll.Manager.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Manager.csproj](CanDoItAll.Manager.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Tailwind Watch

The Manager does not watch or rebuild Tailwind sources itself. It runs the standard npm
watch script of the Tailwind workspace (`npm run watch` in `Tailwind/`), and Tailwind's
own watcher rebuilds `output.css` incrementally when a source changes. The Manager only
keeps that process alive and reports whether builds keep completing:

- npm runs through `node` and npm's `npm-cli.js`, so no command shell sits between the
  Manager and npm.
- The watch process gets an open standard input pipe. `tailwindcss --watch` exits when its
  standard input ends, so the watch keeps running while the Manager owns it and stops if
  the Manager disappears.
- Each `Done in …` line marks a finished build. A build that printed an error first is
  reported as failed while the watch keeps running; the next clean build restores `Ready`.
- If the watch process exits, it is restarted after 2 seconds, doubling up to 1 minute
  while it keeps failing; a run that lasted a minute resets the delay.
- Missing dependencies are installed with `npm install` when
  `Manager:TailwindInstallDependenciesIfMissing` is enabled.

| Setting | Default | Meaning |
|---|---|---|
| `Manager:AutoStartTailwindWatch` | `true` | Start the watch with the Manager. It also requires `Manager:AutoStartWatch`. |
| `Manager:TailwindWorkspacePath` | `Tailwind` | npm package that owns the Tailwind scripts. |
| `Manager:TailwindWatchScript` | `watch` | npm script started as `npm run <script>`. |
| `Manager:TailwindInputPath` | `Tailwind/input.css` | Shown on the dashboard. |
| `Manager:TailwindOutputPath` | `src/App/CanDoItAll.Web/wwwroot/css/output.css` | Must match the script's `-o` path; a finished build without this file is reported as a fault. |
| `Manager:TailwindInstallDependenciesIfMissing` | `true` | Run `npm install` when `node_modules/.bin/tailwindcss` is missing. |
| `Manager:TailwindEchoOutputToConsole` | `true` | Echo Tailwind output to the Manager log. |

`GET /api/tailwind/status` and the dashboard report the state, the command, the last build
time, the number of builds since the watch started, and restarts. `GET /api/tailwind/logs`
returns the recent Tailwind output.

## Architecture Notes

This is a local development or operations tool. Keep it explicit about ports, file paths, side effects, and runtime assumptions.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
