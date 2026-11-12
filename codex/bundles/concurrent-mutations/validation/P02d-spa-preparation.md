# P02d SPA generator and browser-store preparation

Committed preparation with Node/build proof, 2026-10-16. Owned SPA worktree:
`C:\repositories\CanDoItAll.SPA-concurrent-mutations`, branch
`codex/concurrent-mutations`, foundation
`5533969fd7ca0807d9438229303a93c738e009a8`. Parent recorded worktree ownership before
creation. The primary SPA and frozen main engine checkout were not edited. This
note is in the managed engine continuation checkout. Initial preparation performed
no dependency install, generation, build, test, server, database operation or commit.
The parent later permitted the isolated Node and frontend build lanes, recorded
below. SPA checkpoint: `2fe9e8ee1b6b7ce9e8777320b7daa19f895cfebb`.

## Prepared source

- Replaced normal automatic 5032 specification fetch with explicit local
  `--input` and reviewed raw-byte `--sha256`. All preflight runs before intermediate
  output or generated cleanup. The input cannot be inside generator output paths.
- Required all six protected Projects definition operations at their exact
  route/method/operation IDs, their declared success/error JSON responses, request
  bodies and original path/lookup parameters. Traversed local component references
  and nested schemas before legacy repair; missing, unresolved/external,
  circular-only and empty protected shapes reject. Reachable named schemas cannot
  enter `fillEmptySchemas`. Explicit nullable unions/type arrays are preserved.
- Kept the existing TypeSpec import/fix/compile/flatten/fix order. Portable cleanup
  and flattening validate only the physical `packages/api-client/src` tree and
  reject links/junctions. No shell recursive-delete command remains in the normal
  generation pipeline. Generated clients were not regenerated or edited.
- Copied the exact committed engine pending-store blob into SPA assets with LF
  attributes and formatter/linter exclusions, plus provenance manifest. The narrow
  Vite hook validates manifest/length/hash/LF, emits unchanged production bytes,
  and exposes `virtual:project-pending-store-url`; development resolves the local
  verified source under its configured base. There is no `/js` proxy assumption,
  CAS fork, IndexedDB initialization or Vue/session change.

Pinned engine source: commit `702d0a93b1081c42d5a06893e7070219aee5d089`, path
`src/App/CanDoItAll.Web/wwwroot/js/projectPendingSubmissions.js`, **20,884 LF bytes**,
SHA-256 `caaec982e24f9008683fe5a2be6ad81da1786a268573bc9d95fb6f5b38fddc96`.
Bytes came from `git show <full-commit>:<path>` through a binary pipe; no working-tree
CRLF normalization or generator execution was used. The asset manifest does not
claim HTTP/native frozen-body interoperability.

## Exact source-derived expectations

| Owning SPA file | Expected Node cases |
| --- | ---: |
| `packages/api-client-generator/test/protected-projects-spec.test.mjs` | 14: eight direct cases plus six operation-presence rows |
| `packages/app/test/project-pending-store.node.mjs` | 6 direct cases |
| Total | **20**, matched by execution below |

The first file covers preserved nullable input, no input mutation, each missing
operation, missing/empty/unresolved reference graphs including `$ref` siblings,
missing request/response/lookup requirements, duplicate IDs, explicit reviewed-file
arguments and byte mismatch, and bounded cleanup path rejection. The asset file
covers exact provenance, missing bytes, mismatch/CRLF, invalid manifest/path,
actual Vite hooks with a fake emission context, and a non-root development base.
These are pure Node tests; the plugin cases do not invoke Vite or a browser.
Fixture directories are created by `mkdtemp` and removed in finally. Executed
tests left no owned temporary fixture root; build/dependency outputs are retained
for the next SPA slice as recorded below.

Source-counted commands for the parent's isolated validation window:

```text
node --test packages/api-client-generator/test/protected-projects-spec.test.mjs
node --test packages/app/test/project-pending-store.node.mjs
```

Both test commands require no new package. The app normal test script includes its
six Node cases; `.node.mjs` avoids duplicate default Vitest discovery. Detailed
usage and ownership are in SPA `docs/concurrent-mutations-preparation.md`.

## Pure preparation proof

Parent source review found no blocking schema-guard, bounded-cleanup or asset
packaging defect. Before execution, fixture setup was moved inside its existing
try/finally cleanup boundary; a setup write failure now also removes its owned
temporary directory. This did not add a test row or change production behavior.

Node **v24.21.0** executed both named files together: **20 passed, 0 failed,
0 skipped**, TAP duration **131.5672ms**, measured command wall time **0.2001274s**.
The **21** candidate source inputs were hashed before execution and all remained
unchanged afterward. No newly owned preflight/asset temporary root remained.
Evidence is in the main checkout's ignored
`.artifacts/concurrent-mutations/validation/p02d-spa-preparation/run.json` and
`tests.tap.log`. The passing fake-hook tests do not certify a real Vite build.

The .NET Stable gate stayed frozen and active. This separate pure lane touched no
.NET binary, PostgreSQL fixture, application process or generated API client.
Backend generation still waits for validated emitted OpenAPI.

## Isolated app build and reviewed commit

The frozen-lockfile install with scripts disabled passed. The app's actual
`vue-tsc --build && vp build` passed before and after owned formatting; final
bundling took **1.80s** (not the full command wall time). Direct Oxfmt 0.61.0
checked 19 owned inputs and Oxlint 1.76.0 checked 13 with warnings denied. The
final Node rerun after the generator guard repair passed **20/20, zero skipped**,
TAP **95.0649ms**. No backend, database or browser server was started.

The actual dist recovery asset matches the pinned **20,884 bytes** and hash
above. A separate real Vite API build with `write: false` proved the virtual URL
under `/concurrency-proof/` and the identical emitted asset. This proves packaging
and URL resolution; it does not prove IndexedDB or protected editor behavior.

An observed setup side effect is retained in the evidence: pnpm 11.25.0 defaults
`verify-deps-before-run` to `install`. Its exec dependency check initiated a second
install without carrying the earlier `--ignore-scripts`, which ran root
`prepare: vp config`. The single introduced `core.hooksPath` setting was removed;
the entire shared Git config hash matches its recorded original
`b8fea8051927a88fcf6c744be2226e55f6578242d4897001ed0f652759712456`.
The lockfile hash remains
`99d740c60d5d1dd7be044b2aee84fa2fd40ae9ee5e860ec3d9ee71f5c092379b`.
Generator child calls and documented top-level invocation now explicitly use
process-scoped `pnpm_config_verify_deps_before_run=error`. Final source checks
used direct local executables. No global package configuration was changed.

Automatic approval review rejected removal of the generated inactive
`C:/repositories/CanDoItAll.SPA-concurrent-mutations/.vite-hooks/_` directory
and its 16 files with the reason `blocked by policy`. It remains an explicit
cleanup blocker; no alternate removal or whole-worktree deletion was attempted.
Dependency/build outputs remain owned and useful for continuing this task.

SPA `docs/concurrent-mutations-validation.md` records exact commands, versions
and proof paths under ignored `logs/concurrent-mutations/`. The 22-input final
manifest SHA-256 is
`2042d71327c83818fd13baa823a73d34e2f691d63284a7737510161c95cc3bb6`.
Parent independently verified every file hash, reviewed the source/asset boundary,
staged exactly those 22 paths, passed staged whitespace enforcement and committed
them. The SPA worktree had no remaining tracked/untracked source changes after
commit; ignored retained outputs and the cleanup blocker are not implied absent.

## Remaining gates

The follow-on dormant pending-store adapter is committed separately at
`0411cd3e65d50d43961a97480ac120d7d1552d30`. Its exact four reviewed files passed
18 Node cases (zero skips), owned format/lint, app typecheck and build. The final
four-input manifest is `fa6540b66130f82df17862f62ec0cbb258f0f06d468bff45fe46d03349fb074f`.
Parent verified each hash and staged whitespace before committing. See the SPA's
`docs/concurrent-mutations-pending-adapter.md` for its retained initial fixture
failure, exact commands and limits. It is not imported by either editor, and its
controlled-module tests do not establish actual IndexedDB/browser integration.
The pinned asset, prior preparation inputs, lock and shared Git config remained
unchanged; no server, database or additional dependency setup was used.

Parent-owned actual backend OpenAPI tests must pass before selecting a reviewed
input and generating the client. The structural guard is not full JSON Schema or
owner-wire semantic verification. Verify real generated
request/result/status models and serializers, decimal-string epoch/timestamp/null
round trips. Generation has not been attempted; the existing generated fix
pipeline's compatibility with the new models still needs proof. The real app
build, emitted bytes and non-root base URL have passed the narrower checks above.

Vue create/overview callers, retry configuration, frozen HTTP submissions,
authorization/dataset binding, draft lifetime, exact receipt reconciliation,
sticky uncertainty and restart/two-tab browser recovery are still unimplemented.
Legacy SPA writes remain active; this preparation does not close the client
concurrency guarantee or the end-to-end bundle.
