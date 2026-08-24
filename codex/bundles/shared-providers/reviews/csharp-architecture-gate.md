# C# architecture gate

Initial result: `NOT_EXECUTED`

Complete at every architecture checkpoint and finally in SB12.

## Gate questions

### Ownership

- Are publication/source/import/audit entities owned by Workspace?
- Are public protocol records independent of Workspace EF and MAF models?
- Are provider-specific HTTP details isolated in Integration implementation?
- Are Web endpoints thin?
- Is local tool/workflow execution still local?

### Dependency direction

- Is Abstractions free of Web/UI/EF/SDK dependencies?
- Does Workspace reference only Abstractions?
- Is concrete Http wiring in Composition/Web?
- Do inner MAF projects have no new outer reference?
- Are there zero cycles by CodeAnalytics/project graph?

### Canonical model

- Is Workspace provider data still master?
- Is publication a separate explicit public projection?
- Is source credential stored once?
- Does import preserve one stable local provider ID?
- Is availability distinct from enabled intent?
- Is invocation record the one relay usage source?

### Pattern selection

- Is upstream dispatch adapter/registry driven?
- Are compatibility fields policy driven?
- Is routing ID handled by one codec/index?
- Is sync one deterministic reconciliation service?
- Is legacy execution thin or explicitly excluded?

### Testability

- Can each policy be directly unit tested?
- Are real PostgreSQL/API/streaming/three-instance seams present?
- Are negative tests meaningful?
- Are test filters/discovery recorded?
- Is broad gate count within budget?

### Partial class policy

- Were cohesive top-level files added?
- Did `WorkspaceModels.cs` shrink/remain stable rather than absorb the feature?
- Did runtime partials avoid new provider-specific behavior?

## Required evidence

- before/after ProjectReference tables;
- CodeAnalytics snapshot IDs and no-cycle output;
- changed namespace/type dependency report;
- direct project builds;
- architecture guardrail tests;
- review of every new public type;
- explanation for every exception.

## Final decision

- Result: `NOT_EXECUTED`
- Evidence:
- Repairs:
- Downstream work:
