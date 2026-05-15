# DS Capture WPF 빌드 스크립트
# Python build.py 에 대응하는 PowerShell 버전
#
# 사용법:
#   .\build.ps1           -- 버전 자동 증가 후 Release 빌드
#   .\build.ps1 -NoVersion -- 버전 증가 없이 빌드

param(
    [switch]$NoVersion
)

$ProjectFile  = "DSCapture\DSCapture.csproj"
$MainCs       = "DSCapture\App.xaml.cs"
$OutputDir    = "dist_production"
$ExeName      = "DS Capture.exe"

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── 1. 버전 자동 증가 ─────────────────────────────────────
if (-not $NoVersion) {
    $content = Get-Content $MainCs -Raw

    if ($content -match 'private const string AppVersion = "v1\.(\d+)"') {
        $currentMinor = [int]$Matches[1]
        $newMinor     = $currentMinor + 1
        $newVersion   = "v1.{0:D2}" -f $newMinor

        $content = $content -replace 'private const string AppVersion = "v1\.\d+"',
                                     "private const string AppVersion = `"$newVersion`""

        # MainWindow.xaml.cs 에도 동일한 상수가 있으면 갱신
        $mainWinCs = "DSCapture\Views\MainWindow.xaml.cs"
        if (Test-Path $mainWinCs) {
            $mw = Get-Content $mainWinCs -Raw
            $mw = $mw -replace 'private const string AppVersion = "v1\.\d+"',
                               "private const string AppVersion = `"$newVersion`""
            Set-Content $mainWinCs $mw -NoNewline
        }

        Set-Content $MainCs $content -NoNewline
        Write-Host "[버전] $newVersion 으로 업데이트됨" -ForegroundColor Cyan
    } else {
        Write-Warning "AppVersion 상수를 찾을 수 없습니다. 버전 증가를 건너뜁니다."
    }
}

# ── 2. dotnet publish ─────────────────────────────────────
Write-Host "`n[빌드] Nuitka onefile 동등 — dotnet publish 실행 중..." -ForegroundColor Cyan

dotnet publish $ProjectFile `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishReadyToRun=true `
    -o $OutputDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "[ERROR] 빌드 실패 (exit code $LASTEXITCODE)"
    exit $LASTEXITCODE
}

# ── 3. 임시 파일 정리 ─────────────────────────────────────
Write-Host "`n[정리] 임시 빌드 파일 삭제 중..." -ForegroundColor Yellow

$cleanTargets = @(
    "$OutputDir\*.pdb",
    "DSCapture\bin",
    "DSCapture\obj"
)
foreach ($t in $cleanTargets) {
    if (Test-Path $t) {
        Remove-Item $t -Recurse -Force
        Write-Host "  삭제: $t"
    }
}

Write-Host "`n[SUCCESS] 빌드 완료!" -ForegroundColor Green
Write-Host "결과물: $OutputDir\$ExeName" -ForegroundColor Green
