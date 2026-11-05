param([string]$VamRoot = 'F:\Virt A Mate\Virt A Mate')
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Build.ps1') -VamRoot $VamRoot
$projectRoot = Split-Path $PSScriptRoot -Parent
$buildRoot = Join-Path $projectRoot 'build'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo ('/reference:' + (Join-Path $buildRoot 'QuickFill.dll')) ('/out:' + (Join-Path $buildRoot 'TestSoftbox.exe')) (Join-Path $PSScriptRoot 'TestSoftbox.cs')
if ($LASTEXITCODE -ne 0) { throw 'Softbox test compilation failed.' }
& (Join-Path $buildRoot 'TestSoftbox.exe')
if ($LASTEXITCODE -ne 0) { throw 'Softbox sampling tests failed.' }
