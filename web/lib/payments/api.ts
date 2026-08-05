import { API_BASE_URL } from "@/lib/game/api";
import { getOrCreatePlayerId } from "@/lib/identity";
import type { PaymentErrorResponse, PaymentInvoiceDto, PayoutSummaryDto, WalletDto, WithdrawalRequestDto } from "./types";

async function parsePaymentResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    let message = `İstek başarısız oldu (${res.status})`;
    try {
      const body = (await res.json()) as PaymentErrorResponse;
      message = body.message || message;
    } catch {
      // Yanıt JSON değilse varsayılan mesaj kullanılır.
    }
    throw new Error(message);
  }
  return res.json() as Promise<T>;
}

export async function createPaymentInvoice(
  matchId: string,
  playerId: string,
  playerName: string,
  payoutAddress: string
): Promise<PaymentInvoiceDto> {
  const res = await fetch(`${API_BASE_URL}/api/matches/${matchId}/payments`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ playerId, playerName, payoutAddress }),
  });
  return parsePaymentResponse<PaymentInvoiceDto>(res);
}

/** docs/07-pages.md `/odeme/[invoiceId]`: matchId'den bağımsız, tek sorgu ucu (top-up ve maça giriş invoice'ları için ortak). */
export async function getPaymentInvoice(invoiceId: string): Promise<PaymentInvoiceDto> {
  const res = await fetch(`${API_BASE_URL}/api/payments/${invoiceId}`);
  return parsePaymentResponse<PaymentInvoiceDto>(res);
}

/**
 * 🛠️ Bölüm 0.3 ön koşulu: BTCPay regtest/testnet erişilemediği için geliştirme
 * ortamında ödeme akışını uçtan uca tetiklemenin tek yolu bu uçtur (bkz.
 * api/Controllers/PaymentsDevController.cs — yalnızca Development + FakePaymentProvider
 * ile çalışır, gerçek/mainnet provider'a geçildiğinde sunucu tarafında otomatik devre dışı kalır).
 */
export async function simulatePaymentPaid(invoiceId: string): Promise<void> {
  const res = await fetch(`${API_BASE_URL}/api/dev/payments/${invoiceId}/simulate-paid`, { method: "POST" });
  if (!res.ok) {
    throw new Error(`Simülasyon başarısız oldu (${res.status})`);
  }
}

export async function getInvoiceHistory(): Promise<PaymentInvoiceDto[]> {
  const res = await fetch(`${API_BASE_URL}/api/wallet/${getOrCreatePlayerId()}/invoices`);
  return parsePaymentResponse<PaymentInvoiceDto[]>(res);
}

/** null: bu maç için henüz bir payout oluşmadı (ör. hâlâ oynanıyor). */
export async function getPayoutSummary(matchId: string): Promise<PayoutSummaryDto | null> {
  const res = await fetch(`${API_BASE_URL}/api/matches/${matchId}/payout`);
  if (res.status === 404) {
    return null;
  }
  return parsePaymentResponse<PayoutSummaryDto>(res);
}

export async function getWalletBalance(): Promise<WalletDto> {
  const res = await fetch(`${API_BASE_URL}/api/wallet/${getOrCreatePlayerId()}`);
  return parsePaymentResponse<WalletDto>(res);
}

/** docs/08-page-content.md Bölüm 3.9: "Bekleyen Transferler" kartı — henüz sonuçlanmamış çekim talepleri. */
export async function getPendingWithdrawals(): Promise<WithdrawalRequestDto[]> {
  const res = await fetch(`${API_BASE_URL}/api/wallet/${getOrCreatePlayerId()}/withdrawals`);
  return parsePaymentResponse<WithdrawalRequestDto[]>(res);
}

export async function createTopUpInvoice(amountUsd: number, payoutAddress: string): Promise<PaymentInvoiceDto> {
  const res = await fetch(`${API_BASE_URL}/api/wallet/topup`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ playerId: getOrCreatePlayerId(), amountUsd, payoutAddress }),
  });
  return parsePaymentResponse<PaymentInvoiceDto>(res);
}

export async function requestWithdrawal(amountUsd: number, destinationLtcAddress: string): Promise<WithdrawalRequestDto> {
  const res = await fetch(`${API_BASE_URL}/api/wallet/withdraw`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ playerId: getOrCreatePlayerId(), amountUsd, destinationLtcAddress }),
  });
  return parsePaymentResponse<WithdrawalRequestDto>(res);
}
