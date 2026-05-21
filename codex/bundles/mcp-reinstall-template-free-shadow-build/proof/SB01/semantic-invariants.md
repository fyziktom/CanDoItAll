# SB01 Semantic Invariants

- Invariant ID: MCP-INSTALL-TEMPLATE-FREE
- Source raw note: NOTE-001 through NOTE-007 require keeping `Templates`, excluding them from MCP install needs, preserving skill setup, using standard Release build output before artifact copy, and validating the full reinstall path.
- Expected behavior: `tools\Reinstall-CanDoItAllMcps.ps1` builds and installs MCP servers without copying repository templates into current MCP artifacts, while still syncing repo-managed skills.
- Disallowed shallow implementation: Shortening the shadow hash without disabling template copying, or passing a targeted DotNetWatch build without validating the full reinstall script.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-current-state.txt` exits non-zero and shows `Directory.Build.targets` copying `Templates\Agents` into a too-long shadow path.
- Passing test: `bundle://proof/SB01/transcripts/reinstall-pass.txt` exits zero for the full reinstall script, and `bundle://proof/SB01/transcripts/artifact-scan.txt` exits zero with no copied `Templates` directories in current MCP artifacts.
- Changed source files: `repo://Directory.Build.targets`, `repo://tools/Reinstall-CanDoItAllMcps.ps1`, and `repo://tools/CanDoItAll.Mcp.DotNetWatch/Start-CanDoItAllDotNetWatchMcp.ps1`.
- Production assertions: `bundle://proof/SB01/source-assertions.md` documents the template opt-out, standard build plus artifact copy, and skill sync preservation.
- Red-team negative case: A hash-shortening-only fix is rejected by `bundle://proof/SB01/transcripts/failing-first-current-state.txt` because the reproduced failure is caused by template copying, and by `bundle://proof/SB01/transcripts/artifact-scan.txt` because artifacts are scanned for copied templates.
- Downstream dependency check: `bundle://proof/SB01/transcripts/reinstall-pass.txt` covers DotNetWatch, Components, CodeAnalytics, SshOps, Manager, Tray, config updates, manifest writing, shortcuts, and skills sync.
