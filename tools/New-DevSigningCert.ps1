<#
.SYNOPSIS
Creates and trusts the local development code-signing certificate (once).

Creates a self-signed code-signing certificate under CN=AceDrive Development in
the current user's personal store, then trusts it as a root and as a publisher
so Authenticode signatures made with it validate on this machine.

WHY per-user stores: no elevation is required and the trust is scoped to this
account rather than the whole machine. The private key never leaves the Windows
certificate store and is never written into the repository.

Idempotent: re-running reuses the existing certificate.

.NOTES
Adding certificates to the Root store needs write access to
HKCU\Software\Microsoft\SystemCertificates, which some sandboxed shells deny.
Run this from a normal PowerShell window.
#>
[CmdletBinding()]
param(
    [string]$Subject = 'CN=AceDrive Development',
    [int]$Years = 5
)

$ErrorActionPreference = 'Stop'

$existing = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert -ErrorAction SilentlyContinue |
    Where-Object { $_.Subject -eq $Subject } |
    Select-Object -First 1

if ($existing) {
    Write-Host "Reusing existing certificate $($existing.Thumbprint)"
    $cert = $existing
}
else {
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $Subject `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -KeyUsage DigitalSignature `
        -KeyExportPolicy Exportable `
        -NotAfter (Get-Date).AddYears($Years) `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3')

    Write-Host "Created certificate $($cert.Thumbprint)"
}

# Authenticode needs the self-signed cert to chain to a trusted root, and the
# publisher to be known, so it lands in both stores.
foreach ($storeName in @('Root', 'TrustedPublisher')) {
    $store = New-Object System.Security.Cryptography.X509Certificates.X509Store($storeName, 'CurrentUser')
    $store.Open('ReadWrite')
    try {
        if (-not ($store.Certificates | Where-Object { $_.Thumbprint -eq $cert.Thumbprint })) {
            $store.Add($cert)
            Write-Host "Added to CurrentUser\$storeName"
        }
        else {
            Write-Host "Already trusted in CurrentUser\$storeName"
        }
    }
    finally {
        $store.Close()
    }
}

Write-Host ''
Write-Host "Done. Thumbprint: $($cert.Thumbprint)"
