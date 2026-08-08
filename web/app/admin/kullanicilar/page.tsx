"use client";

import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
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

      <div className="flex gap-3">
        <Input
          className="flex-1"
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
          <Card>
            <CardContent>
              <span className="text-xs text-muted-foreground">Bakiye</span>
              <p className="text-xl font-semibold tabular-nums">${user.balanceUsd}</p>
            </CardContent>
          </Card>
          <div>
            <h2 className="text-sm font-semibold">Ödeme Geçmişi</h2>
            {user.invoices.length === 0 ? (
              <p className="mt-1 text-sm text-muted-foreground">Kayıt yok.</p>
            ) : (
              <ul className="mt-2 flex flex-col gap-3 text-sm">
                {user.invoices.map((i) => (
                  <li key={i.invoiceId}>
                    <Card size="sm">
                      <CardContent className="flex justify-between">
                        <span>{i.matchId ? "Maça Giriş" : "Para Yatırma"}</span>
                        <span className="flex items-center gap-3 tabular-nums">
                          ${i.amountUsd} <Badge variant="outline">{i.status}</Badge>
                        </span>
                      </CardContent>
                    </Card>
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
