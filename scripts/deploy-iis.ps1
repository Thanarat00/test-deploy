[CmdletBinding()]
param(
    [string]$PublishPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts/publish'),
    [string]$DeployPath = 'C:\inetpub\wwwroot\test-deploy',
    [string]$AppPoolName = '',
    [string]$BackupRoot = 'C:\inetpub\deploy-backups\test-deploy',
    [string]$HealthCheckUrl = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-FullPath([string]$Path) {
    [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($Path)).TrimEnd('\', '/')
}

function Test-WithinPath([string]$Path, [string]$Parent) {
    $Path.Equals($Parent, [StringComparison]::OrdinalIgnoreCase) -or
        $Path.StartsWith($Parent + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
}

function Wait-AppPool([string]$State) {
    $deadline = [DateTime]::UtcNow.AddSeconds(60)
    while ((Get-WebAppPoolState -Name $AppPoolName).Value -ne $State) {
        if ([DateTime]::UtcNow -ge $deadline) { throw "App Pool '$AppPoolName' did not reach $State within 60 seconds." }
        Start-Sleep -Milliseconds 500
    }
}

function Stop-DeploymentPool {
    if ((Get-WebAppPoolState -Name $AppPoolName).Value -ne 'Stopped') {
        Stop-WebAppPool -Name $AppPoolName
    }
    Wait-AppPool 'Stopped'
}

function Start-DeploymentPool {
    if ((Get-WebAppPoolState -Name $AppPoolName).Value -ne 'Started') {
        Start-WebAppPool -Name $AppPoolName
    }
    Wait-AppPool 'Started'
}

function Copy-DeploymentFiles([string]$Source, [string]$Destination, [switch]$PreserveSettings) {
    # /MIR removes obsolete application files. These server-owned paths are excluded.
    $copyArguments = @($Source, $Destination, '/MIR', '/XJ', '/R:2', '/W:2', '/NP', '/NFL', '/NDL',
        '/XD', '.git', 'logs', 'uploads', 'App_Data', '/XF', 'app_offline.htm')
    if ($PreserveSettings) { $copyArguments += 'appsettings*.json' }
    & robocopy.exe @copyArguments | Out-Host
    # Robocopy exit codes 0-7 are successful; 8 and above are failures.
    if ($LASTEXITCODE -ge 8) { throw "Robocopy failed with exit code $LASTEXITCODE ($Source -> $Destination)." }
}

function Test-DeploymentHealth {
    if (-not $HealthCheckUrl) { return }
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri $HealthCheckUrl -UseBasicParsing -TimeoutSec 15
            if ($response.StatusCode -eq 200) { return }
            throw "Unexpected HTTP status $($response.StatusCode)."
        }
        catch {
            if ($attempt -eq 5) { throw "Health check failed at ${HealthCheckUrl}: $($_.Exception.Message)" }
            Start-Sleep -Seconds 2
        }
    }
}

$publishFullPath = Get-FullPath $PublishPath
$deployFullPath = Get-FullPath $DeployPath
$backupFullPath = Get-FullPath $BackupRoot
# This script is intentionally scoped to the deployment destination requested for this project.
if ($deployFullPath -ine 'C:\inetpub\wwwroot\test-deploy') {
    throw 'DeployPath must be C:\inetpub\wwwroot\test-deploy.'
}
foreach ($pair in @(@($publishFullPath, $deployFullPath), @($backupFullPath, $deployFullPath), @($publishFullPath, $backupFullPath))) {
    if ((Test-WithinPath $pair[0] $pair[1]) -or (Test-WithinPath $pair[1] $pair[0])) {
        throw 'Publish, deployment, and backup directories must not contain each other.'
    }
}
foreach ($requiredFile in @('EmployeeManagement.dll', 'EmployeeManagement.runtimeconfig.json', 'web.config')) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishFullPath $requiredFile) -PathType Leaf)) {
        throw "Publish output is incomplete: missing $requiredFile. Run dotnet publish first."
    }
}
if (-not (Test-Path -LiteralPath $deployFullPath -PathType Container)) {
    throw "Create the IIS site and deployment directory first: $deployFullPath"
}
# Reject junctions/reparse points before a mirror copy can remove or overwrite anything.
foreach ($path in @($deployFullPath, $publishFullPath, $backupFullPath)) {
    $cursor = $path
    while ($cursor) {
        if ((Test-Path -LiteralPath $cursor) -and
            ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Reparse points are not supported in deployment paths: $cursor"
        }
        $cursor = Split-Path -Parent $cursor
    }
}
$offlineFile = Join-Path $deployFullPath 'app_offline.htm'
if (Test-Path -LiteralPath $offlineFile) { throw 'An existing app_offline.htm indicates maintenance or another deployment. Resolve it first.' }
if ($HealthCheckUrl -and $HealthCheckUrl -notmatch '^https?://') { throw 'HealthCheckUrl must use http:// or https://.' }

Import-Module WebAdministration -ErrorAction Stop
$sites = @(Get-Website)
$matchingSites = @($sites | Where-Object { (Get-FullPath $_.PhysicalPath) -ieq $deployFullPath })
if ($matchingSites.Count -ne 1) { throw "Expected exactly one IIS site pointing to $deployFullPath; found $($matchingSites.Count)." }
$site = $matchingSites[0]
if ($site.State -ne 'Started') { throw "IIS site '$($site.Name)' is stopped. Start the site before deploying." }
if ($AppPoolName -and $AppPoolName -ine $site.ApplicationPool) { throw 'AppPoolName does not match the IIS site application pool.' }
$AppPoolName = $site.ApplicationPool
$poolUsers = @($sites | Where-Object { $_.ApplicationPool -ieq $AppPoolName }) +
    @(Get-WebApplication | Where-Object { $_.ApplicationPool -ieq $AppPoolName })
if ($poolUsers.Count -ne 1) { throw "App Pool '$AppPoolName' is shared. Assign a dedicated App Pool to this site first." }
if ((Get-WebAppPoolState -Name $AppPoolName).Value -ne 'Started') { throw "App Pool '$AppPoolName' must be started before deployment." }

# A machine-wide lock also prevents two different Jenkins jobs from deploying simultaneously.
$deploymentLock = New-Object Threading.Mutex($false, 'Global\EmployeeManagement-test-deploy')
$lockTaken = $false
$backupReady = $false
$copyStarted = $false
$offlineCreated = $false
$createdSettings = New-Object 'Collections.Generic.List[string]'
$backupPath = Join-Path $backupFullPath ((Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N'))
try {
    try { $lockTaken = $deploymentLock.WaitOne(0) }
    catch [Threading.AbandonedMutexException] { $lockTaken = $true }
    if (-not $lockTaken) { throw 'Another deployment is running for test-deploy.' }
    # Check again under the lock, since another run may have entered maintenance meanwhile.
    if (Test-Path -LiteralPath $offlineFile) { throw 'The site is already in maintenance.' }
    Write-Host "Deploying to IIS site '$($site.Name)', App Pool '$AppPoolName'."
    Set-Content -LiteralPath $offlineFile -Value '<h1>Deployment in progress</h1>' -Encoding UTF8
    $offlineCreated = $true
    Stop-DeploymentPool
    New-Item -ItemType Directory -Path $backupPath -Force | Out-Null
    Copy-DeploymentFiles $deployFullPath $backupPath
    $backupReady = $true
    Write-Host "Backup: $backupPath"
    $copyStarted = $true
    Copy-DeploymentFiles $publishFullPath $deployFullPath -PreserveSettings
    # Supply default settings on first deployment without replacing server configuration.
    Get-ChildItem -LiteralPath $publishFullPath -Filter 'appsettings*.json' -File | ForEach-Object {
        $settingsTarget = Join-Path $deployFullPath $_.Name
        if (-not (Test-Path -LiteralPath $settingsTarget)) {
            $createdSettings.Add($settingsTarget)
            Copy-Item -LiteralPath $_.FullName -Destination $settingsTarget
        }
    }
    Remove-Item -LiteralPath $offlineFile -Force
    $offlineCreated = $false
    Start-DeploymentPool
    Test-DeploymentHealth
    Write-Host "Deployment complete. Backup retained at $backupPath"
}
catch {
    $deploymentError = $_
    if ($copyStarted -and $backupReady) {
        Write-Warning "Deployment failed. Restoring backup: $backupPath"
        try {
            Set-Content -LiteralPath $offlineFile -Value '<h1>Restoring previous version</h1>' -Encoding UTF8
            $offlineCreated = $true
            Stop-DeploymentPool
            Copy-DeploymentFiles $backupPath $deployFullPath -PreserveSettings
            foreach ($settingsFile in $createdSettings) {
                if (Test-Path -LiteralPath $settingsFile) { Remove-Item -LiteralPath $settingsFile -Force }
            }
        }
        catch {
            # Keep maintenance enabled if rollback is incomplete; do not serve mixed files.
            $offlineCreated = $false
            throw "Deployment and rollback failed. Site remains offline. Restore $backupPath manually. $($_.Exception.Message)"
        }
    }
    throw $deploymentError
}
finally {
    try {
        if ($offlineCreated) {
            Remove-Item -LiteralPath $offlineFile -Force
            Start-DeploymentPool
        }
    }
    finally {
        if ($lockTaken) { $deploymentLock.ReleaseMutex() }
        $deploymentLock.Dispose()
    }
}
# Robocopy uses nonzero success codes. Jenkins must see zero after a successful deployment.
$global:LASTEXITCODE = 0
