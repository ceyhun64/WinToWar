# BTCPay REGTEST Sandbox — WinToWar

> Görev kaynağı: `docs/21-payment-sandbox-e2e.md` (Bölüm 4). Bu klasör **uygulama
> kodu değildir**, geliştirme ortamı altyapısıdır — bu yüzden `api/` ve `web/`
> ağacının dışında durur (bkz. `docs/21` Bölüm 4 "Dosya konumu" kararı).

Müşterinin iyzico sandbox beklentisinin BTCPay karşılığı: gerçek sağlayıcıyla,
gerçek Greenfield protokolü üzerinden, ama **gerçek para olmadan** konuşulur.
"Fake payment" + "mine block", iyzico'nun test kartlarının birebir karşılığıdır:
anında, deterministik, dış bağımlılıksız (faucet yok), sınırsız tekrarlanabilir.

## Tek komutla ayağa kaldırma

```powershell
.\sandbox\btcpay\up.ps1
```

Kapatma / tam temizlik (store, wallet, API key, webhook dahil hiçbir şey kalmaz):

```powershell
.\sandbox\btcpay\down.ps1
```

Kullanıcıdan **hiçbir hesap açması, URL vermesi veya kayıt bilgisi girmesi
istenmez** — script BTCPay'in kendi arayüzü/Greenfield API'si üzerinden admin
hesabını, store'u, LTC hot wallet'ını, API key'i ve webhook'u kendisi kurar.
Sandbox silinirse aynı komutla sıfırdan yeniden kurulur.

> **Neden PowerShell (`up.ps1`), `up.sh` değil?** `docs/21` Bölüm 4 tek komut
> şartını koyar, betiğin dilini değil (`ör. ./sandbox/btcpay/up.sh`). Bu depo
> Windows üzerinde geliştiriliyor ve bootstrap adımları BTCPay'in anti-forgery
> korumalı web formlarıyla oturum paylaşımı gerektiriyor; bu, PowerShell'in
> `System.Net` yığınıyla ek bağımlılık olmadan yapılabiliyor.

## Ne kurulur

| Servis | İmaj | Not |
| --- | --- | --- |
| `postgres` | `btcpayserver/postgres:18.4` | BTCPay + NBXplorer + **WinToWar API**'nin veritabanları (`pg-init/`) |
| `bitcoind` | `btcpayserver/bitcoin:29.2` | BTCPay çok-zincir kurulumunda zorunlu |
| `litecoind` | `btcpayserver/litecoin:0.21.5.5` | **Asıl ağ** — regtest LTC |
| `nbxplorer` | `nicolasdorier/nbxplorer:2.6.11` | Zincir indeksleyici |
| `btcpayserver` | `btcpayserver/btcpayserver:2.4.2` | http://localhost:49392 |
| `wintowar-api` | `api.Dockerfile` | http://localhost:5299, `Payment__Mode=Sandbox` |

## Webhook erişimi

BTCPay container'ı host'taki `localhost`'a güvenilir şekilde ulaşamıyor
(Windows/WSL2). Bu yüzden **tünel (ngrok/Cloudflare) kullanılmaz**; WinToWar API
aynı compose ağında bir container olarak çalışır ve webhook doğrudan
`http://wintowar-api:8080/api/webhooks/btcpay` adresine gider. Bu, dış bir
servise (ve onun oturum/limitine) bağımlılığı tamamen ortadan kaldırır.

## API key izinleri (asgari yetki)

`up.ps1` yalnızca `BtcPayGreenfieldProvider`'ın gerçekten çağırdığı 4 endpoint'in
gerektirdiği 6 izni verir ve **oluşturduktan sonra canlı API'den doğrular**
(`GET /api/v1/api-keys/current`); `canmodifystoresettings` verilmez, verilmiş
olsaydı script hata fırlatıp durur.

| Çağrılan endpoint | Gereken izin |
| --- | --- |
| `POST /api/v1/stores/{id}/invoices` | `btcpay.store.cancreateinvoice` |
| `GET /api/v1/invoices/{id}/payment-methods` | `btcpay.store.canviewinvoices` |
| `POST /api/v1/stores/{id}/payment-methods/LTC-CHAIN/wallet/transactions` | `cancreatetransactions`, `cansigntransactions`, `canbroadcasttransactions` |
| `GET  /api/v1/stores/{id}/payment-methods/LTC-CHAIN/wallet/transactions/{txid}` | `btcpay.store.canviewwallet` |

## Secrets

API key ve webhook secret'ı **hiçbir tracked dosyaya yazılmaz**:

- `dotnet user-secrets` (host'ta `dotnet run` için)
- `sandbox/btcpay/.env` (yalnızca `docker compose`'un okuması için; `.gitignore`
  kapsamında, `git check-ignore` ile doğrulandı)

`down.ps1` ikisini de temizler.

## Regtest'te ödeme yapmak / blok üretmek

```powershell
# Sandbox içindeki litecoind ile ödeme gönder (fake payment):
docker compose -f sandbox/btcpay/docker-compose.yml exec -T litecoind `
  litecoin-cli -regtest -datadir=/data sendtoaddress <invoice-adresi> <tutar>

# Blok üret (mine):
docker compose -f sandbox/btcpay/docker-compose.yml exec -T litecoind `
  litecoin-cli -regtest -datadir=/data -generate 1
```

## Production'a geçiş

`Sandbox` ile `Live` arasında **kod farkı yoktur** — yalnızca şu config alanları
değişir: `Payment:Mode`, `Payment:BtcPayBaseUrl`, `Payment:BtcPayApiKey`,
`Payment:BtcPayStoreId`, `Payment:WebhookSecret`. `Live` modunda bu alanlardan
biri boşsa uygulama **başlamaz** (fail-fast, bkz. `Program.cs`).
