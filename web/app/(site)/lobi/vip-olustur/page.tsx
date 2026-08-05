"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { PayoutAddressPrompt } from "@/components/lobby/PayoutAddressPrompt";
import { createVipRoom, getGameConfig, joinRoom, type JoinRoomResult } from "@/lib/game/api";
import type { GameConfigDto } from "@/lib/game/types";
import { getStoredDisplayName, getStoredPayoutAddress, isSignedIn, setStoredPayoutAddress } from "@/lib/identity";
import { getWalletBalance } from "@/lib/payments/api";

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
  const [balanceUsd, setBalanceUsd] = useState<string | null>(null);

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
    getWalletBalance()
      .then((dto) => setBalanceUsd(dto.balanceUsd))
      .catch(() => setBalanceUsd(null));
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

  // docs/08-page-content.md Bölüm 3.5: canlı havuz önizlemesi — rakamlar
  // yeniden tanımlanmaz, yalnızca docs/05-payment.md formülünün (Havuz =
  // Giriş Ücreti × Oyuncu Sayısı, Kazanç = Havuz × (1 − CommissionRate))
  // canlı hesaplanmış hâlidir.
  const commissionRate = Number(config.commissionRate);
  const pool = entryFeeUsd * maxPlayers;
  const commission = pool * commissionRate;
  const winnerAmount = pool - commission;
  const balance = balanceUsd !== null ? Number(balanceUsd) : null;
  const shortfall = balance !== null ? Math.max(0, entryFeeUsd - balance) : null;

  return (
    <div className="mx-auto flex w-full max-w-sm flex-1 flex-col gap-4 px-4 py-6">
      <h1 className="text-lg font-semibold">VIP Oda Kur</h1>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="maxPlayers">
          Oyuncu sayısı ({config.vipRoomMinPlayers}-{config.vipRoomMaxPlayers})
        </Label>
        <Input
          id="maxPlayers"
          type="number"
          min={config.vipRoomMinPlayers}
          max={config.vipRoomMaxPlayers}
          value={maxPlayers}
          onChange={(e) => setMaxPlayers(Number(e.target.value))}
        />
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="greyDefense">
          Gri bölge savunması ({config.greyRegionDefenseMin}-{config.greyRegionDefenseMax})
        </Label>
        <Input
          id="greyDefense"
          type="number"
          min={config.greyRegionDefenseMin}
          max={config.greyRegionDefenseMax}
          value={greyRegionDefenseCount}
          onChange={(e) => setGreyRegionDefenseCount(Number(e.target.value))}
        />
        <p className="text-xs text-muted-foreground">Yüksek değer = daha zor fetih.</p>
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="entryFee">Giriş ücreti (USD)</Label>
        <Input
          id="entryFee"
          type="number"
          min={0}
          step={0.01}
          value={entryFeeUsd}
          onChange={(e) => setEntryFeeUsd(Number(e.target.value))}
          className="text-right"
        />
        {shortfall !== null ? (
          <p className="text-xs text-muted-foreground">
            {shortfall > 0
              ? `Bakiyeniz $${balance!.toFixed(2)} — $${shortfall.toFixed(2)} eksik, ödeme ekranına yönlendirileceksiniz.`
              : `Bakiyenizden $${entryFeeUsd.toFixed(2)} düşülecek (bakiye: $${balance!.toFixed(2)}).`}
          </p>
        ) : null}
      </div>

      <div className="flex flex-col gap-1 rounded-2xl border border-border bg-muted/40 p-3 text-sm">
        <div className="flex items-center justify-between">
          <span className="text-muted-foreground">Toplam Havuz</span>
          <span className="font-medium tabular-nums">${pool.toFixed(2)}</span>
        </div>
        <div className="flex items-center justify-between">
          <span className="text-muted-foreground">Komisyon (%{(commissionRate * 100).toFixed(0)})</span>
          <span className="font-medium tabular-nums">${commission.toFixed(2)}</span>
        </div>
        <div className="flex items-center justify-between">
          <span className="text-muted-foreground">Kazanana Düşen</span>
          <span className="font-semibold tabular-nums">${winnerAmount.toFixed(2)}</span>
        </div>
      </div>

      <Label className="font-normal">
        <Checkbox checked={fogOfWar} onCheckedChange={(checked) => setFogOfWar(checked)} />
        Fog of War (sisli harita)
      </Label>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="password">
          Parola (opsiyonel — girilirse oda şifreli olur, herkese açık listede görünmez)
        </Label>
        <Input
          id="password"
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
        />
      </div>

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

      <Button disabled={busy} onClick={handleSubmit}>
        {busy ? "Oda kuruluyor..." : "Odayı Kur ve Katıl"}
      </Button>

      {error ? <p className="text-sm text-destructive">{error}</p> : null}
    </div>
  );
}
