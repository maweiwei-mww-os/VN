param(
    [Parameter(Mandatory=$true)][string]$Editor,
    [ValidateSet('EditMode','PlayMode','Build')][string]$Action='EditMode',
    [string]$Output='',
    [int]$TimeoutSeconds=600
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$project=Join-Path $root 'VerificationProject'
if(!(Test-Path -LiteralPath $Editor -PathType Leaf)){throw 'Pass the full path to Unity.exe using -Editor.'}
if(!(Test-Path -LiteralPath (Join-Path $project 'Packages/manifest.json'))){throw 'VerificationProject is missing. Extract the whole delivery archive first.'}
if(!$Output){$Output=Join-Path $root ('LocalResults/'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+$Action)}
if((Test-Path -LiteralPath $Output) -and @(Get-ChildItem -LiteralPath $Output -Force).Count){throw 'Use a new output directory; do not mix verification runs.'}
if($TimeoutSeconds -lt 1){throw 'TimeoutSeconds must be positive.'}
New-Item -ItemType Directory -Path $Output -Force | Out-Null
$log=Join-Path $Output ($Action+'.log')
$xml=Join-Path $Output ($Action+'.xml')
$argsList=@('-batchmode','-projectPath',$project,'-logFile',$log)
if($Action -eq 'Build'){$argsList+=@('-executeMethod','HighPerfUI.Validation.UpmPlayerBuild.Build','-quit')}
else{$argsList+=@('-runTests','-testPlatform',$Action,'-testResults',$xml)}
# Graphics stay enabled: this delivery is for actual UGUI rendering tests.
$quoted=$argsList | ForEach-Object {'"'+($_ -replace '"','\"')+'"'}
$process=Start-Process -FilePath $Editor -ArgumentList $quoted -WindowStyle Hidden -PassThru
if(!$process.WaitForExit($TimeoutSeconds*1000)){
    $process.Kill()
    throw "Unity timed out; only this script's editor was stopped. Inspect $log"
}
if($process.ExitCode -ne 0){throw "Unity exited $($process.ExitCode). Inspect $log"}
if($Action -ne 'Build'){
    if(!(Test-Path -LiteralPath $xml)){throw 'Unity exited without a test report.'}
    [xml]$report=Get-Content -LiteralPath $xml -Raw
    $run=$report.'test-run'
    if($run.result -ne 'Passed' -or [int]$run.failed -ne 0 -or [int]$run.passed -lt 1){throw "Tests did not pass. Inspect $xml"}
    Write-Output "$Action passed: $($run.passed) tests. Evidence: $Output"
} else {
    $exe=Join-Path $project 'Builds/Windows/HighPerfUI.exe'
    if(!(Test-Path -LiteralPath $exe)){throw 'Build exited without the expected player.'}
    Write-Output "Installed-package sample built: $exe"
}
