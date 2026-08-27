# Structured Input

## Core Objective

Make source and imported providers faithful to their real upstream inventory and prices.

## Success Criteria

Ollama shows only installed upstream models. OpenAI offers real available IDs, not test
names. Client names, full membership, prices and private flag equal the source.

## Hard Constraints

Preserve unrelated dirty work, source/import/publication IDs, histories, volumes, secrets,
and 5032. No model aliases in UI. No fabricated rates or silent fallback. No new layers.

## Allowed Side Effects

Repair scoped source code/tests and configure existing 5210/5212 test profiles through UI.
Replace only known test-instance settings after retaining recoverable configuration.
Live provider calls are in scope; keep bounded and never log credentials.

## Source Artifacts

See 01-source-artifacts.md and literal 00-original-request.md.

## Input Coverage Signals

N001 polluted Ollama; N002 fake OpenAI models/prices; N003 exact client mirror;
N004 new bundle and real two-instance validation (including chat, agent, image, vision, usage).

## Dependency And Sequencing Signals

SB01 catalog authority is a foundation for SB02. Ollama connectivity blocks Ollama proof,
not local repair or OpenAI work.

## Validation Expectations

Focused tests, 1920x1080 UI setup on both apps, real nondefault selections/execution,
source usage evidence, negative refresh behavior, screenshots inspected.

## Evidence Contract

Governed artifacts under proof/SB01 and proof/SB02: transcripts, source hashes,
semantic invariants and assertions. Fixture tests prove mechanisms only, not live readiness.
