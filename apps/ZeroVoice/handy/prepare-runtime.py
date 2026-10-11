"""Assemble the pinned local runtime; no user data or model weights in CI."""
import hashlib, json, pathlib, shutil, subprocess, sys, urllib.request, zipfile

rid, destination = sys.argv[1:]
root=pathlib.Path(destination).resolve(); backend=root/'backend';backend.mkdir(exist_ok=True)
assets={
 'win-x64':('llama-b11552-bin-win-vulkan-x64.zip','83dc16325b532ba603034719e8377e28ca5093c5d4370141a6522f1a0250386c'),
 'osx-arm64':('llama-b11552-bin-macos-arm64.tar.gz','f4b33fe40d098033fb1a19a8a4a84383c4ca84c37b6a60d9f9a253f349b48c07'),
 'osx-x64':('llama-b11552-bin-macos-x64.tar.gz','b7977620cf5c30b8905caa45d7dbcc08766b5d4a94b3cde66ecf0323a495096f'),
}
name,digest=assets[rid];archive=root.parent/name
urllib.request.urlretrieve('https://github.com/ggml-org/llama.cpp/releases/download/b11552/'+name,archive)
assert hashlib.sha256(archive.read_bytes()).hexdigest()==digest,'Runtime checksum mismatch'
if rid=='win-x64':
 with zipfile.ZipFile(archive) as bundle:
  for item in bundle.infolist():
   if pathlib.PurePosixPath(item.filename).suffix=='.dll':
    (backend/pathlib.PurePosixPath(item.filename).name).write_bytes(bundle.read(item))
 library='backend/llama.dll'
else:
 subprocess.run([sys.executable,str(pathlib.Path(__file__).parent.parent/'scripts/extract-runtime.py'),str(archive),str(backend)],check=True)
 library=str(next(backend.rglob('libllama.dylib')).relative_to(root)).replace('\\','/')
files=[dict(path=str(p.relative_to(root)).replace('\\','/'),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in backend.rglob('*') if p.is_file()]
config=dict(library=library,model='models/gemma-4-E2B_q4_0-it.gguf',modelSha256='fa401b55b07ee70a54c6dae3903c783a6e65064312529ea57175cb5f8dec6634',files=files,format='gemma4',gpuLayers=99,modelStorage='user')
(root/'worker-model.json').write_text(json.dumps(config,indent=2),encoding='utf8')
manifest=[dict(path=p.name,sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in root.iterdir() if p.is_file() and p.name!='files.json']
(root/'files.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
archive.unlink()
