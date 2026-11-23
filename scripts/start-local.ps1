param(
    [switch]$ConfigureOnly,
    [switch]$SkipRun,
    [string]$MySqlBin = 'C:/xampp/mysql/bin/mysql.exe',
    [string]$DatabaseName = 'EmployeeManagementDb'
)

$ErrorActionPreference = 'Stop'
$workspacePath = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $workspacePath 'EmployeeManagement'
$localSettingsPath = Join-Path $projectPath 'appsettings.Local.json'

if (-not (Test-Path -LiteralPath $MySqlBin -PathType Leaf)) {
    throw 'MySQL client not found. Pass -MySqlBin with the path to mysql.exe.'
}
if ($DatabaseName -notmatch '^[A-Za-z][A-Za-z0-9_]{0,63}$') {
    throw 'DatabaseName must contain only ASCII letters, numbers and underscores (maximum 64 characters).'
}

function Invoke-LocalSql {
    param([string]$Sql, [string]$User = 'root', [string]$Password = '')
    $previousPassword = $env:MYSQL_PWD
    $previousErrorPreference = $ErrorActionPreference
    try {
        $arguments = @('--no-defaults', '--protocol=TCP', '--host=127.0.0.1', '--port=3306',
            '--connect-timeout=5', '--default-character-set=utf8mb4', '--batch', '--skip-column-names', "--user=$User")
        if ($Password.Length -eq 0) {
            $arguments += '--skip-password'
            Remove-Item Env:MYSQL_PWD -ErrorAction SilentlyContinue
        }
        else { $env:MYSQL_PWD = $Password }
        # SQL and credentials never appear in process arguments or console output.
        $ErrorActionPreference = 'Continue'
        $result = $Sql | & $MySqlBin @arguments 2>&1
        $exitCode = $LASTEXITCODE
        $ErrorActionPreference = 'Stop'
        if ($exitCode -ne 0) {
            throw "Database command failed (exit $exitCode). Check that XAMPP MySQL is running and the local account can connect."
        }
        return $result
    }
    finally {
        $ErrorActionPreference = $previousErrorPreference
        if ($null -eq $previousPassword) { Remove-Item Env:MYSQL_PWD -ErrorAction SilentlyContinue }
        else { $env:MYSQL_PWD = $previousPassword }
    }
}

function Stop-LocalApplication {
    $outputDirectory = [System.IO.Path]::GetFullPath((Join-Path $projectPath 'bin')) + [System.IO.Path]::DirectorySeparatorChar
    foreach ($runningApp in @(Get-Process -Name EmployeeManagement -ErrorAction SilentlyContinue)) {
        $executablePath = $runningApp.Path
        if (-not $executablePath -or -not $executablePath.StartsWith($outputDirectory, [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }
        Write-Host "Stopping this project's running application (PID $($runningApp.Id)) before building..."
        try {
            Stop-Process -InputObject $runningApp -ErrorAction Stop
            if (-not $runningApp.WaitForExit(5000)) { throw 'The application did not exit.' }
        }
        catch {
            throw 'Could not stop the running application. Press Ctrl+C in the terminal that started EmployeeManagement, then run this script again.'
        }
    }
}

Push-Location $workspacePath
try {
    $serverVersionText = [string](Invoke-LocalSql -Sql 'SELECT VERSION();')
    $serverProvider = if ($serverVersionText -match 'MariaDB') { 'MariaDb' } else { 'MySql' }
    if ($serverVersionText -notmatch '(\d+\.\d+\.\d+)') { throw 'Could not identify the database server version.' }
    $serverVersion = $Matches[1]
    Write-Host "Detected $serverProvider $serverVersion at 127.0.0.1:3306."

    if (Test-Path -LiteralPath $localSettingsPath) {
        $localSettings = Get-Content -LiteralPath $localSettingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $connectionValues = New-Object System.Data.Common.DbConnectionStringBuilder
        # PowerShell treats this builder as a dictionary; call its property methods explicitly.
        $connectionValues.set_ConnectionString($localSettings.ConnectionStrings.DefaultConnection)
        if ($connectionValues.get_Item('Database') -ne $DatabaseName) {
            throw 'The existing local settings target another database. Pass the matching -DatabaseName or edit your local settings.'
        }
        $null = Invoke-LocalSql -Sql "USE $DatabaseName; SELECT 1;" -User ([string]$connectionValues.get_Item('User ID')) -Password ([string]$connectionValues.get_Item('Password'))
        Write-Host 'Reusing the existing local application account and settings.'
    }
    else {
        $userName = 'employee_local_' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
        $randomBytes = New-Object byte[] 32
        $randomGenerator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
        try { $randomGenerator.GetBytes($randomBytes) }
        finally { $randomGenerator.Dispose() }
        $applicationPassword = [Convert]::ToBase64String($randomBytes)
        # Only the database and a dedicated account are provisioned here.
        # All application tables and indexes are created exclusively by EF migrations.
        $setupSql = @"
CREATE DATABASE IF NOT EXISTS $DatabaseName CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER '$userName'@'localhost' IDENTIFIED BY '$applicationPassword';
GRANT ALL PRIVILEGES ON $DatabaseName.* TO '$userName'@'localhost';
"@
        $null = Invoke-LocalSql -Sql $setupSql
        $null = Invoke-LocalSql -Sql "USE $DatabaseName; SELECT 1;" -User $userName -Password $applicationPassword
        $localSettings = @{
            ConnectionStrings = @{
                DefaultConnection = "Server=127.0.0.1;Port=3306;Database=$DatabaseName;User ID=$userName;Password=$applicationPassword;CharSet=utf8mb4;DateTimeKind=Utc"
            }
            Database = @{ Provider = $serverProvider; ServerVersion = $serverVersion }
        }
        $localSettings | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $localSettingsPath -Encoding UTF8
        Write-Host 'Created the database and a dedicated application account. Local credentials are saved in the ignored appsettings.Local.json.'
    }
    if ($ConfigureOnly) { return }

    $env:DOTNET_CLI_HOME = Join-Path $workspacePath '.local/dotnet'
    $env:NUGET_PACKAGES = Join-Path $workspacePath '.local/packages'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_ROLL_FORWARD = 'Major'
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ConnectionStrings__DefaultConnection = $localSettings.ConnectionStrings.DefaultConnection
    $env:Database__Provider = $localSettings.Database.Provider
    $env:Database__ServerVersion = $localSettings.Database.ServerVersion

    Write-Host 'Restoring NuGet packages and EF tools...'
    dotnet restore EmployeeManagement.sln
    if ($LASTEXITCODE -ne 0) { throw 'NuGet restore failed. Run this script from a normal PowerShell window with access to api.nuget.org.' }
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'EF tool restore failed.' }
    Stop-LocalApplication
    dotnet build EmployeeManagement.sln --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

    Push-Location $projectPath
    try {
        dotnet ef migrations has-pending-model-changes --no-build
        if ($LASTEXITCODE -ne 0) { throw 'Model and migration snapshot differ. Resolve the migration before updating the database.' }
        dotnet ef database update --no-build
        if ($LASTEXITCODE -ne 0) { throw 'Database migration failed.' }
        if (-not $SkipRun) {
            Write-Host 'Open http://localhost:5034 (Ctrl+C stops the application).'
            dotnet run --no-build --launch-profile http
            if ($LASTEXITCODE -ne 0) { throw 'Application stopped with an error.' }
        }
    }
    finally { Pop-Location }
}
finally { Pop-Location }
