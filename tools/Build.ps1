param([string]$VamRoot = 'F:\Virt A Mate\Virt A Mate')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$buildRoot = Join-Path $projectRoot 'build'
New-Item -ItemType Directory -Path $buildRoot -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$managed = Join-Path $VamRoot 'VaM_Data\Managed'
$refs = @('Assembly-CSharp.dll', 'UnityEngine.dll', 'UnityEngine.CoreModule.dll', 'UnityEngine.PhysicsModule.dll', 'UnityEngine.UI.dll')
$compilerArgs = @('/nologo', '/target:library', ('/out:' + (Join-Path $buildRoot 'QuickFill.dll')))
foreach ($ref in $refs) { $compilerArgs += '/reference:' + (Join-Path $managed $ref) }
$compilerArgs += Join-Path $projectRoot 'Custom\Scripts\Lai\QuickFill\QuickFill.cs'
& $compiler @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'QuickFill compilation failed.' }
Write-Host 'QuickFill compilation passed against installed VaM assemblies.'
