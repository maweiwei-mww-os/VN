param([Parameter(Mandatory=$true)][string]$Release,[switch]$IncludePlayer)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$releasePath=(Resolve-Path -LiteralPath $Release).Path
$manifest=Get-Content -LiteralPath (Join-Path $releasePath 'Source/com.highperfui.runtime/package.json') -Raw | ConvertFrom-Json
$tarball=Join-Path $releasePath ("$($manifest.name)-$($manifest.version).tgz")
$installed=Join-Path $root 'ValidationInstall/InstalledPackage.tgz'
if((Get-FileHash -LiteralPath $tarball).Hash -ne (Get-FileHash -LiteralPath $installed).Hash){throw 'The installed tarball differs from the deliverable.'}
foreach($file in @('ValidationResults.md','README.md','PackageFingerprint.json')){
    if(!(Test-Path -LiteralPath (Join-Path $releasePath $file))){throw "Missing delivery file $file"}
}
$cases=@(
    @{Stem='upm-verified-source-EditMode';Count=96},
    @{Stem='upm-verified-source-PlayMode';Count=9},
    @{Stem='upm-installed-runtime-graphics-EditMode';Count=58},
    @{Stem='upm-installed-runtime-graphics-PlayMode';Count=3},
    @{Stem='upm-installed-samples-EditMode';Count=98},
    @{Stem='upm-installed-samples-PlayMode';Count=9}
)
$evidence=Join-Path $releasePath 'Evidence'
foreach($case in $cases){
    $xml=Join-Path $root ('Artifacts/'+$case.Stem+'.xml')
    [xml]$report=Get-Content -LiteralPath $xml -Raw
    $run=$report.'test-run'
    if($run.result -ne 'Passed' -or [int]$run.failed -ne 0 -or [int]$run.passed -ne $case.Count){throw "Unexpected test result $xml"}
    foreach($ext in @('.xml','.log')){Copy-Item -LiteralPath (Join-Path $root ('Artifacts/'+$case.Stem+$ext)) -Destination $evidence}
}
Copy-Item -LiteralPath (Join-Path $root 'Artifacts/upm-installed-samples-Build.log') -Destination $evidence
$project=Join-Path $releasePath 'VerificationProject'
if(Test-Path -LiteralPath $project){throw 'VerificationProject already exists; do not overwrite a delivery.'}
New-Item -ItemType Directory -Path $project | Out-Null
foreach($dir in @('Assets','ProjectSettings')){
    Copy-Item -LiteralPath (Join-Path $root ('ValidationInstall/'+$dir)) -Destination (Join-Path $project $dir) -Recurse
}
$packages=Join-Path $project 'Packages'
New-Item -ItemType Directory -Path $packages | Out-Null
$projectManifest=Get-Content -LiteralPath (Join-Path $root 'ValidationInstall/Packages/manifest.json') -Raw | ConvertFrom-Json
$projectManifest.dependencies.'com.highperfui.runtime'='file:../../'+[IO.Path]::GetFileName($tarball)
$projectManifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $packages 'manifest.json') -Encoding utf8
$scripts=Join-Path $releasePath 'Scripts'
New-Item -ItemType Directory -Path $scripts | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'DeliveryRunUnity.ps1') -Destination (Join-Path $scripts 'RunUnity.ps1')
foreach($script in @('Compare.ps1','ValidateResults.ps1','ViewportSmoke.ps1','TestValidation.ps1')){
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $script) -Destination $scripts
}
if($IncludePlayer){
    $examples=Join-Path $releasePath 'Examples'
    New-Item -ItemType Directory -Path $examples | Out-Null
    Copy-Item -LiteralPath (Join-Path $root 'ValidationInstall/Builds/Windows') -Destination $examples -Recurse
}
$zip=Join-Path $releasePath ("HighPerfUI-$($manifest.version)-UPM-Source-Docs-Evidence.zip")
if(Test-Path -LiteralPath $zip){throw 'Delivery ZIP already exists.'}
$include=@([IO.Path]::GetFileName($tarball),'Source','Documentation','Evidence','Scripts','VerificationProject','README.md','ValidationResults.md','PackageFingerprint.json')
foreach($optional in @('Benchmarks','Viewports','InterviewWalkthrough.md','Examples')){
    if(Test-Path -LiteralPath (Join-Path $releasePath $optional)){$include+=$optional}
}
$paths=$include | ForEach-Object {Join-Path $releasePath $_}
Compress-Archive -LiteralPath $paths -DestinationPath $zip -CompressionLevel Optimal
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try{
    if(!($archive.Entries | Where-Object FullName -eq ([IO.Path]::GetFileName($tarball)))){throw 'ZIP is missing the UPM tarball.'}
    if($archive.Entries | Where-Object FullName -match '(^|/)(Library|Temp|\.git|BuildStaging)/'){throw 'ZIP contains a generated cache or Git data.'}
    Write-Output "Verified delivery ZIP entries: $($archive.Entries.Count)"
} finally {$archive.Dispose()}
Get-FileHash -LiteralPath $zip -Algorithm SHA256 | Select-Object Path,Hash | ConvertTo-Json | Set-Content -LiteralPath ($zip+'.sha256.json') -Encoding utf8
Write-Output "Companion delivery: $zip"
