param([Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'ExportArmorySample.ps1')
$source=Join-Path $root 'Packages/com.highperfui.runtime'
$manifest=Get-Content -LiteralPath (Join-Path $source 'package.json') -Raw | ConvertFrom-Json
if(Test-Path -LiteralPath $Output){throw 'Choose a new release directory; existing releases are not overwritten.'}
New-Item -ItemType Directory -Path $Output | Out-Null
$sourceRoot=Join-Path $Output 'Source'
New-Item -ItemType Directory -Path $sourceRoot | Out-Null
Copy-Item -LiteralPath $source -Destination (Join-Path $sourceRoot 'com.highperfui.runtime') -Recurse
$stage=Join-Path $Output 'BuildStaging'
New-Item -ItemType Directory -Path $stage | Out-Null
Copy-Item -LiteralPath $source -Destination (Join-Path $stage 'package') -Recurse
$tarball=Join-Path $Output ("$($manifest.name)-$($manifest.version).tgz")
& tar -czf $tarball --exclude=package/Tests~ -C $stage package
if($LASTEXITCODE -ne 0){throw 'UPM archive generation failed'}
$entries=@(& tar -tf $tarball)
if($LASTEXITCODE -ne 0 -or 'package/package.json' -notin $entries){throw 'Invalid tarball structure'}
if($entries | Where-Object {$_ -match '(^|/)(Library|Temp|\.git)/|Builds/Windows'}){throw 'UPM archive contains project/build cache data'}
Copy-Item -LiteralPath (Join-Path $source 'Documentation~') -Destination (Join-Path $Output 'Documentation') -Recurse
Copy-Item -LiteralPath (Join-Path $root 'docs/PackageReadme.md') -Destination (Join-Path $Output 'README.md')
New-Item -ItemType Directory -Path (Join-Path $Output 'Evidence') | Out-Null
Get-FileHash -Algorithm SHA256 -LiteralPath $tarball | Select-Object Path,Hash | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Output 'PackageFingerprint.json') -Encoding utf8
Write-Output "UPM artifact generated, pending clean installation verification: $tarball"
