<#
.SYNOPSIS
Signs an overlay executable with a local or supplied code-signing certificate.

WHY this exists: SmartScreen / Defender app reputation is keyed to the file's
hash together with its signing certificate. Every rebuild mints a new hash, so
an unsigned build is always a brand-new "unknown app" and Windows prompts on
each launch. Signing every build with the same certificate keeps the publisher
identity stable across rebuilds.

Trust across PCs comes from the certificate, not from this script: a publicly
trusted OV/EV certificate (or Azure Trusted Signing) is trusted everywhere, the
per-user development certificate only here. Supply a trusted certificate as a
PFX through -PfxPath / ACEDRIVE_SIGN_PFX (it needs its private key), or import
it into Cert:\CurrentUser\My and pass its subject with -Subject.

The development certificate is per-user and deliberately NOT committed: it
lives in Cert:\CurrentUser\My under CN=AceDrive Development. Run
tools/New-DevSigningCert.ps1 once to create and trust it.

Exits 0 even when no certificate is available, so a clean checkout or a CI
build (which has no signing identity) still builds normally.

.PARAMETER ExePath
Executable to sign; the build passes the apphost or the published exe.

.PARAMETER Subject
Certificate subject to look for in Cert:\CurrentUser\My.

.PARAMETER PfxPath
Optional PFX with private key. Wins over the certificate store when present.

.PARAMETER PfxPassword
Password for -PfxPath; defaults to ACEDRIVE_SIGN_PFX_PASSWORD.

.PARAMETER TimestampServer
RFC 3161 server. Required for a publicly trusted signature to outlive the
certificate; ignored when empty.
#>
param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [string]$Subject = 'CN=AceDrive Development',
    [string]$PfxPath = $env:ACEDRIVE_SIGN_PFX,
    [string]$PfxPassword = $env:ACEDRIVE_SIGN_PFX_PASSWORD,
    [string]$TimestampServer = $env:ACEDRIVE_SIGN_TIMESTAMP
)

if (-not (Test-Path -LiteralPath $ExePath)) { exit 0 }

$cert = $null
$fromPfx = $false
if ($PfxPath -and (Test-Path -LiteralPath $PfxPath)) {
    try {
        $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2(
            $PfxPath, $PfxPassword)
        $fromPfx = $true
    }
    catch {
        Write-Warning "Sign-Overlay: cannot load $PfxPath ($($_.Exception.Message))"
        exit 0
    }
}
else {
    $cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert -ErrorAction SilentlyContinue |
        Where-Object { $_.Subject -eq $Subject -and $_.HasPrivateKey } |
        Select-Object -First 1
}

# No usable certificate on this machine: nothing to do, and never fail the
# build over a local convenience.
if (-not $cert) { exit 0 }

try {
    $signature = @{
        FilePath      = $ExePath
        Certificate   = $cert
        HashAlgorithm = 'SHA256'
    }
    if ($TimestampServer) { $signature['TimestampServer'] = $TimestampServer }
    $sig = Set-AuthenticodeSignature @signature
    if ($sig.Status -ne 'Valid') {
        Write-Warning "Sign-Overlay: $ExePath signed with status $($sig.Status)"
    }
}
catch {
    Write-Warning "Sign-Overlay: signing $ExePath failed ($($_.Exception.Message))"
}
finally {
    if ($fromPfx) { $cert.Dispose() }
}

exit 0
