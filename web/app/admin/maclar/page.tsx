"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
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
            <li key={m.matchId}>
              <Card size="sm" className="flex-row items-center justify-between">
                <CardContent className="flex-1">
                  <p className="flex items-center gap-1.5 font-medium">
                    {m.roomType} <Badge variant="outline">{m.status}</Badge>
                  </p>
                  <p className="text-xs text-muted-foreground">
                    {m.playerCount}/{m.maxPlayers} oyuncu · ${m.entryFeeUsd}
                  </p>
                </CardContent>
                <Link href={`/mac/${m.matchId}`} className="pr-(--card-spacing) text-sm underline">
                  Detay
                </Link>
              </Card>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
