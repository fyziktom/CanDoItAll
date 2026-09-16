# Engineering language policy

The entire normative package is English. Downstream Codex/Astra implementation bundles, plans, architecture decisions, instructions, code comments, test explanations, reviews, and completion reports must be English unless a later explicit user requirement overrides it.

Do not infer the language of engineering deliverables from the user's conversational language. All source requirements included here are English translations. The old instruction to write package documentation in Czech is superseded and is not included as an operative instruction.

Preserve existing identifiers, serialized symbols, compatibility names, proper nouns, and explicitly required localized product strings. Translating engineering prose does not authorize translating UI resources, changing API names, or altering user data. Original external input filenames and historical source paths may remain unchanged as provenance metadata; they are not writing-language examples.

No untranslated previous archive, hidden Czech instruction, or Czech implementation subbundle is included. The translation-coverage catalog maps all original files to this full replacement. The validator detects Czech diacritics and known instruction remnants; it is a practical check, not a mathematically complete natural-language classifier. Semantic review accompanies it.
