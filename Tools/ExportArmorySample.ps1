$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$target=Join-Path $root 'Packages/com.highperfui.runtime/Samples~/Armory'
$copies=@(
  @{Source='ReferenceProject/Assets/HighPerfUI.Reference';Target='Runtime'},
  @{Source='ReferenceProject/Assets/Resources/ArmoryArt';Target='Resources/ArmoryArt'},
  @{Source='ReferenceProject/Assets/Scenes';Target='Scenes'}
)
foreach($copy in $copies){
  $destination=Join-Path $target $copy.Target
  $absolute=[IO.Path]::GetFullPath($destination)
  $allowed=[IO.Path]::GetFullPath($target)+[IO.Path]::DirectorySeparatorChar
  if(!$absolute.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Sample export target escapes the generated sample root'}
  if(Test-Path -LiteralPath $destination){Remove-Item -LiteralPath $absolute -Recurse -Force}
  New-Item -ItemType Directory -Path $destination -Force | Out-Null
  Get-ChildItem -LiteralPath (Join-Path $root $copy.Source) -File | Copy-Item -Destination $destination -Force
}
Copy-Item -LiteralPath (Join-Path $root 'docs/ArtProvenance.md') -Destination (Join-Path $target 'ArtProvenance.md') -Force
& (Join-Path $PSScriptRoot 'TestPackageContract.ps1')
