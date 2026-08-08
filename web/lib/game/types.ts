// Backend DTO'larıyla (api/Models/Dtos) birebir eşleşen tipler.
// Backend domain modeli hiçbir zaman doğrudan buraya yansıtılmaz; her zaman DTO üzerinden.

export type MatchStatus = "Lobby" | "Countdown" | "Playing" | "Completed" | "Cancelled";
export type RoomType = "Standard" | "Vip" | "Practice";

export interface RoomDto {
  type: RoomType;
  maxPlayers: number;
  greyRegionDefenseCount: number;
  fogOfWar: boolean;
  entryFeeUsd: string;
  isPasswordProtected: boolean;
  /** Standart/Practice'te boş string — yalnızca VIP'de anlamlıdır. */
  creatorPlayerId: string;
}

export interface PlayerDto {
  id: string;
  slot: number;
  name: string;
  isEliminated: boolean;
  isConnected: boolean;
  isPaymentConfirmed: boolean;
  /** docs/03-game-rules.md Bölüm 7: her zaman şeffaf gösterilir — bkz. Bot rozeti kuralı. */
  isBot: boolean;
}

export interface RegionStateDto {
  id: string;
  originalOwnerId: string | null;
  ownerId: string | null;
  soldierCount: number;
  /** Fog of War açıkken (bkz. RoomDto.fogOfWar) false olabilir — sahip/asker bilgisi sunucuda zaten gizlenmiştir. */
  isVisible: boolean;
}

export interface ArmyDto {
  id: string;
  ownerId: string;
  soldierCount: number;
  fromRegionId: string;
  toRegionId: string;
  arrivesInSeconds: number;
}

export interface MatchStateDto {
  matchId: string;
  status: MatchStatus;
  room: RoomDto;
  lobbyConfirmedCount: number;
  countdownRemainingSeconds: number | null;
  winners: string[];
  players: PlayerDto[];
  regions: RegionStateDto[];
  armies: ArmyDto[];
  startedAtUtc: string | null;
  completedAtUtc: string | null;
}

export interface MapRegionDto {
  id: string;
  name: string;
  x: number;
  y: number;
  neighborIds: string[];
}

export interface MapDto {
  regions: MapRegionDto[];
}

/** GameConfig'in (api/GameConfig.cs) arayüzde ihtiyaç duyulan alt kümesi — tek doğruluk kaynağı backend'dir. */
export interface GameConfigDto {
  standardRoomPlayerCount: number;
  vipRoomMinPlayers: number;
  vipRoomMaxPlayers: number;
  greyRegionDefenseMin: number;
  greyRegionDefenseMax: number;
  practiceRoomDefaultPlayerCount: number;
  baseProductionPerInterval: number;
  productionIntervalSeconds: number;
  productionBonusPerRegion: number;
  movementDurationSeconds: number;
  lobbyFillTimeoutSeconds: number;
  practiceLobbyFillTimeoutSeconds: number;
  lobbyCountdownSeconds: number;
  abandonmentTimeoutSeconds: number;
  resultScreenDurationSeconds: number;
  commissionRate: string;
}
