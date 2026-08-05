"use client";

import { use, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { PayoutAddressPrompt } from "@/components/lobby/PayoutAddressPrompt";
import {
  getRoomByInviteToken,
  joinRoom,
  verifyRoomPassword,
  type JoinRoomResult,
  type RoomSummary,
} from "@/lib/game/api";
import { getStoredDisplayName, getStoredPayoutAddress, isSignedIn, setStoredPayoutAddress } from "@/lib/identity";

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
 * docs/03-game-rules.md Bölüm 2.2 "DÜZELTME"). docs/05-payment.md Bölüm 1.9:
 * bir LTC ödül adresi de gereklidir.
 */
export default function InviteLobbyPage({ params }: InviteLobbyPageProps) {
  const { inviteToken } = use(params);
  const router = useRouter();
  const [playerName, setPlayerName] = useState<string | null>(null);
  const [payoutAddress, setPayoutAddress] = useState("");
  const [room, setRoom] = useState<RoomSummary | null | undefined>(undefined);
  const [password, setPassword] = useState("");
  const [passwordOk, setPasswordOk] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [pendingAddressPrompt, setPendingAddressPrompt] = useState<string | null | undefined>(undefined);

  useEffect(() => {
    if (!isSignedIn()) {
      router.replace("/giris");
      return;
    }
    setPlayerName(getStoredDisplayName());
    setPayoutAddress(getStoredPayoutAddress() ?? "");

    getRoomByInviteToken(inviteToken)
      .then((dto) => {
        setRoom(dto);
        setPasswordOk(!dto.isPasswordProtected);
      })
      .catch(() => setRoom(null));
  }, [inviteToken, router]);

  function handleResult(result: JoinRoomResult) {
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
        if (playerName) {
          storeSession(result.matchId, result.invoice.playerId, playerName);
        }
        router.push(`/odeme/${result.invoice.invoiceId}`);
        return;
      }
      setPendingAddressPrompt(result.shortfallUsd ?? "0.00");
      return;
    }

    if (result.outcome === "PayoutAddressRequired" || result.outcome === "InvalidPayoutAddress") {
      setPendingAddressPrompt(null);
      if (result.outcome === "InvalidPayoutAddress") {
        setError("Girdiğiniz LTC adresi geçersiz.");
      }
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
      handleResult(await joinRoom(room.matchId, playerName, payoutAddress.trim() || undefined));
    } catch (err) {
      setError(String(err));
      setBusy(false);
    }
  }

  async function handlePayoutAddressSubmit(address: string) {
    if (!room || !playerName) return;
    setBusy(true);
    setError(null);
    try {
      handleResult(await joinRoom(room.matchId, playerName, address));
      setPendingAddressPrompt(undefined);
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

  if (pendingAddressPrompt !== undefined) {
    return (
      <div className="mx-auto flex w-full max-w-sm flex-1 flex-col justify-center px-4 py-16">
        <PayoutAddressPrompt
          shortfallUsd={pendingAddressPrompt}
          busy={busy}
          submitLabel={pendingAddressPrompt ? "Ödeme Ekranına Geç" : "Devam Et"}
          onSubmit={handlePayoutAddressSubmit}
          onCancel={() => setPendingAddressPrompt(undefined)}
        />
        {error ? <p className="mt-3 text-sm text-destructive">{error}</p> : null}
      </div>
    );
  }

  return (
    <div className="mx-auto flex w-full max-w-sm flex-1 flex-col justify-center gap-4 px-4 py-16">
      <div>
        <h1 className="text-lg font-semibold">Özel Davet</h1>
        {/* docs/08-page-content.md Bölüm 3.6: parola girilmeden oda detayları (oyuncu sayısı, giriş ücreti) gösterilmez. */}
        {passwordOk ? (
          <p className="flex items-center gap-2 text-sm text-muted-foreground">
            {room.playerCount}/{room.maxPlayers} oyuncu · <Badge variant="outline">${room.entryFeeUsd} giriş ücreti</Badge>
          </p>
        ) : (
          <p className="text-sm text-muted-foreground">Devam etmek için oda parolasını girin.</p>
        )}
      </div>

      {!passwordOk ? (
        <div className="flex flex-col gap-1.5">
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
        <>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="payoutAddress">LTC ödül adresiniz</Label>
            <Input
              id="payoutAddress"
              className="font-mono"
              value={payoutAddress}
              onChange={(e) => setPayoutAddress(e.target.value)}
              placeholder="ltc1q... veya L..."
            />
          </div>
          <Button disabled={busy} onClick={handleJoin}>
            Odaya Katıl
          </Button>
        </>
      )}

      {error ? <p className="text-sm text-destructive">{error}</p> : null}
    </div>
  );
}
