import { authFetch } from "@/lib/identity";
import type { PaymentInvoiceDto, WithdrawalRequestDto } from "@/lib/payments/types";

/**
 * docs/11-auth.md Bölüm 1.9/0.1: paylaşılan X-Admin-Key header'ı yerine gerçek
 * oturum + Player.Role == Admin kontrolüne taşındı (bkz. api/Services/AdminAuthFilter.cs,
 * components/admin/AdminGate.tsx). authFetch zaten Authorization Bearer header'ını
 * ekliyor; [AdminAuth] filtresi bunun Admin rolüne ait olup olmadığını doğrular.
 */
async function adminFetch<T>(path: string, options: RequestInit = {}): Promise<T> {
  const res = await authFetch(path, options);

  if (res.status === 401) {
    throw new Error("Yetkisiz — admin oturumu geçersiz.");
  }
  if (!res.ok) {
    throw new Error(`İstek başarısız oldu (${res.status})`);
  }
  if (res.status === 204) {
    return undefined as T;
  }
  return res.json() as Promise<T>;
}

export interface AdminMetrics {
  pendingWithdrawalCount: number;
  activeMatchCount: number;
  dailyVolumeUsd: string;
}

export const getAdminMetrics = () => adminFetch<AdminMetrics>("/api/admin/metrics");

export const getPendingWithdrawals = () => adminFetch<WithdrawalRequestDto[]>("/api/admin/payments/withdrawals");

export const approveWithdrawal = (id: string) =>
  adminFetch<void>(`/api/admin/payments/withdrawals/${id}/approve`, { method: "POST" });

export const rejectWithdrawal = (id: string) =>
  adminFetch<void>(`/api/admin/payments/withdrawals/${id}/reject`, { method: "POST" });

export const getFailedInvoices = () => adminFetch<PaymentInvoiceDto[]>("/api/admin/payments/invoices/failed");

/** docs/05-payment.md Bölüm 10.1: teknik arıza kaynaklı, admin-onaylı manuel iade. */
export const refundInvoice = (invoiceId: string) =>
  adminFetch<void>(`/api/admin/payments/invoices/${invoiceId}/refund`, { method: "POST" });

export interface AdminMatchSummary {
  matchId: string;
  status: string;
  roomType: string;
  playerCount: number;
  maxPlayers: number;
  entryFeeUsd: string;
}

export const getAdminMatches = () => adminFetch<AdminMatchSummary[]>("/api/admin/matches");

export interface AdminUser {
  playerId: string;
  balanceUsd: string;
  invoices: PaymentInvoiceDto[];
}

export const getAdminUser = (playerId: string) => adminFetch<AdminUser>(`/api/admin/users/${playerId}`);

export type SupportTicketStatus = "Open" | "Answered" | "Closed";

export interface AdminSupportTicket {
  id: string;
  subject: string;
  description: string;
  contactEmail: string;
  matchId: string | null;
  status: SupportTicketStatus;
  createdAtUtc: string;
}

export const getSupportTickets = () => adminFetch<AdminSupportTicket[]>("/api/admin/support-tickets");

export const updateSupportTicketStatus = (id: string, status: SupportTicketStatus) =>
  adminFetch<void>(`/api/admin/support-tickets/${id}/status`, {
    method: "POST",
    body: JSON.stringify({ status }),
  });

export interface AdminLogEntry {
  timestampUtc: string;
  level: string;
  category: string;
  message: string;
}

export const getAdminLogs = (level?: string, search?: string) => {
  const params = new URLSearchParams();
  if (level) params.set("level", level);
  if (search) params.set("search", search);
  const query = params.toString();
  return adminFetch<AdminLogEntry[]>(`/api/admin/logs${query ? `?${query}` : ""}`);
};
