#Requires -Version 5.1
<#
.SYNOPSIS
    Starts (or stops) the infrastructure GlobalPay Bridge needs so the API can be debugged from Visual Studio.

.DESCRIPTION
    Runs SQL Server, Redis, Azurite, Keycloak, the payment provider simulator and (optionally) the
    Functions host in Docker, leaving the API and the provider simulator free to run under the debugger.

    Use -Stop to stop just these services, and -WithoutFunctions to run the import/outbox workers
    outside Docker (for example from Visual Studio with the Azure development workload installed).

.EXAMPLE
    .\scripts\start-local-dependencies.ps1
    Starts every dependency and waits for SQL Server to become healthy.

.EXAMPLE
    .\scripts\start-local-dependencies.ps1 -WithoutFunctions
    Starts dependencies only; run the Functions project from Visual Studio instead.

.EXAMPLE
    .\scripts\start-local-dependencies.ps1 -Stop
    Stops the dependency services without removing data volumes.
#>
[CmdletBinding()]
param(
    [switch]$Stop,

    [switch]$WithoutFunctions,

    [int]$SqlTimeoutSeconds = 150
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot

$services = @('sqlserver', 'redis', 'azurite', 'keycloak', 'provider-simulator')

if (-not $WithoutFunctions) {
    $services += 'functions'
}

Push-Location $repoRoot

try {
    if ($Stop) {
        docker compose stop @services

        Write-Host "Stopped: $($services -join ', ')" -ForegroundColor Yellow

        return
    }

    Write-Host "Starting: $($services -join ', ')" -ForegroundColor Cyan
    docker compose up -d @services

    Write-Host 'Waiting for SQL Server to report healthy...' -ForegroundColor Cyan

    $containerId = (docker compose ps -q sqlserver)
    $deadline = (Get-Date).AddSeconds($SqlTimeoutSeconds)
    $status = 'unknown'

    while ((Get-Date) -lt $deadline) {
        if ($containerId) {
            $status = (docker inspect --format '{{.State.Health.Status}}' $containerId 2>$null)

            if ($status -eq 'healthy') {
                break
            }
        }

        Start-Sleep -Seconds 3
    }

    if ($status -eq 'healthy') {
        Write-Host 'SQL Server is healthy.' -ForegroundColor Green
    }
    else {
        Write-Warning "SQL Server did not report healthy within $SqlTimeoutSeconds seconds (last status: $status)."
    }

    Write-Host ''
    docker compose ps
    Write-Host ''
    Write-Host 'Dependencies are up. In Visual Studio:' -ForegroundColor Green
    Write-Host '  1. Apply migrations once (see docs/development/visual-studio.md).'
    Write-Host '  2. Set GlobalPay.Api as the startup project and press F5 (http://localhost:5080).'
    Write-Host '  3. Interactive API reference: http://localhost:5080/scalar/v1'
}
finally {
    Pop-Location
}
