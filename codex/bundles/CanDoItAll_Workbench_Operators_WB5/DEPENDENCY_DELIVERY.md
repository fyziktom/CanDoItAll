# Source pair and delivery

The remotely inspected pair is main `42cd807dec761daa79fa8b48ff78c2330f615354` and Components `a3fd4d22f2c4e0432cf389f194c6468b44ab7371`. The latter
includes the WB3 composer/minimap work and WB4 CanvasOverlayDialog. The report's
"verified locally only" sentence is historical; this review observed the required
Components ref remotely. There is no remaining request to create or push that same
repair. [S01, S03, S30]

At entry record actual main/Components/FileTools HEADs, branches, index/working-tree
changes, signatures and evaluated restore inputs. The review SHAs are not execution
pins. Do not reset, force checkout, cherry-pick duplicate repairs or discard user work.
FileTools equivalence is reported by WB4; verify the actual checkout/build inputs rather
than infer equivalence from matching version labels. [S02]

CI currently checks out the Components branch matching its target and records that SHA
for later jobs. It does not consume every arbitrary feature-branch commit automatically.
A feature branch push alone is not proof of a CI build. Keep the dependency lane intact;
record the intended downstream branch/ref and actual asset hashes. [S28]

If WB5 needs a shared primitive fix, change the real sibling owner and test its positive
and negative consumers. Never copy it locally to avoid the dependency. Make the signed
sibling checkpoint and main checkpoint reproducible; source, package version, loaded
assembly, manifest and served bytes have distinct evidence. Push/merge is not authorized
by this bundle. A genuinely unavailable required sibling must be reported accurately;
local verification and remote delivery must not be conflated.
