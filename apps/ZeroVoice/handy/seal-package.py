"""Recompute manifests after the platform bundler signs Mach-O files."""
import hashlib,json,pathlib,sys
root=pathlib.Path(sys.argv[1]);config=json.loads((root/'worker-model.json').read_text(encoding='utf8'))
for item in config['files']:
 item['sha256']=hashlib.sha256((root/item['path']).read_bytes()).hexdigest()
(root/'worker-model.json').write_text(json.dumps(config,indent=2),encoding='utf8')
for directory in [root,root/'native']:
 files=[dict(path=p.name,sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in directory.iterdir() if p.is_file() and p.name!='files.json']
 (directory/'files.json').write_text(json.dumps(files,indent=2),encoding='utf8')
