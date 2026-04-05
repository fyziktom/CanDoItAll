# P7-006 - Workbench metadata still carries foreign identifiers and keeps dual marker truth

- Severity: High
- Gate: Hard blocker
- Status: Open
- Repeated from: PW6-006

## Problem

Metadata envelopes still contain cross-module ids such as participant, artifact, provider, resource, secret, and storage references. Markers are also represented both through legacy columns and metadata marker sets. This invites hidden canonical truth to leak back into metadata again.

## Required direction

Keep descriptive node-local payload in metadata only. Move foreign ids and reusable bindings to explicit canonical tables. Keep X/Y and markers canonical, but collapse markers to one canonical representation instead of legacy columns plus metadata fallback.

## Closure proof

Foreign-id helper fields are removed from metadata envelopes or clearly moved into binding tables; marker storage has one canonical representation only; guardrail tests cover both constraints.
