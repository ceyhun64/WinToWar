FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Proje dosyasını kopyala ve bağımlılıkları yükle
COPY ["api.csproj", "./"]
RUN dotnet restore "api.csproj"

# Tüm kaynak kodunu ve Data/map.json gibi dosyaları kopyala
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Çalıştırma aşaması (Runtime)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
ENTRYPOINT ["dotnet", "api.dll"]