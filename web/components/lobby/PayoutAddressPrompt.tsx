"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

interface PayoutAddressPromptProps {
  /** null: yalnızca geçersiz/eksik bir adres isteniyor. Doluysa: bakiye yetersiz kaldığında eksik tutar. */
  shortfallUsd: string | null;
  busy: boolean;
  submitLabel?: string;
  onSubmit: (payoutAddress: string) => void;
  onCancel: () => void;
}

/**
 * docs/05-payment.md Bölüm 1.9: hem "bakiye yetersiz, eksik tutar için LTC
 * ödemesi gerekiyor" hem de "geçerli bir LTC ödül adresi gerekiyor" (bkz.
 * RoomEntryOutcome.PayoutAddressRequired/InvalidPayoutAddress) durumlarında
 * kullanılan ortak form — Standart Form şablonu (bkz. `04-style.md`).
 */
export function PayoutAddressPrompt({ shortfallUsd, busy, submitLabel, onSubmit, onCancel }: PayoutAddressPromptProps) {
  const [payoutAddress, setPayoutAddress] = useState("");

  return (
    <Card>
      <CardHeader>
        <CardTitle>{shortfallUsd ? "Bakiye yetersiz" : "LTC ödül adresi gerekli"}</CardTitle>
        <p className="text-sm text-muted-foreground">
          {shortfallUsd
            ? `Eksik ${shortfallUsd} USD için LTC ödemesi gerekiyor. Kazanırsanız ödülünüzün gideceği adresi girin.`
            : "Kazanırsanız ödülünüzün gönderileceği geçerli bir LTC adresi girin."}
        </p>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
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
        <div className="flex gap-2">
          <Button disabled={busy || !payoutAddress.trim()} onClick={() => onSubmit(payoutAddress.trim())}>
            {busy ? "İşleniyor..." : (submitLabel ?? "Devam Et")}
          </Button>
          <Button variant="outline" disabled={busy} onClick={onCancel}>
            Vazgeç
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
