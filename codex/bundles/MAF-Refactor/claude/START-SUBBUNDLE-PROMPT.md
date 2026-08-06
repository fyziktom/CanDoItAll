# Start a bundle subbundle in Claude Code

Use this short prompt from the repository root after choosing one unlocked subbundle:

```text
Execute the subbundle at <bundle-path>/subbundles/<subbundle-id>.

Read its CLAUDE-CODE-PROMPT.md and README completely, plus only the referenced root architecture/ADR/plan files. Verify current branch evidence with CodeAnalytics MCP and exact source/.csproj inspection. Implement and test the subbundle; do not return only a plan. Maintain proof/proof-manifest.json and proof/SESSION-HANDOFF.md throughout. Do not cross a checkpoint or commit/push unless explicitly requested.
```

Replace both placeholders with actual paths. For a fallback model/session, first read the existing handoff and verify it against the working tree.
