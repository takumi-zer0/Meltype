"""Package local deliverables without relying on Windows executable-mode metadata."""
import pathlib
import sys
import tarfile
import zipfile

source, outputs = map(lambda s: pathlib.Path(s).resolve(), sys.argv[1:])
with zipfile.ZipFile(outputs / 'ZeroVoice-source.zip', 'w', zipfile.ZIP_DEFLATED) as bundle:
    for path in sorted(source.rglob('*')):
        relative = path.relative_to(source)
        if path.is_file() and not any(p in {'bin', 'obj', '.git', '__pycache__'} for p in relative.parts):
            if path.suffix == '.gguf' or path.name == 'model.json':
                raise ValueError('Unexpected private/runtime file in source')
            bundle.write(path, 'ZeroVoice/' + relative.as_posix())

for name in ['ZeroVoice-Mac-AppleSilicon', 'ZeroVoice-Mac-Intel']:
    folder = outputs / name
    def portable_mode(info):
        info.uid = info.gid = 0
        info.uname = info.gname = ''
        leaf = pathlib.PurePosixPath(info.name).name
        executable = leaf.endswith('.command') or leaf in {'ZeroVoice', 'ZeroVoice.Mac', 'llama-completion'}
        info.mode = 0o755 if info.isdir() or executable else 0o644
        return info
    with tarfile.open(outputs / (name + '.tar.gz'), 'w:gz', compresslevel=6) as bundle:
        bundle.add(folder, arcname=name, filter=portable_mode)
    with tarfile.open(outputs / (name + '.tar.gz'), 'r:gz') as bundle:
        for suffix in ['/MacOS/ZeroVoice', '/native/ZeroVoice.Mac', '/backend/llama-completion', '/Setup-Model.command']:
            entry = next(m for m in bundle.getmembers() if m.name.endswith(suffix))
            if not entry.mode & 0o111:
                raise ValueError('Missing executable permission: ' + entry.name)
    print('Packaged ' + name)
