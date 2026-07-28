# install_cert.ps1 - Installs the Hotel Sankalp code-signing cert as trusted on this machine
$CerPath = "E:\hotel_sankalp-retreat\Setup\HotelSankalp_CodeSign.cer"

Write-Host "Installing certificate to Trusted Root CA..." -ForegroundColor Cyan
Import-Certificate -FilePath $CerPath -CertStoreLocation Cert:\LocalMachine\Root | Out-Null

Write-Host "Installing certificate to Trusted Publishers..." -ForegroundColor Cyan
Import-Certificate -FilePath $CerPath -CertStoreLocation Cert:\LocalMachine\TrustedPublisher | Out-Null

Write-Host "Done - this machine will no longer show SmartScreen warnings for Hotel Sankalp software." -ForegroundColor Green
