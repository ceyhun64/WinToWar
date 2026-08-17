-- BTCPay regtest sandbox: NBXplorer ve BTCPay Server kendi veritabanlarını
-- bekliyor, resmi postgres image'ı POSTGRES_DB ile yalnızca TEK bir veritabanı
-- oluşturuyor — geri kalanı burada elle oluşturulur (yalnızca ilk container
-- başlangıcında, /docker-entrypoint-initdb.d konvansiyonu ile çalışır).
CREATE DATABASE nbxplorerregtest;
CREATE DATABASE btcpayserverregtest;

-- WinToWar API'nin kendi veritabanı (Payment/Auth/GameEvents DbContext'leri aynı
-- connection string'i paylaşır, bkz. Program.cs). Host'ta ayrı bir Postgres
-- kurulmuş olmasını gerektirmemek için sandbox'ın kendi Postgres'inde tutulur —
-- docs/21 Bölüm 4 "tek komutla sıfırdan çalışır hale gelmeli".
CREATE DATABASE wintowar;
