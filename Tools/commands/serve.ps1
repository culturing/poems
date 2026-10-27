#!/usr/bin/env pwsh
<#
Open docs/ in the browser at http://localhost:5500, first starting the docs server, hidden, unless
something (it, or Live Server) already listens there. It logs to Logs/docs-server.log.

    serve
#>

$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$url = "http://localhost:5500/"

function Listening {
    [bool](Get-NetTCPConnection -LocalPort 5500 -State Listen -ErrorAction SilentlyContinue)
}

if (-not (Listening)) {
    New-Item -ItemType Directory -Force "$root/Logs" | Out-Null
    Start-Process dotnet "run", "--file", "Tools/docs-server.cs" -WorkingDirectory $root -WindowStyle Hidden `
        -RedirectStandardOutput "$root/Logs/docs-server.log" -RedirectStandardError "$root/Logs/docs-server-errors.log"

    # The first run compiles the server, which takes a while
    $deadline = (Get-Date).AddSeconds(60)
    while (-not (Listening) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 250
    }
    if (-not (Listening)) {
        "docs server did not start; see Logs/docs-server-errors.log"
        exit 1
    }
    "docs server started on $url, logging to Logs/docs-server.log"
}

Start-Process $url
