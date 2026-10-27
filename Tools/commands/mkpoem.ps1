#!/usr/bin/env pwsh
<#
Start a new poem in today's folder under Poems/, making the folder if needed, and open it in VS Code.

    mkpoem
#>

$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$today = Get-Date
$folder = Join-Path $root "Poems" $today.ToString("yyyy") $today.ToString("MM") $today.ToString("dd")
New-Item -ItemType Directory -Force $folder | Out-Null

$numbers = Get-ChildItem $folder -Filter "*.md" | Where-Object Name -match '^\d\d ' | ForEach-Object { [int]$_.Name.Substring(0, 2) }
$next = ($numbers | Measure-Object -Maximum).Maximum + 1
$poem = Join-Path $folder ("{0:00} Untitled.md" -f $next)

$date = $today.ToString("dd MMMM yyyy", [cultureinfo]::InvariantCulture)
[IO.File]::WriteAllText($poem, "`r`n$date`r`n`r`n") # The title goes on line 1, as the build reads it
code -r -g "${poem}:1"
$poem
