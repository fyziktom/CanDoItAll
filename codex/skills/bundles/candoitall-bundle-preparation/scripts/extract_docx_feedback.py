#!/usr/bin/env python3

from __future__ import annotations

import argparse
import shutil
import textwrap
import zipfile
from pathlib import Path
from xml.etree import ElementTree

WORD_NAMESPACE = {"w": "http://schemas.openxmlformats.org/wordprocessingml/2006/main"}


def read_paragraphs(docx_path: Path) -> list[str]:
    with zipfile.ZipFile(docx_path) as archive:
        root = ElementTree.fromstring(archive.read("word/document.xml"))

    paragraphs: list[str] = []
    for paragraph in root.findall(".//w:p", WORD_NAMESPACE):
        text_nodes = [node.text for node in paragraph.findall(".//w:t", WORD_NAMESPACE) if node.text]
        text = "".join(text_nodes).strip()
        if text:
            paragraphs.append(text)

    return paragraphs


def extract_media(docx_path: Path, output_directory: Path) -> list[Path]:
    output_directory.mkdir(parents=True, exist_ok=True)

    written_files: list[Path] = []
    with zipfile.ZipFile(docx_path) as archive:
        for name in archive.namelist():
            if not name.startswith("word/media/"):
                continue

            target_path = output_directory / Path(name).name
            with archive.open(name) as source_stream:
                with target_path.open("wb") as target_stream:
                    shutil.copyfileobj(source_stream, target_stream)
            written_files.append(target_path)

    return written_files


def format_markdown(docx_path: Path, paragraphs: list[str], media_files: list[Path]) -> str:
    lines = [
        "# Extracted Feedback",
        "",
        f"Source: `{docx_path}`",
        "",
        "## Notes",
        "",
    ]

    for index, paragraph in enumerate(paragraphs, start=1):
        lines.append(f"- `N{index:03d}` {paragraph}")

    if media_files:
        lines.extend(
            [
                "",
                "## Extracted Media",
                "",
            ]
        )
        for media_file in media_files:
            lines.append(f"- `{media_file}`")

    return "\n".join(lines) + "\n"


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Extract text and embedded media from a feedback .docx into markdown.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=textwrap.dedent(
            """\
            Examples:
              python extract_docx_feedback.py Feedback2.docx --output-text extracted.md
              python extract_docx_feedback.py Feedback2.docx --output-text extracted.md --media-dir media
            """
        ),
    )
    parser.add_argument("source_docx", help="Path to the .docx file.")
    parser.add_argument("--output-text", required=True, help="Path to the markdown output file.")
    parser.add_argument("--media-dir", help="Optional directory for extracted embedded images.")
    return parser.parse_args()


def main() -> int:
    arguments = parse_arguments()
    docx_path = Path(arguments.source_docx).resolve()
    output_text_path = Path(arguments.output_text).resolve()

    paragraphs = read_paragraphs(docx_path)
    media_files: list[Path] = []

    if arguments.media_dir:
        media_directory = Path(arguments.media_dir).resolve()
        media_files = extract_media(docx_path, media_directory)

    output_text_path.parent.mkdir(parents=True, exist_ok=True)
    output_text_path.write_text(format_markdown(docx_path, paragraphs, media_files), encoding="utf-8")

    print(f"Wrote extracted markdown to {output_text_path}")
    if media_files:
        print(f"Extracted {len(media_files)} media file(s) to {media_files[0].parent}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
