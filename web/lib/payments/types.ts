// Backend Payments DTO'larıyla (api/Payments/Dtos/PaymentDtos.cs) birebir eşleşen tipler.
// Ödeme modülü ayrı bir katman olduğundan lib/game/types.ts'e karışmaz.

export type PaymentInvoiceStatus = "Pending" | "Confirmed" | "Expired" | "Refunded" | "Failed";

export type MatchJoinOutcome = "NotApplicable" | "Pending" | "Joined" | "RoomFull";

export interface PaymentInvoiceDto {
  invoiceId: string;
  matchId: string | null;
  playerId: string;
  status: PaymentInvoiceStatus;
  amountUsd: string;
  amountLtc: string;
  lockedUsdPerLtc: string;
  receivingAddress: string;
  bip21Uri: string;
  expiresAt: string;
  createdAt: string;
  rateServedFromCache: boolean;
  matchJoinOutcome: MatchJoinOutcome;
  /** docs/07-pages.md `/odeme/[invoiceId]`: canlı onay ilerlemesi ("1/2 onay" gibi). */
  currentConfirmations: number;
  requiredConfirmations: number;
}

/** Kazanç artık on-chain LTC değil, doğrudan Wallet.BalanceUsd'ye kredi olarak işlenir (bkz. PayoutService). */
export interface PayoutRecipientDto {
  winnerPlayerId: string;
  amountUsd: string;
}

export interface PayoutSummaryDto {
  matchId: string;
  totalPoolUsd: string;
  commissionUsd: string;
  recipients: PayoutRecipientDto[];
}

export interface WalletDto {
  playerId: string;
  balanceUsd: string;
}

export type WithdrawalRequestStatus = "Pending" | "Approved" | "Sent" | "Completed" | "Rejected" | "Failed";

export interface WithdrawalRequestDto {
  id: string;
  playerId: string;
  amountUsd: string;
  amountLtc: string;
  destinationLtcAddress: string;
  status: WithdrawalRequestStatus;
  createdAt: string;
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
