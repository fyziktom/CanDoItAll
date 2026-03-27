# Problem And Goals

## Problem

Bundle 2 restored the fast watch loop, but the operator still has weak visibility when something goes wrong outside the current MCP stdio lifetime.

Pain points:

- an agent can lose the backend in the middle of a long run
- duplicate backends can compete for files, ports, or perceived ownership
- the current backend manager page exists, but the operator has to know where it is and open it manually
- reinstalling the local Codex skill and MCP wiring across multiple PCs is still manual

## Goals

- keep a visible operator presence on Windows through a tray icon
- expose current backend state and quick recovery actions without requiring a live Codex session
- make duplicate or unreachable backend states obvious
- let the operator open the richer backend manager webpage from the tray
- make cross-PC resetup simple and repeatable

## Non-goals

- replacing the backend HTTP manager page
- changing bundle-2 hot-reload semantics
- rewriting the detached backend architecture from scratch in this bundle
