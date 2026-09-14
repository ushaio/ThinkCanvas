# Publish ThinkCanvas as a framework-dependent single-file exe.
# Target machines need the .NET 10 Desktop Runtime installed. Output: <repo>/publish-fdd/
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $root ".tools\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) {
    Write-Error "dotnet SDK not found at $dotnet"
    exit 1
}

# Keep NuGet cache inside the repo (offline-friendly, no user-level pollution).
$env:NUGET_PACKAGES = Join-Path $root ".tools\packages"

& $dotnet publish (Join-Path $root "ThinkCanvas.csproj") -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o (Join-Path $root "publish-fdd")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Get-ChildItem (Join-Path $root "publish-fdd") -Filter *.exe | ForEach-Object {
    Write-Output ("{0}  {1:N1} MB" -f $_.Name, ($_.Length / 1MB))
}
