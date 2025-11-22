# Upload Squirrel Release to GitHub
# This script creates a GitHub release and uploads the Squirrel files

param(
    [string]$Version = "1.5.7",
    [string]$ReleaseNotes = "Major improvements: Click/scroll timer adjustments now work smoothly with rapid input. Timer display format switches at 1-hour mark. Settings page shows time in HH:MM:SS format. Fixed debounced saving for instant UI response.",
    [string]$RepoOwner = "jlhalej",
    [string]$RepoName = "Pomodoro4Obsidian"
)

Write-Host "Creating GitHub Release v$Version for $RepoOwner/$RepoName" -ForegroundColor Green

# Set paths
$ReleaseDir = Join-Path (Split-Path $PSScriptRoot -Parent) "releases\Releases"
$TagName = "v$Version"

# Verify release files exist
$RequiredFiles = @(
    "PomodoroForObsidian-$Version-full.nupkg",
    "PomodoroForObsidianSetup.exe",
    "RELEASES"
)

Write-Host "Verifying release files..." -ForegroundColor Yellow
foreach ($file in $RequiredFiles) {
    $filePath = Join-Path $ReleaseDir $file
    if (-not (Test-Path $filePath)) {
        throw "Required file not found: $filePath"
    }
    $fileSize = (Get-Item $filePath).Length
    Write-Host "  OK $file ($([math]::Round($fileSize / 1MB, 2)) MB)" -ForegroundColor Green
}

# Create GitHub release using GitHub CLI (gh)
Write-Host "Creating GitHub release $TagName..." -ForegroundColor Yellow

try {
    # Check if gh CLI is available
    $ghVersion = gh --version 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub CLI (gh) not found. Please install it from https://cli.github.com/"
    }
    Write-Host "Using GitHub CLI: $($ghVersion[0])" -ForegroundColor Cyan

    # Create the release
    $releaseNotesFull = @"
# Pomodoro4Obsidian v$Version - Auto-Update Release

$ReleaseNotes

## Installation
- **New Users**: Download and run ``PomodoroForObsidianSetup.exe``
- **Existing Users**: The app will auto-update to this version

## Technical Details
- Built with Clowd.Squirrel for reliable auto-update functionality
- Supports delta updates for efficient bandwidth usage
- GitHub-integrated release distribution

"@

    # Create release with files
    gh release create $TagName `
        --repo "$RepoOwner/$RepoName" `
        --title "Pomodoro4Obsidian v$Version" `
        --notes $releaseNotesFull `
        --latest `
    (Join-Path $ReleaseDir "PomodoroForObsidian-$Version-full.nupkg") `
    (Join-Path $ReleaseDir "PomodoroForObsidianSetup.exe") `
    (Join-Path $ReleaseDir "RELEASES")

    if ($LASTEXITCODE -eq 0) {
        Write-Host "GitHub release created successfully!" -ForegroundColor Green
        Write-Host "Release URL: https://github.com/$RepoOwner/$RepoName/releases/tag/$TagName" -ForegroundColor Cyan
    }
    else {
        throw "GitHub release creation failed with exit code $LASTEXITCODE"
    }
}
catch {
    Write-Host "Error creating GitHub release: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Manual upload instructions:" -ForegroundColor Yellow
    Write-Host "1. Go to https://github.com/$RepoOwner/$RepoName/releases/new" -ForegroundColor White
    Write-Host "2. Tag: $TagName" -ForegroundColor White
    Write-Host "3. Title: Pomodoro4Obsidian v$Version" -ForegroundColor White
    Write-Host "4. Upload these files:" -ForegroundColor White
    foreach ($file in $RequiredFiles) {
        Write-Host "   - $file" -ForegroundColor Cyan
    }
    Write-Host "5. Mark as latest release" -ForegroundColor White
    exit 1
}

Write-Host ""
Write-Host "Release v$Version published successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Magenta
Write-Host "1. Test the update functionality in the application" -ForegroundColor White
Write-Host "2. Verify that UpdateManager can detect the new release" -ForegroundColor White
Write-Host "3. Test the complete update workflow" -ForegroundColor White
