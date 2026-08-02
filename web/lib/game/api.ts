import type { GameConfigDto, MapDto } from "./types";

// 🛠️ Varsayım: backend geliştirme ortamında http://localhost:5019 üzerinde çalışır
// (bkz. api/Properties/launchSettings.json). Farklı bir ortamda NEXT_PUBLIC_API_BASE_URL ile geçilebilir.
export const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5019";

export interface JoinMatchResponse {
  matchId: string;
  playerId: string;
  slot: number;
}

async function parseResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const message = await res.text();
    throw new Error(message || `İstek başarısız oldu (${res.status})`);
  }
  return res.json() as Promise<T>;
}

export async function createMatch(playerName: string): Promise<JoinMatchResponse> {
  const res = await fetch(`${API_BASE_URL}/api/matches`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ playerName }),
  });
  return parseResponse<JoinMatchResponse>(res);
}

export async function joinMatch(matchId: string, playerName: string): Promise<JoinMatchResponse> {
  const res = await fetch(`${API_BASE_URL}/api/matches/${matchId}/join`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ playerName }),
  });
  return parseResponse<JoinMatchResponse>(res);
}

export async function getMap(): Promise<MapDto> {
  const res = await fetch(`${API_BASE_URL}/api/matches/map`);
  return parseResponse<MapDto>(res);
}

export async function getGameConfig(): Promise<GameConfigDto> {
  const res = await fetch(`${API_BASE_URL}/api/matches/config`);
  return parseResponse<GameConfigDto>(res);
}
