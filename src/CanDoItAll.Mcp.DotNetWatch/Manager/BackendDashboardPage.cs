using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Mcp.DotNetWatch.Backend;

namespace CanDoItAll.Mcp.DotNetWatch.Manager;

internal static class BackendDashboardPage
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static string Render(BackendManagerStatusResponse status)
    {
        var json = JsonSerializer.Serialize(status, JsonOptions);
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
      --bg: #eff4fb;
      --panel: rgba(255,255,255,0.95);
      --line: rgba(15,23,42,0.10);
      --ink: #0f172a;
      --muted: #64748b;
      --accent: #0f766e;
      --accent-soft: rgba(15,118,110,0.12);
      --warn: #b45309;
      --warn-soft: rgba(245,158,11,0.14);
      --bad: #be123c;
      --bad-soft: rgba(244,63,94,0.12);
      --blue: #1d4ed8;
      --blue-soft: rgba(29,78,216,0.12);
    }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      font-family: "Segoe UI", "Trebuchet MS", sans-serif;
      color: var(--ink);
      background:
        radial-gradient(circle at top left, rgba(255,255,255,0.85), rgba(255,255,255,0) 34%),
        linear-gradient(135deg, #dbeafe 0%, var(--bg) 42%, #fef9c3 100%);
    }
    main {
      max-width: 1320px;
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
    h1, h2, h3 { margin: 0; }
    h1 { font-size: clamp(2rem, 4vw, 3rem); letter-spacing: -0.04em; }
    h2 { font-size: 1.2rem; }
    h3 { font-size: 1rem; }
    .subtitle { margin: 0; color: var(--muted); max-width: 88ch; line-height: 1.5; }
    .pill-row, .meta-row, .action-row { display: flex; flex-wrap: wrap; gap: 10px; align-items: center; }
    .pill, .badge {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      min-height: 2.2rem;
      padding: 0.5rem 0.85rem;
      border-radius: 999px;
      font-weight: 700;
      font-size: 0.88rem;
    }
    .pill { background: var(--accent-soft); color: var(--accent); }
    .badge-ok { background: var(--accent-soft); color: var(--accent); }
    .badge-warn { background: var(--warn-soft); color: var(--warn); }
    .badge-bad { background: var(--bad-soft); color: var(--bad); }
    .badge-info { background: var(--blue-soft); color: var(--blue); }
    .layout { display: grid; gap: 18px; grid-template-columns: 1.2fr 0.8fr; }
    .stack { display: grid; gap: 12px; }
    .backend-grid { display: grid; gap: 16px; }
    .backend-card, .card {
      border: 1px solid var(--line);
      border-radius: 18px;
      background: rgba(255,255,255,0.88);
      padding: 14px;
      display: grid;
      gap: 12px;
    }
    .metrics-row {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      align-items: center;
    }
    .session-grid, .operation-grid { display: grid; gap: 10px; }
    .session-card, .operation-card {
      border: 1px solid rgba(15,23,42,0.08);
      border-radius: 16px;
      padding: 12px;
      background: rgba(248,250,252,0.92);
      display: grid;
      gap: 10px;
    }
    dl {
      margin: 0;
      display: grid;
      grid-template-columns: minmax(110px, 145px) 1fr;
      gap: 8px 12px;
      font-size: 0.93rem;
    }
    dt { color: var(--muted); }
    dd { margin: 0; word-break: break-word; }
    .link {
      color: var(--blue);
      text-decoration: none;
      font-weight: 600;
    }
    .link:hover { text-decoration: underline; }
    button {
      border: 0;
      border-radius: 999px;
      padding: 0.6rem 0.95rem;
      font: inherit;
      font-weight: 700;
      cursor: pointer;
      color: white;
      background: linear-gradient(135deg, #0f766e, #1d4ed8);
      box-shadow: 0 10px 18px rgba(29,78,216,0.18);
    }
    button.secondary { background: linear-gradient(135deg, #475569, #1f2937); }
    button.danger { background: linear-gradient(135deg, #be123c, #9f1239); }
    button.warn { background: linear-gradient(135deg, #d97706, #b45309); }
    button:disabled {
      cursor: not-allowed;
      opacity: 0.55;
      box-shadow: none;
    }
    .message {
      display: none;
      padding: 0.9rem 1rem;
      border-radius: 16px;
      font-weight: 600;
    }
    .message.show { display: block; }
    .message.ok { background: var(--accent-soft); color: var(--accent); }
    .message.error { background: var(--bad-soft); color: var(--bad); }
    pre {
      margin: 0;
      padding: 14px;
      border-radius: 16px;
      background: #111827;
      color: #e5eefb;
      overflow: auto;
      font-size: 0.83rem;
      line-height: 1.45;
      max-height: 26rem;
    }
    .empty { color: var(--muted); font-style: italic; }
    @media (max-width: 1080px) {
      .layout { grid-template-columns: 1fr; }
    }
  </style>
</head>
<body>
  <main>
    <section class="hero">
      <div class="pill-row">
        <span class="pill">DotNetWatch Backend</span>
        <span class="pill">Current PID <strong id="backend-pid"></strong></span>
        <span class="pill">Live Backends <strong id="backend-count"></strong></span>
        <span class="pill">Sessions <strong id="session-count"></strong></span>
        <span class="pill">Operations <strong id="operation-count"></strong></span>
      </div>
      <h1>Persistent Runtime Control</h1>
      <p class="subtitle">This manager aggregates every live backend daemon registered on the machine for this MCP server family. Use it to confirm which workspaces are alive, which sessions are running, and to execute basic operational actions without losing backend ownership when MCP stdio is re-instanced.</p>
      <div id="message" class="message"></div>
    </section>

    <section class="layout">
      <article class="panel stack">
        <div class="meta-row">
          <h2>Discovered Backends</h2>
          <button class="secondary" onclick="refresh()">Refresh</button>
        </div>
        <div id="backend-list" class="backend-grid"></div>
      </article>

      <article class="panel stack">
        <h2>Current Backend</h2>
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
        <h2>Status JSON</h2>
        <pre id="status-json"></pre>
      </article>
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

    function formatEnum(value) {
      if (value === null || value === undefined) return "n/a";
      const text = String(value);
      return text.replace(/([a-z0-9])([A-Z])/g, "$1 $2");
    }

    function escapeHtml(value) {
      return String(value ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;");
    }

    function stateBadge(state) {
      const normalized = String(state || "").toLowerCase();
      if (normalized.includes("healthy") || normalized.includes("completed") || normalized.includes("running")) return "badge badge-ok";
      if (normalized.includes("restart") || normalized.includes("start") || normalized.includes("queue")) return "badge badge-warn";
      return "badge badge-bad";
    }

    function reachableBadge(backend) {
      if (!backend.isReachable) return '<span class="badge badge-bad">Unreachable</span>';
      return backend.isCurrentBackend
        ? '<span class="badge badge-info">Current backend</span>'
        : '<span class="badge badge-ok">Reachable</span>';
    }

    function showMessage(kind, text) {
      const node = document.getElementById("message");
      node.className = `message show ${kind}`;
      node.textContent = text;
    }

    function clearMessage() {
      const node = document.getElementById("message");
      node.className = "message";
      node.textContent = "";
    }

    async function postAction(payload) {
      clearMessage();
      try {
        const response = await fetch(`/api/manager/action?token=${encodeURIComponent(token || "")}`, {
          method: "POST",
          headers: { "content-type": "application/json" },
          body: JSON.stringify(payload)
        });

        if (!response.ok) {
          showMessage("error", `Manager action failed with HTTP ${response.status}.`);
          return;
        }

        const result = await response.json();
        showMessage(result.success ? "ok" : "error", result.message || "Action finished.");
        await refresh();
      } catch (error) {
        showMessage("error", `Manager action failed: ${error}`);
      }
    }

    function sessionActions(backendId, session) {
      if (!session || !session.sessionId) return "";
      const safeBackend = JSON.stringify(backendId);
      const safeSession = JSON.stringify(session.sessionId);
      return `
        <div class="action-row">
          <button class="warn" onclick='postAction({ backendId: ${safeBackend}, action: "RebuildSession", sessionId: ${safeSession} })'>Rebuild</button>
          <button onclick='postAction({ backendId: ${safeBackend}, action: "ForceRebuildSession", sessionId: ${safeSession} })'>Force Rebuild</button>
          <button class="secondary" onclick='postAction({ backendId: ${safeBackend}, action: "StopSession", sessionId: ${safeSession} })'>Stop</button>
          <button class="danger" onclick='postAction({ backendId: ${safeBackend}, action: "ForceStopSession", sessionId: ${safeSession} })'>Force Stop</button>
        </div>`;
    }

    function renderSessions(backend) {
      const sessions = backend.activeSessions || [];
      if (sessions.length === 0) return '<div class="empty">No live sessions.</div>';

      return `<div class="session-grid">${sessions.map(session => `
        <article class="session-card">
          <div class="meta-row">
            <strong>${escapeHtml(session.projectPath)}</strong>
            <span class="${stateBadge(session.state)}">${escapeHtml(session.state)}</span>
          </div>
          <div class="meta-row">
            <span>Logical App <strong>${escapeHtml(session.logicalAppId ?? "n/a")}</strong></span>
            <span>Lane <strong>${escapeHtml(formatEnum(session.laneKind ?? "n/a"))}</strong></span>
            <span>Session <strong>${escapeHtml(session.sessionId)}</strong></span>
            <span>Watcher PID <strong>${escapeHtml(session.watch?.watcherPid ?? "n/a")}</strong></span>
            <span>Runtime PID <strong>${escapeHtml(session.watch?.runtimePid ?? session.lastKnownPid ?? "n/a")}</strong></span>
            <span>Mode <strong>${escapeHtml(formatEnum(session.mode))}</strong></span>
          </div>
          <div class="meta-row">
            <span>Revision <strong>${escapeHtml(session.revision?.value ?? "n/a")}</strong></span>
            <span>Slot <strong>${escapeHtml(session.slotId ?? "n/a")}</strong></span>
            <span>Txn <strong>${escapeHtml(session.activeTransactionId ?? "n/a")}</strong></span>
            <span>Rollback <strong>${escapeHtml(session.rollbackAvailable ? "yes" : "no")}</strong></span>
          </div>
          <div>${escapeHtml(session.watch?.summary || session.health?.summary || "No summary.")}</div>
          ${sessionActions(backend.backendId, session)}
        </article>`).join("")}</div>`;
    }

    function renderOperations(backend) {
      const operations = backend.activeOperations?.length ? backend.activeOperations : (backend.recentOperations || []).slice(0, 4);
      if (!operations || operations.length === 0) return '<div class="empty">No recent operations.</div>';

      return `<div class="operation-grid">${operations.map(operation => `
        <article class="operation-card">
          <div class="meta-row">
            <strong>${escapeHtml(operation.operationType)}: ${escapeHtml(operation.summary)}</strong>
            <span class="${stateBadge(operation.state)}">${escapeHtml(operation.state)}</span>
          </div>
          <div class="meta-row">
            <span>Operation <strong>${escapeHtml(operation.operationId)}</strong></span>
            <span>Runner <strong>${escapeHtml(operation.runner ?? "n/a")}</strong></span>
          </div>
        </article>`).join("")}</div>`;
    }

    function renderBackends(status) {
      const host = document.getElementById("backend-list");
      clearNode(host);
      const backends = status.backends || [];
      if (backends.length === 0) {
        const empty = document.createElement("div");
        empty.className = "empty";
        empty.textContent = "No live backends were discovered.";
        host.appendChild(empty);
        return;
      }

      backends.forEach(backend => {
        const card = document.createElement("article");
        card.className = "backend-card";
        const managerLink = backend.managerUrl
          ? `<a class="link" href="${escapeHtml(backend.managerUrl)}" target="_blank" rel="noreferrer">Open backend page</a>`
          : "";
        const controlButtons = backend.isReachable
          ? `<div class="action-row">
               <button onclick='postAction({ backendId: ${JSON.stringify(backend.backendId)}, action: "StartDefaultApp" })'>Start Default App</button>
               <button class="warn" onclick='postAction({ backendId: ${JSON.stringify(backend.backendId)}, action: "BuildWorkspace" })'>Build Workspace</button>
             </div>`
          : "";

        card.innerHTML = `
          <div class="meta-row">
            <h3>${escapeHtml(backend.identity?.workspaceRoot || backend.backendId)}</h3>
            ${reachableBadge(backend)}
          </div>
          <div class="metrics-row">
            <span class="badge badge-info">Backend PID ${escapeHtml(backend.processId)}</span>
            <span class="badge badge-info">Sessions ${escapeHtml(backend.activeSessions?.length ?? 0)}</span>
            <span class="badge badge-info">Operations ${escapeHtml(backend.activeOperations?.length ?? 0)}</span>
          </div>
          <dl>
            <dt>Backend Id</dt>
            <dd>${escapeHtml(backend.backendId)}</dd>
            <dt>Base URL</dt>
            <dd>${escapeHtml(backend.baseUrl || "n/a")}</dd>
            <dt>Settings</dt>
            <dd>${escapeHtml(backend.identity?.settingsPath || "n/a")}</dd>
            <dt>Manager</dt>
            <dd>${managerLink || "n/a"}</dd>
          </dl>
          ${backend.unavailableReason ? `<div class="badge badge-bad">${escapeHtml(backend.unavailableReason)}</div>` : ""}
          ${controlButtons}
          <div class="stack">
            <div>
              <strong>Sessions</strong>
              ${renderSessions(backend)}
            </div>
            <div>
              <strong>Operations</strong>
              ${renderOperations(backend)}
            </div>
          </div>`;
        host.appendChild(card);
      });
    }

    function apply(status) {
      setText("backend-id", status.backendId);
      setText("backend-pid", status.processId);
      setText("backend-count", status.liveBackendCount ?? status.backends?.length ?? 0);
      setText("session-count", status.totalActiveSessionCount ?? status.activeSessions?.length ?? 0);
      setText("operation-count", status.totalActiveOperationCount ?? status.activeOperations?.length ?? 0);
      setText("base-url", status.baseUrl);
      setText("workspace-root", status.identity?.workspaceRoot);
      setText("settings-path", status.identity?.settingsPath);
      setText("version-marker", status.identity?.binaryVersionMarker);
      setText("status-json", JSON.stringify(status, null, 2));
      renderBackends(status);
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

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
