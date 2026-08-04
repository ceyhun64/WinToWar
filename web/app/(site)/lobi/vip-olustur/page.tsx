"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { PayoutAddressPrompt } from "@/components/lobby/PayoutAddressPrompt";
import { createVipRoom, getGameConfig, joinRoom, type JoinRoomResult } from "@/lib/game/api";
import type { GameConfigDto } from "@/lib/game/types";
import { getStoredDisplayName, getStoredPayoutAddress, isSignedIn, setStoredPayoutAddress } from "@/lib/identity";

function storeSession(matchId: string, playerId: string, playerName: string) {
  window.localStorage.setItem(`wintowar:match:${matchId}:playerId`, playerId);
  window.localStorage.setItem(`wintowar:match:${matchId}:playerName`, playerName);
}

/**
 * docs/07-pages.md `/lobi/vip-olustur`: gri bölge savunması (1-7), Fog of
 * War/Açık Harita, giriş ücreti, oyuncu sayısı (2-12), opsiyonel şifre. Kurucu
 * formu gönderdiği anda odanın 1. oyuncusu olur ve giriş ücretini kendisi de
 * öder (bkz. docs/03-game-rules.md Bölüm 2.2). docs/05-payment.md Bölüm 1.9:
 * bir LTC ödül adresi de aynı anda gereklidir.
 */
export default function VipOlusturPage() {
  const router = useRouter();
  const [playerName, setPlayerName] = useState<string | null>(null);
  const [config, setConfig] = useState<GameConfigDto | null>(null);

  const [maxPlayers, setMaxPlayers] = useState(4);
  const [greyRegionDefenseCount, setGreyRegionDefenseCount] = useState(1);
  const [fogOfWar, setFogOfWar] = useState(false);
  const [entryFeeUsd, setEntryFeeUsd] = useState(1);
  const [password, setPassword] = useState("");
  const [payoutAddress, setPayoutAddress] = useState("");

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [pendingJoin, setPendingJoin] = useState<{ matchId: string; shortfallUsd: string | null } | null>(null);

  useEffect(() => {
    if (!isSignedIn()) {
      router.replace("/giris");
      return;
    }
    setPlayerName(getStoredDisplayName());
    setPayoutAddress(getStoredPayoutAddress() ?? "");
    getGameConfig()
      .then((dto) => {
        setConfig(dto);
        setMaxPlayers(dto.vipRoomMinPlayers);
        setGreyRegionDefenseCount(dto.greyRegionDefenseMin);
      })
      .catch((err) => setError(String(err)));
  }, [router]);

  function handleResult(result: JoinRoomResult) {
    setBusy(false);
    if (!result.matchId) {
      setError("Oda oluşturulamadı.");
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
      setPendingJoin({ matchId: result.matchId, shortfallUsd: result.shortfallUsd ?? "0.00" });
      return;
    }

    if (result.outcome === "PayoutAddressRequired" || result.outcome === "InvalidPayoutAddress") {
      // Oda zaten kuruldu, kurucu rezerve edildi (ödemesi onaysız) — aynı odaya
      // joinRoom ile geçerli bir adres sağlanarak tekrar denenir (bkz.
      // handlePayoutAddressSubmit — createVipRoom TEKRAR çağrılmaz, ikinci bir
      // oda oluşmasını önler).
      setPendingJoin({ matchId: result.matchId, shortfallUsd: null });
      if (result.outcome === "InvalidPayoutAddress") {
        setError("Girdiğiniz LTC adresi geçersiz, lütfen tekrar deneyin.");
      }
      return;
    }

    setError("Oda oluşturulamadı, tekrar deneyin.");
  }

  async function handleSubmit() {
    if (!playerName) return;
    if (!payoutAddress.trim()) {
      setError("LTC ödül adresinizi girin.");
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const result = await createVipRoom({
        playerName,
        maxPlayers,
        greyRegionDefenseCount,
        fogOfWar,
        entryFeeUsd,
        password: password.trim() || undefined,
        payoutAddress: payoutAddress.trim(),
      });
      handleResult(result);
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
      const result = await joinRoom(pendingJoin.matchId, playerName, address);
      handleResult(result);
      setPendingJoin(null);
    } catch (err) {
      setError(String(err));
      setBusy(false);
    }
  }

  if (!playerName || !config) {
    return null;
  }

  if (pendingJoin) {
    return (
      <div className="mx-auto flex w-full max-w-sm flex-1 flex-col justify-center px-4 py-16">
        <PayoutAddressPrompt
          shortfallUsd={pendingJoin.shortfallUsd}
          busy={busy}
          submitLabel={pendingJoin.shortfallUsd ? "Ödeme Ekranına Geç" : "Devam Et"}
          onSubmit={handlePayoutAddressSubmit}
          onCancel={() => setPendingJoin(null)}
        />
        {error ? <p className="mt-3 text-sm text-destructive">{error}</p> : null}
      </div>
    );
  }

  return (
    <div className="mx-auto flex w-full max-w-sm flex-1 flex-col gap-4 px-4 py-6">
      <h1 className="text-lg font-semibold">VIP Oda Kur</h1>

      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-medium" htmlFor="maxPlayers">
          Oyuncu sayısı ({config.vipRoomMinPlayers}-{config.vipRoomMaxPlayers})
        </label>
        <input
          id="maxPlayers"
          type="number"
          min={config.vipRoomMinPlayers}
          max={config.vipRoomMaxPlayers}
          value={maxPlayers}
          onChange={(e) => setMaxPlayers(Number(e.target.value))}
          className="h-9 rounded-md border border-input bg-background px-3 text-sm"
        />
      </div>

      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-medium" htmlFor="greyDefense">
          Gri bölge savunması ({config.greyRegionDefenseMin}-{config.greyRegionDefenseMax})
        </label>
        <input
          id="greyDefense"
          type="number"
          min={config.greyRegionDefenseMin}
          max={config.greyRegionDefenseMax}
          value={greyRegionDefenseCount}
          onChange={(e) => setGreyRegionDefenseCount(Number(e.target.value))}
          className="h-9 rounded-md border border-input bg-background px-3 text-sm"
        />
      </div>

      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-medium" htmlFor="entryFee">
          Giriş ücreti (USD)
        </label>
        <input
          id="entryFee"
          type="number"
          min={0}
          step={0.01}
          value={entryFeeUsd}
          onChange={(e) => setEntryFeeUsd(Number(e.target.value))}
          className="h-9 rounded-md border border-input bg-background px-3 text-right text-sm"
        />
      </div>

      <label className="flex items-center gap-2 text-sm">
        <input type="checkbox" checked={fogOfWar} onChange={(e) => setFogOfWar(e.target.checked)} />
        Fog of War (sisli harita)
      </label>

      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-medium" htmlFor="password">
          Parola (opsiyonel — girilirse oda şifreli olur, herkese açık listede görünmez)
        </label>
        <input
          id="password"
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          className="h-9 rounded-md border border-input bg-background px-3 text-sm"
        />
      </div>

      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-medium" htmlFor="payoutAddress">
          LTC ödül adresiniz
        </label>
        <input
          id="payoutAddress"
          className="h-9 rounded-md border border-input bg-background px-3 font-mono text-sm"
          value={payoutAddress}
          onChange={(e) => setPayoutAddress(e.target.value)}
          placeholder="ltc1q... veya L..."
        />
      </div>

      <Button disabled={busy} onClick={handleSubmit}>
        {busy ? "Oda kuruluyor..." : "Odayı Kur ve Katıl"}
      </Button>

      {error ? <p className="text-sm text-destructive">{error}</p> : null}
    </div>
  );
}
