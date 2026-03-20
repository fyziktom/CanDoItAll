# Risk register

## 1. Score merge quality risk
**Risk:** MusicXML-only merge is too weak.  
**Mitigation:** store canonical score JSON with stable ids now; keep MusicXML as source/export bridge.

## 2. API/controller sprawl
**Risk:** `src/api/v1/index.php` becomes unmaintainable.  
**Mitigation:** extract repository logic into shared libs and keep only dispatch wiring in the front controller.

## 3. Public fork legal risk
**Risk:** users fork copyrighted sellable content and try to resell it.  
**Mitigation:** fork policy + derivative-rights gate + default listing block on non-owner forks.

## 4. Read model drift
**Risk:** repository default branch and entity read model diverge.  
**Mitigation:** add verification tool and explicit refresh-on-main-update bridge.

## 5. Storage growth
**Risk:** content-addressed blobs accumulate unreachable data.  
**Mitigation:** add GC tool later; v1 can defer deletion while tracking reachability.

## 6. Browser storage limits
**Risk:** very large offline clones exceed browser storage budget.  
**Mitigation:** lazy fetch blobs, use IndexedDB/OPFS, keep localStorage tiny.

## 7. Branch confusion in PHP UI
**Risk:** users assume switching branch changes public content immediately.  
**Mitigation:** strong branch status badges and clear label that only default branch updates the public/current read model.

## 8. Backfill gaps for events
**Risk:** no historical event snapshots exist.  
**Mitigation:** create initial current-state commit and document that older history is unavailable.

## 9. Marketplace pinning bug
**Risk:** purchases still resolve “latest” content.  
**Mitigation:** add exact commit-hash columns and use them for content delivery.

## 10. Merge request staleness
**Risk:** MR is evaluated against old target tip.  
**Mitigation:** store source/target head hashes and revalidate mergeability before merge.

## 11. Offline commit hash mismatch
**Risk:** PHP and C# canonicalization differ.  
**Mitigation:** one documented canonicalization contract + unit tests in both languages.

## 12. Force-push/rewriting history
**Risk:** users destroy branch history.  
**Mitigation:** no force push in v1, protected default branches, append-only commits.
