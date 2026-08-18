"use client";

import { use, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Lock } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { CardContent } from "@/components/ui/card";
import { GameCard } from "@/components/layout/GameCard";
import {
  getRoomByInviteToken,
  joinRoom,
  verifyRoomPassword,
  type JoinRoomResult,
  type RoomSummary,
} from "@/lib/game/api";
import { getStoredDisplayName } from "@/lib/identity";
import { AuthGuard } from "@/components/layout/AuthGuard";

interface InviteLobbyPageProps {
  params: Promise<{ inviteToken: string }>;
}

function storeSession(matchId: string, playerId: string, playerName: string) {
  window.localStorage.setItem(`wintowar:match:${matchId}:playerId`, playerId);
  window.localStorage.setItem(`wintowar:match:${matchId}:playerName`, playerName);
}

/**
 * docs/07-pages.md `/lobi/[inviteToken]`: şifreli/özel VIP odaya kısayol linki.
 * Link tek başına yeterli değildir — oda şifreliyse parola da istenir (bkz.
 * docs/03-game-rules.md Bölüm 2.2 "DÜZELTME"). docs/05-payment.md Bölüm 1.9
 * (2026-08-08): katılım hiçbir LTC adresi istemez.
 */
export default function InviteLobbyPage(props: InviteLobbyPageProps) {
  return (
    <AuthGuard>
      <InviteLobbyPageContent {...props} />
    </AuthGuard>
  );
}

function InviteLobbyPageContent({ params }: InviteLobbyPageProps) {
  const { inviteToken } = use(params);
  const router = useRouter();
  const [playerName, setPlayerName] = useState<string | null>(null);
  const [room, setRoom] = useState<RoomSummary | null | undefined>(undefined);
  const [password, setPassword] = useState("");
  const [passwordOk, setPasswordOk] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setPlayerName(getStoredDisplayName());

    getRoomByInviteToken(inviteToken)
      .then((dto) => {
        setRoom(dto);
        setPasswordOk(!dto.isPasswordProtected);
      })
      .catch(() => setRoom(null));
  }, [inviteToken]);

  function handleResult(result: JoinRoomResult) {
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

    setError("Bu oda dolu/başlamış.");
  }

  async function handleVerifyPassword() {
    if (!room) return;
    setBusy(true);
    setError(null);
    try {
      const ok = await verifyRoomPassword(room.matchId, password);
      setPasswordOk(ok);
      if (!ok) {
        setError("Parola hatalı.");
      }
    } catch (err) {
      setError(String(err));
    } finally {
      setBusy(false);
    }
  }

  async function handleJoin() {
    if (!room || !playerName) return;
    setBusy(true);
    setError(null);
    try {
      handleResult(await joinRoom(room.matchId, playerName));
    } catch (err) {
      setError(String(err));
      setBusy(false);
    }
  }

  if (!playerName || room === undefined) {
    return null;
  }

  if (room === null) {
    return (
      <div className="mx-auto flex w-full max-w-sm flex-1 flex-col items-center justify-center gap-3 px-4 py-16 text-center">
        <h1 className="text-lg font-semibold">Bu davet artık geçerli değil</h1>
        <p className="text-sm text-muted-foreground">Oda bulunamadı ya da süresi doldu.</p>
        <Button variant="outline" onClick={() => router.push("/lobi")}>
          Lobiye Dön
        </Button>
      </div>
    );
  }

  return (
    <div className="mx-auto flex w-full max-w-sm flex-1 flex-col justify-center gap-6 px-4 py-(--gutter-16)">
      <div className="flex flex-col items-center gap-3 text-center">
        <span className="flex size-12 items-center justify-center rounded-2xl" style={{ backgroundColor: "#F5B94222", color: "#F5B942" }}>
          <Lock className="size-6" aria-hidden="true" />
        </span>
        <div>
          <h1 className="text-lg font-semibold">Özel Davet</h1>
          {/* docs/08-page-content.md Bölüm 3.6: parola girilmeden oda detayları (oyuncu sayısı, giriş ücreti) gösterilmez. */}
          {passwordOk ? (
            <p className="flex items-center gap-3 text-sm text-muted-foreground">
              {room.playerCount}/{room.maxPlayers} oyuncu · <Badge variant="outline">${room.entryFeeUsd} giriş ücreti</Badge>
            </p>
          ) : (
            <p className="text-sm text-muted-foreground">Devam etmek için oda parolasını girin.</p>
          )}
        </div>
      </div>

      <GameCard>
        <CardContent className="flex flex-col gap-4">
          {!passwordOk ? (
            <div className="flex flex-col gap-3">
              <Label htmlFor="roomPassword">Oda parolası</Label>
              <Input
                id="roomPassword"
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
              />
              <Button disabled={busy || !password} onClick={handleVerifyPassword}>
                Doğrula
              </Button>
            </div>
          ) : (
            <Button disabled={busy} onClick={handleJoin}>
              Odaya Katıl
            </Button>
          )}

          {error ? <p className="text-sm text-destructive">{error}</p> : null}
        </CardContent>
      </GameCard>
    </div>
  );
}
