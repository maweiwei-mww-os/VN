param([int]$Count=1000,[int]$Repeats=10,[int]$Frames=1200,[string]$Output='',[string]$Executable='')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$exe=if($Executable){$Executable}else{Join-Path $root 'Builds/Windows/HighPerfUI.exe'}
if(!(Test-Path -LiteralPath $exe)){throw 'Build the Windows player first: Tools/Unity.ps1 -Action Build'}
if(!$Output){$Output=Join-Path $root ('Benchmarks/run-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
if($Count -lt 8 -or $Count -gt 3000 -or $Frames -lt 1 -or $Repeats -lt 1){throw 'Use 8..3000 items, positive frames and repeats.'}
if((Test-Path -LiteralPath $Output) -and @(Get-ChildItem -LiteralPath $Output -Force).Count -gt 0){throw 'Output must be empty to prevent mixing old and new runs.'}
New-Item -ItemType Directory -Path $Output -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $Output 'attempts') -Force | Out-Null
for($round=0;$round -lt $Repeats;$round++){
  $modes=@('A','B','C')
  for($i=0;$i -lt 3;$i++){
    $mode=$modes[($i+$round)%3]
    $before=@(Get-ChildItem -LiteralPath $Output -Filter 'mode-*.json').Count
    $attempt=[ordered]@{Round=$round;Mode=$mode;TimedOut=$false;ExitCode=-1;ResultCount=0;Started=(Get-Date).ToUniversalTime().ToString('O')}
    $argsList=@('--benchmark','--mode',$mode,'--count',"$Count",'--frames',"$Frames",'--out',('"'+$Output+'"'),'-screen-width','1440','-screen-height','900','-screen-fullscreen','0','-logFile',('"'+(Join-Path $Output "player-$round-$mode.log")+'"'))
    # This is the interactive rendering workload, not a background helper. Hidden windows skip rendering.
    $process=Start-Process -FilePath $exe -ArgumentList $argsList -WindowStyle Normal -PassThru
    if(!$process.WaitForExit(180000)){
      Stop-Process -Id $process.Id
      $attempt.TimedOut=$true
      Write-Warning "Timed out: round=$round mode=$mode"
    } else {$attempt.ExitCode=$process.ExitCode; if($process.ExitCode -ne 0){Write-Warning "Failed: round=$round mode=$mode code=$($process.ExitCode)"}}
    $attempt.ResultCount=@(Get-ChildItem -LiteralPath $Output -Filter 'mode-*.json').Count-$before
    $attempt | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Output "attempts/$round-$mode.json") -Encoding utf8
    Write-Output "Finished round $($round+1)/$Repeats mode $mode, exit $($attempt.ExitCode)"
  }
}
& (Join-Path $PSScriptRoot 'ValidateResults.ps1') -Output $Output -Repeats $Repeats -Count $Count -Frames $Frames
