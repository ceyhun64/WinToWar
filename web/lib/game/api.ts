import { getOrCreatePlayerId } from "@/lib/identity";
import type { PaymentInvoiceDto } from "@/lib/payments/types";
import type { GameConfigDto, MapDto, MatchStateDto, RoomType } from "./types";

// 🛠️ Varsayım: backend geliştirme ortamında http://localhost:5019 üzerinde çalışır
// (bkz. api/Properties/launchSettings.json). Farklı bir ortamda NEXT_PUBLIC_API_BASE_URL ile geçilebilir.
export const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5019";

export type JoinRoomOutcome =
  | "Joined"
  | "InsufficientBalance"
  | "RoomFull"
  | "PayoutAddressRequired"
  | "InvalidPayoutAddress";

export interface JoinRoomResult {
  outcome: JoinRoomOutcome;
  matchId: string | null;
  playerId: string | null;
  slot: number | null;
  shortfallUsd: string | null;
  invoice: PaymentInvoiceDto | null;
}

async function parseResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const message = await res.text();
    throw new Error(message || `İstek başarısız oldu (${res.status})`);
  }
  return res.json() as Promise<T>;
}

/** docs/03-game-rules.md Bölüm 7: Practice tek paylaşılan otomatik eşleşme kuyruğudur, ödeme akışına hiç girmez. */
export async function joinPracticeRoom(playerName: string): Promise<JoinRoomResult> {
  const res = await fetch(`${API_BASE_URL}/api/rooms/practice/join`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ playerId: getOrCreatePlayerId(), playerName, payoutAddress: null }),
  });
  return parseResponse<JoinRoomResult>(res);
}

/** Standart odaya hızlı katılım: dolmamış açık bir Standart maç varsa ona, yoksa yeni bir tanesine. */
export async function joinStandardRoom(playerName: string, payoutAddress?: string): Promise<JoinRoomResult> {
  const res = await fetch(`${API_BASE_URL}/api/rooms/standard/join`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ playerId: getOrCreatePlayerId(), playerName, payoutAddress: payoutAddress ?? null }),
  });
  return parseResponse<JoinRoomResult>(res);
}

/** payoutAddress opsiyonel: yalnızca bakiye yetersiz kaldığında bir top-up invoice'ı açmak için kullanılır. */
export async function joinRoom(matchId: string, playerName: string, payoutAddress?: string): Promise<JoinRoomResult> {
  const res = await fetch(`${API_BASE_URL}/api/rooms/${matchId}/join`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ playerId: getOrCreatePlayerId(), playerName, payoutAddress: payoutAddress ?? null }),
  });
  return parseResponse<JoinRoomResult>(res);
}

export interface RoomSummary {
  matchId: string;
  /** docs/08-page-content.md Bölüm 3.4: kurucunun görünen adından türetilen salt-okunur oda kimliği (ör. "Ali'nin Odası"). */
  roomName: string;
  playerCount: number;
  maxPlayers: number;
  entryFeeUsd: string;
  fogOfWar: boolean;
  greyRegionDefenseCount: number;
  isPasswordProtected: boolean;
}

export async function listRooms(type: RoomType): Promise<RoomSummary[]> {
  const res = await fetch(`${API_BASE_URL}/api/rooms?type=${type}`);
  return parseResponse<RoomSummary[]>(res);
}

export interface CreateVipRoomInput {
  playerName: string;
  maxPlayers: number;
  greyRegionDefenseCount: number;
  fogOfWar: boolean;
  entryFeeUsd: number;
  password?: string;
  payoutAddress?: string;
}

export async function createVipRoom(input: CreateVipRoomInput): Promise<JoinRoomResult> {
  const res = await fetch(`${API_BASE_URL}/api/rooms/vip`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      playerId: getOrCreatePlayerId(),
      playerName: input.playerName,
      maxPlayers: input.maxPlayers,
      greyRegionDefenseCount: input.greyRegionDefenseCount,
      fogOfWar: input.fogOfWar,
      entryFeeUsd: input.entryFeeUsd,
      password: input.password || null,
      payoutAddress: input.payoutAddress || null,
    }),
  });
  return parseResponse<JoinRoomResult>(res);
}

export async function getRoomByInviteToken(inviteToken: string): Promise<RoomSummary> {
  const res = await fetch(`${API_BASE_URL}/api/rooms/invite/${inviteToken}`);
  return parseResponse<RoomSummary>(res);
}

export async function verifyRoomPassword(matchId: string, password: string): Promise<boolean> {
  const res = await fetch(`${API_BASE_URL}/api/rooms/${matchId}/verify-password`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ password }),
  });
  return parseResponse<boolean>(res);
}

/** docs/07-pages.md `/mac/[matchId]`: SignalR gerektirmeyen, salt-okunur bir anlık görüntü. */
export async function getMatchSnapshot(matchId: string): Promise<MatchStateDto> {
  const res = await fetch(`${API_BASE_URL}/api/matches/${matchId}`);
  return parseResponse<MatchStateDto>(res);
}

export async function getMap(): Promise<MapDto> {
  const res = await fetch(`${API_BASE_URL}/api/matches/map`);
  return parseResponse<MapDto>(res);
}

export async function getGameConfig(): Promise<GameConfigDto> {
  const res = await fetch(`${API_BASE_URL}/api/matches/config`);
  return parseResponse<GameConfigDto>(res);
}
