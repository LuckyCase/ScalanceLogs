# =============================================================
#  viewer_server.ps1
#  HTTP server for Scalance log viewer
#  Run:  powershell -ExecutionPolicy Bypass -File viewer_server.ps1
#  Open: http://localhost:8080
# =============================================================

. "$PSScriptRoot\config.ps1"

$VIEWER_PORT = 8080
$SCRIPT_DIR  = $PSScriptRoot

# =============================================================

$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add("http://localhost:$VIEWER_PORT/")
$listener.Prefixes.Add("http://127.0.0.1:$VIEWER_PORT/")

try {
    $listener.Start()
} catch {
    Write-Host "ERROR: failed to start HTTP server on port $VIEWER_PORT" -ForegroundColor Red
    exit 1
}

Write-Host "Scalance Log Viewer  ->  http://localhost:$VIEWER_PORT"
Write-Host "Log dir: $LOG_DIR"
Write-Host "Press Ctrl+C to stop"

function Send-Response($ctx, $content, $mime, $code = 200) {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($content)
    $ctx.Response.StatusCode = $code
    $ctx.Response.ContentType = "$mime; charset=utf-8"
    $ctx.Response.ContentLength64 = $bytes.Length
    $ctx.Response.OutputStream.Write($bytes, 0, $bytes.Length)
    $ctx.Response.OutputStream.Close()
}

function Get-QueryParam($url, $param) {
    $uri = [System.Uri]$url
    $query = [System.Web.HttpUtility]::ParseQueryString($uri.Query)
    return $query[$param]
}

Add-Type -AssemblyName System.Web

try {
    while ($listener.IsListening) {
        $ctx = $listener.GetContext()
        $req = $ctx.Request
        $path = $req.Url.AbsolutePath

        # ── GET / → serve viewer.html ─────────────────────────
        if ($path -eq "/" -or $path -eq "/index.html") {
            $htmlPath = Join-Path $SCRIPT_DIR "viewer.html"
            if (Test-Path $htmlPath) {
                $html = Get-Content $htmlPath -Raw -Encoding UTF8
                Send-Response $ctx $html "text/html"
            } else {
                Send-Response $ctx "viewer.html not found next to the script" "text/plain" 404
            }
        }

        # ── GET /api/files → list of .log files ───────────────
        elseif ($path -eq "/api/files") {
            $files = @()
            if (Test-Path $LOG_DIR) {
                $files = Get-ChildItem -Path $LOG_DIR -Filter "*.log" |
                    Sort-Object Name |
                    Select-Object -ExpandProperty Name
            }
            $json = $files | ConvertTo-Json -Compress
            if ($files.Count -eq 0) { $json = "[]" }
            Send-Response $ctx $json "application/json"
        }

        # ── GET /api/log?file=...&lines=...&search=... ────────
        elseif ($path -eq "/api/log") {
            $query    = [System.Web.HttpUtility]::ParseQueryString($req.Url.Query)
            $filename = $query["file"]
            if (-not $filename) { $filename = "events.log" }
            $maxLines = 200
            if ($query["lines"]) { $maxLines = [int]$query["lines"] }
            $search = ""
            if ($query["search"]) { $search = $query["search"].ToLower() }

            # Path traversal protection
            $safeFile = [System.IO.Path]::GetFileName($filename)
            $fullPath = Join-Path $LOG_DIR $safeFile

            $result = @{ file = $safeFile; lines = @(); total = 0 }

            if (Test-Path $fullPath) {
                $allLines = [string[]](Get-Content $fullPath -Encoding UTF8 -ErrorAction SilentlyContinue)
                if ($null -eq $allLines) { $allLines = @() }
                if ($allLines -isnot [array]) { $allLines = @($allLines) }

                $result.total = $allLines.Count

                if ($search) {
                    $allLines = $allLines | Where-Object { $_.ToLower().Contains($search) }
                }

                $result.lines = [string[]]($allLines |
                    Select-Object -Last $maxLines)
            } else {
                $result.error = "File not found: $safeFile"
            }

            $json = $result | ConvertTo-Json -Compress -Depth 3
            Send-Response $ctx $json "application/json"
        }

        else {
            Send-Response $ctx "Not found" "text/plain" 404
        }
    }
} finally {
    $listener.Stop()
    Write-Host "Stopped."
}
