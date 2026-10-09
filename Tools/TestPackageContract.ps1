param([string]$Package='')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$Package){$Package=Join-Path $root 'Packages/com.highperfui.runtime'}
$manifest=Get-Content -LiteralPath (Join-Path $Package 'package.json') -Raw | ConvertFrom-Json
if($manifest.name -ne 'com.highperfui.runtime'){throw 'Unexpected package identity'}
foreach($dependency in @('com.unity.ugui','com.unity.modules.screencapture','com.unity.modules.imageconversion','com.unity.modules.jsonserialize')){
  if(!$manifest.dependencies.$dependency){throw "Missing declared dependency $dependency"}
}
foreach($sample in $manifest.samples){
  if(!(Test-Path -LiteralPath (Join-Path $Package $sample.path))){throw "Missing sample $($sample.path)"}
}
if(!(@($manifest.samples | Where-Object path -eq 'Samples~/Armory').Count)){throw 'Missing optional Armory business sample in package manifest'}
foreach($file in @('Documentation~/Architecture.md','Documentation~/Migration.md','Documentation~/Interview.md','Documentation~/Validation.md','Samples~/Armory/Runtime/InventoryScreen.cs','Samples~/Armory/Runtime/HighPerfUI.Reference.asmdef','Samples~/Armory/Editor/ArmorySampleMenu.cs','Samples~/Armory/Resources/ArmoryArt/Commander.png')){
  if(!(Test-Path -LiteralPath (Join-Path $Package $file))){throw "Missing package deliverable $file"}
}
$runtime=Get-ChildItem -LiteralPath (Join-Path $Package 'Runtime') -Recurse -Filter '*.cs'
if($runtime | Select-String -Pattern 'HighPerfUI.Reference|using UnityEditor|D:\\SLG|Builds/Windows'){throw 'Runtime depends on a project/editor/business path'}
$illegal=Get-ChildItem -LiteralPath $Package -Recurse -Directory | Where-Object Name -in @('Library','Temp','Obj','.git')
if($illegal){throw 'Package contains generated caches or Git data'}
foreach($pair in @(
  @{Source='ReferenceProject/Assets/HighPerfUI.Reference';Target='Runtime'},
  @{Source='ReferenceProject/Assets/Resources/ArmoryArt';Target='Resources/ArmoryArt'},
  @{Source='ReferenceProject/Assets/Scenes';Target='Scenes'}
)){
  $source=Join-Path $root $pair.Source
  $exportRoot=Join-Path $Package ('Samples~/Armory/'+$pair.Target)
  if(!(Test-Path -LiteralPath $source)){continue}
  $files=@(Get-ChildItem -LiteralPath $source -File)
  $exported=@(Get-ChildItem -LiteralPath $exportRoot -File)
  if(Compare-Object @($files.Name | Sort-Object) @($exported.Name | Sort-Object)){throw "Obsolete or missing generated sample files: $($pair.Target)"}
  foreach($file in $files){
    $export=Join-Path $exportRoot $file.Name
    if((Get-FileHash -LiteralPath $export).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash){throw "Sample source out of sync: $($file.Name)"}
  }
}
Write-Output "PASS: package $($manifest.version), optional samples, runtime isolation and exported-source fingerprints."
