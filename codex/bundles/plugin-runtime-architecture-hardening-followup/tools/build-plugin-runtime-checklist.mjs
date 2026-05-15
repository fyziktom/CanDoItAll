import fs from "node:fs/promises";
import path from "node:path";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const bundleRoot = "C:/repositories/CanDoItAll/codex/bundles/plugin-runtime-architecture-hardening-followup";
const outputPath = path.join(bundleRoot, "inventories", "plugin-runtime-architecture-hardening-checklist.xlsx");
const previewDir = path.join(bundleRoot, "inventories", "rendered-checklist-preview");

const workbook = Workbook.create();

const sheets = [
  {
    name: "Overview",
    widths: [170, 740],
    rows: [
      ["Field", "Value"],
      ["Bundle", "plugin-runtime-architecture-hardening-followup"],
      ["Purpose", "Architecture hardening follow-up for runtime plugins, logging, workflow canvas plugin executors, icons, performance/EF, and Docker ZIP handoff."],
      ["Prior baseline", "C:/repositories/CanDoItAll/codex/bundles/plugin-runtime-package-install"],
      ["Preparation status", "Prepared for implementation; no product code implemented by this preparation pass."],
      ["Critical foundation", "SB01 must prove installed package assemblies can activate executable code without contributing bundled plugin descriptors."],
      ["Closure condition", "SB06 must leave the app running without Docker registered by default and record the tested Docker ZIP path/checksum."],
      ["Primary docs", "README.md; architecture/01-target-solution.md; analysis/01-current-state.md; analysis/03-performance-and-ef-scan.md; reviews/01-execution-report.md"],
    ],
  },
  {
    name: "Findings",
    widths: [90, 90, 360, 520, 360, 140],
    rows: [
      ["Id", "Severity", "Finding", "Evidence", "Required action", "Subbundle"],
      ["FIND-001", "Critical", "Runtime package assembly auto-registration can import bundled plugin descriptors and conflict with installed manifest identity.", "PluginPackageServices.cs:514, :800, :801; DockerBundledPlugin.cs:20; GmailBundledPlugin.cs:24; Office365BundledPlugin.cs:28", "Make installed package manifest the source of truth. Do not auto-register bundled ICanDoItAllPlugin descriptors from package assemblies.", "SB01"],
      ["FIND-002", "High", "Installed package discovery recursively scans nested manifests.", "PluginPackageServices.cs:299 and :787 use SearchOption.AllDirectories.", "Replace with direct installed package root enumeration and nested-manifest test.", "SB01/SB05"],
      ["FIND-003", "High", "Plugin installation/runtime logs are not durable or user-visible.", "WorkflowExecutorObservability.cs:34 exists, but AgentFrameworkModuleServiceCollectionExtensions.cs:88 and Hosting extension :61 use null observer.", "Add durable plugin log store, write install/runtime events, and expose logs in plugins page.", "SB02"],
      ["FIND-004", "Medium", "Plugins page/catalog still uses bundled-only wording and fallback identity.", "PluginsPage.razor:24, :131, :137; PluginCatalogServices.cs:186, :270, :276.", "Make wording and unavailable source/trust fallback generic and snapshot-based.", "SB01/SB02"],
      ["FIND-005", "Medium", "Concrete plugin namespaces/project references still look module-owned.", "src/plugins/... files use namespace CanDoItAll.Modules.Plugins; plugin csprojs reference CanDoItAll.Modules.Plugins.", "Clean only what is needed for a safe generic boundary; avoid broad churn.", "SB01"],
      ["FIND-006", "High", "Workflow canvas right-click menu lists plugin executors directly under Executors.", "WorkflowExecutorCanvasCatalog.cs:14 and :39.", "Build Executors -> Plugins -> plugin -> executor menu hierarchy.", "SB03"],
      ["FIND-007", "Medium", "Icon contract is string/path based and not shared cleanly across target surfaces.", "WorkflowExecutorModels.cs:358 IconName; PluginPackageModels.cs:101 IconPath.", "Add typed icon descriptor and safe asset resolver.", "SB04"],
      ["FIND-008", "Medium", "Latest connection lookup materializes before ordering.", "PluginPermissionServices.cs:146, :155, :157.", "Push ordering and first-row selection into EF.", "SB05"],
      ["FIND-009", "Medium", "OAuth workflow connection resolution materializes joined candidates before latest selection.", "PluginOAuthService.cs:329, :364, :365.", "Reduce/order in EF before bounded in-memory scope filtering.", "SB05"],
      ["FIND-010", "Medium", "Executor descriptor availability can repeatedly perform sync DB reads.", "PluginPermissionServices.cs:32, :278; DockerWorkflowExecutors.cs:34/:148; GmailWorkflowExecutor.cs:22/:163; Office365WorkflowExecutor.cs:21/:162.", "Use batch/cached availability snapshot for catalog construction.", "SB05"],
      ["FIND-011", "Critical", "Docker cannot be a valid manual package handoff while default-registered.", "RuntimeHostServiceCollectionExtensions.cs:54 registers AddCanDoItAllDockerPlugin.", "Remove Docker default registration only in final handoff subbundle.", "SB06"],
      ["FIND-012", "High", "Existing package tests are manifest-only and do not prove assembly/executor activation.", "PluginCatalogIntegrationTests.cs:121, :703; PluginsPageTests.cs:121.", "Add real package assembly fixture tests.", "SB01/SB06"],
    ],
  },
  {
    name: "Requirements",
    widths: [90, 420, 470, 130],
    rows: [
      ["Requirement", "Statement", "Observable success criteria", "Subbundle"],
      ["PRH-001", "Runtime package manifests are source of truth for installed package identity; assemblies may register operational services/executors but not bundled descriptors.", "Package assembly fixture loads executor; duplicate identity validation still works; no bundled descriptor from installed package.", "SB01"],
      ["PRH-002", "Installed manifest discovery is direct package root only.", "Nested manifest ignored/rejected predictably; package listing and assembly activation share same enumerator.", "SB01"],
      ["PRH-003", "Generic runtime cleanup removes stale bundled-only assumptions.", "UI/catalog wording generic; unavailable state derives source/trust from snapshot; Docker can be removed from default composition.", "SB01"],
      ["PRH-004", "Durable installation logs exist.", "Upload/validation/install/enable/disable/restart-required/activation events persist with redaction.", "SB02"],
      ["PRH-005", "Durable runtime logs exist.", "Plugin executor start/success/failure records persist through observer/event bridge.", "SB02"],
      ["PRH-006", "Plugins page has logs subtab separating installation and runtime logs.", "Subtab filters/sorts logs and browser proof shows both streams.", "SB02"],
      ["PRH-007", "Workflow canvas groups plugin executors behind generic plugin entry and plugin-specific submenu.", "Plugin executors absent from direct Executors children; Office365-style executor list grouped under plugin.", "SB03"],
      ["PRH-008", "Docker/Gmail/Office365 icons are available for plugin page, menu, and executor node.", "Typed icon descriptor; safe package icon resolver; browser proof for all surfaces.", "SB04"],
      ["PRH-009", "Performance and EF findings are hardened.", "Latest-row selection in EF; bounded OAuth filtering; no repeated sync per-descriptor DB reads; direct manifest scan.", "SB05"],
      ["PRH-010", "Docker default disabled and packaged as runtime ZIP.", "App runs without default Docker; tested ZIP path/checksum recorded; Docker appears only after package install.", "SB06"],
      ["PRH-011", "Every subbundle captures proof.", "Execution report has commands, screenshots/artifacts, residual risks, and gate result.", "All"],
    ],
  },
  {
    name: "Subbundle Checklist",
    widths: [90, 110, 150, 410, 330, 510, 410, 210, 120],
    rows: [
      ["Subbundle", "Task ID", "Phase", "Task", "Reasoning", "Source references", "Acceptance/proof", "Dependencies", "Status"],
      ["SB01", "SB01-01", "Architecture", "Inspect package activation flow from manifest install through runtime assembly registration.", "Implementation must understand current identity ownership before editing.", "PluginPackageServices.cs:299, :514, :787, :800, :801", "Execution report notes current flow and chosen contract.", "None", "Ready"],
      ["SB01", "SB01-02", "Architecture", "Replace recursive installed manifest scans with one direct package-root enumerator.", "Recursive scans can discover nested package manifests and create false packages.", "PluginPackageServices.cs:299, :787", "Nested manifest test passes; no SearchOption.AllDirectories for installed manifest discovery.", "SB01-01", "Ready"],
      ["SB01", "SB01-03", "Architecture", "Prevent installed package assemblies from auto-registering bundled ICanDoItAllPlugin descriptors.", "Runtime package manifest must own source/trust/package identity.", "PluginPackageServices.cs:801; DockerBundledPlugin.cs:20; GmailBundledPlugin.cs:24; Office365BundledPlugin.cs:28", "Package assembly fixture cannot emit bundled descriptor; catalog uses manifest identity.", "SB01-01", "Ready"],
      ["SB01", "SB01-04", "Testing", "Add real runtime package assembly fixture with registrar and workflow executor.", "Manifest-only tests cannot prove package code works.", "PluginCatalogIntegrationTests.cs:121, :703", "Integration test loads executor after activation/startup.", "SB01-03", "Ready"],
      ["SB01", "SB01-05", "Cleanup", "Correct bundled-only messages and unavailable fallback identity.", "User-facing page and catalog should describe generic package/runtime state.", "PluginsPage.razor:24, :131, :137; PluginCatalogServices.cs:186, :270, :276", "Tests/browser proof show generic wording and snapshot-derived source/trust.", "SB01-03", "Ready"],
      ["SB01", "SB01-06", "Gate", "Run targeted package tests and build.", "Later subbundles depend on activation contract correctness.", "CanDoItAll.sln; PluginCatalogIntegrationTests.cs", "Build/test summaries in execution report; progression gate marked passed.", "SB01-02..05", "Ready"],
      ["SB02", "SB02-01", "Design", "Add typed plugin log stream/operation/severity model.", "Logs need structured sort/filter and no magic strings.", "PluginRuntimeRecords.cs; PluginInstallationRecord.cs", "Models use enums/typed ids for plugin/package/executor/log operation.", "SB01", "Ready"],
      ["SB02", "SB02-02", "Persistence", "Add durable plugin log storage and query service.", "ILogger output is not enough for plugins page diagnostics.", "PluginSchemaInitializer.cs; Persistence folder", "Persistence tests cover write/query/sort/filter.", "SB02-01", "Ready"],
      ["SB02", "SB02-03", "Install logs", "Write install/package lifecycle logs.", "Users need to diagnose package upload and install failures.", "PluginPackageServices.cs; PluginCatalogServices.cs", "Success/failure/restart-required logs persisted with package/plugin ids.", "SB02-02", "Ready"],
      ["SB02", "SB02-04", "Runtime logs", "Bridge workflow executor audit and plugin execution events into durable runtime logs.", "Runtime plugin failures must be visible per plugin.", "WorkflowExecutorObservability.cs:34; WorkflowExecutorContracts.cs:254; PluginExecutionContracts.cs:275", "Plugin executor start/completed/failed records persist; built-ins filtered out.", "SB02-02", "Ready"],
      ["SB02", "SB02-05", "Redaction", "Centralize secret redaction for settings, OAuth values, command args, and package details.", "Logs must be actionable without leaking secrets.", "WorkflowExecutorContracts.cs; PluginExecutionContracts.cs", "Tests prove redaction on sensitive fields.", "SB02-03/04", "Ready"],
      ["SB02", "SB02-06", "UI", "Add plugins page logs subtab with installation/runtime separation.", "Requested user-facing log access belongs in plugins page.", "PluginsPage.razor; PluginsPageTests.cs", "Component/browser proof shows separate sorted streams.", "SB02-02..05", "Ready"],
      ["SB03", "SB03-01", "Menu model", "Classify descriptors into built-in and plugin executors by source metadata.", "Grouping must stay generic and work for future plugins.", "WorkflowExecutorModels.cs:63, :100, :367", "Unit test covers built-in, one plugin, multiple plugin descriptors.", "SB01", "Ready"],
      ["SB03", "SB03-02", "Menu tree", "Build Executors -> Plugins -> plugin -> executor action hierarchy.", "Plugin executors currently crowd second layer.", "WorkflowExecutorCanvasCatalog.cs:14, :39, :52", "Generated action tree test proves plugin executors are not direct Executors children.", "SB03-01", "Ready"],
      ["SB03", "SB03-03", "Compatibility", "Preserve create action id parse/resolve path.", "Menu grouping must not break node creation.", "WorkflowCanvasEditor.razor.cs:901; WorkflowExecutorCanvasCatalog.cs:70", "Selecting nested executor creates correct node.", "SB03-02", "Ready"],
      ["SB03", "SB03-04", "Browser proof", "Open real workflow canvas context menu and create plugin executor node from nested menu.", "Canvas submenu behavior is JS-driven and needs browser proof.", "04-context-menu-and-composer.js:617, :644, :711", "Screenshots for layered menu and created node.", "SB03-02/03", "Ready"],
      ["SB04", "SB04-01", "Contract", "Add typed plugin icon descriptor or equivalent strongly typed contract.", "Avoid raw plugin id -> icon string branching in UI.", "WorkflowExecutorModels.cs:358; PluginPackageModels.cs:101", "Tests cover descriptor/fallback serialization or mapping.", "SB01", "Ready"],
      ["SB04", "SB04-02", "Asset safety", "Implement safe package icon asset resolution.", "Installed package paths must not expose arbitrary files.", "PluginPackageServices.cs:467, :485, :520", "Traversal test fails safely; valid package icon resolves.", "SB04-01", "Ready"],
      ["SB04", "SB04-03", "Assets", "Assign Docker/Gmail/Office365 icons using reviewed local assets or documented fallbacks.", "Requested plugins need icons in page/menu/node.", "src/plugins/CanDoItAll.Plugin.Docker; Gmail; Office365", "Chosen source/fallback recorded in execution report.", "SB04-01", "Ready"],
      ["SB04", "SB04-04", "Rendering", "Render icons in plugins page, workflow menu, and small executor node mark.", "The same plugin identity should be recognizable across surfaces.", "PluginsPage.razor; WorkflowExecutorCanvasCatalog.cs:56; WorkflowCanvasModels.cs:1067", "Browser screenshots for all three surfaces.", "SB03, SB04-03", "Ready"],
      ["SB05", "SB05-01", "EF query", "Move FindFirstByKeyAsync latest selection into EF.", "Avoid materializing all matching connections.", "PluginPermissionServices.cs:146, :155, :157", "Test preserves latest UpdatedAtUtc behavior.", "SB01", "Ready"],
      ["SB05", "SB05-02", "EF query", "Reduce/order OAuth workflow connection candidates before materialization.", "Avoid unbounded joined candidate materialization.", "PluginOAuthService.cs:329, :364, :365", "Test covers latest connected/unconnected and scope behavior.", "SB05-01", "Ready"],
      ["SB05", "SB05-03", "Availability", "Replace repeated sync per-descriptor grant reads with batch/cached availability snapshot.", "Catalog/UI should not create N repeated sync DB reads.", "PluginPermissionServices.cs:32, :278; Docker/Gmail/Office365 executor Descriptor properties", "Tests or instrumentation prove catalog construction does not query per descriptor.", "SB01/SB02", "Ready"],
      ["SB05", "SB05-04", "Scan", "Rerun targeted anti-pattern searches and update PERF finding status.", "Closure requires evidence, not assumptions.", "analysis/03-performance-and-ef-scan.md", "Execution report maps every PERF finding to resolved/deferred.", "SB05-01..03", "Ready"],
      ["SB06", "SB06-01", "Default disable", "Remove Docker from default app registration.", "User must install Docker manually as package.", "RuntimeHostServiceCollectionExtensions.cs:54", "Build/startup proof shows Docker absent before install.", "SB01", "Ready"],
      ["SB06", "SB06-02", "Package manifest", "Prepare Docker runtime package manifest with correct source/trust/icon/assembly data.", "ZIP must conform to runtime package contract.", "DockerBundledPlugin.cs; DockerPluginConstants.cs; PluginPackageServices.cs", "Manifest validation passes; source/trust are runtime package safe.", "SB01/SB04", "Ready"],
      ["SB06", "SB06-03", "ZIP build", "Build Docker plugin output and assemble tested ZIP.", "User needs a real installable artifact, not source files.", "CanDoItAll.Plugin.Docker.csproj", "ZIP path and checksum recorded.", "SB06-02", "Ready"],
      ["SB06", "SB06-04", "Install proof", "Install/activate Docker ZIP and prove executors appear.", "Handoff is invalid without real package activation proof.", "PluginCatalogIntegrationTests.cs; PluginsPage.razor; Workflow canvas", "Automated test and browser/log proof show Docker after install.", "SB06-03", "Ready"],
      ["SB06", "SB06-05", "Final state", "Leave app running without Docker default module and record manual install instructions/artifact.", "User explicitly wants to load Docker manually.", "RuntimeHostServiceCollectionExtensions.cs; reviews/01-execution-report.md", "Execution report states final app state, ZIP path, checksum.", "SB06-04", "Ready"],
    ],
  },
  {
    name: "Source References",
    widths: [180, 530, 110, 440],
    rows: [
      ["Area", "File", "Lines", "Notes"],
      ["Composition", "C:/repositories/CanDoItAll/src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs", "53-56", "Current default plugin registrations, including Docker to remove in SB06."],
      ["Package services", "C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Plugins/Catalog/PluginPackageServices.cs", "299, 514-515, 787, 800-801", "Recursive manifest scan, manifest trust validation, package assembly activation."],
      ["Catalog services", "C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Plugins/Catalog/PluginCatalogServices.cs", "186, 270-289", "Bundled-only not found/fallback wording and source/trust fallback."],
      ["Plugins page", "C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Plugins/Pages/PluginsPage.razor", "24, 131, 137-138, 907", "Bundled-only visible text and OAuth client wording."],
      ["Workflow audit", "C:/repositories/CanDoItAll/src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExecutorObservability.cs", "15, 34", "Audit record and observer contract."],
      ["Workflow invoker", "C:/repositories/CanDoItAll/src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExecutorContracts.cs", "106, 254", "Invoker uses observer and creates audit record."],
      ["Null observer registration", "C:/repositories/CanDoItAll/src/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs", "88", "Currently scoped null observer."],
      ["Plugin events", "C:/repositories/CanDoItAll/src/CanDoItAll.Plugins.Abstractions/PluginExecutionContracts.cs", "41, 275", "Plugin event contract exists without durable implementation in inspected paths."],
      ["Canvas catalog", "C:/repositories/CanDoItAll/src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowExecutorCanvasCatalog.cs", "14, 39, 52, 70", "Quick-create action hierarchy and create action id."],
      ["Canvas submenu", "C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/04-context-menu-and-composer.js", "617, 644, 711", "Nested submenu rendering support."],
      ["Executor model", "C:/repositories/CanDoItAll/src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowExecutorModels.cs", "63, 100, 358, 367", "Source descriptor and string IconName."],
      ["Package icon", "C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Plugins/Catalog/PluginPackageModels.cs", "101, 120", "Package manifest/UI icon path."],
      ["Connection store", "C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Plugins/Catalog/PluginPermissionServices.cs", "146-158, 278", "In-memory latest selection and sync grant list."],
      ["OAuth service", "C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Plugins/OAuth/PluginOAuthService.cs", "329, 364-365", "Materializes OAuth candidates before latest ordering."],
      ["Docker plugin", "C:/repositories/CanDoItAll/src/plugins/CanDoItAll.Plugin.Docker", "various", "Bundled descriptor, workflow executors, constants, service registrar."],
      ["Gmail plugin", "C:/repositories/CanDoItAll/src/plugins/CanDoItAll.Plugin.Gmail", "various", "Bundled descriptor, workflow executors, constants, service registrar."],
      ["Office365 plugin", "C:/repositories/CanDoItAll/src/plugins/CanDoItAll.Plugin.Office365", "various", "Bundled descriptor, workflow executors, constants, service registrar."],
    ],
  },
  {
    name: "Performance EF Scan",
    widths: [110, 430, 410, 430, 360],
    rows: [
      ["Finding", "Source", "Current risk", "Required change", "Validation"],
      ["PERF-EF-001", "PluginPermissionServices.cs:146, :155, :157", "All matching connections materialized before latest ordering.", "Order by UpdatedAtUtc descending and select first in EF before projection/materialization.", "Test latest selection; inspect query path."],
      ["PERF-EF-002", "PluginOAuthService.cs:329, :364, :365", "Joined OAuth/connection candidates materialized before ordering; can grow with connections.", "Order/reduce in EF; keep only bounded in-memory scope JSON filtering if unavoidable.", "Tests for latest and scope behavior."],
      ["PERF-EF-003", "PluginPermissionServices.cs:32, :278; concrete executor Descriptor properties", "Descriptor availability can create repeated sync database reads when catalog builds.", "Introduce scoped async/batch availability snapshot or explicit cache for catalog construction.", "Test/instrument no per-descriptor DB read pattern."],
      ["PERF-IO-004", "PluginPackageServices.cs:299, :787", "Recursive scans can discover nested manifests and waste IO.", "Direct package-root enumerator shared by listing and activation.", "Nested manifest test; rg confirms no recursive installed manifest scan."],
      ["Positive", "Targeted scan", "No broad ToLower/ToUpper issue, no new Regex hot path, read-only plugin EF queries generally use AsNoTracking.", "Preserve these good properties while changing queries.", "Do not remove AsNoTracking from read-only paths."],
    ],
  },
  {
    name: "Icon Assets",
    widths: [120, 340, 180, 450, 360, 280],
    rows: [
      ["Plugin", "Preferred local asset", "Fallback icon", "Source guidance", "Usage surfaces", "Acceptance"],
      ["Docker", "Reviewed local Docker SVG from Docker media resources or reviewed Simple Icons docker asset.", "terminal or deployed_code", "https://www.docker.com/company/newsroom/media-resources/; https://www.docker.com/legal/trademark-guidelines/; https://github.com/simple-icons/simple-icons", "Plugins page, workflow plugin submenu, executor node small icon, Docker package manifest.", "Local asset or fallback included; no hotlink; package ZIP includes valid icon."],
      ["Gmail", "Reviewed local Gmail SVG from Google Brand Resource Center or reviewed Simple Icons gmail asset.", "mail", "https://about.google/brand-resource-center/logos-list/; https://about.google/brand-resource-center/brand-elements/; https://github.com/simple-icons/simple-icons", "Plugins page, workflow plugin submenu, executor node small icon.", "Local asset or fallback included; legal/trademark note recorded."],
      ["Office365", "Reviewed local Microsoft 365/Office asset if approved; otherwise neutral apps/cloud icon.", "apps or cloud", "https://learn.microsoft.com/en-us/office/dev/add-ins/design/microsoft-365-extension-management-icons", "Plugins page, workflow plugin submenu, executor node small icon.", "Supports many executors under one Office365 plugin layer."],
    ],
  },
  {
    name: "Validation Plan",
    widths: [100, 440, 410, 350, 170],
    rows: [
      ["Subbundle", "Command or proof", "Expected result", "Artifact/log", "Required before"],
      ["SB01", "Targeted package activation integration test with real package assembly fixture.", "Executor from package assembly appears after activation without bundled descriptor conflict.", "reviews/01-execution-report.md; test output.", "SB02-SB06"],
      ["SB01", "Nested manifest test.", "Nested plugin.package.json is ignored or rejected predictably.", "Test output.", "SB05"],
      ["SB02", "Log persistence/redaction tests.", "Installation/runtime records persist and sensitive fields are masked.", "Test output.", "SB06"],
      ["SB02", "Browser proof on /plugins logs subtab.", "Installation and runtime streams shown separately and sorted.", "artifacts/sb02-plugin-logs-*.png.", "SB06 diagnostics"],
      ["SB03", "Action hierarchy tests.", "Executors -> Plugins -> plugin -> executor tree generated.", "Test output.", "SB04/SB06"],
      ["SB03", "Browser proof on workflow canvas.", "Plugin executor can be created through nested submenu.", "artifacts/sb03-*.png.", "SB04/SB06"],
      ["SB04", "Icon resolution/path traversal tests.", "Valid package icon resolves; unsafe path rejected; fallback explicit.", "Test output.", "SB06"],
      ["SB04", "Browser proof for icons.", "Icons render in plugin page, menu, and executor node.", "artifacts/sb04-*.png.", "SB06"],
      ["SB05", "Targeted EF/performance tests and rg scan.", "All PERF findings resolved or explicitly deferred.", "reviews/01-execution-report.md.", "SB06"],
      ["SB06", "dotnet build C:/repositories/CanDoItAll/CanDoItAll.sln", "App builds without Docker default registration.", "Build output summary.", "Closure"],
      ["SB06", "Docker ZIP install/activation test.", "Docker executors appear only after package install/activation.", "Test output; plugin logs.", "Closure"],
      ["SB06", "Browser proof before/after Docker install.", "Docker absent before install; present after install; workflow menu groups Docker executors.", "artifacts/sb06-*.png.", "Closure"],
      ["SB06", "ZIP checksum.", "Handoff artifact path and checksum recorded.", "reviews/01-execution-report.md.", "Closure"],
    ],
  },
  {
    name: "Docker Package Handoff",
    widths: [110, 390, 390, 380, 300],
    rows: [
      ["Task", "Source", "Required output", "Proof", "Notes"],
      ["Disable default", "RuntimeHostServiceCollectionExtensions.cs:54", "Remove AddCanDoItAllDockerPlugin from default composition.", "Build/start proof shows Docker absent before install.", "Do not remove Gmail or Office365."],
      ["Package manifest", "DockerBundledPlugin.cs; DockerPluginConstants.cs; PluginPackageServices.cs", "Runtime package manifest with local package source/trust, plugin id, package id, version, icon, assemblies.", "Manifest validation test.", "Must follow SB01 activation contract."],
      ["Build output", "CanDoItAll.Plugin.Docker.csproj", "Compiled Docker plugin assembly and dependencies staged for package.", "Package assembly activation test.", "Do not depend on default registration."],
      ["ZIP artifact", "Bundle or repo artifacts folder selected by implementer", "Installable Docker plugin ZIP.", "Checksum recorded.", "Final path belongs in execution report."],
      ["Install proof", "Plugins package upload/install flow", "Docker catalog entry appears after install/activation.", "Plugin logs and browser proof.", "Failure logs must be actionable."],
      ["Workflow proof", "Workflow canvas context menu", "Docker executors under Plugins -> Docker -> exact executor.", "Canvas screenshot after install.", "Depends on SB03/SB04."],
      ["Final state", "Running app", "App is running without Docker default module; user can manually install ZIP.", "Execution report final note.", "This is the closure gate."],
    ],
  },
];

function columnName(index) {
  let n = index + 1;
  let name = "";
  while (n > 0) {
    const remainder = (n - 1) % 26;
    name = String.fromCharCode(65 + remainder) + name;
    n = Math.floor((n - 1) / 26);
  }
  return name;
}

function addSheet(definition) {
  const sheet = workbook.worksheets.add(definition.name);
  const rowCount = definition.rows.length;
  const colCount = definition.rows[0].length;
  const lastCol = columnName(colCount - 1);
  const range = sheet.getRange(`A1:${lastCol}${rowCount}`);
  range.values = definition.rows;

  sheet.showGridLines = false;
  sheet.freezePanes.freezeRows(1);

  const header = sheet.getRange(`A1:${lastCol}1`);
  header.format = {
    fill: "#1F4E78",
    font: { bold: true, color: "#FFFFFF" },
    wrapText: true,
  };

  range.format = {
    wrapText: true,
    font: { name: "Aptos", size: 10, color: "#111827" },
  };
  header.format = {
    fill: "#1F4E78",
    font: { bold: true, color: "#FFFFFF", name: "Aptos", size: 10 },
    wrapText: true,
  };

  for (let i = 0; i < definition.widths.length; i += 1) {
    sheet.getRangeByIndexes(0, i, rowCount, 1).format.columnWidthPx = definition.widths[i];
  }

  sheet.getRangeByIndexes(0, 0, rowCount, colCount).format.rowHeightPx = 42;
  sheet.getRange(`A1:${lastCol}1`).format.rowHeightPx = 32;

  return sheet;
}

for (const definition of sheets) {
  addSheet(definition);
}

await fs.mkdir(path.dirname(outputPath), { recursive: true });
await fs.mkdir(previewDir, { recursive: true });

for (const definition of sheets) {
  const preview = await workbook.render({
    sheetName: definition.name,
    range: `A1:${columnName(definition.rows[0].length - 1)}${Math.min(definition.rows.length, 30)}`,
    scale: 1,
    format: "png",
  });
  await fs.writeFile(
    path.join(previewDir, `${definition.name.replace(/[^a-z0-9]+/gi, "-").toLowerCase()}.png`),
    new Uint8Array(await preview.arrayBuffer()));
}

const inspection = await workbook.inspect({
  kind: "sheet,table",
  maxChars: 6000,
  tableMaxRows: 5,
  tableMaxCols: 6,
});
console.log(inspection.ndjson);

const errors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 300 },
  summary: "final formula error scan",
});
console.log(errors.ndjson);

const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);
console.log(`Saved ${outputPath}`);
