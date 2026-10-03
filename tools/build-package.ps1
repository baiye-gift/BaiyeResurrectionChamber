param(
    [string]$GameManaged='D:\Steam\steamapps\common\OxygenNotIncluded\OxygenNotIncluded_Data\Managed',
    [string]$InstallTo
)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
& dotnet build (Join-Path $root 'ResurrectionChamber.csproj') -c Release -v:q -clp:ErrorsOnly "-p:GameManaged=$GameManaged"
if($LASTEXITCODE -ne 0){throw 'Build failed'}
$package=Join-Path $root 'dist/BaiyeResurrectionChamber'
New-Item -ItemType Directory -Path $package -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'bin/Release/netstandard2.1/BaiyeResurrectionChamber.dll') -Destination $package -Force
foreach($name in @('mod.yaml','mod_info.yaml','README.md')){Copy-Item -LiteralPath (Join-Path $root $name) -Destination $package -Force}
$asset=Join-Path $root 'anim/assets/baiye_reconstruction'
$assetOut=Join-Path $package 'anim/assets/baiye_reconstruction'
New-Item -ItemType Directory -Path $assetOut -Force | Out-Null
foreach($name in @('baiye_reconstruction_anim.bytes','baiye_reconstruction_build.bytes','baiye_reconstruction.png')){
    $file=Join-Path $asset $name
    if(!(Test-Path -LiteralPath $file)){throw "Missing native asset: $name"}
    Copy-Item -LiteralPath $file -Destination $assetOut -Force
}
$dlls=Get-ChildItem -LiteralPath $package -Recurse -Filter '*.dll'
if($dlls.Count -ne 1 -or $dlls[0].Name -ne 'BaiyeResurrectionChamber.dll'){throw 'Package contains unexpected dependencies'}
$version=((Get-Content -LiteralPath (Join-Path $root 'mod_info.yaml') | Where-Object {$_ -match '^version:'} | Select-Object -First 1) -replace '^version:\s*','').Trim()
if($version -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid package version'}
$zip=Join-Path $root "dist/BaiyeResurrectionChamber-$version.zip"
Compress-Archive -LiteralPath $package -DestinationPath $zip -Force
$hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
Set-Content -LiteralPath ($zip+'.sha256') -Value ($hash+'  '+[IO.Path]::GetFileName($zip)) -Encoding ascii
if($InstallTo){
    if(Get-Process -Name OxygenNotIncluded -ErrorAction SilentlyContinue){throw 'Exit Oxygen Not Included before installation'}
    if(Test-Path -LiteralPath $InstallTo){
        $backupRoot=Join-Path $root 'dist/install-backups';New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
        $backup=Join-Path $backupRoot ((Split-Path -Leaf $InstallTo)+'-'+(Get-Date -Format yyyyMMdd-HHmmss))
        Copy-Item -LiteralPath $InstallTo -Destination $backup -Recurse
        Write-Output "Backup $backup"
    }
    New-Item -ItemType Directory -Path $InstallTo -Force | Out-Null
    Get-ChildItem -LiteralPath $package | ForEach-Object{Copy-Item -LiteralPath $_.FullName -Destination $InstallTo -Recurse -Force}
    $prefix=$package.TrimEnd([char[]]'\/')+[IO.Path]::DirectorySeparatorChar
    foreach($file in Get-ChildItem -LiteralPath $package -Recurse -File){
        $relative=$file.FullName.Substring($prefix.Length);$installed=Join-Path $InstallTo $relative
        if((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $installed).Hash){throw "Installation hash mismatch: $relative"}
    }
    Write-Output "Installed $InstallTo"
}
Write-Output "Package $zip"
Write-Output "SHA256 $hash"
