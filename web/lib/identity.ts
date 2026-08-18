// docs/11-auth.md: gerçek e-posta/parola + Google kimlik doğrulama sistemi.
// Bu dosya önceden yalnızca tarayıcıya kayıtlı, sunucu tarafından hiç
// doğrulanmayan bir kimliği (localStorage) tutuyordu — artık gerçek bir oturumu
// (kısa ömürlü JWT access token, yalnızca memory'de — bkz. docs/11-auth.md
// Bölüm 1.4 "Frontend localStorage'a yazmaz") temsil eder. Çağıran kod
// (isSignedIn/getStoredDisplayName/signOut) aynı kalır — yalnızca iç
// implementasyon değişti (bu dosyanın önceki hâlindeki yorumun vaat ettiği gibi).
//
// Ağ çağrıları (register/login/vb.) burada değil `web/lib/auth/api.ts`'te —
// bu dosya yalnızca oturum STATE'ini ve kimlik doğrulamalı istekler için ortak
// `authFetch` yardımcısını taşır (bkz. 02-architecture.md `lib/<modül>/` deseni).

// 🛠️ web/lib/game/api.ts'teki API_BASE_URL ile aynı değer — döngüsel import'tan
// kaçınmak için (game/api.ts zaten bu dosyadan importluyor) burada ayrıca tanımlanır.
const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5019";

export interface SessionPlayer {
  id: string;
  email: string;
  displayName: string;
  role: "Player" | "Admin";
  status: "Active" | "Suspended" | "PendingDeletion" | "Deleted";
  emailVerified: boolean;
  hasPassword: boolean;
  googleLinked: boolean;
}

let accessToken: string | null = null;
let currentPlayer: SessionPlayer | null = null;
let sessionLoadPromise: Promise<void> | null = null;

/**
 * Header/Navbar gibi bileşenler oturum değiştiğinde (login/register/logout/
 * displayName güncellemesi) yeniden mount olmadan haberdar olsun diye —
 * `currentPlayer` düz bir modül değişkeni olduğundan React state/context
 * dışında bu basit pub-sub olmadan değişiklik hiçbir yerde yansımıyordu.
 */
const sessionListeners = new Set<() => void>();

function notifySessionListeners(): void {
  sessionListeners.forEach((listener) => listener());
}

export function subscribeToSession(listener: () => void): () => void {
  sessionListeners.add(listener);
  return () => sessionListeners.delete(listener);
}

/** web/lib/auth/api.ts: register/login/google/refresh başarılı olduğunda çağırır. */
export function applySession(token: string, player: SessionPlayer): void {
  accessToken = token;
  currentPlayer = player;
  notifySessionListeners();
}

function clearSession(): void {
  accessToken = null;
  currentPlayer = null;
  notifySessionListeners();
}

export function getAccessToken(): string | null {
  return accessToken;
}

export function getCurrentPlayerId(): string | null {
  return currentPlayer?.id ?? null;
}

export function getCurrentRole(): "Player" | "Admin" | null {
  return currentPlayer?.role ?? null;
}

export function isSignedIn(): boolean {
  return currentPlayer !== null;
}

export function getStoredDisplayName(): string | null {
  return currentPlayer?.displayName ?? null;
}

/** 🛠️ Bölüm 6'da bir "görünen ad değiştir" ucu tanımlanmadığından yalnızca yerel/oturum içi bir önbellek güncellemesidir (bkz. hesap-ayarlari sayfası). */
export function setStoredDisplayName(name: string): void {
  if (currentPlayer) {
    currentPlayer = { ...currentPlayer, displayName: name };
    notifySessionListeners();
  }
}

export function signOut(): void {
  const token = accessToken;
  clearSession();
  fetch(`${API_BASE_URL}/api/auth/logout`, {
    method: "POST",
    credentials: "include",
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  }).catch(() => {
    // Sunucu tarafı logout başarısız olsa bile client oturumu zaten temizlendi.
  });
}

async function refreshSessionFromCookie(): Promise<void> {
  try {
    const res = await fetch(`${API_BASE_URL}/api/auth/refresh`, {
      method: "POST",
      credentials: "include",
    });
    if (!res.ok) {
      clearSession();
      return;
    }
    const body = (await res.json()) as { accessToken: string; player: SessionPlayer };
    applySession(body.accessToken, body.player);
  } catch {
    clearSession();
  }
}

/**
 * Sayfa ilk yüklendiğinde (access token yalnızca memory'de tutulduğundan sayfa
 * yenilemede kaybolur) HttpOnly refresh cookie'siyle sessiz bir oturum
 * yenilemesi dener. AuthGuard/AdminGate ve isSignedIn()'e güvenen bileşenler
 * ilk kontrolden önce bunu `await` etmelidir.
 */
export function ensureSessionLoaded(): Promise<void> {
  if (currentPlayer !== null) {
    return Promise.resolve();
  }
  if (!sessionLoadPromise) {
    sessionLoadPromise = refreshSessionFromCookie();
  }
  return sessionLoadPromise;
}

/**
 * docs/11-auth.md Bölüm 0.4: kimlik doğrulamalı tüm istekler (cüzdan/lobi/profil/
 * hesap-ayarları) Authorization header'ı üzerinden gider, playerId asla body/query
 * içinde taşınmaz. Access token süresi dolmuşsa (401) bir kez sessiz refresh
 * denenir, sonra istek tekrarlanır.
 */
export async function authFetch(path: string, options: RequestInit = {}): Promise<Response> {
  await ensureSessionLoaded();

  const doFetch = () =>
    fetch(`${API_BASE_URL}${path}`, {
      ...options,
      credentials: "include",
      headers: {
        ...(options.headers ?? {}),
        ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
        ...(options.body ? { "Content-Type": "application/json" } : {}),
      },
    });

  let res = await doFetch();
  if (res.status === 401 && accessToken !== null) {
    await refreshSessionFromCookie();
    res = await doFetch();
  }
  return res;
}
