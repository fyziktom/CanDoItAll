#!/usr/bin/env python3
"""Offline OpenAPI description audit and conservative documentation-only comparison.

This helper is not a full OpenAPI/JSON Schema validator or a semantic coverage proof.
It does not fetch remote references, change documents, or infer business meanings.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from collections import Counter
from pathlib import Path
from typing import Any
from urllib.parse import unquote

METHODS = frozenset(('get', 'put', 'post', 'delete', 'options', 'head', 'patch', 'trace'))
SCHEMA_MAPS = frozenset(('properties', 'patternProperties', '$defs', 'definitions', 'dependentSchemas'))
SCHEMA_SINGLE = frozenset(('items', 'additionalProperties', 'unevaluatedProperties', 'unevaluatedItems', 'contains', 'not', 'if', 'then', 'else', 'propertyNames', 'additionalItems', 'contentSchema'))
SCHEMA_ARRAYS = frozenset(('allOf', 'anyOf', 'oneOf', 'prefixItems'))
DOC_KEYS = frozenset(('description', 'summary', 'example', 'examples', 'externalDocs'))
PLACEHOLDER = re.compile(r'^(?:todo\b|tbd\b|gets?\s+or\s+sets?\b)|^(?:ok|success|successful response|error|the (?:data|model|request|response|identifier))\.?$', re.I)


def escape(value: str) -> str:
    return value.replace('~', '~0').replace('/', '~1')


def pointer(base: str, key: str | int) -> str:
    return base + '/' + escape(str(key))


def unique_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f'Duplicate JSON member: {key!r}')
        result[key] = value
    return result


def load_document(path: Path) -> dict[str, Any]:
    if path.stat().st_size > 100 * 1024 * 1024:
        raise ValueError('Document exceeds the 100 MiB offline review limit.')
    text = path.read_text(encoding='utf-8-sig')
    def reject_constant(value: str) -> None:
        raise ValueError(f'Non-standard JSON number: {value}')
    data = json.loads(text, object_pairs_hook=unique_object, parse_constant=reject_constant)
    if not isinstance(data, dict):
        raise ValueError('OpenAPI document must be a JSON object.')
    version = str(data.get('openapi', ''))
    if not (version.startswith('3.0.') or version.startswith('3.1.')):
        raise ValueError(f'Only OpenAPI 3.0.x and 3.1.x are supported; found {version!r}.')
    return data


def resolve_pointer(document: Any, ref: str) -> tuple[Any, str]:
    if ref == '#':
        return document, '#'
    if not ref.startswith('#/'):
        raise ValueError(f'External or unsupported reference must be resolved explicitly: {ref!r}')
    fragment = unquote(ref[1:])
    current = document
    canonical = '#'
    for encoded in fragment[1:].split('/'):
        if re.search(r'~(?![01])', encoded):
            raise ValueError(f'Invalid JSON Pointer escape in {ref!r}')
        key = encoded.replace('~1', '/').replace('~0', '~')
        canonical = pointer(canonical, key)
        if isinstance(current, dict):
            if key not in current:
                raise ValueError(f'Unresolved reference: {ref!r}')
            current = current[key]
        elif isinstance(current, list) and re.fullmatch(r'0|[1-9][0-9]*', key):
            try:
                current = current[int(key)]
            except IndexError as exc:
                raise ValueError(f'Unresolved reference: {ref!r}') from exc
        else:
            raise ValueError(f'Unresolved reference: {ref!r}')
    return current, canonical


class Auditor:
    def __init__(self, document: dict[str, Any]):
        self.document = document
        self.findings: list[dict[str, str]] = []
        self.counts: Counter[str] = Counter()
        self.visited_schemas: set[str] = set()
        self.visited_paths: set[str] = set()
        self.operation_ids: dict[str, str] = {}

    def finding(self, code: str, at: str, message: str, severity: str = 'error') -> None:
        self.findings.append(dict(code=code, pointer=at, message=message, severity=severity))

    def description(self, node: dict[str, Any], at: str, category: str, key: str = 'description') -> None:
        self.counts[category + '_checks'] += 1
        value = node.get(key)
        if not isinstance(value, str) or not value.strip():
            self.finding('missing_' + key, at, f'Missing {category} {key}.')
        elif PLACEHOLDER.search(value.strip()):
            self.finding('suspected_placeholder', pointer(at, key), f'Review non-specific {category} text: {value!r}', 'warning')
        else:
            self.counts[category + '_nonblank'] += 1

    def object(self, node: Any, at: str) -> tuple[dict[str, Any], str] | None:
        seen: set[str] = set()
        while isinstance(node, dict) and '$ref' in node:
            ref = node['$ref']
            if not isinstance(ref, str):
                self.finding('invalid_ref', at, '$ref must be a string.')
                return None
            if ref in seen:
                self.finding('reference_cycle', at, 'Object reference chain has no concrete target.')
                return None
            seen.add(ref)
            try:
                target, target_at = resolve_pointer(self.document, ref)
            except ValueError as exc:
                self.finding('unresolved_ref', at, str(exc))
                return None
            # OpenAPI reference descriptions can override an object's description in 3.1.
            if isinstance(target, dict) and 'description' in node:
                target = {**target, 'description': node['description']}
            node, at = target, target_at
        if not isinstance(node, dict):
            self.finding('invalid_object', at, 'Expected an OpenAPI object.')
            return None
        return node, at

    def schema(self, node: Any, at: str, named: bool = False) -> None:
        if at in self.visited_schemas:
            return
        self.visited_schemas.add(at)
        self.counts['schema_nodes'] += 1
        if isinstance(node, bool):
            if named:
                self.finding('boolean_schema_review', at, 'Named boolean schema has no place for a description; classify its public use.', 'warning')
            return
        if not isinstance(node, dict):
            self.finding('invalid_schema', at, 'Expected a schema object or boolean.')
            return
        if named or ('properties' in node and '$ref' not in node) or 'enum' in node:
            self.description(node, at, 'schema')
        if '$ref' in node:
            try:
                if not isinstance(node['$ref'], str):
                    raise ValueError('$ref must be a string.')
                target, target_at = resolve_pointer(self.document, node['$ref'])
                self.schema(target, target_at, named=target_at.startswith('#/components/schemas/'))
            except ValueError as exc:
                self.finding('unresolved_ref', at, str(exc))
        if '$dynamicRef' in node:
            self.finding('dynamic_ref_review', at, 'Dynamic reference requires a schema-aware validation pass.', 'warning')
        for map_name in SCHEMA_MAPS:
            entries = node.get(map_name, {})
            if not isinstance(entries, dict):
                self.finding('invalid_schema_map', pointer(at, map_name), 'Expected a schema-name map.')
                continue
            for name, child in entries.items():
                child_at = pointer(pointer(at, map_name), name)
                if map_name in ('properties', 'patternProperties'):
                    self.counts['properties'] += 1
                    if isinstance(child, dict):
                        # A generic referenced type description does not explain this property's role.
                        self.description(child, child_at, 'property')
                    else:
                        self.finding('property_description_review', child_at, 'Boolean property schema needs a reviewed documentation disposition.')
                self.schema(child, child_at, named=map_name in ('$defs', 'definitions'))
        for name in SCHEMA_SINGLE:
            if name in node:
                value = node[name]
                if isinstance(value, list):
                    for index, item in enumerate(value):
                        self.schema(item, pointer(pointer(at, name), index))
                else:
                    self.schema(value, pointer(at, name))
        for name in SCHEMA_ARRAYS:
            if name in node:
                values = node[name]
                if not isinstance(values, list):
                    self.finding('invalid_schema_array', pointer(at, name), 'Expected an array of schemas.')
                else:
                    for index, item in enumerate(values):
                        self.schema(item, pointer(pointer(at, name), index))

    def content(self, node: Any, at: str) -> None:
        if not isinstance(node, dict):
            self.finding('invalid_content', at, 'Expected media-type map.')
            return
        for media_type, media in node.items():
            if isinstance(media, dict) and 'schema' in media:
                self.schema(media['schema'], pointer(pointer(at, media_type), 'schema'))

    def parameter(self, node: Any, at: str, category: str = 'parameter') -> None:
        resolved = self.object(node, at)
        if resolved is None:
            return
        value, where = resolved
        self.description(value, where, category)
        if 'schema' in value:
            self.schema(value['schema'], pointer(where, 'schema'))
        if 'content' in value:
            self.content(value['content'], pointer(where, 'content'))

    def response(self, node: Any, at: str) -> None:
        resolved = self.object(node, at)
        if resolved is None:
            return
        value, where = resolved
        self.description(value, where, 'response')
        if 'content' in value:
            self.content(value['content'], pointer(where, 'content'))
        for name, header in value.get('headers', {}).items():
            self.parameter(header, pointer(pointer(where, 'headers'), name), 'header')

    def request_body(self, node: Any, at: str) -> None:
        resolved = self.object(node, at)
        if resolved is not None:
            value, where = resolved
            self.description(value, where, 'request_body')
            self.content(value.get('content', {}), pointer(where, 'content'))

    def path_item(self, item: Any, at: str) -> None:
        resolved = self.object(item, at)
        if resolved is None:
            return
        value, where = resolved
        if where in self.visited_paths:
            return
        self.visited_paths.add(where)
        for index, parameter in enumerate(value.get('parameters', [])):
            self.parameter(parameter, pointer(pointer(where, 'parameters'), index))
        for method, operation in value.items():
            if method not in METHODS or not isinstance(operation, dict):
                continue
            op_at = pointer(where, method)
            self.counts['operations'] += 1
            self.description(operation, op_at, 'operation', 'summary')
            self.description(operation, op_at, 'operation')
            op_id = operation.get('operationId')
            if isinstance(op_id, str) and op_id:
                if op_id in self.operation_ids and self.operation_ids[op_id] != op_at:
                    self.finding('duplicate_operation_id', op_at, f'Also used at {self.operation_ids[op_id]}: {op_id}')
                self.operation_ids[op_id] = op_at
            else:
                self.finding('operation_id_review', op_at, 'No stable operationId; review, do not auto-rename existing operations.', 'warning')
            for index, parameter in enumerate(operation.get('parameters', [])):
                self.parameter(parameter, pointer(pointer(op_at, 'parameters'), index))
            if 'requestBody' in operation:
                self.request_body(operation['requestBody'], pointer(op_at, 'requestBody'))
            responses = operation.get('responses')
            if not isinstance(responses, dict) or not responses:
                self.finding('missing_responses', op_at, 'Operation has no declared responses.')
            else:
                for status, response in responses.items():
                    if not status.startswith('x-'):
                        self.response(response, pointer(pointer(op_at, 'responses'), status))
            for name, callback in operation.get('callbacks', {}).items():
                cb = self.object(callback, pointer(pointer(op_at, 'callbacks'), name))
                if cb:
                    for expression, path in cb[0].items():
                        if not expression.startswith('x-'):
                            self.path_item(path, pointer(cb[1], expression))

    def run(self) -> dict[str, Any]:
        for family in ('paths', 'webhooks'):
            values = self.document.get(family, {})
            if not isinstance(values, dict):
                self.finding('invalid_path_map', '#/' + family, 'Expected a path-item map.')
                continue
            for path, item in values.items():
                if not path.startswith('x-'):
                    self.path_item(item, pointer('#/' + family, path))
        components = self.document.get('components', {})
        for name, schema in components.get('schemas', {}).items():
            self.schema(schema, pointer('#/components/schemas', name), named=True)
        for family, method in (('parameters', self.parameter), ('headers', lambda n, p: self.parameter(n, p, 'header')), ('responses', self.response), ('requestBodies', self.request_body), ('pathItems', self.path_item)):
            for name, item in components.get(family, {}).items():
                method(item, pointer('#/components/' + family, name))
        if self.counts['operations'] == 0:
            self.finding('no_operations', '#', 'No operations were inspected; an empty export is not documentation coverage proof.')
        return dict(tool='openapi_review.py',scope='Description presence/placeholder and local-reference review; not semantic certification',counts=dict(sorted(self.counts.items())),errors=sum(f['severity']=='error' for f in self.findings),warnings=sum(f['severity']=='warning' for f in self.findings),findings=self.findings)


def strip_docs(node: Any, context: str = 'document') -> Any:
    """Remove prose only at known OpenAPI/Schema positions; preserve business names."""
    if not isinstance(node, dict):
        return node
    removable = set(DOC_KEYS)
    if context == 'schema':
        removable = {'description', 'title', 'example', 'examples', '$comment', 'externalDocs'}
    elif context == 'document':
        removable = {'externalDocs'}
    elif context in ('info', 'server', 'tag'):
        removable = {'description'}
    result: dict[str, Any] = {}
    for key, value in node.items():
        if key in removable:
            continue
        if context == 'schema':
            if key in SCHEMA_MAPS and isinstance(value, dict):
                result[key] = {name: strip_docs(child, 'schema') for name, child in value.items()}
            elif key in SCHEMA_SINGLE:
                result[key] = [strip_docs(v, 'schema') for v in value] if isinstance(value, list) else strip_docs(value, 'schema')
            elif key in SCHEMA_ARRAYS and isinstance(value, list):
                result[key] = [strip_docs(v, 'schema') for v in value]
            else:
                # Required arrays, discriminator mappings, constants, defaults and extensions stay exact.
                result[key] = value
            continue
        if context == 'document' and key in ('paths', 'webhooks'):
            result[key] = {name: strip_docs(child, 'path') for name, child in value.items()}
        elif context == 'document' and key == 'components':
            kinds = {'schemas':'schema','parameters':'parameter','headers':'parameter','responses':'response','requestBodies':'request_body','pathItems':'path','callbacks':'callback'}
            result[key] = {family: {name: strip_docs(child, kinds[family]) for name, child in children.items()} if family in kinds else children for family, children in value.items()}
        elif context == 'document' and key == 'info':
            result[key] = strip_docs(value, 'info')
        elif key in ('servers', 'tags') and isinstance(value, list):
            result[key] = [strip_docs(v, 'server' if key == 'servers' else 'tag') if isinstance(v, dict) else v for v in value]
        elif context == 'path' and key in METHODS:
            result[key] = strip_docs(value, 'operation')
        elif key == 'parameters' and isinstance(value, list):
            result[key] = [strip_docs(v, 'parameter') for v in value]
        elif key == 'requestBody':
            result[key] = strip_docs(value, 'request_body')
        elif key == 'responses' and isinstance(value, dict):
            result[key] = {name: strip_docs(v, 'response') for name, v in value.items()}
        elif key == 'headers' and isinstance(value, dict):
            result[key] = {name: strip_docs(v, 'parameter') for name, v in value.items()}
        elif key == 'content' and isinstance(value, dict):
            result[key] = {name: strip_docs(v, 'media') for name, v in value.items()}
        elif key == 'schema' and context in ('media', 'parameter'):
            result[key] = strip_docs(value, 'schema')
        elif key == 'callbacks' and isinstance(value, dict):
            result[key] = {name: strip_docs(v, 'callback') for name, v in value.items()}
        elif context == 'callback' and key != '$ref' and not key.startswith('x-'):
            result[key] = strip_docs(value, 'path')
        else:
            # Unknown extension/protocol/security bodies are preserved, not recursively guessed.
            result[key] = value
    return result


def canonical_hash(value: Any) -> str:
    return hashlib.sha256(json.dumps(value,sort_keys=True,separators=(',',':'),ensure_ascii=False,allow_nan=False).encode('utf-8')).hexdigest()


def differences(left: Any, right: Any, at: str = '#') -> list[dict[str, Any]]:
    if type(left) is not type(right):
        return [dict(pointer=at,kind='type_changed')]
    if isinstance(left, dict):
        result = []
        for key in sorted(left.keys() | right.keys()):
            loc = pointer(at, key)
            if key not in left:
                result.append(dict(pointer=loc,kind='added'))
            elif key not in right:
                result.append(dict(pointer=loc,kind='removed'))
            else:
                result.extend(differences(left[key],right[key],loc))
        return result
    if isinstance(left, list):
        if len(left) != len(right):
            return [dict(pointer=at,kind='array_changed')]
        result = []
        for index, (a,b) in enumerate(zip(left,right)):
            result.extend(differences(a,b,pointer(at,index)))
        return result
    return [] if left == right else [dict(pointer=at,kind='value_changed')]


def compare(before: dict[str,Any], after: dict[str,Any]) -> dict[str,Any]:
    a,b = strip_docs(before),strip_docs(after)
    delta = differences(a,b)
    return dict(tool='openapi_review.py',scope='Conservative review: every non-prose delta requires disposition; not a breaking-change classifier',documentation_only=not delta,before_structural_sha256=canonical_hash(a),after_structural_sha256=canonical_hash(b),difference_count=len(delta),differences=delta)


def main() -> int:
    parser=argparse.ArgumentParser(description=__doc__)
    sub=parser.add_subparsers(dest='command',required=True)
    a=sub.add_parser('audit'); a.add_argument('document',type=Path); a.add_argument('--output',type=Path)
    c=sub.add_parser('compare'); c.add_argument('before',type=Path); c.add_argument('after',type=Path); c.add_argument('--output',type=Path)
    args=parser.parse_args()
    try:
        report=Auditor(load_document(args.document)).run() if args.command=='audit' else compare(load_document(args.before),load_document(args.after))
        text=json.dumps(report,indent=2,ensure_ascii=False)+'\n'
        if args.output:
            args.output.write_text(text,encoding='utf-8')
        else:
            print(text,end='')
        return int(report['errors']>0 or report['warnings']>0) if args.command=='audit' else int(not report['documentation_only'])
    except (OSError,ValueError,TypeError,KeyError,AttributeError,RecursionError) as exc:
        print(f'Review failed: {exc}',file=sys.stderr)
        return 2

if __name__=='__main__':
    raise SystemExit(main())
