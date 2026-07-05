param(
    [string] $Configuration = "Release",
    [string] $Runtime = "win-x64",
    [string] $Output = "outputs\PortLensMaterial",
    [string] $Version = "",
    [string] $AssemblyVersion = "",
    [string] $FileVersion = "",
    [string] $InformationalVersion = ""
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path "$PSScriptRoot\.."
$project = Join-Path $root "work\PortLens.Desktop\PortLens.Desktop.csproj"
$outputPath = if ([System.IO.Path]::IsPathRooted($Output)) {
    [System.IO.Path]::GetFullPath($Output)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $root $Output))
}

$publishArgs = @(
    $project,
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", "false",
    "-o", $outputPath
)

if ($Version) { $publishArgs += "-p:Version=$Version" }
if ($AssemblyVersion) { $publishArgs += "-p:AssemblyVersion=$AssemblyVersion" }
if ($FileVersion) { $publishArgs += "-p:FileVersion=$FileVersion" }
if ($InformationalVersion) { $publishArgs += "-p:InformationalVersion=$InformationalVersion" }

& dotnet publish $publishArgs

Write-Host "Published PortLens to $outputPath"
