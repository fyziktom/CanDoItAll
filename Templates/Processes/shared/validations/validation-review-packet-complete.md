# Review packet complete

**Key:** `validation-review-packet-complete`  
**Scope:** shared  
**Process:** shared  
**Owner role key:** `review-lead`  
**Gate:** Review router  
**Failure severity:** Error

## Summary
Blocks route selection when the pull request packet omits key evidence or changed-surface context.

## Pass criteria
The pull request packet names changed surfaces, proof, rollback notes, and reviewer asks clearly enough to choose a governed lane.

## Fail criteria
Any route would rely on guesswork because the packet omits changed-surface, proof, rollback, or reviewer-context data.

## Escalation rule
Return the workflow to the author and do not normalize the packet implicitly.
