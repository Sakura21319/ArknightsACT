"""Source-level compile check for this Unity project using Unity's bundled Roslyn.

Why this exists: when a Unity Editor already holds the project lock, a batch Unity run
dies with "another Unity instance is running with this project open", so the usual
`-batchmode -quit` compile check is unavailable.

How it works: Unity writes a response file per assembly under
`Library/Bee/artifacts/<dag>/<Assembly>.rsp` containing the exact reference list, defines
and source list it used. This script reuses those arguments, redirects the output, appends
source files Unity has not seen yet, and feeds everything to `Data/DotNetSdkRoslyn/csc.dll`.
`Game.Editor` references the previous `Game.Gameplay` output, so anything rebuilt in this
run is substituted into later assemblies to avoid a cascade of false CS1061 errors.

Usage (from anywhere):
    python Tools/compile_check.py

Set UNITY_EDITOR to override the auto-detected editor folder.
"""

import glob
import os
import re
import subprocess
import sys

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(PROJECT, "Temp", "CompileCheck")
UNITY_VERSION_FILE = os.path.join(PROJECT, "ProjectSettings", "ProjectVersion.txt")

ASSEMBLIES = ("Game.Gameplay", "Game.Editor")

# Assembly roots used to pick up source files Unity has not compiled yet.
EXTRA_SOURCES = {
    "Game.Gameplay": ["Assets/_Game/Scripts/Gameplay"],
    "Game.Editor": ["Assets/_Game/Editor"],
}


def unity_version():
    if not os.path.exists(UNITY_VERSION_FILE):
        raise SystemExit("Missing ProjectSettings/ProjectVersion.txt")
    with open(UNITY_VERSION_FILE, "r", encoding="utf-8") as handle:
        for line in handle:
            if line.startswith("m_EditorVersion:"):
                return line.split(":", 1)[1].strip()
    raise SystemExit("Could not read m_EditorVersion")


def find_editor(version):
    override = os.environ.get("UNITY_EDITOR")
    if override:
        candidates = [override]
    else:
        hubs = [
            os.path.join(os.environ.get("PROGRAMFILES", r"C:\Program Files"), "Unity", "Hub", "Editor"),
            os.path.join(os.environ.get("LOCALAPPDATA", ""), "Unity", "Hub", "Editor"),
            r"D:\Unity\Hub\Editor",
        ]
        candidates = [os.path.join(hub, version, "Editor") for hub in hubs]
        candidates.append(os.path.join("/Applications/Unity/Hub/Editor", version, "Unity.app", "Contents"))
    for candidate in candidates:
        if os.path.exists(os.path.join(candidate, "Data", "DotNetSdkRoslyn", "csc.dll")):
            return candidate
    raise SystemExit(
        "Unity %s not found. Set UNITY_EDITOR to its Editor folder." % version
    )


def find_dag(assembly):
    """Unity's incremental build folder is content hashed, so locate it by the response file."""
    pattern = os.path.join(PROJECT, "Library", "Bee", "artifacts", "*", assembly + ".rsp")
    matches = sorted(glob.glob(pattern), key=os.path.getmtime, reverse=True)
    if not matches:
        raise SystemExit(
            "No response file for %s. Open the project in Unity once so it compiles." % assembly
        )
    return matches[0]


def build(assembly, editor, fresh):
    rsp_path = find_dag(assembly)
    with open(rsp_path, "r", encoding="utf-8") as handle:
        raw = [line.rstrip("\n") for line in handle]

    options, sources, declared = [], [], set()
    for line in raw:
        stripped = line.strip()
        if not stripped:
            continue
        if stripped.startswith("-out:") or stripped.startswith("-refout:"):
            continue

        # Point references at assemblies rebuilt in this run instead of last session's copies.
        swapped = None
        for name, path in fresh.items():
            if stripped.startswith("-r:") and (
                stripped.endswith('/%s.dll"' % name) or stripped.endswith('/%s.ref.dll"' % name)
            ):
                swapped = '-r:"%s"' % path
                break
        options.append(swapped or line)

        if re.match(r'^"Assets/.*\.cs"$', stripped):
            sources.append(stripped)
            declared.add(stripped.strip('"').replace("\\", "/"))

    # Pick up files Unity has not compiled yet (added this round).
    added = 0
    for root in EXTRA_SOURCES.get(assembly, []):
        for path in glob.glob(os.path.join(PROJECT, root, "**", "*.cs"), recursive=True):
            rel = os.path.relpath(path, PROJECT).replace("\\", "/")
            if rel not in declared:
                sources.append('"%s"' % rel)
                declared.add(rel)
                added += 1

    os.makedirs(OUT_DIR, exist_ok=True)
    out_dll = os.path.join(OUT_DIR, assembly + ".dll").replace("\\", "/")
    merged_path = os.path.join(OUT_DIR, assembly + ".check.rsp")
    with open(merged_path, "w", encoding="utf-8") as handle:
        handle.write("\n".join(options + ['-out:"%s"' % out_dll] + sources) + "\n")

    dotnet = os.path.join(editor, "Data", "NetCoreRuntime", "dotnet.exe")
    if not os.path.exists(dotnet):
        dotnet = os.path.join(editor, "Data", "NetCoreRuntime", "dotnet")
    csc = os.path.join(editor, "Data", "DotNetSdkRoslyn", "csc.dll")
    result = subprocess.run(
        [dotnet, csc, "@" + os.path.relpath(merged_path, PROJECT).replace("\\", "/")],
        cwd=PROJECT, capture_output=True, text=True, encoding="utf-8", errors="replace",
    )
    output = (result.stdout or "") + (result.stderr or "")
    errors = [line.strip() for line in output.splitlines() if "error CS" in line]

    print("=" * 72)
    print("%s: %d sources (+%d new), EXIT=%d, errors=%d"
          % (assembly, len(sources), added, result.returncode, len(errors)))
    for line in errors[:40]:
        print("  " + line)
    if result.returncode != 0 and not errors:
        print(output[-3000:])
    return len(errors), out_dll


def main():
    version = unity_version()
    editor = find_editor(version)
    print("Unity %s at %s" % (version, editor))
    total = 0
    fresh = {}
    for assembly in ASSEMBLIES:
        count, out_dll = build(assembly, editor, fresh)
        total += count
        fresh[assembly] = out_dll
    print("=" * 72)
    print("TOTAL_ERRORS=%d" % total)
    return 1 if total else 0


if __name__ == "__main__":
    sys.exit(main())
