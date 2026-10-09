param([Parameter(Mandatory=$true)][string]$Output,[int]$Repeats=10,[int]$Count=1000,[int]$Frames=1200)
$ErrorActionPreference='Stop'
$attempts=@(Get-ChildItem -LiteralPath (Join-Path $Output 'attempts') -Filter '*.json' | ForEach-Object {Get-Content -Raw -LiteralPath $_.FullName | ConvertFrom-Json})
$results=@(Get-ChildItem -LiteralPath $Output -Filter 'mode-*.json' | ForEach-Object {Get-Content -Raw -LiteralPath $_.FullName | ConvertFrom-Json})
if($attempts.Count -ne $Repeats*3 -or @($attempts | Where-Object {$_.TimedOut -or $_.ExitCode -ne 0 -or $_.ResultCount -ne 1}).Count){throw 'Incomplete or failed player attempts. See attempts/.'}
if($results.Count -ne $Repeats*3){throw 'Unexpected number of result files.'}
foreach($mode in @('A','B','C')){
  if(@($results | Where-Object Mode -eq $mode).Count -ne $Repeats){throw "Missing or duplicate mode $mode"}
}
foreach($result in $results){
  if($result.Status -ne 'Passed' -or !$result.VisibleStateCorrect -or !$result.RenderedPixelsValidated -or !$result.GridPixelsValidated){throw "Invalid semantic/render result: $($result.JsonPath)"}
  if($result.Items -ne $Count -or $result.WorkloadFrames -ne $Frames -or $result.Frames -lt $Frames -or $result.OpenMs -lt 0){throw 'Workload or timing mismatch.'}
  if(!$result.SemanticHash -or !$result.GcRecorderAvailable){throw 'Missing semantic hash or GC recorder.'}
  foreach($extension in @('.csv','.png')){
    $artifact=Join-Path $Output ([IO.Path]::GetFileNameWithoutExtension($result.JsonPath)+$extension)
    if(!(Test-Path -LiteralPath $artifact)){throw "Missing artifact $artifact"}
  }
}
foreach($field in @('SemanticHash','Protocol','CPU','GPU','OS','UnityVersion','Width','Height')){
  if(@($results | Select-Object -ExpandProperty $field -Unique).Count -ne 1){throw "Inconsistent $field across modes"}
}
function Median($values){$v=@($values | Sort-Object); $n=$v.Count; if($n%2){return $v[[int][Math]::Floor($n/2)]}; return ($v[$n/2-1]+$v[$n/2])/2}
$summary=foreach($mode in @('A','B','C')){
  $rows=@($results | Where-Object Mode -eq $mode)
  [pscustomobject]@{Mode=$mode;Runs=$rows.Count;OpenMedianMs=(Median $rows.OpenMs);OpenMinMs=($rows.OpenMs | Measure-Object -Minimum).Minimum;OpenMaxMs=($rows.OpenMs | Measure-Object -Maximum).Maximum;P95MedianMs=(Median $rows.FrameP95Ms);P99MedianMs=(Median $rows.FrameP99Ms);GcMedianBytes=(Median $rows.GcAllocatedBytes);NodesMedian=(Median $rows.NativeNodes);ActiveViewsMedian=(Median $rows.ActiveViews);HeapMedianBytes=(Median $rows.MonoBytes);OpenBindingCpuMedianMs=(Median $rows.OpeningBindingCpuMs);ScrollBindingCpuMedianMs=(Median $rows.ScrollBindingCpuMs);UpdateBindingCpuMedianMs=(Median $rows.UpdateBindingCpuMs);BusinessBindingCpuMedianMs=(Median $rows.BusinessBindingCpuMs);BindingCallsMedian=(Median $rows.BindingCalls);StateApplyCallsMedian=(Median $rows.StateApplyCalls)}
}
$summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $Output 'summary.json') -Encoding utf8
$summary | Format-Table -AutoSize
Write-Output "Validated $($results.Count) independent runs. Reports: $Output"
