<#
.SYNOPSIS
    Makes a self-signed code-signing certificate for test installs of the EyeBrowse MSIX.

.DESCRIPTION
    Writes EyeBrowse-test.pfx (private, for signing; keep it out of the repository) and
    EyeBrowse-test.cer (public, to trust on the PCs you install on), and prints the PFX
    as base64 for the MSIX_CERTIFICATE repository secret. The subject must match the
    package publisher, so set the MSIX_PUBLISHER repository variable to the same value.
    Not for the Store: Partner Center signs Store packages itself.
#>
param(
    [string] $Subject = "CN=EyeBrowse Test",
    [Parameter(Mandatory)] [SecureString] $Password
)
$ErrorActionPreference = "Stop"

$certificate = New-SelfSignedCertificate -Type Custom -Subject $Subject -KeyUsage DigitalSignature `
    -FriendlyName "EyeBrowse test signing" -CertStoreLocation Cert:\CurrentUser\My `
    -NotAfter (Get-Date).AddYears(3) `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

Export-PfxCertificate -Cert $certificate -FilePath EyeBrowse-test.pfx -Password $Password | Out-Null
Export-Certificate -Cert $certificate -FilePath EyeBrowse-test.cer | Out-Null
Remove-Item "Cert:\CurrentUser\My\$($certificate.Thumbprint)"

Write-Host "Wrote EyeBrowse-test.pfx and EyeBrowse-test.cer for $Subject"
Write-Host "MSIX_CERTIFICATE secret (base64 of the .pfx):"
[Convert]::ToBase64String([IO.File]::ReadAllBytes((Resolve-Path EyeBrowse-test.pfx)))
