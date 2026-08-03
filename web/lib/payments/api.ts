import { API_BASE_URL } from "@/lib/game/api";
import type { PaymentErrorResponse, PaymentInvoiceDto } from "./types";

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
  payoutAddress: string
): Promise<PaymentInvoiceDto> {
  const res = await fetch(`${API_BASE_URL}/api/matches/${matchId}/payments`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ playerId, payoutAddress }),
  });
  return parsePaymentResponse<PaymentInvoiceDto>(res);
}

export async function getPaymentInvoice(matchId: string, invoiceId: string): Promise<PaymentInvoiceDto> {
  const res = await fetch(`${API_BASE_URL}/api/matches/${matchId}/payments/${invoiceId}`);
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
