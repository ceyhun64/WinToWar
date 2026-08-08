"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { GameModeTiles } from "@/components/lobby/GameModeTiles";
import { LobiBackground } from "@/components/lobby/LobiBackground";
import { RoomCard } from "@/components/lobby/RoomCard";
import { VipCreateDialog } from "@/components/lobby/VipCreateDialog";
import {
  joinPracticeRoom,
  joinRoom,
  joinStandardRoom,
  listRooms,
  type JoinRoomResult,
  type RoomSummary,
} from "@/lib/game/api";
import type { RoomType } from "@/lib/game/types";
import { getStoredDisplayName } from "@/lib/identity";
import { AuthGuard } from "@/components/layout/AuthGuard";

function storeSession(matchId: string, playerId: string, playerName: string) {
  window.localStorage.setItem(`wintowar:match:${matchId}:playerId`, playerId);
  window.localStorage.setItem(
    `wintowar:match:${matchId}:playerName`,
    playerName,
  );
}

/**
 * docs/07-pages.md `/lobi`: Standart/VIP sekmeleri (oda listesi) + tek bir
 * "Pratik Oyna" butonu (bkz. docs/03-game-rules.md Bölüm 7 — Practice bir oda
 * listesi değil, doğrudan tek paylaşılan kuyruğa katılan bir aksiyondur).
 * docs/05-payment.md Bölüm 1.9 (2026-08-08 güncellemesi): katılım hiçbir LTC
 * adresi istemez — bakiye yeterliyse doğrudan katılınır, yetmiyorsa sunucu
 * otomatik bir top-up invoice'ı döner (`/odeme/[invoiceId]`, BTCPay'in kendi
 * ödeme adresi/QR'ı ile). LTC adresi yalnızca `/cuzdan`'daki para çekme
 * akışında, oyuncu isterse istenir.
 */
export default function LobiPage() {
  return (
    <AuthGuard>
      <LobiPageContent />
    </AuthGuard>
  );
}

function LobiPageContent() {
  const router = useRouter();
  const [playerName, setPlayerName] = useState<string | null>(null);
  const [tab, setTab] = useState<RoomType>("Standard");
  const [visualMode, setVisualMode] = useState<"Practice" | RoomType>(
    "Standard",
  );
  const [rooms, setRooms] = useState<RoomSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [vipDialogOpen, setVipDialogOpen] = useState(false);

  useEffect(() => {
    setPlayerName(getStoredDisplayName());
  }, []);

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
      storeSession(result.matchId, result.playerId, playerName);
      router.push(`/game/${result.matchId}`);
      return;
    }

    if (result.outcome === "InsufficientBalance") {
      if (result.invoice) {
        if (playerName) {
          storeSession(result.matchId, result.invoice.playerId, playerName);
        }
        router.push(`/odeme/${result.invoice.invoiceId}`);
        return;
      }
      setError("Ödeme oluşturulamadı, lütfen tekrar deneyin.");
      return;
    }

    setError("Bu oda dolu, lütfen başka bir oda deneyin.");
    refreshRooms();
  }

  async function handlePractice() {
    if (!playerName) return;
    setVisualMode("Practice");
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
      handleOutcome(await joinStandardRoom(playerName));
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
      handleOutcome(await joinRoom(matchId, playerName));
    } catch (err) {
      setError(String(err));
      setBusy(false);
    }
  }

  if (!playerName) {
    return null;
  }

  return (
    <div className="mx-auto flex w-full max-w-7xl flex-1 flex-col gap-8 px-8 py-8 md:py-10">
      <LobiBackground mode={visualMode} />
      {/*
        docs/04-style.md Landing İstisnası pilotu — 3. tur geri bildirim
        ("Lobi'yi oyunun bir parçası gibi tasarla, form merkezli değil oyun
        merkezli"): sayfa artık "hangi savaşa katılacağım?" sorusuyla açılıyor
        — kısa başlık + üç büyük oyun modu karosu.
        6. tur geri bildirim ("modlar yukarıdan aşağı, sol tarafta olsun"):
        `lg`+ genişlikte modlar dar bir sol kolonda dikey sıralanır, oda
        ızgarası sağda kalır; dar ekranda tek sütuna düşer (modlar üstte).
      */}
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-bold tracking-tight sm:text-3xl">Lobi</h1>
        <p className="text-sm text-muted-foreground">
          Hazır bir odaya katıl veya kendi savaşını başlat.
        </p>
      </div>

      <div className="grid grid-cols-1 gap-8 lg:grid-cols-[300px_1fr] lg:items-start">
        <GameModeTiles
          active={tab}
          busy={busy}
          onSelect={(mode) => {
            setVisualMode(mode);
            setTab(mode);
          }}
          onPractice={handlePractice}
        />

        <section className="flex flex-col gap-3">
          <div className="flex items-center justify-between">
            <h2 className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">
              {tab === "Standard" ? "Standart Odalar" : "VIP Odalar"}
            </h2>
            {tab === "Standard" ? (
              <Button
                variant="white"
                disabled={busy}
                onClick={handleStandardQuickJoin}
              >
                Hızlı Katıl
              </Button>
            ) : (
              <Button variant="white" onClick={() => setVipDialogOpen(true)}>
                + Oda Kur
              </Button>
            )}
          </div>

          {loading ? (
            <p className="text-sm text-muted-foreground">Yükleniyor...</p>
          ) : rooms.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              Şu anda açık {tab === "Standard" ? "Standart" : "VIP"} oda yok.
            </p>
          ) : (
            <ul className="grid grid-cols-1 gap-4 sm:grid-cols-3">
              {rooms.map((room) => (
                <li key={room.matchId}>
                  <RoomCard
                    room={room}
                    type={tab}
                    busy={busy}
                    onJoin={() => handleJoinRoom(room.matchId)}
                  />
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>

      {error ? <p className="text-sm text-destructive">{error}</p> : null}

      <VipCreateDialog open={vipDialogOpen} onOpenChange={setVipDialogOpen} />
    </div>
  );
}
