"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { getAdminUser, type AdminUser } from "@/lib/admin/api";

/** docs/07-pages.md `/admin/kullanicilar`: kullanıcı arama, bakiye görüntüleme. */
export default function AdminKullanicilarPage() {
  const [playerId, setPlayerId] = useState("");
  const [user, setUser] = useState<AdminUser | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function handleSearch() {
    if (!playerId.trim()) return;
    setBusy(true);
    setError(null);
    try {
      setUser(await getAdminUser(playerId.trim()));
    } catch (err) {
      setError(String(err));
      setUser(null);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-lg font-semibold">Kullanıcılar</h1>

      <div className="flex gap-2">
        <input
          className="h-9 flex-1 rounded-md border border-input bg-background px-3 text-sm"
          value={playerId}
          onChange={(e) => setPlayerId(e.target.value)}
          placeholder="Oyuncu ID"
        />
        <Button disabled={busy} onClick={handleSearch}>
          Ara
        </Button>
      </div>

      {error ? <p className="text-sm text-destructive">{error}</p> : null}

      {user ? (
        <div className="flex flex-col gap-3">
          <div className="rounded-md border border-border bg-card p-4">
            <span className="text-xs text-muted-foreground">Bakiye</span>
            <p className="text-xl font-semibold tabular-nums">${user.balanceUsd}</p>
          </div>
          <div>
            <h2 className="text-sm font-semibold">Ödeme Geçmişi</h2>
            {user.invoices.length === 0 ? (
              <p className="mt-1 text-sm text-muted-foreground">Kayıt yok.</p>
            ) : (
              <ul className="mt-2 flex flex-col gap-1 text-sm">
                {user.invoices.map((i) => (
                  <li key={i.invoiceId} className="flex justify-between rounded-md border border-border bg-card px-3 py-2">
                    <span>{i.matchId ? "Maça Giriş" : "Bakiye Yükleme"}</span>
                    <span className="tabular-nums">
                      ${i.amountUsd} · {i.status}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>
      ) : null}
    </div>
  );
}
