"""
Generate design variants for the EmptyStateOverlay component using the OpenAI Images API.

Usage:
    python generate_design_variants.py
    python generate_design_variants.py --model dall-e-3 --size 1024x1024 --quality hd

Requirements:
    pip install openai

Environment:
    OPENAI_API_KEY must be set.
"""

from __future__ import annotations

import argparse
import base64
import os
from pathlib import Path

from openai import OpenAI

COMPONENT_NAME = "EmptyStateOverlay"
MODEL = "dall-e-3"
SIZE = "1024x1024"
QUALITY = "hd"
STYLE = "natural"
OUTPUT_DIR = Path(__file__).resolve().parent / "design_variants"
PROMPTS = [
    "Design a desktop web app UI concept for the `EmptyStateOverlay` component in CanDoItAll. The component purpose is: Render meaningful empty-state overlays when a canvas surface has no content or no valid projection. Show the component inside a realistic Blazor-style application workbench with a canvas-centric layout. Use a clean professional SaaS product style, high information clarity, restrained motion language. Prioritize usability, spatial clarity, strong affordances, clear states, and realistic product-ready details. Do not create a mobile layout. Focus on this component, not a generic dashboard.",
    "Design a desktop web app UI concept for the `EmptyStateOverlay` component in CanDoItAll. The component purpose is: Render meaningful empty-state overlays when a canvas surface has no content or no valid projection. Show the component inside a realistic Blazor-style application workbench with a canvas-centric layout. Use a modern SaaS workbench aesthetic, subtle gradients, polished depth, sharp hierarchy. Prioritize usability, spatial clarity, strong affordances, clear states, and realistic product-ready details. Do not create a mobile layout. Focus on this component, not a generic dashboard.",
    "Design a desktop web app UI concept for the `EmptyStateOverlay` component in CanDoItAll. The component purpose is: Render meaningful empty-state overlays when a canvas surface has no content or no valid projection. Show the component inside a realistic Blazor-style application workbench with a canvas-centric layout. Use a high-clarity productivity UI, dense but readable information design, precise spacing. Prioritize usability, spatial clarity, strong affordances, clear states, and realistic product-ready details. Do not create a mobile layout. Focus on this component, not a generic dashboard.",
    "Design a desktop web app UI concept for the `EmptyStateOverlay` component in CanDoItAll. The component purpose is: Render meaningful empty-state overlays when a canvas surface has no content or no valid projection. Show the component inside a realistic Blazor-style application workbench with a canvas-centric layout. Use a canvas-heavy advanced editor interface with overlays, guides, and spatial editing affordances. Prioritize usability, spatial clarity, strong affordances, clear states, and realistic product-ready details. Do not create a mobile layout. Focus on this component, not a generic dashboard.",
    "Design a desktop web app UI concept for the `EmptyStateOverlay` component in CanDoItAll. The component purpose is: Render meaningful empty-state overlays when a canvas surface has no content or no valid projection. Show the component inside a realistic Blazor-style application workbench with a canvas-centric layout. Use a subtle premium enterprise design, elegant contrast, refined materials, quiet sophistication. Prioritize usability, spatial clarity, strong affordances, clear states, and realistic product-ready details. Do not create a mobile layout. Focus on this component, not a generic dashboard."
]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=f"Generate design variants for {COMPONENT_NAME}.")
    parser.add_argument("--model", default=MODEL, help="Image model to use.")
    parser.add_argument("--size", default=SIZE, help="Image size, for example 1024x1024.")
    parser.add_argument("--quality", default=QUALITY, help="Image quality setting.")
    parser.add_argument("--style", default=STYLE, help="Image style setting.")
    parser.add_argument("--dry-run", action="store_true", help="Print prompts without calling the API.")
    return parser.parse_args()


def save_png(output_path: Path, b64_data: str) -> None:
    output_path.write_bytes(base64.b64decode(b64_data))


def main() -> None:
    args = parse_args()
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    if args.dry_run:
        for index, prompt in enumerate(PROMPTS, start=1):
            print(f"[{index}] {prompt}\n")
        return

    api_key = os.environ.get("OPENAI_API_KEY")
    if not api_key:
        raise RuntimeError("OPENAI_API_KEY is not set.")

    client = OpenAI(api_key=api_key)

    for index, prompt in enumerate(PROMPTS, start=1):
        print(f"Generating variant {index} for {COMPONENT_NAME}...")
        result = client.images.generate(
            model=args.model,
            prompt=prompt,
            size=args.size,
            quality=args.quality,
            style=args.style,
            response_format="b64_json",
        )
        image_data = result.data[0].b64_json
        output_path = OUTPUT_DIR / f"{index:02d}-{COMPONENT_NAME}.png"
        save_png(output_path, image_data)
        print(f"Saved {output_path}")


if __name__ == "__main__":
    main()
