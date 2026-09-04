# Observed source baseline

## Branch state at preparation

| Ref | Observed SHA | Meaning |
|---|---|---|
| `components-decoupling` | `c225bf2445835bf12fa5054bc15571d2ce23b4fe` | Target branch containing the shared architecture bundle |
| `development` | `d446dc2bad461c7e753cceb53a7969d6ff6b9cb2` | Current development base after the independent test repair |

At observation time, `components-decoupling` was two commits ahead of and zero commits
behind `development`.

These SHAs are evidence, not execution pins. SB01 must fetch and re-evaluate both refs.

## Important current source facts

- `AgentsHomePage` directly injects eight services, including
  `IDbContextFactory<AppDbContext>`, and aggregates overview, usage, HR-agent lookup,
  avatars, and bound-resource count in Razor code-behind.
- `AgentCatalogPanel` injects six services and owns catalog loading/repair, selection,
  dialog orchestration, team mutations, and managed-chat launch.
- `AgentDetailsDialog` injects seven services, directly coordinates Workspace, provider,
  Projects, Secrets, infrastructure alias handling, save/delete, and ten editor sections.
- target component baseline discovery is 46 cases: 6 home, 10 catalog, and 30 details.
- existing route-state baseline discovery is 10 cases.
