# SVG assets

These SVGs are **font-based**: they render SMuFL glyphs using the **Bravura** font via `<text>` elements.

Pros:
- Exact shapes (when Bravura is available)
- Small file size

Cons:
- Requires Bravura font available in the environment that renders the SVG.
- If you need outline paths instead, use a font-to-SVG-path generator (e.g., fonttools + svgo).

Files with `*_guess.svg` use SMuFL standard codepoints that are expected to match Bravura.
Codex should verify the codepoints against Bravura metadata or by rendering.
