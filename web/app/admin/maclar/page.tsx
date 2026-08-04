"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { getAdminMatches, type AdminMatchSummary } from "@/lib/admin/api";

/** docs/07-pages.md `/admin/maclar`: aktif/geçmiş maç listesi. */
export default function AdminMaclarPage() {
  const [matches, setMatches] = useState<AdminMatchSummary[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getAdminMatches().then(setMatches).catch((err) => setError(String(err)));
  }, []);

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-lg font-semibold">Maçlar</h1>
      {error ? <p className="text-sm text-destructive">{error}</p> : null}
      {matches.length === 0 ? (
        <p className="text-sm text-muted-foreground">Aktif maç yok.</p>
      ) : (
        <ul className="flex flex-col gap-2">
          {matches.map((m) => (
            <li key={m.matchId} className="flex items-center justify-between rounded-md border border-border bg-card px-4 py-3 text-sm">
              <div>
                <p className="font-medium">
                  {m.roomType} · {m.status}
                </p>
                <p className="text-xs text-muted-foreground">
                  {m.playerCount}/{m.maxPlayers} oyuncu · ${m.entryFeeUsd}
                </p>
              </div>
              <Link href={`/mac/${m.matchId}`} className="text-sm underline">
                Detay
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
