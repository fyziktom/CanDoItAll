## Senior QA review

### Verdict
The refactor is materially better than the previous review point, but it is still not sufficient for the next large integration/plugin wave.

### Main QA concerns
- ownership is still ambiguous where node core meets binding/reference/facet data
- hierarchy duplication is still live in persisted mutation paths
- capability rules are not yet owned by one canonical registry
- the plugin platform is not yet first-class in the active editor and resolution flows
- future side-effecting connectors still lack a durable execution boundary

### QA sign-off
- Current branch sign-off for large plugin wave: `Rejected`
- Sign-off after phase8 hard gates pass and runtime validation is rerun: `Possible`
