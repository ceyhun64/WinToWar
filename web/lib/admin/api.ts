import { API_BASE_URL } from "@/lib/game/api";
import type { PaymentInvoiceDto, WithdrawalRequestDto } from "@/lib/payments/types";

const ADMIN_KEY_STORAGE_KEY = "wintowar:adminKey";

export function getAdminKey(): string | null {
  return window.sessionStorage.getItem(ADMIN_KEY_STORAGE_KEY);
}

export function setAdminKey(key: string): void {
  window.sessionStorage.setItem(ADMIN_KEY_STORAGE_KEY, key);
}

export function clearAdminKey(): void {
  window.sessionStorage.removeItem(ADMIN_KEY_STORAGE_KEY);
}

async function adminFetch<T>(path: string, options: RequestInit = {}): Promise<T> {
  const res = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers: {
      ...(options.headers ?? {}),
      "X-Admin-Key": getAdminKey() ?? "",
      ...(options.body ? { "Content-Type": "application/json" } : {}),
    },
  });

  if (res.status === 401) {
    clearAdminKey();
    throw new Error("Yetkisiz — admin anahtarı geçersiz.");
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
