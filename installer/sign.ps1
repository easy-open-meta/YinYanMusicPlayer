param([Parameter(Mandatory=$true)][string]$FilePath)

$ErrorActionPreference = 'Stop'

$pfxPath = $env:YINYAN_PFX
$pfxPwd  = $env:YINYAN_CERT_PW

if (-not (Test-Path $pfxPath)) { Write-Host "PFX not found: $pfxPath"; exit 1 }
if (-not $pfxPwd)             { Write-Host 'Env YINYAN_CERT_PW missing'; exit 1 }
if (-not (Test-Path $FilePath)) { Write-Host "Target not found: $FilePath"; exit 1 }

$flags = [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet
$cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($pfxPath, $pfxPwd, $flags)

try {
    $null = Set-AuthenticodeSignature -FilePath $FilePath -Certificate $cert `
        -HashAlgorithm SHA256 -TimestampServer 'http://timestamp.digicert.com' -ErrorAction Stop
} catch {
    Write-Host ('Timestamp failed, signing without it: ' + $_.Exception.Message)
    $null = Set-AuthenticodeSignature -FilePath $FilePath -Certificate $cert -HashAlgorithm SHA256
}

$sig = Get-AuthenticodeSignature -FilePath $FilePath
if (-not $sig.SignerCertificate) {
    Write-Host ('Signing produced no signature, status=' + $sig.Status)
    exit 1
}

Write-Host ('signed ' + (Split-Path $FilePath -Leaf) + ' | status=' + $sig.Status + ' | signer=' + $sig.SignerCertificate.Subject)
exit 0