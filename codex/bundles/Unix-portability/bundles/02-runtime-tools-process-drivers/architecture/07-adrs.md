# Runtime architecture decision records

## ADR-R01 — Direct typed execution is primary

**Decision:** Ordinary runtime/tool commands execute from typed plans. Terminal text is display/presentation only.

**Rejected:** PowerShell/POSIX shell as universal transport.

## ADR-R02 — One low-level process primitive

**Decision:** Reuse/harden the existing workspace process host or an extracted lower primitive. Tools/plugins do not implement divergent local runners.

**Rejected:** Separate process code per MCP/tool/plugin.

## ADR-R03 — Registry-first process ownership

**Decision:** Persist launched-process identity. WMI/proc/macOS discovery is bounded recovery evidence.

**Rejected:** Name-only or command-substring termination.

## ADR-R04 — Environment and executable semantics are host-correct

**Decision:** Preserve environment key semantics and resolve/authorize executable identity deterministically.

**Rejected:** Global `OrdinalIgnoreCase` and universal `.exe/.cmd/.bat` candidates.

## ADR-R05 — Terminal and elevation are optional capabilities

**Decision:** Direct headless execution does not require a terminal; Unix/macOS elevation is unavailable by default.

**Rejected:** Automatic sudo/pkexec/osascript mapping.

## ADR-R06 — Controlled Playwright MCP tool root

**Decision:** Production MCP uses a pinned managed installation, not global npx cache discovery.

**Rejected:** Newest recursive cache match.

## ADR-R07 — External dependency claims are quarantinable

**Decision:** FileTools/Docker/native capabilities can be disabled independently and are supported only for tested profiles/versions.

**Rejected:** Inferring support from package metadata or executable presence.

## ADR-R08 — Processes owns semantics

**Decision:** Host capabilities feed process strategies, but Processes owns eligibility, recovery, evidence, escalation, and failure meaning.

**Rejected:** Generic MAF/Infrastructure platform service deciding process outcomes.
