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
foreach($assetName in @('baiye_reconstruction','baiye_watcher_station','baiye_watcher_drone')){
    $asset=Join-Path $root ('anim/assets/'+$assetName)
    $assetOut=Join-Path $package ('anim/assets/'+$assetName)
    New-Item -ItemType Directory -Path $assetOut -Force | Out-Null
    foreach($suffix in @('_anim.bytes','_build.bytes','.png')){
        $name=$assetName+$suffix
        $file=Join-Path $asset $name
        if(!(Test-Path -LiteralPath $file)){throw "Missing native asset: $name"}
        Copy-Item -LiteralPath $file -Destination $assetOut -Force
    }
}
Copy-Item -LiteralPath (Join-Path $root 'preview.png') -Destination $package -Force
$docsOut=Join-Path $package 'docs'
New-Item -ItemType Directory -Path $docsOut -Force | Out-Null
foreach($name in @('watcher-game-acceptance.md','watcher-animation-guide.md','watcher-interaction-review.md')){
    Copy-Item -LiteralPath (Join-Path $root ('docs/'+$name)) -Destination $docsOut -Force
}
$previewOut=Join-Path $package 'preview'
New-Item -ItemType Directory -Path $previewOut -Force | Out-Null
foreach($name in @('watcher-station-final.png','watcher-native-cycle.gif','station-treating.gif','station-transfer_in.gif','drone-pickup.gif','watcher-human-cycle.gif','watcher-human-mirrored.gif','watcher-human-emergency.gif','watcher-human-return.gif','watcher-human-emergency-return.gif','watcher-human-contact.png')){
    Copy-Item -LiteralPath (Join-Path $root ('art/preview/'+$name)) -Destination $previewOut -Force
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
