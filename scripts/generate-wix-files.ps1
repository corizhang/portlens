param(
    [Parameter(Mandatory = $true)]
    [string] $PublishDir,

    [Parameter(Mandatory = $true)]
    [string] $OutputPath
)

$ErrorActionPreference = "Stop"

$publishDirInfo = Get-Item -Path $PublishDir -ErrorAction Stop
$publishFullName = $publishDirInfo.FullName.TrimEnd('\', '/')
$files = Get-ChildItem -Path $publishFullName -File -Recurse |
    Where-Object { $_.Extension -notin @('.pdb', '.deps.json') } |
    Sort-Object FullName

$directoryIds = @{
    "" = "INSTALLFOLDER"
}

$directoryElements = [System.Text.StringBuilder]::new()
$componentElements = [System.Text.StringBuilder]::new()
$index = 0

function Get-SafeId($value) {
    $safe = ($value -replace '[\\/]', '_') -replace '[^A-Za-z0-9_.]', '_'
    if ($safe -match '^\d') { $safe = "_" + $safe }
    return $safe
}

foreach ($file in $files) {
    $relativePath = $file.FullName.Substring($publishFullName.Length).TrimStart('\', '/')
    $relativeDir = [System.IO.Path]::GetDirectoryName($relativePath) -replace '\\', '/'
    $fileName = [System.IO.Path]::GetFileName($relativePath)

    if (-not $directoryIds.ContainsKey($relativeDir)) {
        $dirParts = $relativeDir -split '/'
        $currentPath = ""
        for ($i = 0; $i -lt $dirParts.Count; $i++) {
            $parentPath = $currentPath
            if ($currentPath -eq "") {
                $currentPath = $dirParts[$i]
            }
            else {
                $currentPath = "$currentPath/$($dirParts[$i])"
            }

            if (-not $directoryIds.ContainsKey($currentPath)) {
                $dirId = "Dir_$(Get-SafeId $currentPath)"
                $directoryIds[$currentPath] = $dirId
                $parentId = $directoryIds[$parentPath]
                [void]$directoryElements.AppendLine("    <DirectoryRef Id=`"$parentId`">")
                [void]$directoryElements.AppendLine("      <Directory Id=`"$dirId`" Name=`"$($dirParts[$i])`" />")
                [void]$directoryElements.AppendLine("    </DirectoryRef>")
            }
        }
    }

    $directoryId = $directoryIds[$relativeDir]
    $componentId = "FileComponent_$index"
    $guid = [Guid]::NewGuid().ToString()
    $safeFileId = Get-SafeId $relativePath

    [void]$componentElements.AppendLine("      <Component Id=`"$componentId`" Guid=`"$guid`" Directory=`"$directoryId`">")
    [void]$componentElements.AppendLine("        <File Id=`"$safeFileId`" Source=`"`$(var.PublishDir)$relativePath`" KeyPath=`"yes`" />")
    [void]$componentElements.AppendLine("      </Component>")

    $index++
}

$directoriesText = $directoryElements.ToString().TrimEnd()
$componentsText = $componentElements.ToString().TrimEnd()

$wxs = @"
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Fragment>
$directoriesText

    <ComponentGroup Id="PublishedFiles" Directory="INSTALLFOLDER">
$componentsText
    </ComponentGroup>
  </Fragment>
</Wix>
"@

Set-Content -Path $OutputPath -Value $wxs -Encoding UTF8
Write-Host "Generated $OutputPath with $index file components."
