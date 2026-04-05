# P7-010 - There is still no hard architecture closure mechanism preventing the same blockers from being reintroduced

- Severity: Critical
- Gate: Hard blocker
- Status: Open
- Repeated from: PW6-012

## Problem

The same blockers were already called out in earlier bundles and are still present. Without explicit hard gates, forbidden-pattern checks, and dedicated architecture guardrail tests, Codex can keep improving local code while leaving the structural blockers alive.

## Required direction

Add architecture guardrail tests plus a repo-level hard-gate script. No bundle item may be closed by ADR-only justification; closure requires code search proof, required tests, and the hard-gate script passing.

## Closure proof

Architecture guardrail tests exist; a hard-gate script passes on the refactored branch; bundle closure requires the script output and test names as evidence.
