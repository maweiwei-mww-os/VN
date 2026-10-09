param([string]$Zip='')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$version=(Get-Content -LiteralPath (Join-Path $root 'Packages/com.highperfui.runtime/package.json') -Raw | ConvertFrom-Json).version
$packageName="com.highperfui.runtime-$version.tgz"
if(!$Zip){$Zip=Join-Path $root "Deliverables/SLG-Armory-$version-Windows-Source-Package-Docs.zip"}
Add-Type -AssemblyName System.IO.Compression
$archive=[IO.Compression.ZipFile]::OpenRead($Zip)
try {
  $names=@($archive.Entries.FullName)
  foreach($required in @('RunDemo.cmd','RunFreshDemo.cmd','Builds/Windows/HighPerfUI.exe','ReferenceProject/Assets/Scenes/Inventory.unity','Packages/com.highperfui.runtime/package.json',"Deliverables/$packageName",'docs/ProductBenchmarkReport.md','Artifacts/product-final-EditMode.xml','Artifacts/product-final-PlayMode.xml','Artifacts/package-install-EditMode.xml')){
    if($required -notin $names){throw "Missing delivery file $required"}
  }
  if(@($names | Where-Object {$_ -match '(^|/)(Library|Temp|\.git)/'}).Count){throw 'Cache or Git data included in archive'}
} finally {$archive.Dispose()}
$unpacked=Join-Path $root ('Artifacts/unpacked-'+[guid]::NewGuid().ToString('N'))
[IO.Compression.ZipFile]::ExtractToDirectory($Zip,$unpacked)
$out=Join-Path $unpacked 'smoke'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$exe=Join-Path $unpacked 'Builds/Windows/HighPerfUI.exe'
$argsList=@('--benchmark','--mode','C','--count','1000','--frames','180','--out',('"'+$out+'"'),'-screen-width','1280','-screen-height','800','-screen-fullscreen','0','-logFile',('"'+(Join-Path $out 'player.log')+'"'))
$process=Start-Process -FilePath $exe -ArgumentList $argsList -WindowStyle Normal -PassThru
if(!$process.WaitForExit(60000)){Stop-Process -Id $process.Id; throw 'Unpacked player timeout'}
if($process.ExitCode -ne 0){throw "Unpacked player failed $($process.ExitCode)"}
$files=@(Get-ChildItem -LiteralPath $out -Filter 'mode-*.json')
if($files.Count -ne 1){throw 'Expected one unpacked-player result'}
$file=$files[0]
$result=Get-Content -Raw -LiteralPath $file.FullName | ConvertFrom-Json
if($result.Status -ne 'Passed' -or !$result.VisibleStateCorrect -or !$result.GridPixelsValidated){throw 'Unpacked player semantic or render failure'}
$original=Get-FileHash (Join-Path $root "Deliverables/$packageName")
$copy=Get-FileHash (Join-Path $unpacked "Deliverables/$packageName")
if($original.Hash -ne $copy.Hash){throw 'UPM archive fingerprint mismatch'}
New-Item -ItemType Directory -Path (Join-Path $root 'Evidence') -Force | Out-Null
Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $root 'Evidence/packaged-player.json')
Copy-Item -LiteralPath ([IO.Path]::ChangeExtension($file.FullName,'.png')) -Destination (Join-Path $root 'Evidence/packaged-player.png')
[pscustomobject]@{Archive=$Zip;Files=$names.Count;ExtractedTo=$unpacked;PlayerExit=0;SemanticPassed=$result.VisibleStateCorrect;GridPixelsPassed=$result.GridPixelsValidated;UpmSha256=$copy.Hash} | ConvertTo-Json | Set-Content (Join-Path $root 'Evidence/delivery-verification.json') -Encoding utf8
Write-Output "Verified archive structure, UPM fingerprint and actual extracted player: $unpacked"
