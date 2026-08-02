// Backend DTO'larıyla (api/Models/Dtos) birebir eşleşen tipler.
// Backend domain modeli hiçbir zaman doğrudan buraya yansıtılmaz; her zaman DTO üzerinden.

export type MatchStatus = "WaitingForPlayers" | "InProgress" | "Finished";
export type GeneralStatus = "Garrisoned" | "Moving" | "Dead";

export interface PlayerDto {
  id: string;
  slot: number;
  name: string;
  gold: number;
  isEliminated: boolean;
  isConnected: boolean;
}

export interface RegionStateDto {
  id: string;
  ownerId: string | null;
  nestLevel: number | null;
  garrisonSoldiers: number;
  garrisonArchers: number;
  neutralDefenseSoldiers: number;
}

export interface GeneralDto {
  id: string;
  ownerId: string;
  status: GeneralStatus;
  currentRegionId: string | null;
  respawnInSeconds: number | null;
}

export interface ArmyDto {
  id: string;
  ownerId: string;
  generalId: string;
  soldierCount: number;
  fromRegionId: string;
  toRegionId: string;
  arrivesInSeconds: number;
}

export interface MatchStateDto {
  matchId: string;
  status: MatchStatus;
  remainingSeconds: number;
  winnerId: string | null;
  players: PlayerDto[];
  regions: RegionStateDto[];
  generals: GeneralDto[];
  armies: ArmyDto[];
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
  soldierCost: number;
  generalCost: number;
  nestUpgradeToLevel2Cost: number;
  nestUpgradeToLevel3Cost: number;
  maxNestLevel: number;
  maxGeneralsPerPlayer: number;
  matchDurationSeconds: number;
}
