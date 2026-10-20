#!/usr/bin/env pwsh
<#
Start the review server in its own window unless it is already running, then build in review mode.

    review
#>

$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    if (Get-NetTCPConnection -LocalPort 5501 -State Listen -ErrorAction SilentlyContinue) {
        "review server already running; it picks up the new build itself"
    }
    else {
        Start-Process python "Tools/review-server.py" -WorkingDirectory $root
        "review server started on http://127.0.0.1:5501"
    }

    dotnet run -c Release -- review
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
