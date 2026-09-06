import argparse,json,hashlib,os,time,threading
from pathlib import Path
from http.server import ThreadingHTTPServer,BaseHTTPRequestHandler
parser=argparse.ArgumentParser()
parser.add_argument("--repo",required=True)
parser.add_argument("--port",type=int,default=17299)
args=parser.parse_args()
root=Path(args.repo).resolve()
bundle=root/"codex/bundles/UI_AgentCatalog_01_Extraction_Sandbox_Bundle"
plan=json.loads((bundle/"plan/frozen-edits.json").read_text(encoding="utf-8"))
edits={item["id"]:item for item in plan["edits"]}
output=root/".mcp-state/catalog-measurements"
output.mkdir(parents=True,exist_ok=True)
lock=threading.Lock()
active=None
def append(value):
    with (output/"ledger.jsonl").open("a",encoding="utf-8") as f:
        f.write(json.dumps(value,ensure_ascii=False)+"\n")
        f.flush()
        os.fsync(f.fileno())
def flush(path,data):
    with path.open("wb") as f:
        f.write(data)
        f.flush()
        os.fsync(f.fileno())
class Handler(BaseHTTPRequestHandler):
    def log_message(self,*args):
        pass
    def do_GET(self):
        if self.path=="/status":
            self.reply(200,{"active":None if active is None else {k:v for k,v in active.items() if k not in ["original","patched"]},"clock":"time.perf_counter_ns","pid":os.getpid()})
        else:
            self.reply(404,{"error":"Unknown measurement endpoint"})
    def do_POST(self):
        global active
        try:
            size=int(self.headers.get("Content-Length","0"))
            if size>20000:
                raise ValueError("Oversized measurement request")
            data=json.loads(self.rfile.read(size) or b"{}")
            with lock:
                if self.path=="/cold/start":
                    if active is not None:
                        raise ValueError("An attempt is already active")
                    active={**data,"kind":"cold","t0":time.perf_counter_ns(),"utc":time.strftime("%Y-%m-%dT%H:%M:%SZ",time.gmtime())}
                    self.reply(200,{"started":True})
                elif self.path=="/trial/start":
                    if active is not None:
                        raise ValueError("Previous attempt must complete and undo before another edit")
                    item=edits[data["editId"]]
                    path=(root/(item["beforePath"] if data["host"]=="pre" else item["afterPath"])).resolve()
                    if not path.is_relative_to(root):
                        raise ValueError("Edit path is outside repository")
                    original=path.read_bytes()
                    old=item["old"].encode()
                    new=item["new"].encode()
                    if old not in original or new in original:
                        raise ValueError("Source does not match frozen unedited patch")
                    patched=original.replace(old,new,1)
                    active={**data,"kind":"warm","path":str(path.relative_to(root)).replace("\\","/"),"category":item["category"],"sourceSha256":hashlib.sha256(original).hexdigest(),"patchSha256":hashlib.sha256(old+b"\0"+new).hexdigest(),"original":original,"patched":patched}
                    flush(path,patched)
                    active["t0"]=time.perf_counter_ns()
                    active["utc"]=time.strftime("%Y-%m-%dT%H:%M:%SZ",time.gmtime())
                    self.reply(200,{"started":True,"sourceSha256":active["sourceSha256"],"patchSha256":active["patchSha256"]})
                elif self.path=="/observed":
                    if active is None:
                        raise ValueError("No active attempt")
                    active.update(data)
                    active["t1"]=time.perf_counter_ns()
                    active["elapsedMs"]=(active["t1"]-active["t0"])/1_000_000
                    if active["kind"]=="cold":
                        append(active)
                        result=active
                        active=None
                    else:
                        result={k:v for k,v in active.items() if k not in ["original","patched"]}
                    self.reply(200,result)
                elif self.path=="/undo":
                    if active is None or active["kind"]!="warm":
                        raise ValueError("No warm attempt")
                    path=root/active["path"]
                    if path.read_bytes()!=active["patched"]:
                        raise ValueError("Concurrent source edit; refusing to overwrite")
                    flush(path,active["original"])
                    active["undoT0"]=time.perf_counter_ns()
                    self.reply(200,{"restored":True})
                elif self.path=="/undo-observed":
                    if active is None or "undoT0" not in active:
                        raise ValueError("No restored attempt")
                    active["undoT1"]=time.perf_counter_ns()
                    active["undoConfirmed"]=data["confirmed"]
                    active["undoMetadata"]=data
                    result={k:v for k,v in active.items() if k not in ["original","patched"]}
                    append(result)
                    active=None
                    self.reply(200,result)
                else:
                    self.reply(404,{"error":"Unknown measurement endpoint"})
        except Exception as error:
            self.reply(409,{"error":str(error)})
    def reply(self,status,data):
        body=json.dumps(data,ensure_ascii=False).encode()
        self.send_response(status)
        self.send_header("Content-Type","application/json")
        self.send_header("Content-Length",str(len(body)))
        self.end_headers()
        self.wfile.write(body)
print(json.dumps({"state":"CoordinatorReady","port":args.port,"clock":"time.perf_counter_ns","root":str(root)}),flush=True)
ThreadingHTTPServer(("127.0.0.1",args.port),Handler).serve_forever()
