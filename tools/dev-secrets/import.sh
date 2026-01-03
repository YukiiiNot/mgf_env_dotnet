#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$ROOT/../.." && pwd)"
PROJECT="$REPO_ROOT/src/DevTools/MGF.DevSecretsCli/MGF.DevSecretsCli.csproj"
REQUIRED="$REPO_ROOT/tools/dev-secrets/secrets.required.json"

if [[ ! -f "$PROJECT" ]]; then
  echo "MGF.DevSecretsCli project not found at $PROJECT" >&2
  exit 1
fi

(cd "$REPO_ROOT" && dotnet run --project "$PROJECT" -- import --required "$REQUIRED" "$@")

