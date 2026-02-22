$version = "1.2.0"
$releaseNameOldLocal = "HSK-RR-Content_oldlocal"
$releaseName = "HSK-RR-Content"

$repoRoot = $PSScriptRoot
$tempDir = Join-Path $env:TEMP "ReleaseTemp_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
$outputDir = "$repoRoot\releases"
if (!(Test-Path $outputDir)) { New-Item -ItemType Directory -Path $outputDir }

# Создаём временную папку
New-Item -ItemType Directory -Path $tempDir | Out-Null

# Список исключений
$exclude = @(
    ".git",
    ".vs",
    "releases",
    "Source",
    ".gitignore",
    "make_release.ps1",
    "README.md"
)

# Копируем всё, кроме исключённых
Get-ChildItem -Path $repoRoot -Recurse -Force | ForEach-Object {
    # Получаем относительный путь
    $relPath = $_.FullName.Substring($repoRoot.Length + 1)

    # Проверяем, не входит ли путь в исключения
    $skip = $false
    foreach ($ex in $exclude) {
        if (($relPath -like "$ex*") -or ($relPath -eq $ex)) {
            $skip = $true
            break
        }
    }

    if (-not $skip) {
        $destPath = Join-Path $tempDir $releaseName
        $destPath = Join-Path $destPath $relPath
        $destDir = Split-Path $destPath -Parent
        if (!(Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir | Out-Null
        }
        Copy-Item -Path $_.FullName -Destination $destPath -Force
    }
}

# Архивируем
$zipPathOldLocal = Join-Path $outputDir "${releaseNameOldLocal}_v${version}.zip"
Compress-Archive -Path "$tempDir/*" -DestinationPath $zipPathOldLocal -Force

# Удаляем старую локализацию HSK
$delPath = Join-Path $tempDir $releaseName
$delPath = Join-Path $delPath "Languages\Russian\DefInjected"
Remove-Item $delPath -Recurse -Force

# Архивируем
$zipPath = Join-Path $outputDir "${releaseName}_v${version}.zip"
Compress-Archive -Path "$tempDir/*" -DestinationPath $zipPath -Force

# Очищаем временные файлы
Remove-Item $tempDir -Recurse -Force

Write-Host "Релиз создан: $zipPathOldLocal"
Write-Host "Релиз с новой локализацией создан: $zipPath"