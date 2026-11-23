param([switch]$MySql)
$ErrorActionPreference = 'Stop'
$workspacePath = Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $workspacePath '.local/dotnet'
$env:NUGET_PACKAGES = Join-Path $workspacePath '.local/packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

Push-Location $workspacePath
try {
    dotnet run --project tests/EmployeeManagement.Checks
    if ($LASTEXITCODE -ne 0) { throw 'MVC/validation checks failed.' }
    Get-Content -Raw EmployeeManagement/wwwroot/js/site.js | node --check
    if ($LASTEXITCODE -ne 0) { throw 'JavaScript syntax check failed.' }
    if ($MySql) {
        if (-not $env:EMPLOYEE_TEST_MYSQL) { throw 'Set EMPLOYEE_TEST_MYSQL to a MySQL/MariaDB connection string with CREATE DATABASE permission.' }
        dotnet run --project tests/EmployeeManagement.MySqlChecks
        if ($LASTEXITCODE -ne 0) { throw 'MySQL integration checks failed.' }
    }
}
finally { Pop-Location }
