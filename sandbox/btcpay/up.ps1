<#
.SYNOPSIS
    WinToWar icin self-hosted, gercek para KULLANMAYAN BTCPay REGTEST sandbox'ini
    tek komutla ayaga kaldirir (docs/21-payment-sandbox-e2e.md Bolum 4).

.DESCRIPTION
    docs/21 Bolum 4 "Tekrarlanabilirlik zorunlu": asagidaki TUM adimlar
    otomatiktir, kullanicidan hicbir hesap/kayit/URL istenmez:
      1) Docker Compose ile Postgres/bitcoind/litecoind/NBXplorer/BTCPay ayaga kaldirilir.
      2) BTCPay hazir olana kadar beklenir.
      3) Yerel (regtest, GERCEK OLMAYAN) bir BTCPay admin hesabi otomatik kaydedilir.
      4) Bir Store olusturulur.
      5) LTC regtest hot wallet baglanir.
      6) Greenfield API key, YALNIZCA gercekten cagrilan endpoint'lerin gerektirdigi
         6 minimum permission ile olusturulur (canmodifystoresettings VERILMEZ).
      7) Webhook, WinToWar API container'ina (ayni Docker network uzerinden) isaret
         edecek sekilde olusturulur.
      8) "AllowHotWalletForAll" server policy'si (on-chain gonderim icin zorunlu
         oldugu gercek E2E'de kanitlanmis) diger tum ayarlar korunarak acilir.
      9) Regtest fee-estimation icin gereken islem/blok gecmisi olusturulur.
     10) Payment:Mode=Sandbox + BtcPayBaseUrl/ApiKey/StoreId/WebhookSecret
         `dotnet user-secrets` icine yazilir (hicbir tracked dosyaya YAZILMAZ).
     11) WinToWar API container'i (BTCPay ile ayni network'te) baslatilir.

    GUVENLIK: Bu script yalnizca REGTEST calistirir. Gercek para, gercek BTCPay
    hesabi, mainnet veya production credential kullanilmaz/olusturulmaz. Secret'lar
    konsola tam olarak yazdirilmaz (maskelenir).

    Uygulama kodu (PaymentService.cs, WalletService.cs, PayoutService.cs,
    RefundService.cs, IPaymentProvider.cs, BtcPayGreenfieldProvider.cs,
    PaymentConfig.cs, Program.cs) bu script tarafindan HIC degistirilmez.

.EXAMPLE
    .\sandbox\btcpay\up.ps1
#>

[CmdletBinding()]
param(
    [switch]$SkipApiContainer
)

# NOT: $ErrorActionPreference = "Stop" burada KASITLI OLARAK kullanilmiyor -
# Windows PowerShell 5.1'de bu ayar, docker/dotnet gibi native komutlarin normal
# ilerleme/durum bilgisi icin stderr'e yazdigi (hata olmayan) satirlari bile
# terminating error'a cevirip script'i durduruyor. Bunun yerine her kritik native
# cagridan sonra $LASTEXITCODE acikca kontrol edilir; mantiksal dogrulamalar da
# acikca throw ile yapilir.
$SandboxDir = $PSScriptRoot
$RepoRoot = Split-Path -Parent (Split-Path -Parent $SandboxDir)
$ComposeFile = Join-Path $SandboxDir "docker-compose.yml"
$EnvFile = Join-Path $SandboxDir ".env"
$ApiProject = Join-Path $RepoRoot "api"

$BtcPayUrl = "http://localhost:49392"
$ApiHealthUrl = "http://localhost:5299/api/health"
$WebhookTargetUrl = "http://wintowar-api:8080/api/webhooks/btcpay"

# Yalnizca bu regtest sandbox'ina ozel, gercek olmayan, sabit yerel kimlik
# bilgileri — gercek bir BTCPay hesabi / kisisel e-posta DEGILDIR.
$SandboxEmail = "sandbox-admin@wintowar-regtest.local"
$SandboxPassword = "WinToWar-Sandbox-Regtest-2026!Aa"

# 🔒 docs/21 Bolum 4.1 "asgari yetki": bu liste, Asama 1 denetiminde
# BtcPayGreenfieldProvider'da gercekten cagrildigi tespit edilen 4 endpoint'ten
# turetilmistir (Greenfield dokumantasyonundaki permission tablosundan dogrulandi):
#   POST   /api/v1/stores/{id}/invoices                                  -> cancreateinvoice
#   GET    /api/v1/invoices/{id}/payment-methods                         -> canviewinvoices
#   POST   /api/v1/stores/{id}/payment-methods/LTC-CHAIN/wallet/transactions
#          -> cancreatetransactions + cansigntransactions + canbroadcasttransactions
#   GET    /api/v1/stores/{id}/payment-methods/LTC-CHAIN/wallet/transactions/{txid}
#          -> canviewwallet
# canmodifystoresettings VERILMEZ (store/webhook kurulumu Greenfield API key ile
# degil, BTCPay panel oturumu uzerinden bu script tarafindan yapilir).
$RequiredPermissions = @(
    "btcpay.store.cancreateinvoice",
    "btcpay.store.canviewinvoices",
    "btcpay.store.cancreatetransactions",
    "btcpay.store.cansigntransactions",
    "btcpay.store.canbroadcasttransactions",
    "btcpay.store.canviewwallet"
)

function Write-Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-Ok($msg) { Write-Host "    OK: $msg" -ForegroundColor Green }
function Write-Warn2($msg) { Write-Host "    UYARI: $msg" -ForegroundColor Yellow }

function Get-Mask([string]$value) {
    if ([string]::IsNullOrEmpty($value)) { return "(bos)" }
    if ($value.Length -le 8) { return "****" }
    return "$($value.Substring(0,4))...$($value.Substring($value.Length-4,4))"
}

# ---------------------------------------------------------------------------
# HTTP katmani: Windows PowerShell 5.1'deki Invoke-WebRequest, -MaximumRedirection 0
# ile calisirken (redirect Location header'ini yakalamak icin gerekli) dogrudan
# InvalidOperationException firlatiyor (istek hic gonderilmeden). Bu yuzden BTCPay'in
# kendi web arayuzune karsi yapilan TUM cagrilarda ham System.Net.HttpWebRequest
# kullanilir; oturum, Invoke-WebRequest'in WebRequestSession'iyla AYNI mantikta tek
# bir CookieContainer paylasilarak yonetilir.
function New-SandboxSession { return New-Object System.Net.CookieContainer }

function Invoke-Sandbox {
    param(
        [string]$Method = "GET",
        [Parameter(Mandatory)][string]$Uri,
        [System.Net.CookieContainer]$Cookies,
        [hashtable]$FormBody = $null,
        [hashtable]$Headers = $null
    )
    $req = [System.Net.HttpWebRequest]::Create($Uri)
    $req.Method = $Method
    $req.AllowAutoRedirect = $false
    $req.Timeout = 30000
    if ($Cookies) { $req.CookieContainer = $Cookies }
    if ($Headers) {
        foreach ($k in $Headers.Keys) { $req.Headers[$k] = $Headers[$k] }
    }
    if ($FormBody) {
        $pairs = foreach ($k in $FormBody.Keys) {
            [System.Uri]::EscapeDataString($k) + "=" + [System.Uri]::EscapeDataString([string]$FormBody[$k])
        }
        $bodyString = $pairs -join "&"
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($bodyString)
        $req.ContentType = "application/x-www-form-urlencoded"
        $req.ContentLength = $bytes.Length
        $stream = $req.GetRequestStream()
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Close()
    }
    else {
        $req.ContentLength = 0
    }
    $resp = $null
    try {
        $resp = $req.GetResponse()
    }
    catch [System.Net.WebException] {
        if ($_.Exception.Response) { $resp = $_.Exception.Response }
        else { throw }
    }
    $stream = $resp.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($stream)
    $content = $reader.ReadToEnd()
    $reader.Close()
    $result = [PSCustomObject]@{
        StatusCode = [int]$resp.StatusCode
        Headers    = $resp.Headers
        Content    = $content
    }
    $resp.Close()
    return $result
}

function Get-Token([string]$html) {
    $m = [regex]::Match($html, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $m.Success) { throw "Anti-forgery token bulunamadi." }
    return $m.Groups[1].Value
}

function Get-InputValue([string]$html, [string]$id) {
    $pattern = 'id="' + [regex]::Escape($id) + '"[^>]*value="([^"]*)"'
    $m = [regex]::Match($html, $pattern)
    if ($m.Success) { return $m.Groups[1].Value }
    $pattern2 = 'value="([^"]*)"[^>]*id="' + [regex]::Escape($id) + '"'
    $m2 = [regex]::Match($html, $pattern2)
    if ($m2.Success) { return $m2.Groups[1].Value }
    return $null
}

function Get-CheckboxChecked([string]$html, [string]$id) {
    $m = [regex]::Match($html, '<input[^>]*id="' + [regex]::Escape($id) + '"[^>]*>')
    if (-not $m.Success) { return $false }
    return $m.Value -match 'checked="checked"'
}

Write-Host "WinToWar BTCPay REGTEST Sandbox - Kurulum" -ForegroundColor Magenta
Write-Host "Gercek para/gercek BTCPay hesabi KULLANILMAZ. Yalnizca regtest." -ForegroundColor Magenta

# ---------------------------------------------------------------------------
Write-Step "Docker kontrol ediliyor"
$dockerVersion = docker --version 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Error "Docker bulunamadi. Docker Desktop kurulu ve PATH'te olmali."
    exit 1
}
docker info *>$null
if ($LASTEXITCODE -ne 0) {
    Write-Warn2 "Docker daemon calismiyor gibi görünüyor, Docker Desktop baslatiliyor..."
    Start-Process "C:\Program Files\Docker\Docker\Docker Desktop.exe" -ErrorAction SilentlyContinue
    $ready = $false
    for ($i = 0; $i -lt 24; $i++) {
        Start-Sleep -Seconds 5
        docker info *>$null
        if ($LASTEXITCODE -eq 0) { $ready = $true; break }
    }
    if (-not $ready) {
        Write-Error "Docker daemon 120 saniye icinde hazir olmadi. Docker Desktop'i elle baslatip tekrar deneyin."
        exit 1
    }
}
Write-Ok "Docker hazir ($dockerVersion)"

# Compose, wintowar-api servisinin BTCPAY_API_KEY/STORE_ID/WEBHOOK_SECRET degiskenlerini
# her cagride (bu servisler baslatilmasa bile) dogrulamaya calisip uyari basiyor -
# bootstrap tamamlanana kadar bos bir .env ile bu gurultu onlenir.
if (-not (Test-Path $EnvFile)) {
    "BTCPAY_API_KEY=`nBTCPAY_STORE_ID=`nWEBHOOK_SECRET=`n" | Set-Content -Path $EnvFile -Encoding utf8 -NoNewline
}

# ---------------------------------------------------------------------------
Write-Step "Altyapi + BTCPay container'lari baslatiliyor (docker compose up)"
docker compose --env-file $EnvFile -f $ComposeFile up -d postgres bitcoind litecoind nbxplorer btcpayserver
if ($LASTEXITCODE -ne 0) { Write-Error "docker compose up basarisiz oldu."; exit 1 }
Write-Ok "Container'lar baslatildi"

# ---------------------------------------------------------------------------
Write-Step "BTCPay hazir olana kadar bekleniyor (ilk kurulumda EF migration'lari birkac dakika surebilir)"
$btcpayReady = $false
for ($i = 0; $i -lt 90; $i++) {
    try {
        $r = Invoke-Sandbox -Uri $BtcPayUrl
        if ($r.StatusCode -gt 0) { $btcpayReady = $true; break }
    }
    catch { }
    Start-Sleep -Seconds 5
}
if (-not $btcpayReady) {
    Write-Error "BTCPay 450 saniye icinde ayaga kalkmadi. 'docker compose -f $ComposeFile logs btcpayserver' ile kontrol edin."
    exit 1
}
Write-Ok "BTCPay yaniyor ($BtcPayUrl)"

# ---------------------------------------------------------------------------
Write-Step "Yerel regtest admin hesabi olusturuluyor (GERCEK BTCPay hesabi DEGIL)"
$session = New-SandboxSession
$registerPage = Invoke-Sandbox -Uri "$BtcPayUrl/register" -Cookies $session
$token = Get-Token $registerPage.Content
$registerResp = Invoke-Sandbox -Method POST -Uri "$BtcPayUrl/register" -Cookies $session -FormBody @{
    Email = $SandboxEmail
    Password = $SandboxPassword
    ConfirmPassword = $SandboxPassword
    __RequestVerificationToken = $token
}
$loggedIn = $false
if ($registerResp.StatusCode -eq 302) {
    $loggedIn = $true
    Write-Ok "Yeni sandbox hesabi kaydedildi ($SandboxEmail)"
}
else {
    Write-Warn2 "Kayit basarisiz (muhtemelen hesap zaten var) - login deneniyor"
    $loginPage = Invoke-Sandbox -Uri "$BtcPayUrl/login" -Cookies $session
    $loginToken = Get-Token $loginPage.Content
    $loginResp = Invoke-Sandbox -Method POST -Uri "$BtcPayUrl/login" -Cookies $session -FormBody @{
        Email = $SandboxEmail
        Password = $SandboxPassword
        __RequestVerificationToken = $loginToken
    }
    if ($loginResp.StatusCode -eq 302) {
        $loggedIn = $true
        Write-Ok "Mevcut sandbox hesabiyla giris yapildi"
    }
}
if (-not $loggedIn) { Write-Error "Sandbox hesabina kayit/login basarisiz oldu."; exit 1 }

# ---------------------------------------------------------------------------
Write-Step "Store olusturuluyor / mevcut store tespit ediliyor"
$homeResp = Invoke-Sandbox -Uri "$BtcPayUrl/" -Cookies $session
$homeLocation = $homeResp.Headers["Location"]
$storeId = $null
if ($homeLocation -match "/stores/create") {
    $storeCreatePage = Invoke-Sandbox -Uri "$BtcPayUrl/stores/create" -Cookies $session
    $storeToken = Get-Token $storeCreatePage.Content
    $storeCreateResp = Invoke-Sandbox -Method POST -Uri "$BtcPayUrl/stores/create" -Cookies $session -FormBody @{
        Name = "WinToWar Regtest Sandbox"
        DefaultCurrency = "USD"
        PreferredExchange = ""
        CanEditPreferredExchange = "True"
        __RequestVerificationToken = $storeToken
    }
    $loc = $storeCreateResp.Headers["Location"]
    $m = [regex]::Match($loc, "/stores/([^/]+)")
    if (-not $m.Success) { throw "Store olusturuldu ama ID Location header'indan cikartilamadi: $loc" }
    $storeId = $m.Groups[1].Value
    Write-Ok "Yeni store olusturuldu: $storeId"
}
else {
    $m = [regex]::Match($homeLocation, "/stores/([^/]+)")
    if ($m.Success) {
        $storeId = $m.Groups[1].Value
        Write-Ok "Mevcut store bulundu: $storeId"
    }
    else {
        throw "Store ID tespit edilemedi (home redirect: $homeLocation). Once '.\sandbox\btcpay\down.ps1' calistirip tekrar deneyin."
    }
}

# ---------------------------------------------------------------------------
Write-Step "LTC regtest hot wallet baglaniyor"
$hwPage = Invoke-Sandbox -Uri "$BtcPayUrl/stores/$storeId/onchain/LTC/generate/HotWallet" -Cookies $session
$hwToken = Get-Token $hwPage.Content
$hwResp = Invoke-Sandbox -Method POST -Uri "$BtcPayUrl/stores/$storeId/onchain/LTC/generate/HotWallet" -Cookies $session -FormBody @{
    ScriptPubKeyType = "Segwit"
    SavePrivateKeys = "True"
    Passphrase = ""
    passphrase_conf = ""
    __RequestVerificationToken = $hwToken
}
$mnemonicMatch = [regex]::Match($hwResp.Content, 'name="mnemonic" value="([^"]*)"')
$returnUrlMatch = [regex]::Match($hwResp.Content, 'name="returnUrl" value="([^"]*)"')
if (-not $mnemonicMatch.Success) { throw "LTC wallet mnemonic'i alinamadi (wallet zaten bagli olabilir)." }
$mnemonic = $mnemonicMatch.Groups[1].Value
$returnUrl = $returnUrlMatch.Groups[1].Value
$seedToken = Get-Token $hwResp.Content
$null = Invoke-Sandbox -Method POST -Uri "$BtcPayUrl/recovery-seed-backup" -Cookies $session -FormBody @{
    cryptoCode = "LTC"
    mnemonic = $mnemonic
    passphrase = ""
    isStored = "true"
    requireConfirm = "true"
    returnUrl = $returnUrl
    __RequestVerificationToken = $seedToken
}
$null = Invoke-Sandbox -Uri "$BtcPayUrl/stores/$storeId/onchain/LTC/generate/confirm" -Cookies $session
Write-Ok "LTC hot wallet baglandi (regtest, gercek para YOK)"

# ---------------------------------------------------------------------------
Write-Step "Greenfield API key olusturuluyor (yalnizca 6 minimum izin)"
$apiKeyPage = Invoke-Sandbox -Uri "$BtcPayUrl/account/addapikey" -Cookies $session
$permMatches = [regex]::Matches($apiKeyPage.Content, 'PermissionValues\[(\d+)\]\.Permission" value="([^"]+)"')
$permByIndex = @{}
foreach ($pm in $permMatches) { $permByIndex[[int]$pm.Groups[1].Value] = $pm.Groups[2].Value }
if ($permByIndex.Count -eq 0) { throw "API key izin listesi sayfadan okunamadi." }

$targetIndices = @()
foreach ($perm in $RequiredPermissions) {
    $found = $permByIndex.GetEnumerator() | Where-Object { $_.Value -eq $perm }
    if (-not $found) { throw "Beklenen izin sayfada bulunamadi: $perm" }
    $targetIndices += $found.Name
}

$apiKeyToken = Get-Token $apiKeyPage.Content
$apiKeyBody = @{ Label = "WinToWar Sandbox (auto)"; __RequestVerificationToken = $apiKeyToken }
foreach ($idx in $permByIndex.Keys) {
    $apiKeyBody["PermissionValues[$idx].Permission"] = $permByIndex[$idx]
    $apiKeyBody["PermissionValues[$idx].StoreMode"] = "AllStores"
    if ($targetIndices -contains $idx) { $apiKeyBody["PermissionValues[$idx].Value"] = "true" }
}
$null = Invoke-Sandbox -Method POST -Uri "$BtcPayUrl/account/addapikey" -Cookies $session -FormBody $apiKeyBody

$apiKeysPage = Invoke-Sandbox -Uri "$BtcPayUrl/account/apikeys" -Cookies $session
$labelIdx = $apiKeysPage.Content.IndexOf("WinToWar Sandbox (auto)")
if ($labelIdx -lt 0) { throw "Olusturulan API key sayfada bulunamadi." }
$afterLabel = $apiKeysPage.Content.Substring($labelIdx)
$codeMatch = [regex]::Match($afterLabel, '<code>([a-f0-9]+)</code>')
if (-not $codeMatch.Success) { throw "API key degeri sayfadan okunamadi." }
$apiKey = $codeMatch.Groups[1].Value

# Kendi kendini dogrulama: gercekten yalnizca istenen 6 izin mi var, canli API'den kontrol.
$verifyResp = Invoke-Sandbox -Uri "$BtcPayUrl/api/v1/api-keys/current" -Headers @{ Authorization = "token $apiKey" }
$verifyJson = $verifyResp.Content | ConvertFrom-Json
$actualPerms = $verifyJson.permissions | Sort-Object
$expectedPerms = $RequiredPermissions | Sort-Object
if (($actualPerms -join ",") -ne ($expectedPerms -join ",")) {
    throw "API key izinleri beklenenle uyusmuyor! Beklenen: $($expectedPerms -join ', ') / Gercek: $($actualPerms -join ', ')"
}
if ($actualPerms -contains "btcpay.store.canmodifystoresettings") {
    throw "GUVENLIK: API key'e canmodifystoresettings verilmis, bu beklenmiyor!"
}
Write-Ok "API key olusturuldu ve dogrulandi: $(Get-Mask $apiKey) - tam olarak $(($actualPerms).Count) izin, canmodifystoresettings YOK"

# ---------------------------------------------------------------------------
Write-Step "Webhook olusturuluyor (WinToWar API container'ina, ayni Docker network uzerinden)"
$webhookNewPage = Invoke-Sandbox -Uri "$BtcPayUrl/stores/$storeId/webhooks/new" -Cookies $session
$webhookToken = Get-Token $webhookNewPage.Content
$prefilledSecret = Get-InputValue $webhookNewPage.Content "Secret"
$null = Invoke-Sandbox -Method POST -Uri "$BtcPayUrl/stores/$storeId/webhooks/new" -Cookies $session -FormBody @{
    PayloadUrl = $WebhookTargetUrl
    Secret = $prefilledSecret
    AutomaticRedelivery = "true"
    Active = "true"
    Everything = "true"
    add = "New"
    __RequestVerificationToken = $webhookToken
}
$webhooksListPage = Invoke-Sandbox -Uri "$BtcPayUrl/stores/$storeId/webhooks" -Cookies $session
$whMatch = [regex]::Match($webhooksListPage.Content, 'href="/webhooks/([a-zA-Z0-9]+)"')
if (-not $whMatch.Success) { throw "Webhook olusturuldu ama ID sayfadan okunamadi." }
$webhookId = $whMatch.Groups[1].Value
$webhookEditPage = Invoke-Sandbox -Uri "$BtcPayUrl/webhooks/$webhookId" -Cookies $session
$webhookSecret = Get-InputValue $webhookEditPage.Content "Secret"
if ([string]::IsNullOrEmpty($webhookSecret)) { throw "Webhook secret'i sayfadan okunamadi." }
Write-Ok "Webhook olusturuldu -> $WebhookTargetUrl (secret: $(Get-Mask $webhookSecret))"

# ---------------------------------------------------------------------------
Write-Step "Server policy: AllowHotWalletForAll aciliyor (on-chain gonderim icin zorunlu, digerleri korunuyor)"
$policiesPage = Invoke-Sandbox -Uri "$BtcPayUrl/server/policies" -Cookies $session
$policiesToken = Get-Token $policiesPage.Content
$checkboxFields = @("EnableRegistration","RequiresConfirmedEmail","RequiresUserApproval","EnableNonAdminCreateUserApi","AllowLightningInternalNodeForAll","AllowHotWalletForAll","AllowCreateColdWalletForAll","AllowSearchEngines","PluginPreReleases")
$policyBody = @{ command = "Save"; __RequestVerificationToken = $policiesToken }
foreach ($f in $checkboxFields) {
    $checked = Get-CheckboxChecked $policiesPage.Content $f
    if ($f -eq "AllowHotWalletForAll") { $checked = $true }  # gercek E2E'de zorunlu oldugu kanitlandi
    $policyBody[$f] = $(if ($checked) { "true" } else { "false" })
}
foreach ($f in @("StoreQuota","RegisterPageRedirect","PluginSource","RootAppId")) {
    $v = Get-InputValue $policiesPage.Content $f
    $policyBody[$f] = $(if ($null -ne $v) { $v } else { "" })
}
$policyBody["__Invariant"] = "StoreQuota"
$policyBody["LangTranslation"] = "English"
$policyBody["BlockExplorerLinks[0].PaymentMethodId"] = "LTC-CHAIN"
$policyBody["BlockExplorerLinks[0].Link"] = (Get-InputValue $policiesPage.Content "BlockExplorerLinks_0__Link")
$policyBody["BlockExplorerLinks[1].PaymentMethodId"] = "BTC-CHAIN"
$policyBody["BlockExplorerLinks[1].Link"] = (Get-InputValue $policiesPage.Content "BlockExplorerLinks_1__Link")
$null = Invoke-Sandbox -Method POST -Uri "$BtcPayUrl/server/policies" -Cookies $session -FormBody $policyBody

$policiesCheck = Invoke-Sandbox -Uri "$BtcPayUrl/server/policies" -Cookies $session
if (-not (Get-CheckboxChecked $policiesCheck.Content "AllowHotWalletForAll")) {
    throw "AllowHotWalletForAll acilamadi - on-chain gonderim (payout/withdrawal) 503 ile basarisiz olacaktir."
}
Write-Ok "AllowHotWalletForAll acik, diger policy degerleri korundu"

# ---------------------------------------------------------------------------
Write-Step "Regtest fee-estimation gecmisi olusturuluyor (litecoind icin gerekli)"
for ($i = 0; $i -lt 15; $i++) {
    for ($j = 0; $j -lt 3; $j++) {
        $addr = (docker compose -f $ComposeFile exec -T litecoind litecoin-cli -regtest -datadir=/data getnewaddress 2>$null).Trim()
        docker compose -f $ComposeFile exec -T litecoind litecoin-cli -regtest -datadir=/data sendtoaddress $addr 0.5 2>$null | Out-Null
    }
    docker compose -f $ComposeFile exec -T litecoind litecoin-cli -regtest -datadir=/data -generate 1 2>$null | Out-Null
}
$feeCheckRaw = docker compose -f $ComposeFile exec -T litecoind litecoin-cli -regtest -datadir=/data estimatesmartfee 6 2>$null
$feeCheck = $feeCheckRaw | ConvertFrom-Json
if (-not $feeCheck.feerate) {
    Write-Warn2 "Fee estimation hala hazir degil - ilk on-chain gonderim gecici olarak basarisiz olabilir, script yine de devam ediyor."
}
else {
    Write-Ok "Fee estimation hazir (feerate=$($feeCheck.feerate))"
}

# ---------------------------------------------------------------------------
Write-Step "Secret'lar dotnet user-secrets icine yaziliyor (hicbir tracked dosyaya YAZILMAZ)"
@"
BTCPAY_API_KEY=$apiKey
BTCPAY_STORE_ID=$storeId
WEBHOOK_SECRET=$webhookSecret
"@ | Set-Content -Path $EnvFile -Encoding utf8 -NoNewline
# ^ sandbox/btcpay/.env : mevcut .gitignore'daki ".env" deseniyle zaten kapsanir
# (git check-ignore ile dogrulandi), commit edilmez - yalnizca wintowar-api
# container'ini baslatirken docker compose'un okumasi icin yazilir.

dotnet user-secrets set "Payment:Mode" "Sandbox" --project $ApiProject | Out-Null
dotnet user-secrets set "Payment:BtcPayBaseUrl" $BtcPayUrl --project $ApiProject | Out-Null
dotnet user-secrets set "Payment:BtcPayApiKey" $apiKey --project $ApiProject | Out-Null
dotnet user-secrets set "Payment:BtcPayStoreId" $storeId --project $ApiProject | Out-Null
dotnet user-secrets set "Payment:WebhookSecret" $webhookSecret --project $ApiProject | Out-Null
Write-Ok "user-secrets guncellendi (host'ta 'dotnet run' ile de kullanilabilir)"

# ---------------------------------------------------------------------------
if (-not $SkipApiContainer) {
    Write-Step "WinToWar API container'i BTCPay ile ayni Docker network'unde baslatiliyor"
    docker compose --env-file $EnvFile -f $ComposeFile up -d --build wintowar-api
    if ($LASTEXITCODE -ne 0) { Write-Error "wintowar-api container'i baslatilamadi."; exit 1 }

    $apiReady = $false
    for ($i = 0; $i -lt 24; $i++) {
        try {
            $h = Invoke-Sandbox -Uri $ApiHealthUrl
            if ($h.StatusCode -eq 200) { $apiReady = $true; break }
        } catch {}
        Start-Sleep -Seconds 5
    }
    if (-not $apiReady) {
        Write-Warn2 "WinToWar API 120 saniye icinde /api/health'e yanit vermedi. 'docker compose -f $ComposeFile logs wintowar-api' ile kontrol edin."
    }
    else {
        Write-Ok "WinToWar API hazir (http://localhost:5299)"
    }
}

# ---------------------------------------------------------------------------
Write-Host "`n================== SANDBOX HAZIR ==================" -ForegroundColor Magenta
Write-Host "Network: REGTEST (gercek para YOK)"
Write-Host "BTCPay: $BtcPayUrl"
Write-Host "Store ID: $storeId"
Write-Host "Webhook: $WebhookTargetUrl"
Write-Host "API key: $(Get-Mask $apiKey)"
Write-Host "WinToWar API: http://localhost:5299"
Write-Host "Kapatmak icin: .\sandbox\btcpay\down.ps1"
Write-Host "=====================================================" -ForegroundColor Magenta
