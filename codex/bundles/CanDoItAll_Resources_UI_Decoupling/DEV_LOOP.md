# Development-loop and graph proof

The primary benefit is a usable lightweight Resources sandbox. Do not claim full-Web hot
reload is faster merely because the UI moved into another assembly. Production Web still
composes the backend. Report original Web, extracted Web and sandbox separately.

## Baseline and provenance

Take the Resources baseline after closing S0, before extracting Resources. Record the
current branch/commit/worktree, SDK/MSBuild/OS, configuration, component/filetools sibling
revisions, source versus package mode, warm/cold caches, data scenario and browser viewport.
The review SHA is provenance, not a checkout/reset command. Keep baseline artifacts ignored.

Use comparable task-owned Registry/Browse data: one ordinary connector resource, one
historical/missing reference example, a harmless authorized file and a governed saved
storage resource. The sandbox uses deterministic in-memory file providers/content. Do not
compare a blank sandbox to a busy production startup without disclosing that distinction.

## Four boundaries to inventory

1. Evaluated project graph, including imported targets and sibling replacements, rather
   than grepping ProjectReference alone.
2. Restored package/native/runtime assets and published host composition.
3. Actual renderer tree, including configuration editor, promotion dialog, FileBrowser,
   FileInteraction and optional host Agent context.
4. Static/scoped assets and `dotnet watch` inputs, including linked authoritative theme.

The lightweight host must not reach Resources/Workspace/Projects/Security implementation,
Infrastructure, concrete drivers, production FileTools composition, Web or runtime owner
registrations merely to render. Existing genuinely neutral shared FileTools/core/abstraction
edges may remain after explicit evaluation. Do not blanket forbid a namespace or bless an
assembly because its name contains Abstractions. Add negative forbidden/unresolved tests.

## Measurements

Use the actual current CLI/watch tooling or the available dotnetwatch MCP. Typical local
entry points (replace the configuration/host according to current repository rules):

```powershell
dotnet build <host.csproj> --configuration ResourcesUiProof /m:1
dotnet watch --list --project <host.csproj> --configuration ResourcesUiProof
dotnet watch --non-interactive --project <host.csproj> --configuration ResourcesUiProof --no-launch-profile -- --urls <owned-loopback-url>
```

Do not report the template as an executed command. Use owned process handles and bounded
readiness waits, not the ordinary app on port 5032. Record each actual launch command and
sanitized endpoint. Initial restore/build is separate from edit-to-visible time.

Measure startup to interactive and at least three successful samples for each applicable
edit class. A single startup observation is labelled as one, not a reliable speedup ratio.

| Edit | Observable endpoint |
| --- | --- |
| Child Razor | A real Registry/Browse label/control visibly changes in the shipped renderer |
| C# | An executed projection/policy result changes after the appropriate user action |
| Scoped CSS | A computed style/geometry changes on the intended component descendant |
| Feature JS, if present | Executed host callback after required module refresh changes; no timing of a cached no-op |

If Resources has no feature-owned JS after extraction, label that class N/A and still test
shared FileTools browser behavior/assets. Do not create artificial JS just to fill a row.
For any unsupported hot-reload change requiring restart, record restart separately. Keep
failed runs and environmental lock/restore errors visible; do not count them as green samples.
Restore every probe file byte-for-byte and terminate only the owned process tree.

## Budget and performance safeguards

Preserve the existing source512/page50/search/preview16MiB limits stated in the source notes.
Ordinary typing and tab selection must not list storage roots, query the DB, read secret
values or reauthorize files per keystroke. Location preview is cheap and side-effect-free.

Trace owner calls for a normal registry load, field edit, tab transition, browse source
change, saved promotion and failed read-only retry. Avoid reloading the entire registry,
all sources and all secrets for a local presentation change. Preserve explicit authority
rechecks at effects; do not cache past authorization as a performance optimization.

Verify bounded retained draft/receipt/preview data and complete disposal on repeated
selection/reset. The renderer may own DOM references and visual state; no cross-circuit
cache or global mutable workspace is introduced. No infinite recursive scan or whole-file
buffer to simplify fixtures.

## Source and published asset proof

The RCL must own its actual scoped styles. Split or adapt selectors to the DOM boundaries
introduced by child extraction; simply moving a parent .razor.css file can leave descendants
unstyled. Validate supported desktop geometry, font, overflow, dialog stacking and focus.

The sandbox may link generated Web `output.css` as static content with a documented rebuild
precondition. It must not reference the Web project for CSS. Register real BaseLib/FileTools
component services/assets required by the chosen light APIs, not full production modules.

Publish the sandbox to a task-owned output folder and launch the published DLL in Production
without database configuration. Verify required stylesheet/font/script/image/content fixture
responses and real file browser/preview interactions. Source-host proof does not substitute
for published static-web-asset proof. List URL/asset failures explicitly; never include live
secret-bearing or opaque authorization URLs in committed evidence.

Record graph/node/watch counts, samples/medians, scope differences and raw artifact paths in
the maintained Resources boundary receipt. No numeric speed target or graph-node quota is
imposed; explain every remaining dependency and demonstrate the independent sandbox benefit.
