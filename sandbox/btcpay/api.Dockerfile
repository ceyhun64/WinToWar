# WinToWar API — yalnızca BTCPay regtest sandbox'ı için (developer tooling).
# Gerçek bir yayın (deployment) image'ı değildir; docs/21-payment-sandbox-e2e.md
# Bölüm 4 "Webhook erişimi" kararının uygulanışıdır: BTCPay container'ı host'taki
# `localhost`'a güvenilir şekilde ulaşamadığı için (Windows/WSL2 host<->container
# erişim sorunu, gerçek E2E'de kanıtlandı), API aynı Docker network'ünde bir
# container olarak çalıştırılır ve webhook `http://wintowar-api:8080/...` adresine
# gider — böylece ngrok/Cloudflare gibi bir dış tünel bağımlılığı hiç doğmaz.
# Build context: ../../api (bkz. docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "api.dll"]
