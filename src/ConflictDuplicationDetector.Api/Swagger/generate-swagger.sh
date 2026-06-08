#!/usr/bin/env bash
set -euo pipefail

# ── Configuration ──────────────────────────────────────────────
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
API_PROJECT="$REPO_ROOT/src/ConflictDuplicationDetector.Api"
PORT=5080
BASE_URL="http://localhost:$PORT"
SWAGGER_JSON_URL="$BASE_URL/swagger/v1/swagger.json"
MAX_WAIT=30  # seconds to wait for the API to start

# ── Start the API in the background ───────────────────────────
echo "Starting API on port $PORT..."
ASPNETCORE_ENVIRONMENT=Development dotnet run --project "$API_PROJECT" --no-launch-profile --urls "$BASE_URL" &
API_PID=$!

cleanup() {
    echo "Stopping API (PID $API_PID)..."
    kill "$API_PID" 2>/dev/null || true
    wait "$API_PID" 2>/dev/null || true
}
trap cleanup EXIT

# ── Wait for the API to be ready ──────────────────────────────
echo "Waiting for API to be ready..."
elapsed=0
until curl -sf "$BASE_URL/api/health" > /dev/null 2>&1; do
    sleep 1
    elapsed=$((elapsed + 1))
    if [ "$elapsed" -ge "$MAX_WAIT" ]; then
        echo "ERROR: API did not start within ${MAX_WAIT}s" >&2
        exit 1
    fi
done
echo "API is ready (took ${elapsed}s)."

# ── Fetch swagger.json ────────────────────────────────────────
echo "Fetching swagger.json..."
curl -sf "$SWAGGER_JSON_URL" | python3 -m json.tool > "$REPO_ROOT/swagger.json"
echo "Wrote $REPO_ROOT/swagger.json"

# ── Generate self-contained swagger.html ──────────────────────
echo "Generating swagger.html..."
cat > "$REPO_ROOT/swagger.html" <<'HTML'
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Conflict & Duplication Detector — Swagger UI</title>
    <link rel="stylesheet" href="https://unpkg.com/swagger-ui-dist@5/swagger-ui.css">
</head>
<body>
    <div id="swagger-ui"></div>
    <script src="https://unpkg.com/swagger-ui-dist@5/swagger-ui-bundle.js"></script>
    <script>
HTML

# Embed the spec as a JS variable
printf '    const spec = ' >> "$REPO_ROOT/swagger.html"
cat "$REPO_ROOT/swagger.json" >> "$REPO_ROOT/swagger.html"
printf ';\n' >> "$REPO_ROOT/swagger.html"

cat >> "$REPO_ROOT/swagger.html" <<'HTML'
    SwaggerUIBundle({
        spec: spec,
        dom_id: '#swagger-ui',
        presets: [SwaggerUIBundle.presets.apis],
        layout: "BaseLayout"
    });
    </script>
</body>
</html>
HTML

echo "Wrote $REPO_ROOT/swagger.html"
echo "Done."
