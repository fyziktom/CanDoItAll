# Reproduce the direct development loop

Run from the repository root. Use the entry sibling revisions in live source mode, the SDK selected by global.json, the locked Tailwind install, and the frozen local browser versions in ../plan/frozen-direct-edits.json. Do not run concurrent builds, tests or another watch host while collecting a lane. Tailwind is the intentional companion process in every lane.

Install Tailwind with `npm ci --prefix Tailwind`. Use a local Playwright 1.61.1 installation with its Chromium revision 1228 browser (149.0.7827.55). Pass its absolute `node_modules/playwright` directory through `--playwright-module` or CATALOG_PLAYWRIGHT_MODULE. The harness checks the actual SDK, Node, browser and Playwright versions and its own frozen SHA before measuring; a different installation requires a separately reviewed and frozen protocol, not silent substitution.

The full-app lane reuses the isolated fixture described in ../../UI_AgentCatalog_01_Extraction_Sandbox_Bundle/tools/Fixture/README.md. The exact retained environment/snapshot and embedded rendering fixture hashes are in the inventory. Credentials remain in ignored task files and are passed only to the owned child environment. Never copy them into proof. No launchSettings change is needed: the direct full-app process uses --no-launch-profile and the isolated environment. If rebuilding the fixture on another machine, create a new isolated test database once, verify the sanitized rendering projection, and freeze its identity before running all lanes. Do not regenerate between samples.

Run these sequentially, substituting the absolute Playwright module directory:

```sh
node codex/bundles/UI_AgentCatalog_Harden_01_Development_Loop_Bundle/tools/direct-watch.cjs --host fullapp --phase warm --playwright-module <absolute-module-directory>
node codex/bundles/UI_AgentCatalog_Harden_01_Development_Loop_Bundle/tools/direct-watch.cjs --host parity --phase warm --playwright-module <absolute-module-directory>
node codex/bundles/UI_AgentCatalog_Harden_01_Development_Loop_Bundle/tools/direct-watch.cjs --host fast --phase warm --playwright-module <absolute-module-directory>
```

Use `--phase acceptance` for real state/intent/style/asset checks and screenshots. The harness starts the matching direct Tailwind and dotnet-watch children, owns their exact process trees, launches a local browser at 1600x1000, checks the runtime owner/watch generation and actual compiled asset mode, and stops only those children. It uses no Manager, SourceWatch, MCP browser or screenshot timing endpoint. The existing normal manual two-terminal commands remain in the sandbox README.

The frozen readiness loop proves interactive search 40 -> 12 -> 40 and a stable catalog. It handles the isolated database's public Continue action through hydration. Any such confirmation during a measured reload is included in latency and counted. The primary endpoint is the final expected DOM/computed-style predicate plus two animation frames after the SDK has finished its update. The earlier first-visible timestamp is also retained: an intermediate render can precede a subsequent SDK browser reload. SDK-reported apply time is separate from both browser metrics. This conservative confirmed-visible endpoint must not be confused with the first changed pixel or historical managed-loop timing.

The edits are one real Razor heading, one real C# title expression and one real isolated CSS gap; each has three repetitions and an exact byte undo. Appropriate Tailwind companions run even for CSS isolation. This measures that representative CSS path, not Tailwind-only compilation speed. Failure stops a lane for review and retains source restoration, SDK events, DOM and the run ledger. A runtime crash or incomplete undo is a failed cycle even when its primary visible predicate had passed. Never drop an outlier or silently retry inside a timing record.

Summarize completed lanes with:

```sh
python codex/bundles/UI_AgentCatalog_Harden_01_Development_Loop_Bundle/tools/summarize.py <fullapp-run-directory> <parity-run-directory> <fast-run-directory> --output <report-directory>
```

The summary validates frozen identities, successful repetition numbers, SDK completion, process ownership, byte restoration and production CSS hashes. Keep failed/incomparable runs in a separate calibration ledger alongside the comparable primary cohort. Startup is a prerequisite, not a cold-start benchmark. N=3 per edit/mode is a small same-machine sample; mixed or negative results must be reported.
