using System.Text;
using System.Text.Json;

namespace CanDoItAll.Manager;

public static class ManagerDashboardPage
{
    public static string Render(
        ManagerStatusResponse status,
        CapsuleCoverageSummary coverage,
        bool openApiAvailable)
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var bootstrapJson = JsonSerializer.Serialize(status, jsonOptions);
        var coverageJson = JsonSerializer.Serialize(coverage, jsonOptions);
        var openApiLink = openApiAvailable
            ? """<a class="link" href="/openapi/v1.json" target="_blank" rel="noreferrer">OpenAPI</a>"""
            : """<span class="muted">OpenAPI hidden outside Development.</span>""";

        var page = new StringBuilder();
        page.AppendLine(
            """
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>CanDoItAll Manager</title>
  <link rel="icon" href="data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 64 64'%3E%3Crect width='64' height='64' rx='16' fill='%232f6c56'/%3E%3Cpath d='M18 18h28v8H26v12h16v8H18z' fill='white'/%3E%3C/svg%3E" />
  <style>
    :root {
      color-scheme: light;
      --bg: #f3f0e7;
      --panel: #fffdf8;
      --ink: #1f2a1f;
      --muted: #66725f;
      --line: #d7d0c2;
      --accent: #2f6c56;
      --accent-soft: #dcebe3;
      --warn: #8c5f14;
      --warn-soft: #fff3d6;
      --bad: #8a2f3f;
      --bad-soft: #f8dde2;
    }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      font-family: "Segoe UI", "Trebuchet MS", sans-serif;
      background: radial-gradient(circle at top left, #fff8e8, var(--bg) 42%, #ece7da 100%);
      color: var(--ink);
    }
    main {
      max-width: 1160px;
      margin: 0 auto;
      padding: 32px 20px 56px;
    }
    .hero {
      display: grid;
      gap: 16px;
      margin-bottom: 24px;
    }
    h1 {
      margin: 0;
      font-size: clamp(2rem, 4vw, 3.1rem);
      line-height: 0.95;
      letter-spacing: -0.04em;
    }
    h2 {
      margin: 0 0 14px;
      font-size: 1.05rem;
      letter-spacing: -0.02em;
    }
    h3 {
      margin: 0;
      font-size: 1rem;
      letter-spacing: -0.01em;
    }
    .subtitle {
      margin: 0;
      max-width: 72ch;
      color: var(--muted);
      font-size: 1rem;
      line-height: 1.5;
    }
    .strip {
      display: flex;
      flex-wrap: wrap;
      gap: 10px;
      align-items: center;
    }
    .pill {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      padding: 8px 12px;
      border-radius: 999px;
      background: var(--accent-soft);
      color: var(--accent);
      font-weight: 600;
      font-size: 0.92rem;
    }
    .link {
      color: var(--accent);
      text-decoration: none;
      font-weight: 600;
    }
    .link:hover { text-decoration: underline; }
    .muted { color: var(--muted); }
    .grid {
      display: grid;
      gap: 18px;
      grid-template-columns: repeat(auto-fit, minmax(290px, 1fr));
    }
    .panel {
      background: color-mix(in srgb, var(--panel) 92%, white);
      border: 1px solid var(--line);
      border-radius: 20px;
      padding: 18px;
      box-shadow: 0 12px 30px rgba(70, 56, 26, 0.08);
    }
    dl {
      margin: 0;
      display: grid;
      grid-template-columns: minmax(110px, 150px) 1fr;
      gap: 10px 14px;
      font-size: 0.95rem;
    }
    dt { color: var(--muted); }
    dd { margin: 0; word-break: break-word; }
    pre {
      margin: 0;
      padding: 14px;
      border-radius: 16px;
      background: #1f231f;
      color: #eef5eb;
      overflow: auto;
      font-size: 0.87rem;
      line-height: 1.45;
      min-height: 280px;
    }
    .callout {
      border-radius: 14px;
      padding: 12px 14px;
      margin-top: 14px;
      font-size: 0.92rem;
      border: 1px solid var(--line);
    }
    .service-list {
      display: grid;
      gap: 12px;
    }
    .service-card {
      border: 1px solid var(--line);
      border-radius: 16px;
      padding: 14px;
      background: rgba(255,255,255,0.7);
      display: grid;
      gap: 10px;
    }
    .service-head {
      display: flex;
      justify-content: space-between;
      gap: 10px;
      align-items: center;
    }
    .badge {
      display: inline-flex;
      align-items: center;
      padding: 6px 10px;
      border-radius: 999px;
      font-size: 0.82rem;
      font-weight: 700;
      letter-spacing: 0.02em;
      text-transform: uppercase;
    }
    .badge-ok {
      background: var(--accent-soft);
      color: var(--accent);
    }
    .badge-starting {
      background: var(--warn-soft);
      color: var(--warn);
    }
    .badge-degraded,
    .badge-error {
      background: var(--bad-soft);
      color: var(--bad);
    }
    .service-links,
    .service-meta {
      display: grid;
      gap: 6px;
      font-size: 0.92rem;
    }
    .service-links a {
      width: fit-content;
    }
    ul.url-list {
      margin: 0;
      padding-left: 18px;
    }
    .empty {
      color: var(--muted);
      font-style: italic;
    }
  </style>
</head>
<body>
  <main>
    <section class="hero">
      <div class="strip">
        <span class="pill">CanDoItAll Manager</span>
        <span class="pill">Environment: <strong id="env-name"></strong></span>
      </div>
      <h1>Local Control Surface</h1>
      <p class="subtitle">
        This host manages the watched application and exposes diagnostics for its runtime. Use this page to see which services should be reachable, which ones are healthy, and what the latest watch output says.
      </p>
      <div class="strip">
        <a class="link" href="/api/status" target="_blank" rel="noreferrer">Status JSON</a>
        <a class="link" href="/api/watch/logs" target="_blank" rel="noreferrer">Watch Logs JSON</a>
        <a class="link" href="/api/tailwind/logs" target="_blank" rel="noreferrer">Tailwind Logs JSON</a>
        <a class="link" href="/api/capsules/coverage" target="_blank" rel="noreferrer">Capsule Coverage</a>
"""
        );
        page.Append("        ");
        page.AppendLine(openApiLink);
        page.AppendLine(
            """
      </div>
    </section>

    <section class="grid">
      <article class="panel">
        <h2>Session</h2>
        <dl>
          <dt>Environment</dt>
          <dd id="environment-name"></dd>
          <dt>Session Token</dt>
          <dd id="session-token"></dd>
          <dt>Workspace Root</dt>
          <dd id="workspace-root"></dd>
          <dt>Watch Project</dt>
          <dd id="watch-project"></dd>
        </dl>
      </article>

      <article class="panel">
        <h2>Watch</h2>
        <dl>
          <dt>State</dt>
          <dd id="watch-state"></dd>
          <dt>Summary</dt>
          <dd id="watch-summary"></dd>
          <dt>Iteration</dt>
          <dd id="watch-iteration"></dd>
          <dt>Configured URLs</dt>
          <dd id="configured-urls"></dd>
          <dt>Active URLs</dt>
          <dd id="watch-urls"></dd>
        </dl>
        <div class="callout" id="watch-callout">
          Waiting for manager status...
        </div>
      </article>

      <article class="panel">
        <h2>Tailwind</h2>
        <dl>
          <dt>State</dt>
          <dd id="tailwind-state"></dd>
          <dt>Summary</dt>
          <dd id="tailwind-summary"></dd>
          <dt>Workspace</dt>
          <dd id="tailwind-workspace"></dd>
          <dt>Input</dt>
          <dd id="tailwind-input"></dd>
          <dt>Output</dt>
          <dd id="tailwind-output"></dd>
          <dt>Output Updated</dt>
          <dd id="tailwind-output-updated"></dd>
        </dl>
      </article>

      <article class="panel">
        <h2>Capsules</h2>
        <dl>
          <dt>Covered</dt>
          <dd id="capsules-covered"></dd>
          <dt>Missing</dt>
          <dd id="capsules-missing"></dd>
          <dt>Malformed</dt>
          <dd id="capsules-malformed"></dd>
          <dt>Refreshed</dt>
          <dd id="capsules-refreshed"></dd>
        </dl>
      </article>
    </section>

    <section class="panel" style="margin-top: 18px;">
      <h2>Services</h2>
      <div id="service-list" class="service-list"></div>
    </section>

    <section class="panel" style="margin-top: 18px;">
      <h2>Recent Watch Output</h2>
      <pre id="watch-log">Loading...</pre>
    </section>

    <section class="panel" style="margin-top: 18px;">
      <h2>Recent Tailwind Output</h2>
      <pre id="tailwind-log">Loading...</pre>
    </section>
  </main>

  <script id="manager-status-bootstrap" type="application/json">__BOOTSTRAP_STATUS__</script>
  <script id="manager-coverage-bootstrap" type="application/json">__BOOTSTRAP_COVERAGE__</script>
  <script>
    const bootstrapStatus = JSON.parse(document.getElementById("manager-status-bootstrap").textContent || "{}");
    const bootstrapCoverage = JSON.parse(document.getElementById("manager-coverage-bootstrap").textContent || "{}");

    function setText(id, value) {
      const node = document.getElementById(id);
      if (node) node.textContent = value ?? "";
    }

    function clearNode(node) {
      while (node.firstChild) node.removeChild(node.firstChild);
    }

    function renderItemList(node, items, emptyText) {
      if (!node) return;
      clearNode(node);
      if (!items || items.length === 0) {
        const span = document.createElement("span");
        span.className = "empty";
        span.textContent = emptyText;
        node.appendChild(span);
        return;
      }

      const list = document.createElement("ul");
      list.className = "url-list";
      items.forEach(itemValue => {
        const item = document.createElement("li");
        item.textContent = itemValue;
        list.appendChild(item);
      });
      node.appendChild(list);
    }

    function renderServiceLinks(node, links) {
      clearNode(node);
      if (!links || links.length === 0) {
        const span = document.createElement("span");
        span.className = "empty";
        span.textContent = "No reachable links reported yet.";
        node.appendChild(span);
        return;
      }

      links.forEach(url => {
        const anchor = document.createElement("a");
        anchor.className = "link";
        anchor.href = url;
        anchor.target = "_blank";
        anchor.rel = "noreferrer";
        anchor.textContent = url;
        node.appendChild(anchor);
      });
    }

    function badgeClass(health) {
      const normalized = (health || "").toLowerCase();
      if (normalized === "ok") return "badge badge-ok";
      if (normalized === "starting") return "badge badge-starting";
      if (normalized === "degraded") return "badge badge-degraded";
      return "badge badge-error";
    }

    function renderServices(status) {
      const host = document.getElementById("service-list");
      if (!host) return;

      clearNode(host);
      const services = status?.services || [];
      if (services.length === 0) {
        const empty = document.createElement("div");
        empty.className = "empty";
        empty.textContent = "No services reported.";
        host.appendChild(empty);
        return;
      }

      services.forEach(service => {
        const card = document.createElement("article");
        card.className = "service-card";

        const head = document.createElement("div");
        head.className = "service-head";

        const title = document.createElement("h3");
        title.textContent = service.name;

        const badge = document.createElement("span");
        badge.className = badgeClass(service.health);
        badge.textContent = service.health;

        head.appendChild(title);
        head.appendChild(badge);

        const summary = document.createElement("div");
        summary.textContent = service.summary;

        const links = document.createElement("div");
        links.className = "service-links";
        renderServiceLinks(links, service.links);

        const meta = document.createElement("div");
        meta.className = "service-meta";

        const configured = document.createElement("div");
        configured.innerHTML = `<strong>${service.configuredLabel || "Configured"}:</strong>`;
        const configuredList = document.createElement("div");
        renderItemList(configuredList, service.configuredTargets, "No configured items.");
        configured.appendChild(configuredList);

        const active = document.createElement("div");
        active.innerHTML = `<strong>${service.activeLabel || "Active"}:</strong>`;
        const activeList = document.createElement("div");
        renderItemList(activeList, service.activeTargets, "No active items yet.");
        active.appendChild(activeList);

        meta.appendChild(configured);
        meta.appendChild(active);

        card.appendChild(head);
        card.appendChild(summary);
        card.appendChild(links);
        card.appendChild(meta);
        host.appendChild(card);
      });
    }

    function applyStatus(status) {
      if (!status) return;
      setText("env-name", status.environment);
      setText("environment-name", status.environment);
      setText("session-token", status.sessionToken);
      setText("workspace-root", status.workspaceRoot);
      setText("watch-project", status.watchProjectPath);
      setText("watch-state", status.watch?.stateName || status.watch?.state);
      setText("watch-summary", status.watch?.summary);
      const expected = status.watch?.expectedWatchIteration ?? "n/a";
      const confirmed = status.watch?.confirmedWatchIteration ?? "n/a";
      setText("watch-iteration", `${expected} -> ${confirmed}`);
      renderItemList(document.getElementById("configured-urls"), status.configuredApplicationUrls || [], "No configured URLs.");
      renderItemList(document.getElementById("watch-urls"), status.watch?.activeUrls || [], "No active URLs yet.");
      setText("tailwind-state", status.tailwind?.stateName || status.tailwind?.state);
      setText("tailwind-summary", status.tailwind?.summary);
      setText("tailwind-workspace", status.tailwind?.workspacePath);
      setText("tailwind-input", status.tailwind?.inputFilePath);
      setText("tailwind-output", status.tailwind?.outputFilePath);
      setText("tailwind-output-updated", status.tailwind?.outputLastWriteUtc || (status.tailwind?.outputExists ? "Present" : "Not generated yet"));

      const watchCallout = document.getElementById("watch-callout");
      if (watchCallout) {
        const appService = (status.services || []).find(service => service.key === "web");
        watchCallout.className = "callout";
        if (appService?.health === "Ok") {
          watchCallout.style.background = "var(--accent-soft)";
          watchCallout.style.color = "var(--accent)";
          watchCallout.textContent = appService.summary;
        } else if (appService?.health === "Starting") {
          watchCallout.style.background = "var(--warn-soft)";
          watchCallout.style.color = "var(--warn)";
          watchCallout.textContent = appService.summary;
        } else {
          watchCallout.style.background = "var(--bad-soft)";
          watchCallout.style.color = "var(--bad)";
          watchCallout.textContent = appService?.summary || "Watched application is not healthy.";
        }
      }

      renderServices(status);
    }

    function applyCoverage(coverage) {
      if (!coverage) return;
      setText("capsules-covered", `${coverage.coveredFiles}/${coverage.totalFiles}`);
      setText("capsules-missing", coverage.missingFiles);
      setText("capsules-malformed", coverage.malformedFiles);
      setText("capsules-refreshed", coverage.refreshedAtUtc);
    }

    async function loadLogText(endpoint, targetId) {
      try {
        const response = await fetch(endpoint, { cache: "no-store" });
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const logs = await response.json();
        const text = logs
          .slice()
          .reverse()
          .map(entry => `[${entry.timestampUtc}] ${entry.line}`)
          .join("\\n");
        setText(targetId, text || "No output yet.");
      } catch (error) {
        setText(targetId, `Unable to load logs. ${error}`);
      }
    }

    async function refresh() {
      try {
        const [statusResponse, coverageResponse] = await Promise.all([
          fetch("/api/status", { cache: "no-store" }),
          fetch("/api/capsules/coverage", { cache: "no-store" })
        ]);
        if (statusResponse.ok) applyStatus(await statusResponse.json());
        if (coverageResponse.ok) applyCoverage(await coverageResponse.json());
      } catch {}

      await Promise.all([
        loadLogText("/api/watch/logs?take=60", "watch-log"),
        loadLogText("/api/tailwind/logs?take=60", "tailwind-log")
      ]);
    }

    applyStatus(bootstrapStatus);
    applyCoverage(bootstrapCoverage);
    refresh();
    setInterval(refresh, 3000);
  </script>
</body>
</html>
"""
        );

        return page.ToString()
            .Replace("__BOOTSTRAP_STATUS__", bootstrapJson, StringComparison.Ordinal)
            .Replace("__BOOTSTRAP_COVERAGE__", coverageJson, StringComparison.Ordinal);
    }
}
