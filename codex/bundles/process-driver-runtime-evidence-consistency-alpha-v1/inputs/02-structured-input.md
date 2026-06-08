# Structured Input

## Primary objective
Review and continue the completed `process-driver-alpha-consumer-evidence-pipeline-v1` work on branch `maf-processes-refactor`, taking into account that Codex crashed during work and that report-only proof is not enough.

## Required implementation posture
This bundle must be more comprehensive than a micro-step bundle, but it must remain safe. It may implement a second verification-only driver alpha and supporting read-only process adapters. It must not introduce a generic runtime driver system.

## Allowed next production direction
- Decompose the transcript verifier and process adapter into smaller policy/parser/audit/evidence components.
- Add a new verification-only runtime evidence consistency driver package.
- Add controlled read-only process-module adapters for already-produced Core descriptor payloads.
- Expand test harnesses, malicious corpora, documentation, and roadmap gates.

## Hard non-goals
- No broad Process Core runtime extraction.
- No generic driver registry, selector, host, provider runtime, DI registration, manager command, scheduler hook, workflow hook, or execution-capable driver.
- No shell execution, package restore, external connector calls, Office/Graph calls, workspace/storage writes, process mutation, claim mutation, transition mutation, finalizer application, provider repair, or retry scheduling.
- No UI, browser, small-screen, medium-screen, mobile, or screenshot proof. If UI/media files change unexpectedly, fail and re-scope.
