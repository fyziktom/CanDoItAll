# Implementation wave map

## Wave 0 — Evidence truth

SB00 synchronizes source and proves whether the existing 19 red tests belong to the feature.

## Wave 1 — Canonical persistence

SB01 removes duplicated writable truth and fake transaction composition.

## Wave 2 — Durable operation correctness

SB02 and SB03 make transition/recovery/cancellation/profile behavior deterministic and atomic.

## Wave 3 — Execution lifecycle

SB04 detaches paid inference from request lifetime and adds distributed claim/cancel semantics.
SB05 makes reads sustainable.

## Wave 4 — Backend checkpoint

SB06 proves the non-streaming backend before streaming changes any protocol.

## Wave 5 — True streaming

SB07 adds provider-neutral updates and concrete OpenAI/Azure/Ollama wire support.
SB08 persists/coalesces operation events.

## Wave 6 — External transport

SB09 exposes 202 admission and SSE. SB10 locks security and stable external-client semantics.

## Wave 7 — Proof and release

SB11 proves behavior, SB12 closes documentation/guards, and SB13 performs the sole broad gate and CI
matrix.
