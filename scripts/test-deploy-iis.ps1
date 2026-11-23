# Exercise the real deployment script with real Robocopy, using fixture directories
# and mocked IIS commands. Never import WebAdministration or touch a live IIS site.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$workspacePath = Split-Path -Parent $PSScriptRoot
$fixtureRoot = Join-Path $workspacePath ('.local/deploy-tests/' + [Guid]::NewGuid().ToString('N'))
$deploymentSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'deploy-iis.ps1') -Raw
$nativeRobocopy = Join-Path $env:SystemRoot 'System32/robocopy.exe'
$checks = 0
$fixtureState = @{}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "FAIL: $Message" }
    $script:checks++
}

function Import-Module { param($Name, $ErrorAction) if ($Name -ne 'WebAdministration') { throw "Unexpected module: $Name" } }
function Get-Website {
    [pscustomobject]@{ Name = 'fixture-site'; PhysicalPath = $fixtureState.Deploy; ApplicationPool = 'fixture-pool'; State = 'Started' }
    if ($fixtureState.SharedPool) {
        [pscustomobject]@{ Name = 'other-site'; PhysicalPath = (Join-Path $fixtureRoot 'other-site'); ApplicationPool = 'fixture-pool'; State = 'Started' }
    }
}
function Get-WebApplication { }
function Get-WebAppPoolState { param($Name) [pscustomobject]@{ Value = $fixtureState.PoolState } }
function Stop-WebAppPool { param($Name) $fixtureState.Stops++; $fixtureState.PoolState = 'Stopped' }
function Start-WebAppPool {
    param($Name)
    $fixtureState.Starts++
    if ($fixtureState.FailStart -and $fixtureState.Starts -eq 1) { throw 'Simulated App Pool startup failure.' }
    $fixtureState.PoolState = 'Started'
}
function Start-Sleep { param($Seconds, $Milliseconds) }
function Invoke-WebRequest {
    param($Uri, [switch]$UseBasicParsing, $TimeoutSec)
    $fixtureState.HealthCalls++
    if ($fixtureState.FailHealth) { throw 'Simulated HTTP 500.' }
    [pscustomobject]@{ StatusCode = 200 }
}
function robocopy.exe {
    $fixtureState.CopyCalls++
    if ($fixtureState.FailCopy -and $fixtureState.CopyCalls -eq 2) {
        Set-Content -LiteralPath (Join-Path $fixtureState.Deploy 'EmployeeManagement.dll') -Value 'partial-copy'
        $global:LASTEXITCODE = 8
        return
    }
    if ($fixtureState.FailBackup -and $fixtureState.CopyCalls -eq 1) {
        $global:LASTEXITCODE = 8
        return
    }
    if ($fixtureState.FailRollback -and $fixtureState.CopyCalls -eq 3) {
        $global:LASTEXITCODE = 8
        return
    }
    & $nativeRobocopy @args | Out-Null
    $global:LASTEXITCODE = $LASTEXITCODE
}

function New-Fixture([string]$Name) {
    $caseRoot = Join-Path $fixtureRoot $Name
    $script:fixtureState = @{
        Deploy = (Join-Path $caseRoot 'site'); Publish = (Join-Path $caseRoot 'publish'); Backups = (Join-Path $caseRoot 'backups')
        PoolState = 'Started'; Stops = 0; Starts = 0; CopyCalls = 0; HealthCalls = 0
        FailCopy = $false; FailBackup = $false; FailStart = $false; FailHealth = $false; FailRollback = $false; SharedPool = $false
    }
    foreach ($directory in @($fixtureState.Deploy, $fixtureState.Publish)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    foreach ($file in @('EmployeeManagement.dll', 'EmployeeManagement.runtimeconfig.json', 'web.config')) {
        Set-Content -LiteralPath (Join-Path $fixtureState.Publish $file) -Value 'new-version'
        Set-Content -LiteralPath (Join-Path $fixtureState.Deploy $file) -Value 'old-version'
    }
    Set-Content -LiteralPath (Join-Path $fixtureState.Publish 'new-only.txt') -Value 'new-file'
    Set-Content -LiteralPath (Join-Path $fixtureState.Publish 'appsettings.json') -Value 'repository-defaults'
    Set-Content -LiteralPath (Join-Path $fixtureState.Deploy 'appsettings.json') -Value 'server-settings'
    Set-Content -LiteralPath (Join-Path $fixtureState.Deploy 'obsolete.txt') -Value 'old-file'
    foreach ($directory in @('.git', 'uploads', 'logs', 'App_Data')) {
        $preservedPath = Join-Path $fixtureState.Deploy $directory
        New-Item -ItemType Directory -Path $preservedPath | Out-Null
        Set-Content -LiteralPath (Join-Path $preservedPath 'keep.txt') -Value 'keep-me'
    }
}

function Invoke-Fixture {
    # Replace only the production destination literal with this case's fixture.
    # All path guards, copying, backup, rollback, and lifecycle code remain real.
    $fixtureLiteral = $fixtureState.Deploy.Replace("'", "''")
    $fixtureScript = [scriptblock]::Create($deploymentSource.Replace("'C:\inetpub\wwwroot\test-deploy'", "'$fixtureLiteral'"))
    & $fixtureScript -PublishPath $fixtureState.Publish -DeployPath $fixtureState.Deploy -BackupRoot $fixtureState.Backups -HealthCheckUrl 'http://fixture.invalid/health'
}

function Assert-Preserved {
    Assert-True ((Get-Content -LiteralPath (Join-Path $fixtureState.Deploy 'appsettings.json') -Raw).Trim() -eq 'server-settings') 'Server settings must survive.'
    foreach ($directory in @('.git', 'uploads', 'logs', 'App_Data')) {
        Assert-True (Test-Path -LiteralPath (Join-Path $fixtureState.Deploy "$directory/keep.txt")) "$directory must survive."
    }
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $fixtureState.Deploy 'app_offline.htm'))) 'Maintenance marker must be removed.'
    Assert-True ($fixtureState.PoolState -eq 'Started') 'App Pool must be running.'
}

function Assert-FixtureFails([string]$ExpectedMessage) {
    $caughtError = $null
    try { Invoke-Fixture } catch { $caughtError = $_.Exception.Message }
    Assert-True ($null -ne $caughtError -and $caughtError -like "*$ExpectedMessage*") "Expected failure containing '$ExpectedMessage'; got '$caughtError'."
}

New-Fixture 'success'
Invoke-Fixture
Assert-True ($LASTEXITCODE -eq 0) 'Successful Robocopy status must be normalized to zero for Jenkins.'
Assert-True ((Get-Content -LiteralPath (Join-Path $fixtureState.Deploy 'EmployeeManagement.dll') -Raw).Trim() -eq 'new-version') 'New application files must be deployed.'
Assert-True (-not (Test-Path -LiteralPath (Join-Path $fixtureState.Deploy 'obsolete.txt'))) 'Obsolete application files must be removed.'
Assert-True ($fixtureState.Stops -eq 1 -and $fixtureState.Starts -eq 1 -and $fixtureState.HealthCalls -eq 1) 'Successful deployment must stop, start and check health.'
Assert-True (@(Get-ChildItem -LiteralPath $fixtureState.Backups -Directory).Count -eq 1) 'A backup must be retained outside the site.'
Assert-Preserved

foreach ($failure in @('FailCopy', 'FailStart', 'FailHealth')) {
    New-Fixture $failure
    $fixtureState[$failure] = $true
    $expected = switch ($failure) { 'FailCopy' { 'Robocopy failed' }; 'FailStart' { 'startup failure' }; 'FailHealth' { 'Health check failed' } }
    Assert-FixtureFails $expected
    Assert-True ((Get-Content -LiteralPath (Join-Path $fixtureState.Deploy 'EmployeeManagement.dll') -Raw).Trim() -eq 'old-version') "$failure must restore the previous application."
    Assert-True (Test-Path -LiteralPath (Join-Path $fixtureState.Deploy 'obsolete.txt')) "$failure must restore obsolete files from the old version."
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $fixtureState.Deploy 'new-only.txt'))) "$failure must remove new-version-only files."
    Assert-Preserved
}

New-Fixture 'backup-failure'
$fixtureState.FailBackup = $true
Assert-FixtureFails 'Robocopy failed'
Assert-True ($fixtureState.CopyCalls -eq 1) 'A failed backup must prevent deploying any files.'
Assert-Preserved

New-Fixture 'shared-pool'
$fixtureState.SharedPool = $true
Assert-FixtureFails 'is shared'
Assert-True ($fixtureState.Stops -eq 0 -and $fixtureState.CopyCalls -eq 0) 'Shared pools must be rejected before stopping IIS or copying.'

New-Fixture 'incomplete-publish'
Remove-Item -LiteralPath (Join-Path $fixtureState.Publish 'EmployeeManagement.dll')
Assert-FixtureFails 'Publish output is incomplete'
Assert-True ($fixtureState.Stops -eq 0 -and $fixtureState.CopyCalls -eq 0) 'Incomplete publish output must be rejected before stopping IIS.'

New-Fixture 'existing-maintenance'
Set-Content -LiteralPath (Join-Path $fixtureState.Deploy 'app_offline.htm') -Value 'existing-maintenance'
Assert-FixtureFails 'existing app_offline.htm'
Assert-True ($fixtureState.Stops -eq 0 -and $fixtureState.CopyCalls -eq 0) 'Existing maintenance must be left untouched.'

New-Fixture 'rollback-failure'
$fixtureState.FailCopy = $true
$fixtureState.FailRollback = $true
Assert-FixtureFails 'Deployment and rollback failed'
Assert-True (Test-Path -LiteralPath (Join-Path $fixtureState.Deploy 'app_offline.htm')) 'Failed rollback must leave maintenance enabled.'
Assert-True ($fixtureState.PoolState -eq 'Stopped') 'Failed rollback must not start mixed application files.'

New-Fixture 'new-settings-rollback'
Set-Content -LiteralPath (Join-Path $fixtureState.Publish 'appsettings.Production.json') -Value 'new-defaults'
$fixtureState.FailHealth = $true
Assert-FixtureFails 'Health check failed'
Assert-True (-not (Test-Path -LiteralPath (Join-Path $fixtureState.Deploy 'appsettings.Production.json'))) 'Rollback must remove newly introduced settings.'
Assert-Preserved

New-Fixture 'initial-settings'
Remove-Item -LiteralPath (Join-Path $fixtureState.Deploy 'appsettings.json')
Invoke-Fixture
Assert-True ((Get-Content -LiteralPath (Join-Path $fixtureState.Deploy 'appsettings.json') -Raw).Trim() -eq 'repository-defaults') 'An initial deployment must supply missing default settings.'

New-Fixture 'wrong-target'
$caughtError = $null
try {
    & (Join-Path $PSScriptRoot 'deploy-iis.ps1') -PublishPath $fixtureState.Publish -DeployPath $fixtureState.Deploy -BackupRoot $fixtureState.Backups
}
catch { $caughtError = $_.Exception.Message }
Assert-True ($null -ne $caughtError -and $caughtError -like '*DeployPath must be*') 'Production path guard must reject any other destination.'
Assert-True ($fixtureState.Stops -eq 0 -and $fixtureState.CopyCalls -eq 0) 'A rejected destination must not stop IIS or copy files.'

Write-Host "PASS: $checks deployment checks. Fixtures retained at $fixtureRoot"
$global:LASTEXITCODE = 0
