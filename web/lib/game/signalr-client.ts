import * as signalR from "@microsoft/signalr";
import type { PaymentConfirmedEvent } from "@/lib/payments/types";
import { ensureSessionLoaded, getAccessToken } from "@/lib/identity";
import type { MatchStateDto } from "./types";

const HUB_URL = process.env.NEXT_PUBLIC_API_HUB_URL ?? "http://localhost:5019/hub/game";

export type MatchStateHandler = (state: MatchStateDto) => void;
export type ActionErrorHandler = (message: string) => void;
export type PaymentConfirmedHandler = (event: PaymentConfirmedEvent) => void;

/**
 * SignalR bağlantı yönetimi: bağlan/yeniden bağlan/mesaj dinleme.
 * docs/11-auth.md Bölüm 1.4/3.5: [Authorize] + JWT Bearer, access_token query
 * param'ı üzerinden taşınır (SignalR handshake header taşıyamaz). accessTokenFactory
 * her (yeniden) bağlantı denemesinde çağrılır — süresi dolan bir token'la
 * yeniden bağlanmadan önce sessiz refresh denenmiş olur.
 */
export class GameConnection {
  private connection: signalR.HubConnection;

  constructor() {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL, {
        accessTokenFactory: async () => {
          await ensureSessionLoaded();
          return getAccessToken() ?? "";
        },
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();
  }

  onMatchState(handler: MatchStateHandler) {
    this.connection.on("MatchState", handler);
  }

  onActionError(handler: ActionErrorHandler) {
    this.connection.on("ActionError", handler);
  }

  /** docs/03-game-rules.md Bölüm 7: lobi zaman aşımı sunucudan bir kez bildirilir (otomatik iptal/iade yok). */
  onLobbyTimeoutReached(handler: () => void) {
    this.connection.on("LobbyTimeoutReached", handler);
  }

  /** Bölüm 3.1 (docs/05-payment.md): ödeme onaylandığında sunucudan gelen event. */
  onPaymentConfirmed(handler: PaymentConfirmedHandler) {
    this.connection.on("PaymentConfirmed", handler);
  }

  onReconnected(handler: () => void) {
    this.connection.onreconnected(handler);
  }

  /** docs/07-pages.md Sayfa Bazlı State Matrisi: bağlantı koptu, otomatik yeniden bağlanma deneniyor. */
  onReconnecting(handler: () => void) {
    this.connection.onreconnecting(handler);
  }

  /** withAutomaticReconnect'in yeniden bağlanma denemeleri tükendiğinde (veya ilk `start()` başarısız kalırsa) tetiklenir. */
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

  /**
   * Maça (yeniden) bağlanır ve SignalR grubuna katılır; sunucu güncel MatchState'i
   * yayınlar. playerId artık parametre olarak alınmaz — sunucu Context.UserIdentifier
   * (JWT sub claim) üzerinden kendisi belirler (bkz. docs/11-auth.md Bölüm 0.4).
   */
  async joinMatch(matchId: string) {
    await this.connection.invoke("JoinMatch", matchId);
  }

  async leaveLobby() {
    await this.connection.invoke("LeaveLobby");
  }

  /** docs/03-game-rules.md Bölüm 8: yalnızca VIP masa kurucusu, koltuklar dolmadan çağırabilir. */
  async startVipMatchNow() {
    await this.connection.invoke("StartVipMatchNow");
  }

  /**
   * Sürükle-bırak ile gönderim (docs/03-game-rules.md Bölüm 6/15): toRegionId,
   * fromRegionId'nin doğrudan komşusu olmalı. Asker sayısı client'tan gönderilmez,
   * sunucu kaynak bölgenin mevcut askerinden GameConfig.MinGarrisonPerSend
   * çıkararak kendisi hesaplar.
   */
  async attackRegion(fromRegionId: string, toRegionId: string) {
    await this.connection.invoke("AttackRegion", fromRegionId, toRegionId);
  }
}
