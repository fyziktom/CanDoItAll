# SB04 semantic invariants

## Invariant protected

PostgreSQL batch claiming should be paired with real bounded concurrency and validated runtime settings.

## Producer/consumer lifecycle

Options bind from configuration and are consumed by runtime workers and direct processing calls. Startup validation rejects impossible values.

## Positive proof

Focused options tests and source audits show the configured values bind and the worker defaults are greater than one where intended.

## Adversarial negative proof

Range attributes and startup validation prevent zero, negative, or unbounded parallelism values from entering runtime services.

## Anti-stub proof

The proof reads actual `IOptions<T>` values and appsettings, not only constants.
