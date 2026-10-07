# 3 · Sandboxes, assets and the real development loop

## A scenario host, not a second product

A module sandbox renders the **same production components**, including meaningful children, charts, pickers and deferred overlays. It supplies deterministic presentation/view implementations and narrowly scoped fake read ports. It needs no production module implementation, EF provider, database, LLM credential, Memory provider, scheduler or runtime worker just to show and edit a scenario. Starting production DI and replacing one root service afterwards is not isolation.

Use a feature sandbox under the established `src/Sandboxes` convention where a lightweight host is needed. Reuse an existing suitable sandbox rather than requiring one per assembly. A plain backend project has no automatic sandbox obligation. The current Agents, Prompts and CRM/HR hosts are precedents, not a mandatory project-count target [S06, S08, S15, S16].

Separate scenario-only state from production state. Keep fake registrations inside the sandbox/test composition; production must not accidentally resolve them. Scenario routes need not make all production dialogs bookmarkable. Avoid copying the complete production startup merely to obtain a theme or dialog service.

For reusable capabilities, preserve real BaseLib/Charts registrations and JS assets. A chart placeholder, arbitrary SVG, root-only injection check or mocked child is not proof that the intended rendered closure works. Native/desktop capabilities need an explicit host seam and disabled/unsupported state in the browser sandbox; do not require desktop execution to preview their presentation.

## Scenario coverage by responsibility

Cover loading, empty, representative and large data; selected and missing saved references; non-default sections; restricted controls; core and partial failures; retry and stale same-scope data; important open/nested overlays. For editable surfaces also cover dirty/invalid/busy/conflict states and the chosen save policy. Not every surface has every state: mark an irrelevant case with its reason, not a synthetic implementation.

Browser observations should include the supported large-desktop viewport and a constrained desktop width where layout is at risk, long content, actual plot/table content, intended scroll owner, keyboard focus and dialog stacking. Scope remains desktop-oriented unless the assignment adds a mobile redesign. A screenshot proves appearance at one state, not commit semantics or the absence of console errors.

## Source references, package mode and CI

At the reviewed revision, `Directory.Build.targets` defaults `UseLocalCanDoItAllLibraries=true`, converts Components/FileTools package references to sibling source project references, and validates both sibling roots [S07]. Both repositories being required at evaluation/build preflight does **not** mean every sandbox compiles FileTools. Report the actual evaluated closure.

Keep source mode as the ordinary UI development mode. Do not silently replace sibling source with a stale local NuGet snapshot or mix source and package identities. Reuse existing root override properties for non-sibling layouts. UI-only projects and sandboxes currently disable repository template copying; verify the necessity of generated assets/content instead of inheriting unrelated work [S07, S09, S10, S15].

CI currently resolves the Components branch matching the application branch (a PR uses its target), pins that resolved SHA for all jobs in the run, and separately pins FileTools in workflow configuration [S03, S19]. Therefore a development→main merge can consume Components `main`, not Components `development`. Coordinate real sibling changes at the release boundary, but do not import the old three-repository integration checklist into every UI slice. Record actual sibling SHAs; “matching branch” alone is insufficient evidence.

Source/package dependency mode and Parity/Fast asset mode are **independent axes**. Only expand validation into package publication, independent FileTools builds, container contexts or packaging when their contracts or configuration are affected. Never publish packages merely to make an unrelated sandbox build.

## Assets are part of the move

Move/adjust scoped CSS, JavaScript modules, imports, static-web-asset URLs, generated styles, route assembly discovery and host registrations together with the components that need them. Check production build and publish asset behavior after relevant moves. Avoid accidental URL/assembly-name changes that leave old `_content/...` imports behind. A style attached to a component invocation does not necessarily reach the actual DOM element; inspect computed styles after extraction.

Current sandbox modes are explicit **Parity** and **Fast** [S08, S15]:

| Mode | Purpose | Evidence obligation |
|---|---|---|
| Parity | Production-equivalent theme/assets for representative development | Correct source/generated styles and production comparison; no silent fallback |
| Fast | A narrower documented asset scan for the same meaningful surface | Declare differences; verify required styles/children still work; do not use it as the sole production visual proof |

Parity currently links the production CSS file as content; that is an asset dependency, **not a `ProjectReference` to Web**. Do not reject it with an indiscriminate “no path under Web” guard. Do reject adding Web to the sandbox's compile graph merely to get this CSS. Conversely, a shared CSS file still has generation/watch cost that must be measured.

Keep build-time and runtime asset selection consistent, isolate outputs where modes differ, and fail clearly on missing or mismatched mode assets. Verify Tailwind inputs include moved sources and actual child libraries, without scanning unrelated modules in a claimed fast path. Static generation must remain reproducible on a clean checkout; a developer's pre-existing CSS output is not sufficient provenance.

Use the current `package.json` scripts and the sandbox README, not obsolete commands copied from the input ZIP. Current script families are `catalog:*`, `prompts:*` and `crmhr:*`; future slices may add a justified analogous family. Asset generation and .NET watching may require separate processes, each owned and cleaned up by the test/development session [S08].

## Evaluate the graph before claiming improvement

Microsoft documents that watch follows the project-reference graph, with `--list` enumerating watched files [F01]. Capture evaluated items rather than only grepping project files. MSBuild's item/property query can reveal imported transformations [F02]. For example, at the reviewed revision:

```powershell
$project = './src/Sandboxes/CanDoItAll.CrmHr.UiSandbox/CanDoItAll.CrmHr.UiSandbox.csproj'
dotnet msbuild $project -p:Configuration=Debug -p:CrmHrAssetMode=Parity `
  '-getProperty:TargetFramework,UseLocalCanDoItAllLibraries' `
  '-getItem:ProjectReference,PackageReference'
dotnet watch --project $project --list --property:CrmHrAssetMode=Parity
```

Revalidate command support against the checkout SDK. The first command shows evaluated **direct** items, not the whole graph. Traverse returned project references with the relevant properties, inspect restored package/native assets and capture a build binlog when needed. Unexpected unresolved edges make the analysis incomplete. Keep raw output/configuration in the child's evidence, with secrets removed.

`Watch="false"` is not a dependency cut. Hiding a required source edit or excluding it from compilation is not performance work. MSBuild evaluation, actual rebuilt projects and edit visibility are distinct measurements [F01, F02].

## Measurement contract

Extracting a referenced library does not, by itself, remove heavy dependencies from `dotnet watch` on the production Web host. The sandbox is the principal fast-development host; production remains the integration path. Any improvement to the full Web loop needs its own measured cause, not an assumption that every project reference disappeared.

Use one comparable scenario and record: machine/OS, SDK, application+sibling revisions, branch/local delta, source/package mode, asset mode, launch command, project graph, watch list, process reuse and cache conditions. Separate these comparisons:

- Original production host before the extraction versus the changed production host: detects production regressions.
- Changed production host versus the module sandbox with the same relevant state: measures the practical benefit of the isolated loop.
- Parity versus Fast: isolates asset-scan tradeoffs, not an architectural claim about backend removal.

Measure startup and save-to-visible change for a representative Razor edit, C# presentation edit, scoped CSS and JS where present. Classify hot reload, browser refresh and application restart separately. Record first/warm edits, sample count and range; prefer several repeated warm samples rather than a percentage from one observation. Any sample count is reported, not a hard-coded speedup gate. Keep cold/warm cache conditions and build servers comparable.

The observer ends timing when the changed UI/behavior is visible, not when the CLI prints success. Record a missing refresh or unsupported edit as such. Restore measurement edits safely; never use destructive reset on the user's branch. Use an isolated worktree only with appropriate scope/ownership, and do not run parallel builds into the same outputs. If later visual/code fixes change the scenario, repeat the affected comparison or mark earlier measurements stale.

No machine-independent millisecond promise belongs in this shared bundle. A child reports measured improvement, no improvement, regression, or **not measured**. Structural completion does not authorize an invented performance claim.
