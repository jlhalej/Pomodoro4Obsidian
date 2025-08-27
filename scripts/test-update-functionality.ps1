# Test Squirrel Update Functionality
# This script tests the UpdateManager without needing a GitHub release

param(
    [string]$TestMode = "Local",
    [string]$Version = "1.0.8"
)

Write-Host "🧪 Testing Squirrel Update Functionality" -ForegroundColor Green
Write-Host "Test Mode: $TestMode" -ForegroundColor Cyan

# Set paths
$AppDir = Join-Path $PSScriptRoot "App"
$ReleaseDir = Join-Path $PSScriptRoot "releases\Releases"

Write-Host ""
Write-Host "📋 Verifying build and release files..." -ForegroundColor Yellow

# Check if application builds
Push-Location $AppDir
try {
    Write-Host "Building application..." -ForegroundColor Cyan
    & dotnet build --configuration Release --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        throw "Application build failed"
    }
    Write-Host "✅ Application builds successfully" -ForegroundColor Green
}
finally {
    Pop-Location
}

# Check Squirrel files
$RequiredFiles = @(
    "PomodoroForObsidian-$Version-full.nupkg",
    "PomodoroForObsidianSetup.exe", 
    "RELEASES"
)

foreach ($file in $RequiredFiles) {
    $filePath = Join-Path $ReleaseDir $file
    if (Test-Path $filePath) {
        $fileSize = (Get-Item $filePath).Length
        Write-Host "✅ $file ($([math]::Round($fileSize / 1MB, 2)) MB)" -ForegroundColor Green
    } else {
        Write-Host "❌ Missing: $file" -ForegroundColor Red
        Write-Host "Run .\build-squirrel-release.ps1 first to create release files" -ForegroundColor Yellow
        exit 1
    }
}

Write-Host ""
Write-Host "🔍 Analyzing UpdateManager integration..." -ForegroundColor Yellow

# Check UpdateManager.cs
$updateManagerPath = Join-Path $AppDir "UpdateManager.cs"
if (Test-Path $updateManagerPath) {
    $content = Get-Content $updateManagerPath -Raw
    
    # Check for key components
    $checks = @(
        @{Pattern = "class UpdateManager"; Description = "UpdateManager class"},
        @{Pattern = "GithubSource"; Description = "GitHub source integration"},
        @{Pattern = "CheckForUpdatesAsync"; Description = "Update checking method"},
        @{Pattern = "DownloadAndApplyUpdatesAsync"; Description = "Update download method"},
        @{Pattern = "jlhalej/Pomodoro4Obsidian"; Description = "GitHub repository reference"}
    )
    
    foreach ($check in $checks) {
        if ($content -match $check.Pattern) {
            Write-Host "✅ $($check.Description)" -ForegroundColor Green
        } else {
            Write-Host "❌ Missing: $($check.Description)" -ForegroundColor Red
        }
    }
} else {
    Write-Host "❌ UpdateManager.cs not found" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "🎛️ Checking Settings integration..." -ForegroundColor Yellow

# Check SettingsWindow for Updates tab
$settingsPath = Join-Path $AppDir "SettingsWindow.xaml"
if (Test-Path $settingsPath) {
    $content = Get-Content $settingsPath -Raw
    
    if ($content -match "Updates") {
        Write-Host "✅ Updates tab in Settings" -ForegroundColor Green
    } else {
        Write-Host "❌ Missing Updates tab in Settings" -ForegroundColor Red
    }
    
    if ($content -match "CheckForUpdates") {
        Write-Host "✅ Check for Updates button" -ForegroundColor Green
    } else {
        Write-Host "❌ Missing Check for Updates button" -ForegroundColor Red
    }
} else {
    Write-Host "❌ SettingsWindow.xaml not found" -ForegroundColor Red
}

Write-Host ""
Write-Host "📱 Ready for testing!" -ForegroundColor Green
Write-Host ""
Write-Host "Test steps:" -ForegroundColor Magenta
Write-Host "1. Install the app using PomodoroForObsidianSetup.exe" -ForegroundColor White
Write-Host "2. Run the app and go to Settings > Updates" -ForegroundColor White
Write-Host "3. Click 'Check for Updates' (should show no updates if testing locally)" -ForegroundColor White
Write-Host "4. Create a v1.0.9 release to test actual update functionality" -ForegroundColor White
Write-Host ""
Write-Host "🚀 To create GitHub release:" -ForegroundColor Yellow
Write-Host "   .\create-github-release.ps1" -ForegroundColor Cyan
