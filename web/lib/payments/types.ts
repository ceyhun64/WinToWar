// Backend Payments DTO'larıyla (api/Payments/Dtos/PaymentDtos.cs) birebir eşleşen tipler.
// Ödeme modülü ayrı bir katman olduğundan lib/game/types.ts'e karışmaz.

export type PaymentInvoiceStatus = "Pending" | "Confirmed" | "Expired" | "Refunded" | "Failed";

export interface PaymentInvoiceDto {
  invoiceId: string;
  matchId: string;
  playerId: string;
  status: PaymentInvoiceStatus;
  amountUsd: string;
  amountLtc: string;
  lockedUsdPerLtc: string;
  receivingAddress: string;
  bip21Uri: string;
  expiresAt: string;
  rateServedFromCache: boolean;
}

export interface PaymentErrorResponse {
  code: string;
  message: string;
}

export interface PaymentConfirmedEvent {
  invoiceId: string;
  matchId: string;
  playerId: string;
  amountLtc: string;
}
