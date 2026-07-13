# Structured Input

## Problem

Agents cannot reliably convert PDF/document project assets because `workspace_convert_document` delegates to Python MarkItDown and the runtime environment does not have that Python package available.

## User Preference

Use C#/.NET where possible and place document tool implementations under `CanDoItAll.Tools.Documents`.

## Architectural Direction

Use a Core abstraction and a Tools.Documents implementation. The MAF runtime should consume the composed service only.

## E2E Target

- App URL: `http://localhost:5032`
- Project: `f28c07cd-982c-4d2d-bcf2-3e60a32eca72`
- Scenario: project-structure floating agent chat uses the PDF quotation asset and calls `workspace_convert_document`.

## Permission Constraint

The Financial Strategist seed is read-only for project-structure mutations. Node creation is outside this bundle unless explicitly changed by a separate permission decision.

