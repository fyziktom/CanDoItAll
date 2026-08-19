# Cross-bundle architecture invariants

1. Existing user data and Windows behavior are compatibility requirements.
2. Logical path syntax and physical host filesystem addresses are different concepts.
3. A foreign absolute path never becomes an implicit relative path.
4. Root containment cannot rely on case-insensitive strings on every OS.
5. Secure provider selection is truthful and fail-closed.
6. Key bootstrap is acyclic.
7. Headless core does not require desktop/terminal/interactive keyring capabilities.
8. One authoritative low-level process execution/lifecycle stack exists.
9. Workbench terminal presentation is not the execution source.
10. Manager never terminates by name-only evidence.
11. MCP/tools/plugins reuse path/process/secret primitives.
12. Host capabilities never grant authority.
13. Process semantics and recovery stay in `Processes`, not MAF.
14. Support claims are exact OS/profile/RID/dependency evidence, not generic “Unix” assumptions.
15. Every migration and external dependency has rollback/disable behavior.
