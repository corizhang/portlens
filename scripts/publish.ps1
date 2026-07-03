param(
    [string] $Configuration = "Release",
    [string] $Runtime = "win-x64",
    [string] $Output = "outputs\PortLensMaterial"
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path "$PSScriptRoot\.."
$project = Join-Path $root "work\PortLens.Desktop\PortLens.Desktop.csproj"
$outputPath = if ([System.IO.Path]::IsPathRooted($Output)) {
    [System.IO.Path]::GetFullPath($Output)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $root $Output))
}

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained false `
    -o $outputPath

Write-Host "Published PortLens to $outputPath"
