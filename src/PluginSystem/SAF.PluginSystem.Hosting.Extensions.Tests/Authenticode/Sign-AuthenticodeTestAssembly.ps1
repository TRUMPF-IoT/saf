# SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
#
# SPDX-License-Identifier: MPL-2.0

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$InputAssembly,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$OutputDirectory,

    [ValidateNotNullOrEmpty()]
    [string]$SignToolPath
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $InputAssembly -PathType Leaf))
{
    throw "Assembly was not found: $InputAssembly"
}

if ([string]::IsNullOrWhiteSpace($SignToolPath))
{
    $signToolSearchPath = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin\*\x64\signtool.exe'
    $signTool = Get-ChildItem -Path $signToolSearchPath -File |
        Sort-Object -Property FullName |
        Select-Object -Last 1
    if ($null -eq $signTool)
    {
        throw 'Windows SDK signtool.exe was not found.'
    }

    $SignToolPath = $signTool.FullName
}
elseif (-not (Test-Path -LiteralPath $SignToolPath -PathType Leaf))
{
    throw "signtool.exe was not found: $SignToolPath"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$fixtureAssemblyPath = Join-Path $OutputDirectory 'SAF.Authenticode.SignedFixture.dll'
$thumbprintPath = [IO.Path]::ChangeExtension($fixtureAssemblyPath, '.thumbprint')
$pfxPath = Join-Path ([IO.Path]::GetTempPath()) "saf-authenticode-$([Guid]::NewGuid().ToString('N')).pfx"
$pfxPasswordText = [Guid]::NewGuid().ToString('N')
$pfxPassword = ConvertTo-SecureString $pfxPasswordText -AsPlainText -Force
$certificate = $null

try
{
    $certificate = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject 'CN=SAF Authenticode integration test' `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -HashAlgorithm SHA256 `
        -NotBefore (Get-Date).AddMinutes(-5) `
        -NotAfter (Get-Date).AddDays(1)

    Export-PfxCertificate -Cert $certificate -FilePath $pfxPath -Password $pfxPassword | Out-Null
    Copy-Item -LiteralPath $InputAssembly -Destination $fixtureAssemblyPath -Force

    & $SignToolPath sign /fd SHA256 /f $pfxPath /p $pfxPasswordText $fixtureAssemblyPath
    if ($LASTEXITCODE -ne 0)
    {
        throw "signtool.exe failed with exit code $LASTEXITCODE."
    }

    [IO.File]::WriteAllText($thumbprintPath, $certificate.Thumbprint)
}
finally
{
    if ($null -ne $certificate)
    {
        Remove-Item -LiteralPath "Cert:\CurrentUser\My\$($certificate.Thumbprint)" -Force -ErrorAction SilentlyContinue
    }

    Remove-Item -LiteralPath $pfxPath -Force -ErrorAction SilentlyContinue
}

Write-Output $fixtureAssemblyPath
