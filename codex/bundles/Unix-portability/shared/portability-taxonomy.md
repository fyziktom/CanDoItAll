# Portability taxonomy

Use these categories in generated scans and code review.

| Category | Definition | Typical examples | Default owner |
|---|---|---|---|
| `logical-path` | Persisted application-relative identifier | storage locator, managed file route, artifact path | Core path contract |
| `physical-path` | Native host filesystem address | workspace root, executable path, control-plane root | Infrastructure or owning module |
| `uri-route` | URI or HTTP route | `/storage/...`, `https://...` | Web/integration owner |
| `host-bound-record` | Persisted physical path valid only on a host/platform | repository local path, preferred app | Control plane/domain record owner |
| `filesystem-semantics` | Case, links, atomicity, mode, enumeration, watchers | APFS case mode, symlink, FileSystemWatcher | Infrastructure/Manager |
| `secret-provider` | Secure secret persistence/resolution | DPAPI, Keychain, Secret Service | Security |
| `key-bootstrap` | Material needed before protected secrets can be opened | DP key-ring protector, certificate | Infrastructure composition |
| `process-plan` | Typed executable/argv/env intent | dotnet build, python interpreter | Runtime-node/tool owner |
| `process-execution` | Start/cancel/kill/output/lifecycle | LocalWorkspaceProcessHost | AgentFramework Core |
| `terminal-presentation` | Optional visible interactive shell/terminal | Windows Terminal, macOS Terminal | Workbench |
| `process-discovery` | Recovery observation of existing OS processes | WMI, `/proc`, macOS adapter | Manager |
| `mcp-runtime` | Local stdio MCP setup/process/env | Playwright MCP | MCP integration |
| `external-dependency` | Native/package/service outside repository | FileTools, Docker, node/npm | Owning integration |
| `process-domain` | Process strategy, recovery, evidence, escalation | process drivers | Processes |
| `hosting` | Publish, service, roots, health, logging | systemd, launchd | App/operations |
| `test-evidence` | Actual-host proof and support claim | CI matrix, restart test | Owning bundle |

## Classification rules

- A string is not a path merely because it contains `/` or `\`.
- Logical path compatibility may translate legacy `\`; physical paths must not be globally translated.
- A foreign absolute path is host-bound/unresolved, never an implicit relative path.
- OS identity is not a sufficient proxy for volume case sensitivity.
- “Unix” is acceptable for shared POSIX behavior, but Linux and macOS retain separate native service, desktop, process-discovery, and filesystem evidence.
- A capability may be present, absent, unsupported, misconfigured, unavailable, or unverified. Do not collapse these states.
