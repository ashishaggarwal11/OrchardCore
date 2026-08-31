#!/usr/bin/env bash
# PostToolUse hook (Write|Edit): early style/analyzer feedback for .cs files under
# src/OrchardCore.Modules/. Mirrors the same check CI and the code-checker skill run
# (dotnet build --warnaserror -p:RunAnalyzers=true), scoped to the smallest affected
# project so it's fast. Non-blocking (async) — reports via systemMessage only; the
# developer agent's mandatory code-checker pass before marking a phase done remains
# the actual gate.
set -u

input=$(cat)

file=$(printf '%s' "$input" | node -e "
let d = '';
process.stdin.on('data', c => d += c);
process.stdin.on('end', () => {
  try {
    const j = JSON.parse(d);
    process.stdout.write((j.tool_input && j.tool_input.file_path) || '');
  } catch (e) {}
});
")

case "$file" in
  *src/OrchardCore.Modules/*.cs) ;;
  *) exit 0 ;;
esac

dir=$(dirname "$file")
proj=""
while [ "$dir" != "." ] && [ "$dir" != "/" ]; do
  found=$(find "$dir" -maxdepth 1 -name '*.csproj' 2>/dev/null | head -1)
  if [ -n "$found" ]; then
    proj="$found"
    break
  fi
  dir=$(dirname "$dir")
done

if [ -z "$proj" ]; then
  exit 0
fi

out=$(dotnet build "$proj" -c Release -p:TreatWarningsAsErrors=true --warnaserror -p:RunAnalyzers=true -p:NuGetAudit=false 2>&1)
status=$?

if [ "$status" -ne 0 ]; then
  printf '%s' "$out" | tail -30 | node -e "
let s = '';
process.stdin.on('data', c => s += c);
process.stdin.on('end', () => {
  const msg = 'Style/analyzer issues in ' + process.argv[1] + ':\n' + s;
  process.stdout.write(JSON.stringify({ systemMessage: msg }));
});
" -- "$proj"
fi
