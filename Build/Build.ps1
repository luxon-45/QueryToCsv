# QueryToCsv Build Script

param(
    [string]$Runtime = "win-x64"
)

$BuildDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SolutionDir = Split-Path -Parent $BuildDir
Set-Location $SolutionDir

$Framework = "net10.0"
$Configuration = "Release"
$OutputDir = Join-Path $BuildDir "QueryToCsv"
$PublishDir = Join-Path $BuildDir "publish_temp"
$ProjectPath = "QueryToCsv\QueryToCsv.csproj"

# Check dotnet CLI is available
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "   [ERROR] dotnet CLI not found. Please install .NET SDK." -ForegroundColor Red
    exit 1
}

Write-Host "`n=== Building QueryToCsv ===" -ForegroundColor Green

# Cleanup temp folder
if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force
}

# Clean existing output folder
if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}

# Create output folder
New-Item -ItemType Directory -Path $OutputDir -Force -ErrorAction SilentlyContinue | Out-Null

Write-Host "Building QueryToCsv..." -ForegroundColor Cyan

if (Test-Path $ProjectPath) {
    dotnet publish $ProjectPath `
        -c $Configuration `
        -f $Framework `
        -r $Runtime `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=none `
        -p:DebugSymbols=false `
        -o "$PublishDir"

    if ($LASTEXITCODE -eq 0) {
        Copy-Item "$PublishDir\*" "$OutputDir\" -Force

        # Copy appsettings.sample.json as appsettings.json for installer
        $SampleConfig = Join-Path $SolutionDir "QueryToCsv\appsettings.sample.json"
        if (Test-Path $SampleConfig) {
            Copy-Item $SampleConfig (Join-Path $OutputDir "appsettings.json") -Force
            Write-Host "   [OK] appsettings.json created from sample" -ForegroundColor Green
        } else {
            Write-Host "   [WARN] appsettings.sample.json not found - appsettings.json will be missing" -ForegroundColor Yellow
        }

        # Create queries and output folders
        $QueriesDir = Join-Path $OutputDir "queries"
        $OutputCsvDir = Join-Path $OutputDir "output"
        New-Item -ItemType Directory -Path $QueriesDir -Force -ErrorAction SilentlyContinue | Out-Null
        New-Item -ItemType Directory -Path $OutputCsvDir -Force -ErrorAction SilentlyContinue | Out-Null
        Write-Host "   [OK] queries and output folders created" -ForegroundColor Green

        # Cleanup temp folder
        Remove-Item $PublishDir -Recurse -Force

        Write-Host "   [OK] QueryToCsv.exe deployed" -ForegroundColor Green
        Write-Host "`n   Output: $OutputDir" -ForegroundColor Cyan
        Write-Host "`n=== Build Completed Successfully ===" -ForegroundColor Green
        exit 0
    } else {
        # Cleanup temp folder on failure
        if (Test-Path $PublishDir) {
            Remove-Item $PublishDir -Recurse -Force
        }
        Write-Host "   [ERROR] Build failed" -ForegroundColor Red
        Write-Host "`n=== Build Failed ===" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "   [ERROR] Project not found: $ProjectPath" -ForegroundColor Red
    Write-Host "`n=== Build Failed ===" -ForegroundColor Red
    exit 1
}
