# Current State

## What Is Complete

The previous branch completed the first dependency inversion cut:

- The MAF adapter no longer directly references the Processes module through its project file.
- A neutral Tooling project exists for runtime tool-provider contracts.
- MAF composes registered runtime tool providers through DI.
- Processes owns and registers the process tool provider.
- The previous execution report says all SB01-SB09 gates passed.

## What Is Still Not Ready For Process-Core Extraction

The seam is new and should be hardened before splitting process core:

1. MAF still hard-codes first-party product tool attachment for project-structure and image generation.
2. Tool provider contracts do not yet expose enough metadata for future driver packs, manager verification, or evidence tracing.
3. Process provider is large and mixes catalog construction, access checks, tool methods, DTOs, and template import/read operations.
4. Provider context purpose is not yet used deeply enough by first-party providers.
5. Branch hygiene needs a merge-readiness check because the diff includes large `codex/bundles` churn unrelated to runtime source.

## Decision

Proceed with provider seam hardening and remaining MAF product-tool providerization. Do not start `CanDoItAll.Processes.Core` extraction in this bundle.
