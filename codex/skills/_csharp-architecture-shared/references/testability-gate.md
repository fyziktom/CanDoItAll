# Testability Gate

A refactoring is not complete until the extracted responsibility can be tested without constructing the old large class.

## Required tests

For architecture-heavy C# refactoring, require at least:

1. Characterization test before moving risky behavior when existing behavior is not already covered.
2. Isolated unit test for each extracted service, strategy, builder, factory, adapter, provider, or handler.
3. Negative test proving the shallow implementation would fail.
4. Composition or integration smoke proving the old caller is wired to the new implementation.
5. Regression test proving a new tool/provider/driver can be added without editing the old runtime class, when the refactor is meant to create an extension seam.

## Good unit test properties

The test should be:

- fast
- isolated from filesystem, database, network, and external SDKs unless it is explicitly an integration test
- repeatable
- self-checking
- named by method, scenario, and expected behavior
- focused on behavior rather than implementation details

## Proof that separation is real

The architecture gate should reject the refactor if:

- tests still instantiate the original large runtime for the extracted behavior
- tests assert only non-null objects or row counts
- the new interface is implemented only by the original class
- the factory delegates back into the original class for all behavior
- the new project exists but contains only DTOs while logic remains in the old class
- the new tests require external provider credentials for unit-level behavior
