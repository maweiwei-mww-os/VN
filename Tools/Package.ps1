param([string]$Output='',[switch]$PackageOnly)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$Output){$Output=Join-Path $root 'Deliverables'}
New-Item -ItemType Directory -Path $Output -Force | Out-Null
$version=(Get-Content -LiteralPath (Join-Path $root 'Packages/com.highperfui.runtime/package.json') -Raw | ConvertFrom-Json).version
$tarball=Join-Path $Output ("com.highperfui.runtime-$version.tgz")
if($PackageOnly -or !(Test-Path -LiteralPath $tarball)){
  $stage=Join-Path $root ('Artifacts/package-stage-'+[guid]::NewGuid().ToString('N'))
  New-Item -ItemType Directory -Path $stage -Force | Out-Null
  Copy-Item -LiteralPath (Join-Path $root 'Packages/com.highperfui.runtime') -Destination (Join-Path $stage 'package') -Recurse
  & tar -czf $tarball -C $stage package
  if($LASTEXITCODE -ne 0){throw 'Package tar failed'}
}
if($PackageOnly){Get-Item -LiteralPath $tarball | Select-Object FullName,Length; return}
$zip=Join-Path $Output ("SLG-Armory-$version-Windows-Source-Package-Docs.zip")
if(Test-Path -LiteralPath $zip){throw 'Delivery already exists. Use a new output directory.'}
Add-Type -AssemblyName System.IO.Compression
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try {
  $paths=@('Packages','ReferenceProject/Assets','ReferenceProject/Packages','ReferenceProject/ProjectSettings','ValidationProject/Assets','ValidationProject/Packages','ValidationProject/ProjectSettings','Builds/Windows','Tools','docs','Benchmarks/product-1000','Benchmarks/viewports','Evidence')
  foreach($relative in $paths){
    $path=Join-Path $root $relative
    if(!(Test-Path -LiteralPath $path)){throw "Required delivery directory missing: $relative"}
    foreach($file in Get-ChildItem -LiteralPath $path -Recurse -File){
      $name=[IO.Path]::GetRelativePath($root,$file.FullName).Replace('\','/')
      [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$name,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
  }
  foreach($file in Get-ChildItem -LiteralPath (Join-Path $root 'Artifacts') -File | Where-Object { $_.Extension -in @('.xml','.log') }){
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,('Artifacts/'+$file.Name),[IO.Compression.CompressionLevel]::Optimal) | Out-Null
  }
  foreach($name in @('README.md','RunDemo.cmd','RunFreshDemo.cmd','OpenUnity.cmd')){
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $root $name),$name,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
  }
  [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$tarball,('Deliverables/'+[IO.Path]::GetFileName($tarball)),[IO.Compression.CompressionLevel]::Optimal) | Out-Null
} finally {$archive.Dispose()}
Get-FileHash -Algorithm SHA256 -LiteralPath $zip,$tarball | Select-Object Path,Hash | ConvertTo-Json | Set-Content (Join-Path $Output 'SHA256.json') -Encoding utf8
Get-Item -LiteralPath $zip,$tarball | Select-Object FullName,Length
