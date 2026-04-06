# P12-007 — add plugin ingress inbox, cursors, deduplication, and explicit materialization

## Problem
The repo still lacks a generic ingress boundary for external sources such as email, WhatsApp, webhooks, and pollers.

## Why it matters
Without a shared ingress boundary, each plugin would invent its own dedupe/cursor/materialization rules.

## Required outcome
Codex must fully implement this subbundle and supply the required evidence and tests.
