#!/bin/sh
set -eu
BASE=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
# Pass an isolated .rufmaker project as the first argument, or use the UI to open one.
if [ "$#" -gt 0 ]; then
  export RUFMAKER_LINUX_PROJECT=$(realpath "$1")
  shift
fi
if [ -n "${RUFMAKER_MONO_RUNTIME:-}" ]; then
  export MONO_PATH="$RUFMAKER_MONO_RUNTIME/usr/lib/mono/4.5"
  export MONO_CFG_DIR="$RUFMAKER_MONO_RUNTIME/etc"
  export LD_LIBRARY_PATH="$RUFMAKER_MONO_RUNTIME/usr/lib:$RUFMAKER_MONO_RUNTIME/usr/lib/x86_64-linux-gnu${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
  MONO="$RUFMAKER_MONO_RUNTIME/usr/bin/mono"
else
  MONO=mono
fi
cd "$BASE/engine"
exec "$MONO" "$BASE/engine/RufMaker.Linux.exe" "$@"
