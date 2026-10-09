param([ValidateSet('EditMode','PlayMode','Build')][string]$Action='EditMode',[string]$Label='latest',[string]$Project='ReferenceProject',[string]$ExecuteMethod='HighPerfUI.Reference.Editor.BuildReference.Build',[int]$TimeoutSeconds=240,[switch]$Graphics)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$editor='D:\unityeditor2\2022.3.60f1c1\Editor\Unity.exe'
$artifacts=Join-Path $root 'Artifacts'
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
$projectPath=if([IO.Path]::IsPathRooted($Project)){$Project}else{Join-Path $root $Project}
$argsList=@('-batchmode','-projectPath',$projectPath,'-logFile',(Join-Path $artifacts "$Label-$Action.log"))
if(!$Graphics){$argsList+='-nographics'}
if($Action -eq 'Build'){$argsList+=@('-executeMethod',$ExecuteMethod,'-quit')}
else{$argsList+=@('-runTests','-testPlatform',$Action,'-testResults',(Join-Path $artifacts "$Label-$Action.xml"))}
$quoted=$argsList | ForEach-Object { '"'+($_ -replace '"','\"')+'"' }
$process=Start-Process -FilePath $editor -ArgumentList $quoted -WindowStyle Hidden -PassThru
if(!$process.WaitForExit($TimeoutSeconds*1000)){
  $process.Kill()
  throw "Unity $Action timed out. Its process was stopped; inspect Artifacts/$Label-$Action.log and any completed XML."
}
if($process.ExitCode -ne 0){throw "Unity $Action exited $($process.ExitCode). See Artifacts/$Label-$Action.log"}
