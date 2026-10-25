<#
.SYNOPSIS
Signs the overlay executable with the local development code-signing certificate.

WHY this exists: SmartScreen / Defender app reputation is keyed to the file's
hash together with its signing certificate. Every rebuild mints a new hash, so
an unsigned build is always a brand-new "unknown app" and Windows prompts on
each launch. Signing every build with the same trusted certificate keeps the
publisher identity stable across rebuilds.

The certificate is per-user and deliberately NOT committed: it lives in
Cert:\CurrentUser\My under CN=AceDrive Development. Run
tools/New-DevSigningCert.ps1 once to create and trust it.

Exits 0 even when the certificate is missing, so a clean checkout or a CI build
(which has no developer certificate) still builds normally.

.PARAMETER ExePath
Executable to sign; the build passes $(TargetPath).
#>
param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [string]$Subject = 'CN=AceDrive Development'
)

if (-not (Test-Path -LiteralPath $ExePath)) { exit 0 }

$cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert -ErrorAction SilentlyContinue |
    Where-Object { $_.Subject -eq $Subject -and $_.HasPrivateKey } |
    Select-Object -First 1

# No developer certificate on this machine: nothing to do, and never fail the
# build over a local convenience.
if (-not $cert) { exit 0 }

try {
    $sig = Set-AuthenticodeSignature -FilePath $ExePath -Certificate $cert -HashAlgorithm SHA256
    if ($sig.Status -ne 'Valid') {
        Write-Warning "Sign-Overlay: $ExePath signed with status $($sig.Status)"
    }
}
catch {
    Write-Warning "Sign-Overlay: signing $ExePath failed ($($_.Exception.Message))"
}

exit 0
