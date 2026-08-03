"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { createMatch, joinMatch } from "@/lib/game/api";

function storePlayerSession(matchId: string, playerId: string) {
  window.localStorage.setItem(`porsuk:match:${matchId}:playerId`, playerId);
}

export default function GameLobbyPage() {
  const router = useRouter();
  const [playerName, setPlayerName] = useState("");
  const [joinMatchId, setJoinMatchId] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function handleCreate() {
    if (!playerName.trim()) {
      setError("Önce adınızı girin.");
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const res = await createMatch(playerName.trim());
      storePlayerSession(res.matchId, res.playerId);
      router.push(`/game/${res.matchId}/pay`);
    } catch (err) {
      setError(String(err));
      setBusy(false);
    }
  }

  async function handleJoin() {
    if (!playerName.trim() || !joinMatchId.trim()) {
      setError("Adınızı ve maç kodunu girin.");
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const res = await joinMatch(joinMatchId.trim(), playerName.trim());
      storePlayerSession(res.matchId, res.playerId);
      router.push(`/game/${res.matchId}/pay`);
    } catch (err) {
      setError(String(err));
      setBusy(false);
    }
  }

  return (
    <div className="flex flex-1 items-center justify-center bg-background px-4">
      <div className="flex w-full max-w-sm flex-col gap-6 rounded-md border border-border bg-card p-6">
        <div>
          <h1 className="text-lg font-semibold">Porsuk Savaşları</h1>
          <p className="text-sm text-muted-foreground">Bir maç oluşturun veya bir maça katılın.</p>
        </div>

        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium" htmlFor="playerName">
            Oyuncu adı
          </label>
          <input
            id="playerName"
            className="h-9 rounded-md border border-input bg-background px-3 text-sm"
            value={playerName}
            onChange={(e) => setPlayerName(e.target.value)}
            placeholder="Adınız"
          />
        </div>

        <Button disabled={busy} onClick={handleCreate}>
          Yeni Maç Oluştur
        </Button>

        <div className="flex items-center gap-2 text-xs text-muted-foreground">
          <div className="h-px flex-1 bg-border" />
          veya
          <div className="h-px flex-1 bg-border" />
        </div>

        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium" htmlFor="matchId">
            Maç kodu
          </label>
          <input
            id="matchId"
            className="h-9 rounded-md border border-input bg-background px-3 text-sm"
            value={joinMatchId}
            onChange={(e) => setJoinMatchId(e.target.value)}
            placeholder="Rakibinizden aldığınız kod"
          />
        </div>

        <Button variant="outline" disabled={busy} onClick={handleJoin}>
          Maça Katıl
        </Button>

        {error ? <p className="text-sm text-destructive">{error}</p> : null}
      </div>
    </div>
  );
}
