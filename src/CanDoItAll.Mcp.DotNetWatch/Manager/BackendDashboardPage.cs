using System.Text;
using System.Text.Json;
using CanDoItAll.Mcp.DotNetWatch.Backend;

namespace CanDoItAll.Mcp.DotNetWatch.Manager;

internal static class BackendDashboardPage
{
    public static string Render(BackendManagerStatusResponse status)
    {
        var json = JsonSerializer.Serialize(status, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var page = new StringBuilder();
        page.AppendLine(
            """
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>DotNetWatch Backend Manager</title>
  <style>
    :root {
      color-scheme: light;
      --bg: #eef2f7;
      --panel: rgba(255,255,255,0.94);
      --line: rgba(15,23,42,0.1);
      --ink: #0f172a;
      --muted: #64748b;
      --accent: #0f766e;
      --accent-soft: rgba(15,118,110,0.12);
      --warn: #b45309;
      --warn-soft: rgba(245,158,11,0.14);
      --bad: #be123c;
      --bad-soft: rgba(244,63,94,0.12);
    }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      font-family: "Segoe UI", "Trebuchet MS", sans-serif;
      color: var(--ink);
      background:
        radial-gradient(circle at top left, rgba(255,255,255,0.88), rgba(255,255,255,0) 34%),
        linear-gradient(135deg, #e0f2fe 0%, var(--bg) 42%, #fefce8 100%);
    }
    main {
      max-width: 1180px;
      margin: 0 auto;
      padding: 28px 20px 48px;
      display: grid;
      gap: 18px;
    }
    .hero, .panel {
      border: 1px solid var(--line);
      border-radius: 24px;
      background: var(--panel);
      box-shadow: 0 18px 36px rgba(15,23,42,0.08);
      padding: 18px;
      backdrop-filter: blur(10px);
    }
    .hero { display: grid; gap: 12px; }
    h1, h2 { margin: 0; }
    h1 { font-size: clamp(2rem, 4vw, 3rem); letter-spacing: -0.04em; }
    .subtitle { margin: 0; color: var(--muted); max-width: 76ch; line-height: 1.5; }
    .pill-row, .session-meta { display: flex; flex-wrap: wrap; gap: 10px; }
    .pill {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      min-height: 2.35rem;
      padding: 0.55rem 0.9rem;
      border-radius: 999px;
      background: var(--accent-soft);
      color: var(--accent);
      font-weight: 700;
      font-size: 0.9rem;
    }
    .grid { display: grid; gap: 18px; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); }
    dl {
      margin: 0;
      display: grid;
      grid-template-columns: minmax(120px, 150px) 1fr;
      gap: 10px 14px;
      font-size: 0.94rem;
    }
    dt { color: var(--muted); }
    dd { margin: 0; word-break: break-word; }
    .stack { display: grid; gap: 12px; }
    .card {
      border: 1px solid var(--line);
      border-radius: 18px;
      background: rgba(255,255,255,0.86);
      padding: 14px;
      display: grid;
      gap: 10px;
    }
    .badge {
      display: inline-flex;
      align-items: center;
      padding: 0.35rem 0.7rem;
      border-radius: 999px;
      font-size: 0.75rem;
      font-weight: 800;
      text-transform: uppercase;
      letter-spacing: 0.08em;
    }
    .badge-ok { background: var(--accent-soft); color: var(--accent); }
    .badge-warn { background: var(--warn-soft); color: var(--warn); }
    .badge-bad { background: var(--bad-soft); color: var(--bad); }
    pre {
      margin: 0;
      padding: 14px;
      border-radius: 16px;
      background: #111827;
      color: #e5eefb;
      overflow: auto;
      font-size: 0.84rem;
      line-height: 1.45;
      max-height: 22rem;
    }
    .empty { color: var(--muted); font-style: italic; }
  </style>
</head>
<body>
  <main>
    <section class="hero">
      <div class="pill-row">
        <span class="pill">DotNetWatch Backend</span>
        <span class="pill">PID <strong id="backend-pid"></strong></span>
        <span class="pill">Sessions <strong id="session-count"></strong></span>
        <span class="pill">Operations <strong id="operation-count"></strong></span>
      </div>
      <h1>Persistent Runtime Control</h1>
      <p class="subtitle">This backend owns live app sessions and survives MCP stdio process re-instancing. Use it to confirm which sessions are alive, which ones were reused, and whether operations are preempting anything they should not.</p>
    </section>

    <section class="grid">
      <article class="panel">
        <h2>Backend Identity</h2>
        <dl>
          <dt>Backend Id</dt>
          <dd id="backend-id"></dd>
          <dt>Base URL</dt>
          <dd id="base-url"></dd>
          <dt>Workspace</dt>
          <dd id="workspace-root"></dd>
          <dt>Settings</dt>
          <dd id="settings-path"></dd>
          <dt>Version</dt>
          <dd id="version-marker"></dd>
        </dl>
      </article>

      <article class="panel">
        <h2>Recent Operations</h2>
        <div id="operation-list" class="stack"></div>
      </article>
    </section>

    <section class="panel">
      <h2>Live Sessions</h2>
      <div id="session-list" class="stack"></div>
    </section>

    <section class="panel">
      <h2>Status JSON</h2>
      <pre id="status-json"></pre>
    </section>
  </main>

  <script id="bootstrap-status" type="application/json">__STATUS__</script>
  <script>
    const bootstrap = JSON.parse(document.getElementById("bootstrap-status").textContent || "{}");
    const token = new URLSearchParams(window.location.search).get("token");

    function setText(id, value) {
      const node = document.getElementById(id);
      if (node) node.textContent = value ?? "";
    }

    function clearNode(node) {
      while (node.firstChild) node.removeChild(node.firstChild);
    }

    function stateBadge(state) {
      const normalized = String(state || "").toLowerCase();
      if (normalized.includes("healthy") || normalized.includes("completed") || normalized.includes("running")) return "badge badge-ok";
      if (normalized.includes("restart") || normalized.includes("start") || normalized.includes("queue")) return "badge badge-warn";
      return "badge badge-bad";
    }

    function renderSessions(status) {
      const host = document.getElementById("session-list");
      clearNode(host);
      const sessions = status.activeSessions || [];
      if (sessions.length === 0) {
        const empty = document.createElement("div");
        empty.className = "empty";
        empty.textContent = "No live sessions.";
        host.appendChild(empty);
        return;
      }

      sessions.forEach(session => {
        const card = document.createElement("article");
        card.className = "card";
        const badgeClass = stateBadge(session.state);
        card.innerHTML = `
          <div class="session-meta">
            <strong>${session.projectPath}</strong>
            <span class="${badgeClass}">${session.state}</span>
          </div>
          <div class="session-meta">
            <span>Session: <strong>${session.sessionId}</strong></span>
            <span>Watcher PID: <strong>${session.watch?.watcherPid ?? "n/a"}</strong></span>
            <span>Runtime PID: <strong>${session.watch?.runtimePid ?? "n/a"}</strong></span>
            <span>Mode: <strong>${session.mode}</strong></span>
          </div>
          <div>${session.watch?.summary || session.health?.summary || "No summary."}</div>`;
        host.appendChild(card);
      });
    }

    function renderOperations(status) {
      const host = document.getElementById("operation-list");
      clearNode(host);
      const operations = status.activeOperations?.length ? status.activeOperations : (status.recentOperations || []).slice(0, 5);
      if (!operations || operations.length === 0) {
        const empty = document.createElement("div");
        empty.className = "empty";
        empty.textContent = "No recent operations.";
        host.appendChild(empty);
        return;
      }

      operations.forEach(operation => {
        const badgeClass = stateBadge(operation.state);
        const resumed = operation.resumeOutcome?.sessionIds?.join(", ") || operation.resumeOutcome?.sessionId || "n/a";
        const card = document.createElement("article");
        card.className = "card";
        card.innerHTML = `
          <div class="session-meta">
            <strong>${operation.operationType}: ${operation.targetPath}</strong>
            <span class="${badgeClass}">${operation.state}</span>
          </div>
          <div>${operation.summary}</div>
          <div class="session-meta">
            <span>Operation: <strong>${operation.operationId}</strong></span>
            <span>Runner: <strong>${operation.runner ?? "n/a"}</strong></span>
            <span>Resume: <strong>${resumed}</strong></span>
          </div>`;
        host.appendChild(card);
      });
    }

    function apply(status) {
      setText("backend-id", status.backendId);
      setText("backend-pid", status.processId);
      setText("session-count", status.activeSessions?.length ?? 0);
      setText("operation-count", status.activeOperations?.length ?? 0);
      setText("base-url", status.baseUrl);
      setText("workspace-root", status.identity?.workspaceRoot);
      setText("settings-path", status.identity?.settingsPath);
      setText("version-marker", status.identity?.binaryVersionMarker);
      setText("status-json", JSON.stringify(status, null, 2));
      renderSessions(status);
      renderOperations(status);
    }

    async function refresh() {
      try {
        const response = await fetch(`/api/manager/status?token=${encodeURIComponent(token || "")}`, { cache: "no-store" });
        if (!response.ok) return;
        apply(await response.json());
      } catch {}
    }

    apply(bootstrap);
    setInterval(refresh, 3000);
  </script>
</body>
</html>
"""
        );

        return page.ToString().Replace("__STATUS__", json, StringComparison.Ordinal);
    }
}
