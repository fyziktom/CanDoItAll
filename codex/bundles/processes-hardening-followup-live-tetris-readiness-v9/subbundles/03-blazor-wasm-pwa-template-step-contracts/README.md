# SB03: 03-blazor-wasm-pwa-template-step-contracts

## Goal

Harden the Blazor app delivery template for live WASM PWA app creation.

## Work items

- Audit all Blazor-related templates and ensure step contracts are narrow and correct.
- Add explicit WASM PWA acceptance fields to the delivery contract and validation expectations.
- Ensure implementation owns code mutation; validation/revalidation cannot mutate; repair owns post-validation mutation; writeback is external-action controlled.
- Ensure all required artifact inputs/outputs are connected so each downstream step receives exactly the evidence it needs.
- Add red-team tests: architect attempts implementation; QA attempts mutation; writeback attempts product source edit.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- A note explaining how this improves readiness for the real UI-driven Blazor WASM PWA Tetris test.
- A note explaining how generic process behavior remains protected.

## Closure criteria

This subbundle is complete only when its proof manifest is updated and the next subbundle can rely on the result.
