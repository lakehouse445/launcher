# Create a self-signed certificate for code signing
$cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject "CN=ClassicCounter Team" -KeyAlgorithm RSA -KeyLength 2048 -Provider "Microsoft Enhanced RSA and AES Cryptographic Provider" -KeyExportPolicy Exportable -KeyUsage DigitalSignature -CertStoreLocation Cert:\CurrentUser\My

# Export the certificate to a PFX file
$password = ConvertTo-SecureString -String "ClassicCounter123!" -Force -AsPlainText
Export-PfxCertificate -cert $cert -FilePath "ClassicCounterCodeSigning.pfx" -Password $password

Write-Host "Certificate created: ClassicCounterCodeSigning.pfx"
Write-Host "Password: ClassicCounter123!"
Write-Host "Thumbprint: $($cert.Thumbprint)"
