<#
.SYNOPSIS
    WinToWar BTCPay REGTEST sandbox'ini tamamen kapatir ve temizler
    (docs/21-payment-sandbox-e2e.md Bolum 4).

.DESCRIPTION
    docker compose down -v ile tum container/network/volume'leri kaldirir (store,
    wallet, API key, webhook DAHIL - hicbiri kalici degildir), sandbox'a ozel
    dotnet user-secrets girdilerini kaldirir ve gecici .env dosyasini siler.
    Uygulama koduna dokunmaz. Normal (Payment:Mode=Fake) Development akisini
    etkilemez - yalnizca sandbox icin yazilmis olan degerleri temizler.

.EXAMPLE
    .\sandbox\btcpay\down.ps1
#>

$ErrorActionPreference = "Continue"
$SandboxDir = $PSScriptRoot
$RepoRoot = Split-Path -Parent (Split-Path -Parent $SandboxDir)
$ComposeFile = Join-Path $SandboxDir "docker-compose.yml"
$EnvFile = Join-Path $SandboxDir ".env"
$ApiProject = Join-Path $RepoRoot "api"

Write-Host "WinToWar BTCPay REGTEST Sandbox - Kapatiliyor" -ForegroundColor Magenta

Write-Host "`n==> Container/network/volume'ler kaldiriliyor (docker compose down -v)" -ForegroundColor Cyan
docker compose -f $ComposeFile down -v
if ($LASTEXITCODE -eq 0) {
    Write-Host "    OK: Sandbox tamamen kaldirildi (store/wallet/API key/webhook dahil, hicbiri kalmadi)" -ForegroundColor Green
}
else {
    Write-Host "    UYARI: docker compose down komutu hata dondurdu, elle 'docker ps -a' ile kontrol edin." -ForegroundColor Yellow
}

Write-Host "`n==> Sandbox user-secrets temizleniyor" -ForegroundColor Cyan
foreach ($key in @(
    "Payment:Mode",
    "Payment:BtcPayBaseUrl",
    "Payment:BtcPayApiKey",
    "Payment:BtcPayStoreId",
    "Payment:WebhookSecret"
)) {
    dotnet user-secrets remove $key --project $ApiProject *>$null
}
Write-Host "    OK: user-secrets temizlendi - Development akisi tekrar Fake moda donuyor" -ForegroundColor Green

if (Test-Path $EnvFile) {
    Remove-Item $EnvFile -Force
    Write-Host "`n==> Gecici .env dosyasi silindi" -ForegroundColor Cyan
}

Write-Host "`n================== SANDBOX KAPATILDI ==================" -ForegroundColor Magenta
Write-Host "Yeniden kurmak icin: .\sandbox\btcpay\up.ps1"
Write-Host "=========================================================" -ForegroundColor Magenta
