$ErrorActionPreference='Stop'
$directory=Join-Path (Split-Path $PSScriptRoot -Parent) ('Artifacts/validator-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $directory 'attempts') -Force | Out-Null
foreach($mode in @('A','B','C')){
  @{Mode=$mode;TimedOut=$false;ExitCode=0;ResultCount=1} | ConvertTo-Json | Set-Content (Join-Path $directory "attempts/$mode.json")
  $result=@{Mode=$mode;Status='Passed';VisibleStateCorrect=$true;RenderedPixelsValidated=$true;GridPixelsValidated=$true;Items=1000;WorkloadFrames=1200;Frames=1200;OpenMs=100;FrameP95Ms=10;FrameP99Ms=20;GcAllocatedBytes=100;NativeNodes=40;ActiveViews=40;MonoBytes=1000;SemanticHash='test';GcRecorderAvailable=$true;Protocol='test';CPU='test';GPU='test';OS='test';UnityVersion='test';Width=1440;Height=900;JsonPath="mode-$mode.json"}
  $result | ConvertTo-Json | Set-Content (Join-Path $directory "mode-$mode.json")
  New-Item -ItemType File -Path (Join-Path $directory "mode-$mode.csv") | Out-Null
  New-Item -ItemType File -Path (Join-Path $directory "mode-$mode.png") | Out-Null
}
function Validate { & (Join-Path $PSScriptRoot 'ValidateResults.ps1') -Output $directory -Repeats 1 | Out-Null }
function MustFail { param([scriptblock]$Action) $failed=$false; try {& $Action} catch {$failed=$true}; if(!$failed){throw 'Expected invalid run rejection'} }
Validate
$path=Join-Path $directory 'mode-C.json'
$original=Get-Content -Raw $path | ConvertFrom-Json
$original.GridPixelsValidated=$false; $original | ConvertTo-Json | Set-Content $path
MustFail {Validate}
$original.GridPixelsValidated=$true; $original.SemanticHash='mismatch'; $original | ConvertTo-Json | Set-Content $path
MustFail {Validate}
$original.SemanticHash='test'; $original | ConvertTo-Json | Set-Content $path
$attempt=Join-Path $directory 'attempts/C.json'
@{Mode='C';TimedOut=$true;ExitCode=-1;ResultCount=1} | ConvertTo-Json | Set-Content $attempt
MustFail {Validate}
Write-Output 'PASS: valid results accepted; blank grid, semantic divergence and timeout rejected. Fixture data is not performance evidence.'
