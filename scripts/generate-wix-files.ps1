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

$components = [System.Text.StringBuilder]::new()
$index = 0

foreach ($file in $files) {
    $relativePath = $file.FullName.Substring($publishFullName.Length).TrimStart('\', '/')

    $safeId = ($relativePath -replace '[\\/]', '_') -replace '[^A-Za-z0-9_.]', '_'
    if ($safeId -match '^\d') { $safeId = "_" + $safeId }
    $componentId = "FileComponent_$index"
    $guid = [Guid]::NewGuid().ToString()

    $line1 = "      <Component Id=`"$componentId`" Guid=`"$guid`">"
    $line2 = "        <File Id=`"$safeId`" Source=`"`$(var.PublishDir)$relativePath`" KeyPath=`"yes`" />"
    $line3 = "      </Component>"

    [void]$components.AppendLine($line1)
    [void]$components.AppendLine($line2)
    [void]$components.AppendLine($line3)

    $index++
}

$componentsText = $components.ToString().TrimEnd()

$wxs = @"
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Fragment>
    <ComponentGroup Id="PublishedFiles" Directory="INSTALLFOLDER">
$componentsText
    </ComponentGroup>
  </Fragment>
</Wix>
"@

Set-Content -Path $OutputPath -Value $wxs -Encoding UTF8
Write-Host "Generated $OutputPath with $index file components."
