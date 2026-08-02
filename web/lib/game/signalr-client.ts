import * as signalR from "@microsoft/signalr";
import type { MatchStateDto } from "./types";

const HUB_URL = process.env.NEXT_PUBLIC_API_HUB_URL ?? "http://localhost:5019/hub/game";

export type MatchStateHandler = (state: MatchStateDto) => void;
export type ActionErrorHandler = (message: string) => void;

/** SignalR bağlantı yönetimi: bağlan/yeniden bağlan/mesaj dinleme. */
export class GameConnection {
  private connection: signalR.HubConnection;

  constructor() {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL)
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

  onReconnected(handler: () => void) {
    this.connection.onreconnected(handler);
  }

  async start() {
    if (this.connection.state === signalR.HubConnectionState.Disconnected) {
      await this.connection.start();
    }
  }

  async stop() {
    await this.connection.stop();
  }

  /** Maça (yeniden) bağlanır ve SignalR grubuna katılır; sunucu güncel MatchState'i yayınlar. */
  async joinMatch(matchId: string, playerId: string) {
    await this.connection.invoke("JoinMatch", matchId, playerId);
  }

  async trainSoldier(regionId: string) {
    await this.connection.invoke("TrainSoldier", regionId);
  }

  async trainGeneral(regionId: string) {
    await this.connection.invoke("TrainGeneral", regionId);
  }

  async upgradeNest(regionId: string) {
    await this.connection.invoke("UpgradeNest", regionId);
  }

  async attackRegion(fromRegionId: string, toRegionId: string, generalId: string, soldierCount: number) {
    await this.connection.invoke("AttackRegion", fromRegionId, toRegionId, generalId, soldierCount);
  }
}
