# SVG authoring

Use this skill whenever the requested deliverable includes an SVG file or an SVG-backed project asset.

## Authoring contract

- Treat SVG as XML, not as tolerant HTML. Use one balanced `<svg>` document root with `xmlns="http://www.w3.org/2000/svg"`, quote every attribute, close every element, and keep IDs unique.
- Escape all dynamic text and labels before placing them in XML. In text and attribute values, write `&` as `&amp;` and `<` as `&lt;`; escape `>` as `&gt;` when it could close markup. In attributes, also escape the quote character used to delimit that attribute.
- Prefer SVG-native shapes, paths, text, groups, transforms, and styles. Do not add scripts, event-handler attributes, external resource references, or `foreignObject` content.
- Check every `url(#id)`, `href="#id"`, clip path, mask, marker, gradient, and filter reference against a declared unique ID.
- Keep each requested variant in its own file unless the user explicitly asks for a combined sheet.

## Required verification

1. Review the complete serialized SVG for raw ampersands, unbalanced tags, unquoted attributes, duplicate IDs, and broken ID references before registering it.
2. Create the project asset with `project_structure_asset_create`. The platform parses SVG XML and returns an actionable line and position when the document is malformed. Correct the source file and retry; do not bypass or relabel invalid SVG as another content type.
3. Read the created asset and its content back with `project_structure_asset_get` and `project_structure_asset_content_get`. Do not claim an SVG deliverable is complete until both the write and readback succeed.
4. If a raster rendition is requested, use the image-generation tool with an explicitly configured image provider. Store the resulting PNG or WebP as a separate asset; never claim that an image was generated when the tool failed or no binary file was produced.
