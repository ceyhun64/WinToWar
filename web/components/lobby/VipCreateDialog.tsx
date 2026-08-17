"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { NumberInput } from "@/components/ui/number-input";
import { Label } from "@/components/ui/label";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from "@/components/ui/dialog";
import { createVipRoom, getGameConfig, type JoinRoomResult } from "@/lib/game/api";
import type { GameConfigDto } from "@/lib/game/types";
import { getStoredDisplayName } from "@/lib/identity";
import { getWalletBalance } from "@/lib/payments/api";

function storeSession(matchId: string, playerId: string, playerName: string) {
  window.localStorage.setItem(`wintowar:match:${matchId}:playerId`, playerId);
  window.localStorage.setItem(`wintowar:match:${matchId}:playerName`, playerName);
}

/**
 * Kullanıcı talimatı: VIP oda kurma artık ayrı bir sayfa (`/lobi/vip-olustur`)
 * değil, `/lobi` üzerinde açılan bir modal. İş mantığı (form alanları, canlı
 * havuz önizlemesi, katılım/ödeme sonucu yönlendirmesi) eski sayfadan birebir
 * taşındı — yalnızca sunum bir `Dialog` içine alındı. docs/05-payment.md
 * Bölüm 1.9 (2026-08-08): katılım hiçbir LTC adresi istemez.
 */
export function VipCreateDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const router = useRouter();
  const [playerName, setPlayerName] = useState<string | null>(null);
  const [config, setConfig] = useState<GameConfigDto | null>(null);

  const [maxPlayers, setMaxPlayers] = useState(4);
  const [greyRegionDefenseCount, setGreyRegionDefenseCount] = useState(1);
  const [fogOfWar, setFogOfWar] = useState(false);
  const [isPractice, setIsPractice] = useState(false);
  const [entryFeeUsd, setEntryFeeUsd] = useState(1);
  const [password, setPassword] = useState("");
  const [balanceUsd, setBalanceUsd] = useState<string | null>(null);

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setPlayerName(getStoredDisplayName());
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
  }, [open]);

  function handleResult(result: JoinRoomResult) {
    setBusy(false);
    if (!result.matchId) {
      setError("Oda oluşturulamadı.");
      return;
    }

    if (result.outcome === "Joined" && result.playerId && playerName) {
      storeSession(result.matchId, result.playerId, playerName);
      onOpenChange(false);
      router.push(`/game/${result.matchId}`);
      return;
    }

    if (result.outcome === "InsufficientBalance") {
      if (result.invoice) {
        if (playerName) {
          storeSession(result.matchId, result.invoice.playerId, playerName);
        }
        onOpenChange(false);
        router.push(`/odeme/${result.invoice.invoiceId}`);
        return;
      }
      setError("Ödeme oluşturulamadı, lütfen tekrar deneyin.");
      return;
    }

    setError("Oda oluşturulamadı, tekrar deneyin.");
  }

  async function handleSubmit() {
    if (!playerName) return;
    setBusy(true);
    setError(null);
    try {
      const result = await createVipRoom({
        playerName,
        maxPlayers,
        greyRegionDefenseCount,
        fogOfWar,
        entryFeeUsd: isPractice ? 0 : entryFeeUsd,
        password: password.trim() || undefined,
      });
      handleResult(result);
    } catch (err) {
      setError(String(err));
      setBusy(false);
    }
  }

  if (!config) {
    return null;
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
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] overflow-y-auto rounded-2xl border border-border bg-card text-card-foreground backdrop-blur-md">
        <DialogHeader>
          <DialogTitle>VIP Oda Kur</DialogTitle>
          <DialogDescription>Kuralları sen belirle, kurucusu sen ol.</DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-4">
          <div className="flex flex-col gap-3">
            <Label htmlFor="maxPlayers">
              Oyuncu sayısı ({config.vipRoomMinPlayers}-{config.vipRoomMaxPlayers})
            </Label>
            <NumberInput
              id="maxPlayers"
              min={config.vipRoomMinPlayers}
              max={config.vipRoomMaxPlayers}
              value={maxPlayers}
              onValueChange={(value) => setMaxPlayers((prev) => value ?? prev)}
            />
          </div>

          <div className="flex flex-col gap-3">
            <Label htmlFor="greyDefense">
              Gri bölge savunması ({config.greyRegionDefenseMin}-{config.greyRegionDefenseMax})
            </Label>
            <NumberInput
              id="greyDefense"
              min={config.greyRegionDefenseMin}
              max={config.greyRegionDefenseMax}
              value={greyRegionDefenseCount}
              onValueChange={(value) =>
                setGreyRegionDefenseCount((prev) => value ?? prev)
              }
            />
            <p className="text-xs text-muted-foreground">Yüksek değer = daha zor fetih.</p>
          </div>

          <Label className="font-normal">
            <Checkbox checked={isPractice} onCheckedChange={(checked) => setIsPractice(checked)} />
            Practice modu (ücretsiz — kazanç/kayıp doğurmaz)
          </Label>

          {!isPractice ? (
            <>
              <div className="flex flex-col gap-3">
                <Label htmlFor="entryFee">Giriş ücreti (USD)</Label>
                <NumberInput
                  id="entryFee"
                  min={0}
                  step={0.01}
                  value={entryFeeUsd}
                  onValueChange={(value) => setEntryFeeUsd((prev) => value ?? prev)}
                />
                {shortfall !== null ? (
                  <p className="text-xs text-muted-foreground">
                    {shortfall > 0
                      ? `Bakiyeniz $${balance!.toFixed(2)} — $${shortfall.toFixed(2)} eksik, ödeme ekranına yönlendirileceksiniz.`
                      : `Bakiyenizden $${entryFeeUsd.toFixed(2)} düşülecek (bakiye: $${balance!.toFixed(2)}).`}
                  </p>
                ) : null}
              </div>

              <div className="flex flex-col gap-1 rounded-2xl border border-border bg-background/60 p-3 text-sm">
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
                  <span className="font-semibold tabular-nums" style={{ color: "#F5B942" }}>
                    ${winnerAmount.toFixed(2)}
                  </span>
                </div>
              </div>
            </>
          ) : null}

          <Label className="font-normal">
            <Checkbox checked={fogOfWar} onCheckedChange={(checked) => setFogOfWar(checked)} />
            Fog of War (sisli harita)
          </Label>

          <div className="flex flex-col gap-3">
            <Label htmlFor="password">
              Parola (opsiyonel — girilirse oda şifreli olur, herkese açık listede görünmez)
            </Label>
            <Input id="password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
          </div>

          <Button disabled={busy || !playerName} onClick={handleSubmit}>
            {busy ? "Oda kuruluyor..." : "Odayı Kur ve Katıl"}
          </Button>

          {error ? <p className="text-sm text-destructive">{error}</p> : null}
        </div>
      </DialogContent>
    </Dialog>
  );
}
