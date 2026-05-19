# 01-validation-host-and-static-assets

## Status

- `Ready`

## Objective

Harden local validation startup so static assets, Blazor hydration, and production-like hosting failures are explicit and testable.

## Required Edits

- Add startup diagnostics for missing static web assets.
- Document supported validation startup modes.
- Add a smoke test or scripted check for `_framework/blazor.web.js`.

## Closure Proof

- Development startup returns 200 for `_framework/blazor.web.js`.
- Production-like startup either succeeds or reports a precise configuration error.
- Evidence is captured under the validation bundle proof directory.
