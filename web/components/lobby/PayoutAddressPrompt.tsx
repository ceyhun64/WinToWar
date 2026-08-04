"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";

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
    <div className="flex flex-col gap-3 rounded-md border border-border bg-card p-4">
      <div>
        <h3 className="text-sm font-semibold">{shortfallUsd ? "Bakiye yetersiz" : "LTC ödül adresi gerekli"}</h3>
        <p className="text-sm text-muted-foreground">
          {shortfallUsd
            ? `Eksik ${shortfallUsd} USD için LTC ödemesi gerekiyor. Kazanırsanız ödülünüzün gideceği adresi girin.`
            : "Kazanırsanız ödülünüzün gönderileceği geçerli bir LTC adresi girin."}
        </p>
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
      <div className="flex gap-2">
        <Button disabled={busy || !payoutAddress.trim()} onClick={() => onSubmit(payoutAddress.trim())}>
          {busy ? "İşleniyor..." : (submitLabel ?? "Devam Et")}
        </Button>
        <Button variant="outline" disabled={busy} onClick={onCancel}>
          Vazgeç
        </Button>
      </div>
    </div>
  );
}
