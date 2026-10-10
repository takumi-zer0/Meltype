"""Materialize a verified llama.cpp archive, including dylib aliases, on Windows or macOS."""
import hashlib
import pathlib
import shutil
import sys
import tarfile

archive, output = map(pathlib.Path, sys.argv[1:])
approved = {
    'f4b33fe40d098033fb1a19a8a4a84383c4ca84c37b6a60d9f9a253f349b48c07',
    'b7977620cf5c30b8905caa45d7dbcc08766b5d4a94b3cde66ecf0323a495096f',
}
assert hashlib.sha256(archive.read_bytes()).hexdigest() in approved, 'Unapproved runtime archive'
root = output.resolve()
root.mkdir(parents=True, exist_ok=True)
with tarfile.open(archive, 'r:gz') as bundle:
    for member in bundle.getmembers():
        relative = pathlib.PurePosixPath(member.name)
        assert not relative.is_absolute() and '..' not in relative.parts
        if len(relative.parts) < 2 or member.isdir():
            continue
        destination = root.joinpath(*relative.parts[1:]).resolve()
        assert destination.is_relative_to(root)
        assert member.isfile() or member.issym() or member.islnk(), 'Unexpected archive type'
        destination.parent.mkdir(parents=True, exist_ok=True)
        # extractfile resolves aliases within the archive; no filesystem symlink is created.
        with bundle.extractfile(member) as source, destination.open('wb') as target:
            shutil.copyfileobj(source, target)
        destination.chmod(0o755 if member.mode & 0o111 else 0o644)
