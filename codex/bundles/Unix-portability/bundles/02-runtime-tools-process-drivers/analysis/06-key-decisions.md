# Runtime key decisions

1. Rebase only after Core C4.
2. Preserve and harden the existing direct typed workspace process host.
3. One low-level process primitive and one lifecycle/registry owner per runtime aggregate.
4. Environment names use host semantics; secret values are explicit late bindings.
5. Executable resolution and authorization operate on resolved identity, not display text.
6. Direct execution is primary; terminal presentation and elevation are separate optional capabilities.
7. Manager uses launched-process registry first and OS discovery only as bounded recovery evidence.
8. MCP, external tools, Docker, and plugins consume shared process/path/secret primitives.
9. FileTools support is a pinned compatibility claim, not an assumption.
10. Processes owns domain semantics; host capabilities do not grant authority.
11. Actual-host runtime E2E is required before R4.
12. Split again before implementation when B00 measured scope exceeds the declared triggers.
