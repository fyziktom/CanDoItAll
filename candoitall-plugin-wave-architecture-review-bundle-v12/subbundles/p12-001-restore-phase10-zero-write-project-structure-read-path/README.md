# P12-001 — restore phase10 zero-write project structure read path

## Problem
The current upload reintroduces write-on-read behavior into `ProjectStructureAssemblyService.LoadAsync(...)` and removes the explicit repair boundary that previously existed.

## Why it matters
The Workbench canonical graph must stay stable under reads before any larger plugin runtime wave is added.

## Required outcome
Codex must fully implement this subbundle and supply the required evidence and tests.
