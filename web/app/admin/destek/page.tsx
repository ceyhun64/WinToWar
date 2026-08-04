"use client";

import { useCallback, useEffect, useState } from "react";
import {
  getSupportTickets,
  updateSupportTicketStatus,
  type AdminSupportTicket,
  type SupportTicketStatus,
} from "@/lib/admin/api";

const STATUSES: SupportTicketStatus[] = ["Open", "Answered", "Closed"];

/** docs/07-pages.md `/admin/destek`: bekleyen destek talepleri (maç itirazları MatchId ile öne çıkar), durum güncelleme. */
export default function AdminDestekPage() {
  const [tickets, setTickets] = useState<AdminSupportTicket[]>([]);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(() => {
    getSupportTickets().then(setTickets).catch((err) => setError(String(err)));
  }, []);

  useEffect(() => {
    refresh();
  }, [refresh]);

  async function handleStatusChange(id: string, status: SupportTicketStatus) {
    try {
      await updateSupportTicketStatus(id, status);
      refresh();
    } catch (err) {
      setError(String(err));
    }
  }

  const sorted = [...tickets].sort((a, b) => (a.matchId ? -1 : 0) - (b.matchId ? -1 : 0));

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-lg font-semibold">Destek Talepleri</h1>
      {error ? <p className="text-sm text-destructive">{error}</p> : null}
      {sorted.length === 0 ? (
        <p className="text-sm text-muted-foreground">Talep yok.</p>
      ) : (
        <ul className="flex flex-col gap-2">
          {sorted.map((t) => (
            <li key={t.id} className="rounded-md border border-border bg-card p-4 text-sm">
              <div className="flex items-center justify-between">
                <span className="font-medium">
                  {t.subject}
                  {t.matchId ? <span className="ml-2 rounded bg-muted px-1.5 py-0.5 text-xs">Maç itirazı</span> : null}
                </span>
                <select
                  className="h-7 rounded-md border border-input bg-background px-2 text-xs"
                  value={t.status}
                  onChange={(e) => handleStatusChange(t.id, e.target.value as SupportTicketStatus)}
                >
                  {STATUSES.map((s) => (
                    <option key={s} value={s}>
                      {s}
                    </option>
                  ))}
                </select>
              </div>
              <p className="mt-1 text-muted-foreground">{t.description}</p>
              <p className="mt-1 text-xs text-muted-foreground">{t.contactEmail}</p>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
