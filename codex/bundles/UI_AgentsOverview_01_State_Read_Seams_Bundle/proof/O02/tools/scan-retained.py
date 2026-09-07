import pathlib,sys,json,gzip,importlib.util,hashlib,datetime
root=pathlib.Path(sys.argv[1]); report=pathlib.Path(sys.argv[2])
spec=importlib.util.spec_from_file_location('secrets',pathlib.Path('tools/Validation/Portability/scan_artifacts_for_secrets.py'))
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
coverage=[];findings=[]
for file in sorted(root.rglob('*')):
 if not file.is_file() or file==report or file.suffix in ['.jpeg','.jpg','.png','.zip'] or file.name.lower()=='manifest.sha256':continue
 raw=file.read_bytes();compressed=file.suffix=='.gz'
 if compressed:raw=gzip.decompress(raw)
 encoding='utf-16' if raw.startswith((b'\xff\xfe',b'\xfe\xff')) else 'utf-16-le' if raw[:80].count(b'\x00')>10 else 'utf-8-sig'
 try:text=raw.decode(encoding)
 except UnicodeDecodeError:
  if b'\x00' in raw:continue
  raise
 coverage.append({'path':file.relative_to(root).as_posix(),'decodedBytes':len(raw),'encoding':encoding,'decompressed':compressed})
 for rule,pattern,group in m.RULES:
  for match in pattern.finditer(text):
   value=match.group(group)
   if m.is_placeholder(value):continue
   findings.append({'path':file.relative_to(root).as_posix(),'rule':rule,'fingerprint':m.fingerprint(value)})
result={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'scope':str(root),'coverage':coverage,'findings':findings}
report.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'files':len(coverage),'findings':findings}));sys.exit(bool(findings))
