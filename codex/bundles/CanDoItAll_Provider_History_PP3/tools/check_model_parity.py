#!/usr/bin/env python3
"""Check safe source/client snapshot consistency, not actual application execution."""
from __future__ import annotations
import argparse
import json
from pathlib import Path
import re
import sys
from uuid import UUID

sys.dont_write_bytecode = True
OID = re.compile(r"[0-9a-f]{40}")
IMAGE = re.compile(r"sha256:[0-9a-f]{64}")
ROUTE = re.compile(r"sp1\.([0-9a-f]{32})\.[A-Za-z0-9_-]{43}")
MAX_BYTES = 4 * 1024 * 1024

class InvalidSnapshot(ValueError):
    pass

def require(condition: bool, location: str) -> None:
    if not condition:
        raise InvalidSnapshot(location)

def fields(obj: object, names: set[str], location: str) -> dict:
    require(isinstance(obj, dict) and set(obj) == names, location + ": invalid fields")
    return obj

def text(value: object, location: str) -> str:
    require(isinstance(value, str) and bool(value.strip()), location + ": missing text")
    return value

def uid(value: object, location: str) -> str:
    token = text(value, location)
    try:
        parsed = UUID(token)
    except (ValueError, AttributeError):
        raise InvalidSnapshot(location + ": invalid identifier") from None
    require(parsed.int != 0, location + ": empty identifier")
    return str(parsed)

def array(value: object, location: str) -> list:
    require(isinstance(value, list) and len(value) <= 10000, location + ": invalid array")
    return value

def model_map(value: object, publication: str, location: str) -> dict[str, dict]:
    rows = array(value, location)
    require(bool(rows), location + ": empty model catalog")
    result = {}
    for index, row in enumerate(rows):
        where = f"{location}[{index}]"
        fields(row, {"route_id", "display_name", "is_suggested"}, where)
        route = text(row["route_id"], where)
        match = ROUTE.fullmatch(route)
        require(match is not None and match.group(1) == UUID(publication).hex,
                where + ": wrong route/publication")
        require(route not in result, where + ": duplicate route")
        text(row["display_name"], where)
        require(type(row["is_suggested"]) is bool, where + ": invalid suggestion flag")
        result[route] = row
    return result

def validate(snapshot: object) -> tuple[list[str], dict[str, int]]:
    counts = {"publications": 0, "clients": 0, "imports": 0, "labels": 0}
    try:
        data = fields(snapshot, {"schema_version", "status", "application_commit", "image_digest",
                                 "phase", "source_publications", "clients"}, "snapshot")
        require(data["schema_version"] == 1, "snapshot: unsupported schema")
        require(data["status"] == "OBSERVED", "snapshot: no observed data")
        require(isinstance(data["application_commit"], str) and OID.fullmatch(data["application_commit"]) is not None,
                "snapshot: invalid application commit")
        require(isinstance(data["image_digest"], str) and IMAGE.fullmatch(data["image_digest"]) is not None,
                "snapshot: invalid image digest")
        text(data["phase"], "snapshot phase")
        sources = {}
        for index, row in enumerate(array(data["source_publications"], "source_publications")):
            where = f"source_publications[{index}]"
            fields(row, {"source_instance_id", "publication_id", "revision", "default_route_id", "models"}, where)
            key = (uid(row["source_instance_id"], where), uid(row["publication_id"], where))
            require(key not in sources, where + ": duplicate publication")
            text(row["revision"], where)
            models = model_map(row["models"], key[1], where + ".models")
            require(row["default_route_id"] in models, where + ": default not in source catalog")
            sources[key] = (row, models)
        require(bool(sources), "snapshot: no source publications")
        counts["publications"] = len(sources)
        clients = array(data["clients"], "clients")
        require(len(clients) >= 2, "snapshot: two independent clients required")
        client_ids = set()
        for index, client in enumerate(clients):
            where = f"clients[{index}]"
            fields(client, {"client_id", "expected_publications", "imports"}, where)
            client_id = text(client["client_id"], where)
            require(client_id not in client_ids, where + ": duplicate client")
            client_ids.add(client_id)
            expected = set()
            for target in array(client["expected_publications"], where + ".expected_publications"):
                fields(target, {"source_instance_id", "publication_id"}, where)
                key = (uid(target["source_instance_id"], where), uid(target["publication_id"], where))
                require(key in sources and key not in expected, where + ": unknown/duplicate expected publication")
                expected.add(key)
            require(bool(expected), where + ": no expected publication")
            seen = set()
            local_ids = set()
            for n, item in enumerate(array(client["imports"], where + ".imports")):
                here = f"{where}.imports[{n}]"
                fields(item, {"source_instance_id", "publication_id", "local_provider_id", "revision",
                              "default_route_id", "default_display_name", "models", "labels"}, here)
                key = (uid(item["source_instance_id"], here), uid(item["publication_id"], here))
                require(key in expected and key not in seen, here + ": unexpected/duplicate import")
                seen.add(key)
                local_id = uid(item["local_provider_id"], here)
                require(local_id not in local_ids, here + ": duplicate local provider")
                local_ids.add(local_id)
                source, source_models = sources[key]
                models = model_map(item["models"], key[1], here + ".models")
                require(models == source_models, here + ": model identity/name/suggestion mismatch")
                require(item["revision"] == source["revision"], here + ": stale publication revision")
                require(item["default_route_id"] == source["default_route_id"], here + ": default route mismatch")
                require(item["default_display_name"] == source_models[source["default_route_id"]]["display_name"],
                        here + ": default label mismatch")
                seen_labels = set()
                for label in array(item["labels"], here + ".labels"):
                    fields(label, {"surface", "route_id", "display_name"}, here)
                    surface = text(label["surface"], here)
                    route = text(label["route_id"], here)
                    require((surface, route) not in seen_labels, here + ": duplicate surface label")
                    seen_labels.add((surface, route))
                    require(route in source_models and label["display_name"] == source_models[route]["display_name"],
                            here + ": wrong surface label or route")
                    counts["labels"] += 1
                require(bool(seen_labels), here + ": no observed UI labels")
                counts["imports"] += 1
            require(seen == expected, where + ": missing expected import")
        counts["clients"] = len(clients)
        return [], counts
    except (InvalidSnapshot, KeyError, TypeError, ValueError) as exc:
        message = str(exc) if isinstance(exc, InvalidSnapshot) else "snapshot: malformed value"
        return [message], counts

def unique_object(pairs: list[tuple[str, object]]) -> dict:
    result = {}
    for key, value in pairs:
        if key in result:
            raise InvalidSnapshot("snapshot: duplicate JSON field")
        result[key] = value
    return result

def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("snapshot", type=Path)
    args = parser.parse_args()
    try:
        require(args.snapshot.stat().st_size <= MAX_BYTES, "snapshot exceeds safe size limit")
        data = json.loads(args.snapshot.read_text(encoding="utf-8"), object_pairs_hook=unique_object)
        errors, counts = validate(data)
    except (OSError, ValueError, UnicodeError):
        print("FAIL: snapshot could not be read safely", file=sys.stderr)
        return 2
    for error in errors:
        print("FAIL:", error, file=sys.stderr)
    print(json.dumps(counts, sort_keys=True))
    print("Supplied-data consistency only; no proof of execution, provenance authenticity or authorization.")
    return 1 if errors else 0

if __name__ == "__main__":
    raise SystemExit(main())
