#!/usr/bin/env pwsh
<#
Build the site, the PDF and the video, stopping the review server if it is running.
With -r, build in review mode instead, then start the review server, hidden, unless it is
already running. It logs to Logs/review-server.log. Either way, serve docs/ afterwards unless
something already does.

    build
    build -r
#>

param([Alias("r")][switch]$Review)

$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location $root
try {
    $server = Get-NetTCPConnection -LocalPort 5501 -State Listen -ErrorAction SilentlyContinue
    if ($Review) {
        dotnet run -c Release -- review
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }

        if ($server) {
            "review server already running; it picks up the new build itself"
        }
        else {
            New-Item -ItemType Directory -Force Logs | Out-Null
            Start-Process dotnet "run", "--file", "Tools/review-server.cs" -WorkingDirectory $root -WindowStyle Hidden `
                -RedirectStandardOutput "Logs/review-server.log" -RedirectStandardError "Logs/review-server-errors.log"
            "review server started on http://127.0.0.1:5501, logging to Logs/review-server.log"
        }
    }
    else {
        if ($server) {
            Stop-Process -Id ($server.OwningProcess | Select-Object -Unique)
            "review server stopped"
        }

        dotnet run -c Release
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
    }

    if (-not (Get-NetTCPConnection -LocalPort 5500 -State Listen -ErrorAction SilentlyContinue)) {
        & "$PSScriptRoot/serve.ps1"
    }
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
