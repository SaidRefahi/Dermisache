import urllib.request, json, time
url="http://127.0.0.1:8080/mcp"
init=json.dumps({"jsonrpc":"2.0","id":"i1","method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"t","version":"1.0"}}}).encode()
req=urllib.request.Request(url,data=init,headers={"Content-Type":"application/json","Accept":"application/json, text/event-stream"},method="POST")
with urllib.request.urlopen(req,timeout=5) as resp:
    sid=resp.headers.get("mcp-session-id")
    print("SID",sid)
    notif=json.dumps({"jsonrpc":"2.0","method":"notifications/initialized"}).encode()
    urllib.request.urlopen(urllib.request.Request(url,data=notif,headers={"Content-Type":"application/json","Accept":"application/json, text/event-stream","mcp-session-id":sid},method="POST"),timeout=3).read()
    def call(name, args):
        p=json.dumps({"jsonrpc":"2.0","id":"c1","method":"tools/call","params":{"name":name,"arguments":args}}).encode()
        req2=urllib.request.Request(url,data=p,headers={"Content-Type":"application/json","Accept":"application/json, text/event-stream","mcp-session-id":sid},method="POST")
        with urllib.request.urlopen(req2,timeout=12) as r2:
            txt=r2.read().decode("utf-8", errors="replace")
            print(f"CALL {name} ->", txt[:4000])
            return txt
    def read_res(uri):
        p=json.dumps({"jsonrpc":"2.0","id":"r1","method":"resources/read","params":{"uri":uri}}).encode()
        req2=urllib.request.Request(url,data=p,headers={"Content-Type":"application/json","Accept":"application/json, text/event-stream","mcp-session-id":sid},method="POST")
        with urllib.request.urlopen(req2,timeout=12) as r2:
            txt=r2.read().decode("utf-8", errors="replace")
            print(f"READ {uri} ->", txt[:6000])
            return txt
    # try find XRInteractionManager
    try:
        call("find_gameobjects", {"search_term":"XRInteractionManager","search_method":"by_component"})
    except Exception as e:
        print("find XRInteractionManager err", e)
    try:
        call("find_gameobjects", {"search_term":"Stroke_Prototype","search_method":"by_name"})
    except Exception as e:
        print("find Stroke err", e)
    try:
        read_res("mcpforunity://instances")
    except Exception as e:
        print("read instances err", e)
