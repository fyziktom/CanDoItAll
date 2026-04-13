# Corrective proof reset

## Status
- Template

## Purpose
- Repair any mismatch between claimed execution status and the actual live proof artifacts before more architecture work proceeds.

## How to use
1. Copy this playbook into a dated corrective subbundle key if the gate log needs a concrete execution artifact.
2. Name the broken invariant explicitly.
3. Explain why downstream work must stop.
4. Limit the corrective work to restoring that invariant.
5. Rerun the failed gate before downstream execution resumes.

## Minimum sections
- Trigger
- Broken invariant
- Why downstream work must stop
- Corrective objective
- Required deliverables
- Required proof
- Exit rule
