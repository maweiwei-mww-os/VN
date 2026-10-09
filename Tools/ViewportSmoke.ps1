param([string]$Executable='',[string]$Output='')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$exe=if($Executable){$Executable}else{Join-Path $root 'Builds/Windows/HighPerfUI.exe'}
$base=if($Output){$Output}else{Join-Path $root 'Benchmarks/viewports'}
foreach($size in @(@(1920,1080),@(1280,800))){
  $width=$size[0]; $height=$size[1]
  $out=Join-Path $base "$($width)x$height"
  if((Test-Path -LiteralPath $out) -and @(Get-ChildItem -LiteralPath $out -Force).Count -gt 0){throw "Viewport output already exists: $out"}
  New-Item -ItemType Directory -Path $out -Force | Out-Null
  $argsList=@('--benchmark','--capture-rewards','--mode','C','--count','1000','--frames','180','--out',('"'+$out+'"'),'-screen-width',"$width",'-screen-height',"$height",'-screen-fullscreen','0','-logFile',('"'+(Join-Path $out 'player.log')+'"'))
  $process=Start-Process -FilePath $exe -ArgumentList $argsList -WindowStyle Normal -PassThru
  if(!$process.WaitForExit(60000)){Stop-Process -Id $process.Id; throw 'Viewport smoke timeout'}
  if($process.ExitCode -ne 0){throw "Viewport player failed: $($process.ExitCode)"}
  $files=@(Get-ChildItem -LiteralPath $out -Filter 'mode-*.json')
  if($files.Count -ne 1){throw 'Expected one viewport result'}
  $result=Get-Content -Raw -LiteralPath $files[0].FullName | ConvertFrom-Json
  if($result.Status -ne 'Passed' -or !$result.GridPixelsValidated -or $result.Width -ne $width -or $result.Height -ne $height){throw 'Viewport rendering or dimensions failed'}
  Write-Output "Viewport ${width}x${height}: passed semantic and grid checks. This 180-frame smoke is not release performance evidence."
}
