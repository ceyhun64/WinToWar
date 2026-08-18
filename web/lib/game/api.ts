import { authFetch } from "@/lib/identity";
import type { PaymentInvoiceDto } from "@/lib/payments/types";
import type { GameConfigDto, MapDto, MatchStateDto, RoomType } from "./types";

// 🛠️ Varsayım: backend geliştirme ortamında http://localhost:5019 üzerinde çalışır
// (bkz. api/Properties/launchSettings.json). Farklı bir ortamda NEXT_PUBLIC_API_URL ile geçilebilir.
export const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5019";

export type JoinRoomOutcome = "Joined" | "InsufficientBalance" | "RoomFull";

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

/**
 * docs/11-auth.md Bölüm 0.4: `/lobi`'nin tüm uçları (RoomsController) [Authorize]
 * ile korunur, playerId artık body'de taşınmaz — backend JWT'den okur.
 */

/** docs/03-game-rules.md Bölüm 7: Practice tek paylaşılan otomatik eşleşme kuyruğudur, ödeme akışına hiç girmez. */
export async function joinPracticeRoom(playerName: string): Promise<JoinRoomResult> {
  const res = await authFetch(`/api/rooms/practice/join`, {
    method: "POST",
    body: JSON.stringify({ playerName }),
  });
  return parseResponse<JoinRoomResult>(res);
}

/** Standart odaya hızlı katılım: dolmamış açık bir Standart maç varsa ona, yoksa yeni bir tanesine. */
export async function joinStandardRoom(playerName: string): Promise<JoinRoomResult> {
  const res = await authFetch(`/api/rooms/standard/join`, {
    method: "POST",
    body: JSON.stringify({ playerName }),
  });
  return parseResponse<JoinRoomResult>(res);
}

export async function joinRoom(matchId: string, playerName: string): Promise<JoinRoomResult> {
  const res = await authFetch(`/api/rooms/${matchId}/join`, {
    method: "POST",
    body: JSON.stringify({ playerName }),
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
  const res = await authFetch(`/api/rooms?type=${type}`);
  return parseResponse<RoomSummary[]>(res);
}

export interface CreateVipRoomInput {
  playerName: string;
  maxPlayers: number;
  greyRegionDefenseCount: number;
  fogOfWar: boolean;
  entryFeeUsd: number;
  password?: string;
}

export async function createVipRoom(input: CreateVipRoomInput): Promise<JoinRoomResult> {
  const res = await authFetch(`/api/rooms/vip`, {
    method: "POST",
    body: JSON.stringify({
      playerName: input.playerName,
      maxPlayers: input.maxPlayers,
      greyRegionDefenseCount: input.greyRegionDefenseCount,
      fogOfWar: input.fogOfWar,
      entryFeeUsd: input.entryFeeUsd,
      password: input.password || null,
    }),
  });
  return parseResponse<JoinRoomResult>(res);
}

export async function getRoomByInviteToken(inviteToken: string): Promise<RoomSummary> {
  const res = await authFetch(`/api/rooms/invite/${inviteToken}`);
  return parseResponse<RoomSummary>(res);
}

export async function verifyRoomPassword(matchId: string, password: string): Promise<boolean> {
  const res = await authFetch(`/api/rooms/${matchId}/verify-password`, {
    method: "POST",
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
