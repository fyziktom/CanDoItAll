# Generic corrective subbundle template

## Status
- Template

## Purpose
- Use this template when a gate fails and no more specific corrective playbook fits. The corrective work must restore the broken invariant first and only then return to the numbered queue.

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
