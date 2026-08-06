#!/usr/bin/env python3
"""Report C# project references and detect direct project-reference cycles."""

from __future__ import annotations

import argparse
import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=Path, required=True)
    parser.add_argument("--json-out", type=Path)
    return parser.parse_args()


def load_graph(root: Path) -> tuple[dict[str, Path], dict[str, list[str]]]:
    projects: dict[str, Path] = {}
    for path in root.rglob("*.csproj"):
        relative = path.relative_to(root).as_posix()
        projects[relative.casefold()] = path

    graph: dict[str, list[str]] = {}
    for key, project in projects.items():
        references: list[str] = []
        try:
            xml_root = ET.parse(project).getroot()
        except (ET.ParseError, OSError) as exc:
            raise RuntimeError(f"Cannot parse {project}: {exc}") from exc

        for element in xml_root.iter():
            if not element.tag.endswith("ProjectReference"):
                continue
            include = element.attrib.get("Include")
            if not include:
                continue
            normalized_include = include.replace("\\", "/")
            target = (project.parent / normalized_include).resolve()
            try:
                target_relative = target.relative_to(root).as_posix()
            except ValueError:
                target_relative = str(target)
            references.append(target_relative)
        graph[project.relative_to(root).as_posix()] = sorted(set(references))

    return {path.relative_to(root).as_posix(): path for path in projects.values()}, graph


def find_cycles(graph: dict[str, list[str]]) -> list[list[str]]:
    canonical = {key.casefold(): key for key in graph}
    adjacency: dict[str, list[str]] = {}
    for source, targets in graph.items():
        adjacency[source] = [canonical[target.casefold()] for target in targets if target.casefold() in canonical]

    state: dict[str, int] = {}
    stack: list[str] = []
    cycles: list[list[str]] = []
    seen_cycles: set[tuple[str, ...]] = set()

    def visit(node: str) -> None:
        state[node] = 1
        stack.append(node)
        for target in adjacency.get(node, []):
            if state.get(target, 0) == 0:
                visit(target)
            elif state.get(target) == 1:
                start = stack.index(target)
                cycle = stack[start:] + [target]
                normalized = tuple(sorted(cycle[:-1]))
                if normalized not in seen_cycles:
                    seen_cycles.add(normalized)
                    cycles.append(cycle)
        stack.pop()
        state[node] = 2

    for node in sorted(adjacency):
        if state.get(node, 0) == 0:
            visit(node)
    return cycles


def main() -> int:
    args = parse_args()
    root = args.repo_root.resolve()
    _, graph = load_graph(root)
    cycles = find_cycles(graph)

    report = {"repositoryRoot": str(root), "projects": graph, "cycles": cycles}
    if args.json_out:
        args.json_out.parent.mkdir(parents=True, exist_ok=True)
        args.json_out.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")

    print("# Project references")
    for project in sorted(graph):
        print(f"\n## {project}")
        if graph[project]:
            for target in graph[project]:
                print(f"- {target}")
        else:
            print("- (none)")

    print("\n# Cycles")
    if cycles:
        for cycle in cycles:
            print("- " + " -> ".join(cycle))
        return 1

    print("- No direct ProjectReference cycle found.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
