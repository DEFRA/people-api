# This script sets the necessary permissions on appsettings.json for the IIS AppPool\People-API user
# Run this script after deployment with administrator privileges

$scriptDir = Split-Path -Parent -Path $MyInvocation.MyCommand.Definition

$appSettingsPath = Join-Path -Path $scriptDir -ChildPath "appsettings.json"
if (-not (Test-Path $appSettingsPath)) {
    $parentDir = Split-Path -Parent -Path $scriptDir
    $appSettingsPath = Join-Path -Path $parentDir -ChildPath "appsettings.json"
}

if (Test-Path $appSettingsPath) {
    Write-Host "Found appsettings.json at: $appSettingsPath"
    
    Write-Host "Setting permissions for IIS AppPool\People-API"
    try {
        icacls "$appSettingsPath" /grant "IIS AppPool\People-API:(RD,WD)"
        
        Write-Host "Verifying permissions..."
        icacls "$appSettingsPath" /verify
        
        Write-Host "Permissions set successfully" -ForegroundColor Green
    }
    catch {
        Write-Host "Error setting permissions: $_" -ForegroundColor Red
        exit 1
    }
}
else {
    Write-Host "Error: appsettings.json not found at $appSettingsPath" -ForegroundColor Red
    exit 1
}
