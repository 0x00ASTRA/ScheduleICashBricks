#!/usr/bin/env bash
# Run outside Codex's restricted shell to grant access for future sessions.
set -euo pipefail

exec python3 - "$@" <<'PY'
import argparse
import json
import os
import shlex
from pathlib import Path
import stat
import tempfile
import time

try:
    import tomllib
except ImportError:
    raise SystemExit("This script requires Python 3.11 or newer.")

parser = argparse.ArgumentParser(description="Allow Codex to install Cash Bricks into Schedule I.")
parser.add_argument("--config", default=str(Path(os.environ.get("CODEX_HOME", str(Path.home() / ".codex"))) / "config.toml"))
parser.add_argument("--game-dir", default=str(Path.home() / ".local/share/Steam/steamapps/common/Schedule I"))
parser.add_argument("--dry-run", action="store_true", help="Check the update without writing anything.")
parser.add_argument("--session", help="Existing Codex chat ID to include in the explicit resume command.")
args = parser.parse_args()
config = Path(args.config).expanduser().resolve()
game = Path(args.game_dir).expanduser().resolve()
if not game.is_dir():
    raise SystemExit(f"Game folder not found: {game}\nUse --game-dir to specify its location.")

original = config.read_bytes() if config.exists() else b""
text = original.decode("utf-8")
try:
    settings = tomllib.loads(text)
except tomllib.TOMLDecodeError as error:
    raise SystemExit(f"Config is not valid TOML; nothing changed: {error}")

section = settings.get("sandbox_workspace_write", {})
if not isinstance(section, dict):
    raise SystemExit("sandbox_workspace_write must be a TOML table; nothing changed.")
roots = section.get("writable_roots", [])
if not isinstance(roots, list) or not all(isinstance(root, str) for root in roots):
    raise SystemExit("writable_roots must be an array of paths; nothing changed.")
updated_roots = list(roots)
if not any(Path(root).expanduser().resolve() == game for root in roots):
    updated_roots.append(str(game))

# Find complete TOML statements rather than matching lines inside multiline
# arrays or strings. Preserve all unrelated config, comments and tables.
def key_path(parsed):
    path = []
    while isinstance(parsed, dict) and len(parsed) == 1:
        key, parsed = next(iter(parsed.items()))
        path.append(key)
        if isinstance(parsed, list) and parsed and isinstance(parsed[-1], dict):
            parsed = parsed[-1]
    return tuple(path)

statements = []
lines = text.splitlines(keepends=True)
index = offset = 0
scope = ()
while index < len(lines):
    if not lines[index].strip() or lines[index].lstrip().startswith("#"):
        offset += len(lines[index])
        index += 1
        continue
    end = index + 1
    while True:
        raw = "".join(lines[index:end])
        try:
            parsed = tomllib.loads(raw)
            break
        except tomllib.TOMLDecodeError:
            end += 1
            if end > len(lines):
                raise SystemExit("Cannot identify config statements safely; nothing changed.")
    header = raw.lstrip().startswith("[")
    if header:
        scope = key_path(parsed)
    statements.append((offset, offset + len(raw), scope, header, raw, parsed))
    offset += len(raw)
    index = end

def assignment(target):
    for start, end, scope, header, raw, parsed in statements:
        if header or target[:len(scope)] != scope or len(scope) >= len(target):
            continue
        relative = target[len(scope):]
        value = parsed
        for key in relative:
            if not isinstance(value, dict) or key not in value:
                break
            value = value[key]
        else:
            prefix = raw.split("=", 1)[0]
            if scope + key_path(tomllib.loads(prefix + "= 0")) != target:
                raise SystemExit("Sandbox settings use an inline table; nothing changed. Expand it into a TOML section first.")
            return start, end, prefix
    return None

newline = "\r\n" if "\r\n" in text else "\n"
edits = []
mode = assignment(("sandbox_mode",))
prepend_mode = mode is None
if mode and settings.get("sandbox_mode") != "workspace-write":
    start, end, prefix = mode
    edits.append((start, end, prefix + '= "workspace-write"' + newline))

root_setting = assignment(("sandbox_workspace_write", "writable_roots"))
if not root_setting or updated_roots != roots:
    value = json.dumps(updated_roots, ensure_ascii=False)
    if root_setting:
        start, end, prefix = root_setting
        edits.append((start, end, prefix + "= " + value + newline))
    else:
        header = next((item for item in statements if item[3] and item[2] == ("sandbox_workspace_write",)), None)
        if header:
            position = header[1]
            separator = "" if text[:position].endswith("\n") else newline
            edits.append((position, position, separator + "writable_roots = " + value + newline))
        elif section:
            # Existing dotted settings implicitly define this table; extend it
            # with another root-level dotted assignment before any sections.
            edits.append((0, 0, "sandbox_workspace_write.writable_roots = " + value + newline))
        else:
            edits.append((len(text), len(text), newline + newline + "[sandbox_workspace_write]" + newline + "writable_roots = " + value + newline))

updated = text
for start, end, replacement in sorted(edits, reverse=True):
    updated = updated[:start] + replacement + updated[end:]
if prepend_mode:
    updated = 'sandbox_mode = "workspace-write"' + newline + newline + updated
try:
    checked = tomllib.loads(updated)
except tomllib.TOMLDecodeError as error:
    raise SystemExit(f"Proposed update failed validation; nothing changed: {error}")
if checked.get("sandbox_mode") != "workspace-write" or checked["sandbox_workspace_write"]["writable_roots"] != updated_roots:
    raise SystemExit("Proposed settings failed validation; nothing changed.")

if updated == text:
    print(f"Already configured: {game}")
elif args.dry_run:
    print(f"Would update: {config}")
    print(f"Would enable workspace-write and add this writable folder: {game}")
    print("Existing writable folders and unrelated settings are preserved. No files changed.")
else:
    config.parent.mkdir(parents=True, exist_ok=True)
    if (config.read_bytes() if config.exists() else b"") != original:
        raise SystemExit("Config changed during this update; nothing written. Run the script again.")
    mode_bits = stat.S_IMODE(config.stat().st_mode) if config.exists() else 0o600
    if config.exists():
        backup = config.with_name(config.name + f".cashbricks-backup-{time.time_ns()}")
        descriptor = os.open(backup, os.O_WRONLY | os.O_CREAT | os.O_EXCL, mode_bits)
        with os.fdopen(descriptor, "wb") as handle:
            handle.write(original)
        print(f"Backup: {backup}")
    descriptor, temporary_name = tempfile.mkstemp(prefix=".cashbricks-config-", dir=config.parent)
    temporary = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "wb") as handle:
            os.fchmod(handle.fileno(), mode_bits)
            handle.write(updated.encode("utf-8"))
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary, config)
    finally:
        temporary.unlink(missing_ok=True)
    print(f"Updated: {config}")
    print(f"Writable game folder: {game}")

if not args.dry_run:
    print("Config defaults are ready. An existing chat can retain its saved folder permissions.")
    print("Exit Codex, then use this explicit resume command (select the mod chat if prompted):")
    resume = ["codex", "resume", args.session] if args.session else ["codex", "resume", "--all"]
    print(shlex.join(resume + ["--sandbox", "workspace-write", "--add-dir", str(game)]))
PY
