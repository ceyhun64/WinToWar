import * as signalR from "@microsoft/signalr";
import { API_BASE_URL } from "@/lib/game/api";
import { ensureSessionLoaded, getAccessToken } from "@/lib/identity";
import type { WalletDto } from "./types";

const WALLET_HUB_URL = `${API_BASE_URL}/hub/wallet`;

export type WalletBalanceUpdatedHandler = (dto: WalletDto) => void;

/**
 * docs/16-wallet-balance-sync.md Bölüm 2: WalletHub bağlantı yönetimi —
 * lib/game/signalr-client.ts'deki bağlantı/reconnect deseniyle aynı yaklaşım
 * (accessTokenFactory, withAutomaticReconnect); ayrı dosya olarak tutulur
 * çünkü iki modül (oyun/cüzdan) farklı state taşır, aynı soyutlamaya
 * zorlanmaz (bkz. `06-coding-standards.md` "Kod Tekrarını Önleme" istisnası).
 */
export class WalletConnection {
  private connection: signalR.HubConnection;

  constructor() {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(WALLET_HUB_URL, {
        accessTokenFactory: async () => {
          await ensureSessionLoaded();
          return getAccessToken() ?? "";
        },
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();
  }

  onBalanceUpdated(handler: WalletBalanceUpdatedHandler) {
    this.connection.on("WalletBalanceUpdated", handler);
  }

  onReconnected(handler: () => void) {
    this.connection.onreconnected(handler);
  }

  onReconnecting(handler: () => void) {
    this.connection.onreconnecting(handler);
  }

  onClose(handler: () => void) {
    this.connection.onclose(handler);
  }

  async start() {
    if (this.connection.state === signalR.HubConnectionState.Disconnected) {
      await this.connection.start();
    }
  }

  async stop() {
    await this.connection.stop();
  }
}
