# Build and Package Squirrel Release
# This script builds the application and creates a Squirrel-compatible release

param(
    [string]$Version = "",
    [string]$Configuration = "Release"
)

# Set paths
$AppDir = Join-Path (Split-Path $PSScriptRoot -Parent) "App"
$ProjectFile = Join-Path $AppDir "PomodoroForObsidian.csproj"

# Auto-detect version from project file if not provided
if ([string]::IsNullOrEmpty($Version)) {
    if (Test-Path $ProjectFile) {
        $projectContent = Get-Content $ProjectFile -Raw
        if ($projectContent -match '<Version>([^<]+)</Version>') {
            $Version = $matches[1]
            Write-Host "📋 Auto-detected version from project file: $Version" -ForegroundColor Cyan
        } else {
            throw "Could not detect version from project file: $ProjectFile"
        }
    } else {
        throw "Project file not found: $ProjectFile"
    }
}

Write-Host "🚀 Building Squirrel Release for Pomodoro4Obsidian v$Version" -ForegroundColor Green

# Set paths
$AppDir = Join-Path (Split-Path $PSScriptRoot -Parent) "App"
$ReleaseDir = Join-Path (Split-Path $PSScriptRoot -Parent) "releases"
$BuildDir = Join-Path $AppDir "bin\$Configuration\net8.0-windows"

# Clean and create release directory
Write-Host "📁 Setting up release directory..." -ForegroundColor Yellow
if (Test-Path $ReleaseDir) {
    Remove-Item $ReleaseDir -Recurse -Force
}
New-Item -Path $ReleaseDir -ItemType Directory -Force | Out-Null

# Build the application
Write-Host "🔨 Building application..." -ForegroundColor Yellow
Push-Location $AppDir
try {
    & dotnet build --configuration $Configuration --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }
    Write-Host "✅ Build completed successfully" -ForegroundColor Green
}
finally {
    Pop-Location
}

# Verify build output exists
if (-not (Test-Path $BuildDir)) {
    throw "Build directory not found: $BuildDir"
}

# Copy build output to release staging area
$StagingDir = Join-Path $ReleaseDir "staging"
Write-Host "📦 Copying build output to staging..." -ForegroundColor Yellow
Copy-Item -Path $BuildDir -Destination $StagingDir -Recurse -Force

# Create Squirrel package using Squirrel
Write-Host "📋 Creating Squirrel package..." -ForegroundColor Yellow
Push-Location $ReleaseDir
try {
    # Find Squirrel executable in NuGet packages
    $nugetOutput = dotnet nuget locals global-packages --list
    $nugetGlobalDir = ($nugetOutput -split ':',2)[1].Trim()
    $squirrelExe = Join-Path $nugetGlobalDir "clowd.squirrel\2.11.1\tools\Squirrel.exe"
    
    if (-not (Test-Path $squirrelExe)) {
        throw "Squirrel.exe not found at: $squirrelExe"
    }
    
    Write-Host "Using Squirrel.exe: $squirrelExe" -ForegroundColor Cyan

    # Create the Squirrel release
    Write-Host "Creating Squirrel release package..." -ForegroundColor Yellow
    & $squirrelExe pack `
        --packId "PomodoroForObsidian" `
        --packVersion $Version `
        --packDir $StagingDir `
        --outputDir $ReleaseDir `
        --mainExe "PomodoroForObsidian.exe" `
        --packAuthors "Hugo Jacquot" `
        --packTitle "Pomodoro For Obsidian" `
        --allowUnaware `
        --setupIcon "$StagingDir\timer16.ico" `
        --verbose
    
    if ($LASTEXITCODE -ne 0) {
        throw "Squirrel pack failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

# List generated files
Write-Host "📋 Release files generated:" -ForegroundColor Green
Get-ChildItem $ReleaseDir -Recurse -File | ForEach-Object {
    $relativePath = $_.FullName.Substring($ReleaseDir.Length + 1)
    Write-Host "  📄 $relativePath" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "✅ Squirrel release v$Version created successfully!" -ForegroundColor Green
Write-Host "📁 Release files located in: $ReleaseDir" -ForegroundColor Yellow
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Magenta
Write-Host "1. Upload the generated files to GitHub Releases" -ForegroundColor White
Write-Host "2. Tag the release as 'v$Version' in GitHub" -ForegroundColor White
Write-Host "3. Test the update process using the UpdateManager" -ForegroundColor White
