// 🛠️ Auth mekanizması henüz netleşmedi (bkz. docs/07-pages.md ❓). Gerçek bir
// backend auth sistemi kurulana kadar, tarayıcıya özgü kalıcı bir kimlik
// (localStorage) + kullanıcının girdiği bir görünen ad kullanılır. Wallet/oda
// katılımı bu id üzerinden çalışır (bkz. api/Models/Payments/Wallet.cs).
// Gerçek auth eklendiğinde bu modülün iç implementasyonu değişir, çağıran
// kod (getPlayerId/getDisplayName) aynı kalabilir.

const PLAYER_ID_KEY = "wintowar:playerId";
const DISPLAY_NAME_KEY = "wintowar:displayName";
const PAYOUT_ADDRESS_KEY = "wintowar:payoutAddress";

export function getOrCreatePlayerId(): string {
  let id = window.localStorage.getItem(PLAYER_ID_KEY);
  if (!id) {
    id = crypto.randomUUID().replace(/-/g, "");
    window.localStorage.setItem(PLAYER_ID_KEY, id);
  }
  return id;
}

export function getStoredDisplayName(): string | null {
  return window.localStorage.getItem(DISPLAY_NAME_KEY);
}

export function setStoredDisplayName(name: string): void {
  window.localStorage.setItem(DISPLAY_NAME_KEY, name);
}

export function isSignedIn(): boolean {
  return Boolean(getStoredDisplayName());
}

export function signOut(): void {
  window.localStorage.removeItem(PLAYER_ID_KEY);
  window.localStorage.removeItem(DISPLAY_NAME_KEY);
}

/**
 * docs/05-payment.md Bölüm 1.9: ücretsiz (Practice) olmayan her katılımda bir LTC
 * ödül adresi gerekir — burada yalnızca UI'ın formu önceden doldurabilmesi için
 * kolaylık amaçlı saklanır; tek doğruluk kaynağı sunucudaki Wallet.PayoutAddress'tir
 * (bkz. api/Services/Payments/WalletService.cs ResolveAndSavePayoutAddressAsync).
 */
export function getStoredPayoutAddress(): string | null {
  return window.localStorage.getItem(PAYOUT_ADDRESS_KEY);
}

export function setStoredPayoutAddress(address: string): void {
  window.localStorage.setItem(PAYOUT_ADDRESS_KEY, address);
}
