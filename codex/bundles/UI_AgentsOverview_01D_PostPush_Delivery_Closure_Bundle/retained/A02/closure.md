# A02 encoding closure

The exact display-name regression failed for Overview and Governance before the correction and both cases passed after it. Production now emits `Agents · Overview` and `Agents · Governance`. Only the unintended extra code point was removed; intended Unicode remains.

The entry audit found 12 findings in four files. Three Overview documents and the chat context builder were corrected with before/after SHA-256 receipts in [corrections.json](corrections.json). Original document bytes remain compressed, explicitly historical. No binary or compressed historical artifact was rewritten.

The fresh bounded scan passed 3,812 primary text files and all five changed Components text files at the A06 encoding checkpoint. The scope includes every changed predecessor file, current production source/resources, the complete Overview and Governance bundles, this follow-up and new validators. Final resealing repeats the scan for subsequent documentation. The scanner accepts localized Unicode and checks strict UTF-8, replacement/C1/NUL, multi-character mojibake and BOM changes. All 16 delivery/encoding self-tests pass after the later Git clean-filter regression; no source encoding rule was relaxed.
