param([Parameter(Mandatory=$true)][string]$Project,[Parameter(Mandatory=$true)][string]$Tarball)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$extracted=Join-Path $Project 'ArtifactExtraction'
if(Test-Path -LiteralPath $extracted){throw 'Extract into a new sample validation project or remove only its generated extraction deliberately.'}
New-Item -ItemType Directory -Path $extracted | Out-Null
& tar -xzf $Tarball -C $extracted
if($LASTEXITCODE -ne 0){throw 'Cannot extract the actual shipped package'}
$package=Join-Path $extracted 'package'
$manifest=Get-Content -LiteralPath (Join-Path $package 'package.json') -Raw | ConvertFrom-Json
foreach($sample in $manifest.samples){
  $destination=Join-Path $Project ("Assets/Samples/$($manifest.displayName)/$($manifest.version)/$($sample.displayName)")
  if(Test-Path -LiteralPath $destination){throw 'Sample destination already exists'}
  New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
  Copy-Item -LiteralPath (Join-Path $package $sample.path) -Destination $destination -Recurse
}
Copy-Item -LiteralPath (Join-Path $root 'ReferenceProject/Assets/Tests') -Destination (Join-Path $Project 'Assets/ArmoryTests') -Recurse
Copy-Item -LiteralPath (Join-Path $root 'ValidationProject/Assets/Tests/Editor/PackageSceneInstallTests.cs') -Destination (Join-Path $Project 'Assets/ArmoryTests/Editor/PackageSceneInstallTests.cs')
Copy-Item -LiteralPath (Join-Path $root 'ValidationProject/Assets/Editor') -Destination (Join-Path $Project 'Assets/PackageValidationEditor') -Recurse
Write-Output 'Imported every sample from the actual tarball; added external behavioral/scene verification tests.'
