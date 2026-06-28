# Shared Implementation Prompt

```text
Implement only the assigned subbundle from codex/bundles/skill-tool-mcp-isolation-template-migration.

Before editing, read README.md, plan/01-phase-plan.md, analysis/01-current-state.md, analysis/03-codeanalytics-and-performance-review.md, architecture/02-reconnection-map.md, architecture/03-error-and-diagnostics-model.md, architecture/04-implementation-quality-guardrails.md, architecture/05-capability-access-policy.md, requirements/02-naming-and-compatibility-standards.md, and the assigned subbundle README.

Respect the migration order. Do not reconnect MAF before the required abstraction, implementation, SB05 hardening, template-loader, SB07 seed hardening, and setup-test proof exists. Preserve existing capability keys and runtime tool names unless the subbundle explicitly documents a versioned compatibility alias.

Use strongly typed C# contracts and explicit validation failures. Capability restrictions must flow through the shared access policy/effective-set model, not raw selector strings or hidden MAF filters. External tool and MCP failures must expose structured categories, masked bounded detail, correlation IDs, and repair hints. Do not add silent fallbacks to hardcoded defaults when templates fail. Keep project folders grouped by capability domain. Capture unit, integration, component, and e2e proof requested by the subbundle, then update reviews/01-execution-report.md and proof/SBxx/manifest.md.
```
