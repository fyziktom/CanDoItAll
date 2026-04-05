# Plugin Platform Direction

## Problem

The current codebase models providers/resources mostly as static enums plus switches and hardcoded DI registrations.
That is fine for a small closed set, but it is the wrong shape for the next integration wave.

## Target Direction

Introduce a connector/plugin platform with:

- manifest/descriptor registration
- versioned configuration schemas
- secret requirements
- health/test lifecycle
- capability and permission exposure
- optional Workbench node/facet integration points
- optional MCP/agent exposure descriptors

## Minimum Plugin Contract

Each plugin should be able to answer:

- Who am I? (`pluginKey`, version)
- What do I need? (config schema, secrets, external scopes)
- What can I do? (read/write/send/sync/import/etc.)
- How is health checked? (test endpoint / ping / auth verification)
- How do agents see me? (allowed actions, approval requirements, data-scope policy)
- How do I bind into the project model? (resource, node facet, activity, imported artifact, etc.)

## Why this must happen before email/LinkedIn/custom APIs

Without this layer, each new integration will encourage more enums, more switches, more metadata shortcuts, and more hidden canonical state.
