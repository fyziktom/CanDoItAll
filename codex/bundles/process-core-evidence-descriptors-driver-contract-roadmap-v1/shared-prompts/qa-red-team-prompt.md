# QA / Red-Team Prompt

Review whether the bundle stayed narrow:
- Did Core receive only pure descriptors/rules?
- Did any side-effectful behavior move into Core?
- Did production driver API creep into source?
- Did adapters preserve current runtime behavior?
- Did public Core API snapshot change intentionally?
- Did build/test/source scan proof actually run?
- Are warning cleanup decisions explicit?
- Are domain driver lanes read-only and side-effect denied?

Reject shallow passes where markdown claims safety but production source or tests do not prove it.
