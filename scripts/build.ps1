param([string]$IsccPath = '', [switch]$Installer, [switch]$ValidationInstaller, [string]$CargoPath='cargo')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $projectRoot 'src/DesktopBaskets/DesktopBaskets.csproj'
[xml]$projectXml = Get-Content -LiteralPath $project
$version = [string]$projectXml.Project.PropertyGroup.Version
& $CargoPath build --release --locked --manifest-path (Join-Path $projectRoot 'share-core/Cargo.toml')
if ($LASTEXITCODE -ne 0) { throw 'Sharing core build failed.' }
dotnet build $project -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
$stage = Join-Path $projectRoot 'build/stage'
$dist = Join-Path $projectRoot 'dist'
$release = Join-Path $projectRoot 'src/DesktopBaskets/bin/Release/net48'
$resolvedStage = [IO.Path]::GetFullPath($stage)
$buildRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'build')).TrimEnd('\')+'\'
if (!$resolvedStage.StartsWith($buildRoot,[StringComparison]::OrdinalIgnoreCase)) { throw 'Staging directory is outside the project build directory.' }
if (Test-Path -LiteralPath $resolvedStage) { Remove-Item -LiteralPath $resolvedStage -Recurse -Force }
New-Item -ItemType Directory -Path $stage,$dist -Force | Out-Null
foreach ($file in @('DesktopBaskets.exe','DesktopBaskets.exe.config','Newtonsoft.Json.dll','QRCoder.dll','zxing.dll')) { Copy-Item -LiteralPath (Join-Path $release $file) -Destination $stage -Force }
Copy-Item -LiteralPath (Join-Path $projectRoot 'share-core/target/release/desktop-baskets-share.exe') -Destination (Join-Path $release 'DesktopBaskets.Share.exe') -Force
Copy-Item -LiteralPath (Join-Path $release 'DesktopBaskets.Share.exe') -Destination $stage -Force
Copy-Item -LiteralPath (Join-Path $release 'Assets') -Destination $stage -Recurse -Force
foreach ($file in @('THIRD_PARTY_NOTICES.md','Newtonsoft.Json-LICENSE.txt')) { Copy-Item -LiteralPath (Join-Path $projectRoot $file) -Destination $stage -Force }
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/使用說明.txt') -Destination $stage -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/SHARING.md') -Destination (Join-Path $stage '共享使用說明.md') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'licenses') -Destination $stage -Recurse -Force
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath (Join-Path $dist "DesktopBaskets-Portable-$version-x64.zip") -Force
if ($Installer -or $ValidationInstaller) {
  if (!$IsccPath) {
    $compiler = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($compiler) { $IsccPath=$compiler.Source }
    elseif (Test-Path "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe") { $IsccPath="${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" }
    else { throw 'Specify -IsccPath pointing to Inno Setup 6.7.3 or later.' }
  }
  $arguments = @("/DAppVersion=$version", "/DPayloadDir=$stage", "/DOutputPath=$dist")
  if ($ValidationInstaller) { $arguments += '/DBuildValidation=1' }
  & $IsccPath @arguments (Join-Path $projectRoot 'installer/DesktopBaskets.iss')
  if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
}
Write-Output "Build complete: $dist"
