# P11-005 — add plugin ingress inbox, cursors, deduplication, and explicit materialization

## Problem
Plugins that watch email, WhatsApp, webhooks, or polling-based sources need a generic ingress boundary before they create project artifacts.

## Why it matters
This is a platform-level requirement for the next plugin wave.
Without it, each plugin will tend to invent its own orchestration mechanics and the platform will fragment.

## Required outcome
Codex must fully implement this subbundle and supply the required evidence and tests.
