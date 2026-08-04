"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { PayoutAddressPrompt } from "@/components/lobby/PayoutAddressPrompt";
import {
  joinPracticeRoom,
  joinRoom,
  joinStandardRoom,
  listRooms,
  type JoinRoomResult,
  type RoomSummary,
} from "@/lib/game/api";
import type { RoomType } from "@/lib/game/types";
import { getStoredDisplayName, getStoredPayoutAddress, isSignedIn, setStoredPayoutAddress } from "@/lib/identity";

function storeSession(matchId: string, playerId: string, playerName: string) {
  window.localStorage.setItem(`wintowar:match:${matchId}:playerId`, playerId);
  window.localStorage.setItem(`wintowar:match:${matchId}:playerName`, playerName);
}

/**
 * docs/07-pages.md `/lobi`: Standart/VIP sekmeleri (oda listesi) + tek bir
 * "Pratik Oyna" butonu (bkz. docs/03-game-rules.md Bölüm 7 — Practice bir oda
 * listesi değil, doğrudan tek paylaşılan kuyruğa katılan bir aksiyondur).
 * docs/05-payment.md Bölüm 1.9: ücretli her katılım bir LTC ödül adresi ister —
 * daha önce sağlanmışsa (bkz. lib/identity.ts) tekrar sorulmaz.
 */
export default function LobiPage() {
  const router = useRouter();
  const [playerName, setPlayerName] = useState<string | null>(null);
  const [payoutAddress, setPayoutAddress] = useState("");
  const [tab, setTab] = useState<RoomType>("Standard");
  const [rooms, setRooms] = useState<RoomSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [pendingJoin, setPendingJoin] = useState<{ matchId: string; shortfallUsd: string } | null>(null);

  useEffect(() => {
    if (!isSignedIn()) {
      router.replace("/giris");
      return;
    }
    setPlayerName(getStoredDisplayName());
    setPayoutAddress(getStoredPayoutAddress() ?? "");
  }, [router]);

  const refreshRooms = useCallback(() => {
    listRooms(tab)
      .then((list) => {
        setRooms(list);
        setLoading(false);
      })
      .catch((err) => setError(String(err)));
  }, [tab]);

  useEffect(() => {
    setLoading(true);
    refreshRooms();
    // 🛠️ Oda listesi SignalR yerine kısa aralıklı polling ile güncellenir —
    // maçın kendisi (bkz. /game/[matchId]) tamamen SignalR üzerinden akar,
    // burada yalnızca "hangi odalar açık" bilgisi düşük riskli/az kritik bir
    // gecikmeyle güncellenir.
    const interval = window.setInterval(refreshRooms, 4000);
    return () => window.clearInterval(interval);
  }, [refreshRooms]);

  function handleOutcome(result: JoinRoomResult) {
    setBusy(false);
    if (!result.matchId) {
      setError("Oda bulunamadı.");
      return;
    }

    if (result.outcome === "Joined" && result.playerId && playerName) {
      if (payoutAddress.trim()) {
        setStoredPayoutAddress(payoutAddress.trim());
      }
      storeSession(result.matchId, result.playerId, playerName);
      router.push(`/game/${result.matchId}`);
      return;
    }

    if (result.outcome === "InsufficientBalance") {
      if (result.invoice) {
        // payoutAddress zaten verilmişti (bkz. handlePayoutAddressSubmit) — invoice hazır.
        if (playerName) {
          storeSession(result.matchId, result.invoice.playerId, playerName);
        }
        router.push(`/odeme/${result.invoice.invoiceId}`);
        return;
      }
      setPendingJoin({ matchId: result.matchId, shortfallUsd: result.shortfallUsd ?? "0.00" });
      return;
    }

    if (result.outcome === "PayoutAddressRequired") {
      setError("Devam etmeden önce LTC ödül adresinizi girin.");
      return;
    }

    if (result.outcome === "InvalidPayoutAddress") {
      setError("Geçersiz LTC adresi.");
      return;
    }

    setError("Bu oda dolu, lütfen başka bir oda deneyin.");
    refreshRooms();
  }

  async function handlePractice() {
    if (!playerName) return;
    setBusy(true);
    setError(null);
    try {
      handleOutcome(await joinPracticeRoom(playerName));
    } catch (err) {
      setError(String(err));
      setBusy(false);
    }
  }

  async function handleStandardQuickJoin() {
    if (!playerName) return;
    setBusy(true);
    setError(null);
    try {
      handleOutcome(await joinStandardRoom(playerName, payoutAddress.trim() || undefined));
    } catch (err) {
      setError(String(err));
      setBusy(false);
    }
  }

  async function handleJoinRoom(matchId: string) {
    if (!playerName) return;
    setBusy(true);
    setError(null);
    try {
      handleOutcome(await joinRoom(matchId, playerName, payoutAddress.trim() || undefined));
    } catch (err) {
      setError(String(err));
      setBusy(false);
    }
  }

  async function handlePayoutAddressSubmit(address: string) {
    if (!pendingJoin || !playerName) return;
    setBusy(true);
    setError(null);
    try {
      handleOutcome(await joinRoom(pendingJoin.matchId, playerName, address));
      setPendingJoin(null);
    } catch (err) {
      setError(String(err));
      setBusy(false);
    }
  }

  if (!playerName) {
    return null;
  }

  return (
    <div className="mx-auto flex w-full max-w-2xl flex-1 flex-col gap-4 px-4 py-6">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold">Lobi</h1>
        <Button disabled={busy} onClick={handlePractice}>
          Pratik Oyna
        </Button>
      </div>

      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-medium" htmlFor="payoutAddress">
          LTC ödül adresiniz (Standart/VIP için gerekli)
        </label>
        <input
          id="payoutAddress"
          className="h-9 rounded-md border border-input bg-background px-3 font-mono text-sm"
          value={payoutAddress}
          onChange={(e) => setPayoutAddress(e.target.value)}
          placeholder="ltc1q... veya L..."
        />
      </div>

      {pendingJoin ? (
        <PayoutAddressPrompt
          shortfallUsd={pendingJoin.shortfallUsd}
          busy={busy}
          onSubmit={handlePayoutAddressSubmit}
          onCancel={() => setPendingJoin(null)}
        />
      ) : null}

      <div className="flex gap-2 border-b border-border">
        <button
          className={`px-3 py-2 text-sm font-medium ${tab === "Standard" ? "border-b-2 border-foreground text-foreground" : "text-muted-foreground"}`}
          onClick={() => setTab("Standard")}
        >
          Standart
        </button>
        <button
          className={`px-3 py-2 text-sm font-medium ${tab === "Vip" ? "border-b-2 border-foreground text-foreground" : "text-muted-foreground"}`}
          onClick={() => setTab("Vip")}
        >
          VIP
        </button>
      </div>

      {tab === "Standard" ? (
        <div className="flex flex-col gap-3">
          <p className="text-sm text-muted-foreground">Sabit $1 giriş, 4 oyuncu, gri bölge savunması 1.</p>
          <Button variant="outline" disabled={busy} onClick={handleStandardQuickJoin}>
            Hızlı Katıl
          </Button>
        </div>
      ) : (
        <div className="flex justify-end">
          <Link href="/lobi/vip-olustur">
            <Button variant="outline">+ Oda Kur</Button>
          </Link>
        </div>
      )}

      {loading ? (
        <p className="text-sm text-muted-foreground">Yükleniyor...</p>
      ) : rooms.length === 0 ? (
        <p className="text-sm text-muted-foreground">
          Şu anda açık {tab === "Standard" ? "Standart" : "VIP"} oda yok.
        </p>
      ) : (
        <ul className="flex flex-col gap-2">
          {rooms.map((room) => (
            <li
              key={room.matchId}
              className="flex items-center justify-between rounded-md border border-border bg-card px-4 py-3"
            >
              <div className="text-sm">
                <span className="font-medium">
                  {room.playerCount}/{room.maxPlayers} oyuncu
                </span>
                <span className="ml-2 text-muted-foreground">
                  ${room.entryFeeUsd} · gri savunma {room.greyRegionDefenseCount} · {room.fogOfWar ? "Sisli" : "Açık harita"}
                </span>
              </div>
              <Button size="sm" disabled={busy} onClick={() => handleJoinRoom(room.matchId)}>
                Katıl
              </Button>
            </li>
          ))}
        </ul>
      )}

      {error ? <p className="text-sm text-destructive">{error}</p> : null}
    </div>
  );
}
