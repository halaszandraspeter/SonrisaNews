# scripts/dev.ps1 — Sonrisa News
# One-command start. Native processes only. No Docker.
# Usage: .\scripts\dev.ps1 [-Reset]

[CmdletBinding()]
param(
    [switch]$Reset
)

$ErrorActionPreference = 'Stop'

function Write-Cyan   { param($msg) Write-Host $msg -ForegroundColor Cyan }
function Write-Green  { param($msg) Write-Host $msg -ForegroundColor Green }
function Write-Yellow { param($msg) Write-Host $msg -ForegroundColor Yellow }
function Write-Red    { param($msg) Write-Host $msg -ForegroundColor Red }

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

function Test-Command {
    param($name)
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        Write-Red "✗ $name not found. Install it and re-run."
        Write-Red "  See docs/roadmap/2-stack.md §9.2 for installation links."
        exit 1
    } else {
        Write-Green "✓ $name"
    }
}

Write-Cyan "==> Checking prerequisites"
foreach ($cmd in @('dotnet', 'node', 'pnpm', 'python', 'uv')) {
    Test-Command $cmd
}
# MailHog on Windows is `MailHog.exe`; check both spellings.
if (-not (Get-Command 'mailhog' -ErrorAction SilentlyContinue) -and `
    -not (Get-Command 'MailHog' -ErrorAction SilentlyContinue) -and `
    -not (Get-Command 'MailHog.exe' -ErrorAction SilentlyContinue)) {
    Write-Red "✗ mailhog not found in PATH. Download from https://github.com/mailhog/MailHog/releases"
    exit 1
}
Write-Green "✓ mailhog"

Write-Cyan "==> Configuring Git hooks"
git config core.hooksPath .githooks
Write-Green "✓ Git hooks installed"

Write-Cyan "==> Starting MailHog"
$mailhog = Get-Process -Name 'MailHog' -ErrorAction SilentlyContinue
if ($mailhog) {
    Write-Yellow "  MailHog already running"
} else {
    Start-Process -FilePath 'mailhog' -RedirectStandardOutput 'C:\Temp\mailhog.log' -RedirectStandardError 'C:\Temp\mailhog.err' -WindowStyle Hidden
    Start-Sleep -Seconds 2
    Write-Green "✓ MailHog started (SMTP :1025, UI :8025)"
}

Write-Cyan "==> Starting yfinance sidecar"
$sidecar = Get-Process -Name 'uvicorn' -ErrorAction SilentlyContinue
if ($sidecar) {
    Write-Yellow "  yfinance sidecar already running"
} else {
    Push-Location services/yfinance
    Start-Process -FilePath 'uv' -ArgumentList 'run','uvicorn','yfinance_service.main:app','--port','8001' -RedirectStandardOutput 'C:\Temp\yfinance.log' -RedirectStandardError 'C:\Temp\yfinance.err' -WindowStyle Hidden
    Pop-Location
    Start-Sleep -Seconds 3
    Write-Green "✓ yfinance sidecar started (:8001)"
}

Write-Cyan "==> Running EF migrations"
New-Item -ItemType Directory -Force -Path 'data' | Out-Null
dotnet ef database update --project backend/src/SonrisaNews.Infrastructure 2>&1 | Select-Object -Last 5
Write-Green "✓ DB migrated"

if ($Reset) {
    Write-Yellow "  --Reset: DB rebuilt from scratch"
}

Write-Cyan "==> Starting .NET AppHost (Api + Worker)"
$apphost = Get-Process -Name 'SonrisaNews.AppHost' -ErrorAction SilentlyContinue
if ($apphost) {
    Write-Yellow "  AppHost already running"
} else {
    Start-Process -FilePath 'dotnet' -ArgumentList 'run','--project','backend/SonrisaNews.AppHost' -RedirectStandardOutput 'C:\Temp\apphost.log' -RedirectStandardError 'C:\Temp\apphost.err' -WindowStyle Hidden
    Start-Sleep -Seconds 10
    Write-Green "✓ AppHost started (Aspire dashboard :15000, Api :5080)"
}

Write-Cyan "==> Starting Next.js dev server"
$next = Get-CimInstance MSFT_Process -Filter "Name = 'node.exe'" -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -like '*next*dev*' }
if ($next) {
    Write-Yellow "  Next.js dev server already running"
} else {
    Push-Location web
    Start-Process -FilePath 'pnpm' -ArgumentList 'dev' -RedirectStandardOutput 'C:\Temp\nextdev.log' -RedirectStandardError 'C:\Temp\nextdev.err' -WindowStyle Hidden
    Pop-Location
    Start-Sleep -Seconds 6
    Write-Green "✓ Next.js dev server started (:3000)"
}

Write-Cyan ""
Write-Cyan "==> Sonrisa News dev environment is up"
Write-Host ""
Write-Host "  App:           http://localhost:3000"
Write-Host "  API:           http://localhost:5080"
Write-Host "  Scalar (API):  http://localhost:5080/scalar/v1"
Write-Host "  Aspire dash:   http://localhost:15000"
Write-Host "  MailHog:       http://localhost:8025"
Write-Host "  yfinance:      http://localhost:8001/docs"
Write-Host ""
Write-Green "Happy alerting!"
