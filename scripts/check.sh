#!/usr/bin/env bash
# Runs every check that does not need Unity: Python tools, generated data and
# the simulation core. Same steps as .github/workflows/checks.yml.
#
#   scripts/check.sh            all checks
#   scripts/check.sh --fast     skip the stress benchmark
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO"
FAST=0
[[ "${1:-}" == "--fast" ]] && FAST=1

step() { printf '\n\033[1;36m==> %s\033[0m\n' "$*"; }

# Works on Linux/macOS and on Windows (Git Bash), where venvs use Scripts/.
PYTHON="$(command -v python3 || command -v python)"
vpy() { if [[ -d "$1/.venv/Scripts" ]]; then echo "$1/.venv/Scripts/python"; else echo "$1/.venv/bin/python"; fi; }

venv() {  # venv <tool dir> <requirements file>
    local dir="$1" req="$2"
    if [[ ! -d "$dir/.venv" ]]; then
        "$PYTHON" -m venv "$dir/.venv"
        "$(vpy "$dir")" -m pip install -q -r "$dir/$req"
    fi
}

step "osm2tiles: tests"
venv tools/osm2tiles requirements-dev.txt
"$(vpy tools/osm2tiles)" -m pytest -q tools/osm2tiles

step "datagen: tests + generated data up to date"
venv tools/datagen requirements.txt
(cd tools/datagen && "$(vpy .)" -m pytest -q && "$(vpy .)" -m datagen --check)

step "core: dotnet test (C# 9 / netstandard2.1, like Unity 6)"
dotnet test tests/CepaCero.Core.Tests/CepaCero.Core.Tests.csproj -c Release --nologo -v quiet

if [[ $FAST -eq 0 ]]; then
    step "simbench: 100 zombies + 2 players"
    dotnet run -c Release --project tools/simbench -- --zombies 100 --seconds 30
fi

printf '\n\033[1;32mAll checks passed.\033[0m\n'
