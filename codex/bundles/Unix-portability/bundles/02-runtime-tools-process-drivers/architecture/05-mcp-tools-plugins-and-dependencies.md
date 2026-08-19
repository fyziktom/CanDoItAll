# MCP, tools, plugins, and external dependencies

## MCP identity

1. Parse/validate descriptor.
2. Resolve executable through the canonical resolver.
3. Validate resolved identity against the capability-owned command policy.
4. Resolve working directory through workspace authority.
5. Resolve approved environment and secret references.
6. Launch through authoritative process host/registry.
7. Produce bounded/redacted lifecycle and tool receipts.

## Playwright MCP

Production selection uses a controlled application tool root:

- pinned package/runtime version;
- atomic install/update;
- integrity/source evidence;
- versioned directory;
- explicit executable/module path;
- no “newest file in global npx cache” authority.

Global cache may be reported diagnostically but not selected without explicit development policy.

## External tools

The external JSON tool path must not own another general process implementation. It may adapt:

- stdin JSON;
- output schema validation;
- capability-specific exit interpretation.

Timeout, cancellation, tree cleanup, environment, output limits, and redaction come from the shared primitive.

## Docker

Separate capability states:

- CLI missing;
- daemon/socket unavailable;
- denied permissions;
- context/config invalid;
- remote endpoint configured;
- recipe unsupported;
- image/network operation failure.

Inject process/path/env/registry dependencies. Do not copy full host environment.

## FileTools and desktop

Because FileTools is an external package:

- pin exact version;
- test every claimed OS/profile;
- distinguish open/reveal/preferred-app behavior;
- prove path/link safety and headless state;
- quarantine/disable unverified profiles;
- keep package-source work separate when necessary.

## Dependency ledger

No external/native capability is “supported” without:

- version/source;
- profile/OS/architecture;
- probe;
- security boundary;
- failure state/remediation;
- actual-host evidence;
- rollback/disable behavior.
