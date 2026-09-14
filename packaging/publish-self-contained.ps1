# Publish ThinkCanvas as a self-contained single-file exe.
# Target machines need no runtime installed. Output: <repo>/publish/
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $root ".tools\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) {
    Write-Error "dotnet SDK not found at $dotnet"
    exit 1
}

# Keep NuGet cache inside the repo (offline-friendly, no user-level pollution).
$env:NUGET_PACKAGES = Join-Path $root ".tools\packages"

& $dotnet publish (Join-Path $root "ThinkCanvas.csproj") -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o (Join-Path $root "publish")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Get-ChildItem (Join-Path $root "publish") -Filter *.exe | ForEach-Object {
    Write-Output ("{0}  {1:N1} MB" -f $_.Name, ($_.Length / 1MB))
}
