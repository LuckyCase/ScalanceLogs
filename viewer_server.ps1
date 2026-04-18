# =============================================================
#  viewer_server.ps1
#  HTTP server for Scalance log viewer
#  Run:  powershell -ExecutionPolicy Bypass -File viewer_server.ps1
#  Open: http://localhost:8080
# =============================================================

. "$PSScriptRoot\config.ps1"

$VIEWER_PORT = 8080
$SCRIPT_DIR  = $PSScriptRoot
$SERVER_LOG  = Join-Path $LOG_DIR "viewer_server.log"

# =============================================================

New-Item -ItemType Directory -Force -Path $LOG_DIR | Out-Null

function Write-ServerLog($level, $msg) {
    $ts   = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $line = "$ts [$level] $msg"
    Add-Content -Path $SERVER_LOG -Value $line -Encoding UTF8
    $color = switch ($level) {
        "ERROR" { "Red"    }
        "WARN"  { "Yellow" }
        default { "Gray"   }
    }
    Write-Host $line -ForegroundColor $color
}

$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add("http://localhost:$VIEWER_PORT/")
$listener.Prefixes.Add("http://127.0.0.1:$VIEWER_PORT/")

try {
    $listener.Start()
} catch {
    Write-ServerLog "ERROR" "Failed to start HTTP server on port $VIEWER_PORT - $_"
    exit 1
}

Write-ServerLog "INFO" "Server started on port $VIEWER_PORT  |  log dir: $LOG_DIR"
Write-Host "Open: http://localhost:$VIEWER_PORT"
Write-Host "Server log: $SERVER_LOG"
Write-Host "Press Ctrl+C to stop"

function Send-Response($ctx, $content, $mime, $code = 200) {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($content)
    $ctx.Response.StatusCode = $code
    $ctx.Response.ContentType = "$mime; charset=utf-8"
    $ctx.Response.ContentLength64 = $bytes.Length
    $ctx.Response.OutputStream.Write($bytes, 0, $bytes.Length)
    $ctx.Response.OutputStream.Close()
}

Add-Type -AssemblyName System.Web

try {
    while ($listener.IsListening) {
        $ctx = $listener.GetContext()
        try {
            $req  = $ctx.Request
            $path = $req.Url.AbsolutePath

            # -- GET / -> serve viewer.html --------------------
            if ($path -eq "/" -or $path -eq "/index.html") {
                $htmlPath = Join-Path $SCRIPT_DIR "viewer.html"
                if (Test-Path $htmlPath) {
                    Send-Response $ctx (Get-Content $htmlPath -Raw -Encoding UTF8) "text/html"
                    Write-ServerLog "INFO" "GET $path -> 200"
                } else {
                    Send-Response $ctx "viewer.html not found" "text/plain" 404
                    Write-ServerLog "WARN" "GET $path -> 404 (viewer.html missing)"
                }
            }

            # -- GET /api/files -> list of .log files ----------
            elseif ($path -eq "/api/files") {
                $files = @()
                if (Test-Path $LOG_DIR) {
                    $files = @(Get-ChildItem -Path $LOG_DIR -Filter "*.log" |
                        Where-Object { $_.Name -notin @("viewer_server.log", "unknown_sources.log") } |
                        Sort-Object Name |
                        Select-Object -ExpandProperty Name)
                }
                $json = if ($files.Count -eq 0) { "[]" }
                        elseif ($files.Count -eq 1) { "[$(($files[0] | ConvertTo-Json))]" }
                        else { $files | ConvertTo-Json -Compress }
                Send-Response $ctx $json "application/json"
            }

            # -- GET /api/log?file=...&lines=...&search=... ----
            elseif ($path -eq "/api/log") {
                $query    = [System.Web.HttpUtility]::ParseQueryString($req.Url.Query)
                $filename = if ($query["file"]) { $query["file"] } else { "events.log" }
                $maxLines = if ($query["lines"]) { [int]$query["lines"] } else { 200 }
                $search   = if ($query["search"]) { $query["search"].ToLower() } else { "" }

                $safeFile = [System.IO.Path]::GetFileName($filename)
                $fullPath = Join-Path $LOG_DIR $safeFile

                if (Test-Path $fullPath) {
                    $fs      = [System.IO.File]::Open($fullPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
                    $sr      = New-Object System.IO.StreamReader($fs, [System.Text.Encoding]::UTF8)
                    $content = $sr.ReadToEnd()
                    $sr.Close(); $fs.Close()
                    $allLines = @($content -split "`r?`n" | Where-Object { $_ -ne "" })
                    $total    = $allLines.Count
                    if ($search) {
                        $allLines = @($allLines | Where-Object { $_.ToLower().Contains($search) })
                    }
                    $slice = @($allLines | Select-Object -Last $maxLines)
                    $linesJson = if ($slice.Count -eq 0) { "[]" }
                                 elseif ($slice.Count -eq 1) { "[$(($slice[0] | ConvertTo-Json))]" }
                                 else { $slice | ConvertTo-Json -Compress }
                    $json = "{""file"":$(($safeFile | ConvertTo-Json)),""total"":$total,""lines"":$linesJson}"
                } else {
                    $json = "{""file"":$(($safeFile | ConvertTo-Json)),""total"":0,""lines"":[],""error"":""File not found: $safeFile""}"
                    Write-ServerLog "WARN" "GET /api/log -> file not found: $safeFile"
                }

                Send-Response $ctx $json "application/json"
            }

            else {
                Send-Response $ctx "Not found" "text/plain" 404
                Write-ServerLog "WARN" "GET $path -> 404"
            }
        } catch {
            $errMsg = $_
            try { $ctx.Response.StatusCode = 500; $ctx.Response.OutputStream.Close() } catch {}
            try { Write-ServerLog "ERROR" "Request failed [$path]: $errMsg" } catch {
                Write-Host "$(Get-Date -Format 'HH:mm:ss') [ERROR] $path : $errMsg" -ForegroundColor Red
            }
        }
    }
} finally {
    $listener.Stop()
    Write-ServerLog "INFO" "Server stopped"
}
