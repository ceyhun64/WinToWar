import { applySession, authFetch } from "@/lib/identity";
import type {
  AuthErrorResponse,
  AuthResponseDto,
  PlayerAccountDto,
} from "./types";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5019";

async function parseAuthResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    let message = `İstek başarısız oldu (${res.status})`;
    let code = "UNKNOWN_ERROR";
    try {
      const body = (await res.json()) as AuthErrorResponse;
      message = body.message || message;
      code = body.code || code;
    } catch {
      // Yanıt JSON değilse varsayılan mesaj kullanılır.
    }
    throw new AuthApiError(code, message);
  }
  if (res.status === 204) {
    return undefined as T;
  }
  return res.json() as Promise<T>;
}

export class AuthApiError extends Error {
  constructor(
    public code: string,
    message: string,
  ) {
    super(message);
    this.name = "AuthApiError";
  }
}

async function postPublic<T>(path: string, body: unknown): Promise<T> {
  const res = await fetch(`${API_BASE_URL}${path}`, {
    method: "POST",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  return parseAuthResponse<T>(res);
}

export interface RegisterInput {
  email: string;
  password: string;
  displayName: string;
  ageConfirmed: boolean;
  termsAccepted: boolean;
}

export async function register(input: RegisterInput): Promise<AuthResponseDto> {
  const dto = await postPublic<AuthResponseDto>("/api/auth/register", input);
  applySession(dto.accessToken, toSessionPlayer(dto.player));
  return dto;
}

export async function login(
  email: string,
  password: string,
): Promise<AuthResponseDto> {
  const dto = await postPublic<AuthResponseDto>("/api/auth/login", {
    email,
    password,
  });
  applySession(dto.accessToken, toSessionPlayer(dto.player));
  return dto;
}

/**
 * docs/11-auth.md Bölüm 1.2: ageConfirmed/termsAccepted yalnızca hiç var olmayan
 * bir hesabın Google ile İLK kez oluşturulduğu durumda anlamlıdır — zaten var
 * olan bir hesapla giriş yapılırken sunucu tarafında yok sayılır.
 */
export async function googleAuth(
  idToken: string,
  consent?: { ageConfirmed: boolean; termsAccepted: boolean },
): Promise<AuthResponseDto> {
  const dto = await postPublic<AuthResponseDto>("/api/auth/google", {
    idToken,
    ageConfirmed: consent?.ageConfirmed ?? false,
    termsAccepted: consent?.termsAccepted ?? false,
  });
  applySession(dto.accessToken, toSessionPlayer(dto.player));
  return dto;
}

export async function linkGoogle(idToken: string): Promise<PlayerAccountDto> {
  const res = await authFetch("/api/auth/google/link", {
    method: "POST",
    body: JSON.stringify({ idToken }),
  });
  return parseAuthResponse<PlayerAccountDto>(res);
}

export async function logout(): Promise<void> {
  const res = await authFetch("/api/auth/logout", { method: "POST" });
  await parseAuthResponse<void>(res);
}

export async function forgotPassword(email: string): Promise<void> {
  await postPublic<void>("/api/auth/forgot-password", { email });
}

export async function resetPassword(
  token: string,
  newPassword: string,
): Promise<void> {
  await postPublic<void>("/api/auth/reset-password", { token, newPassword });
}

export async function verifyEmail(token: string): Promise<void> {
  await postPublic<void>("/api/auth/verify-email", { token });
}

export async function changePassword(
  currentPassword: string | null,
  newPassword: string,
): Promise<void> {
  const res = await authFetch("/api/auth/change-password", {
    method: "POST",
    body: JSON.stringify({ currentPassword, newPassword }),
  });
  await parseAuthResponse<void>(res);
}

export async function getMe(): Promise<PlayerAccountDto> {
  const res = await authFetch("/api/auth/me");
  return parseAuthResponse<PlayerAccountDto>(res);
}

function toSessionPlayer(player: PlayerAccountDto) {
  return {
    id: player.id,
    email: player.email,
    displayName: player.displayName,
    role: player.role,
    status: player.status,
    emailVerified: player.emailVerified,
    hasPassword: player.hasPassword,
    googleLinked: player.googleLinked,
  };
}
