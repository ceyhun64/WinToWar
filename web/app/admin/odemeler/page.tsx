"use client";

import { useCallback, useEffect, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import {
  approveWithdrawal,
  getFailedInvoices,
  getPendingWithdrawals,
  rejectWithdrawal,
} from "@/lib/admin/api";
import type { PaymentInvoiceDto, WithdrawalRequestDto } from "@/lib/payments/types";

/** docs/07-pages.md `/admin/odemeler`: bekleyen çekim talepleri, başarısız işlemler, manuel onay/red. */
export default function AdminOdemelerPage() {
  const [withdrawals, setWithdrawals] = useState<WithdrawalRequestDto[]>([]);
  const [failedInvoices, setFailedInvoices] = useState<PaymentInvoiceDto[]>([]);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(() => {
    getPendingWithdrawals().then(setWithdrawals).catch((err) => setError(String(err)));
    getFailedInvoices().then(setFailedInvoices).catch(() => {});
  }, []);

  useEffect(() => {
    refresh();
  }, [refresh]);

  async function handleApprove(id: string) {
    setBusyId(id);
    try {
      await approveWithdrawal(id);
      refresh();
    } catch (err) {
      setError(String(err));
    } finally {
      setBusyId(null);
    }
  }

  async function handleReject(id: string) {
    setBusyId(id);
    try {
      await rejectWithdrawal(id);
      refresh();
    } catch (err) {
      setError(String(err));
    } finally {
      setBusyId(null);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-lg font-semibold">Ödemeler</h1>
      {error ? <p className="text-sm text-destructive">{error}</p> : null}

      <section className="flex flex-col gap-2">
        <h2 className="text-sm font-semibold">Bekleyen Çekim Talepleri</h2>
        {withdrawals.length === 0 ? (
          <p className="text-sm text-muted-foreground">Bekleyen talep yok.</p>
        ) : (
          <ul className="flex flex-col gap-2">
            {withdrawals.map((w) => (
              <li key={w.id}>
                <Card size="sm" className="flex-row items-center justify-between">
                  <CardContent className="flex-1">
                    <p className="font-medium">{w.playerId}</p>
                    <p className="text-xs text-muted-foreground">
                      ${w.amountUsd} · {w.amountLtc} LTC → {w.destinationLtcAddress}
                    </p>
                  </CardContent>
                  <div className="flex gap-2 pr-(--card-spacing)">
                    <Button size="sm" disabled={busyId === w.id} onClick={() => handleApprove(w.id)}>
                      Onayla
                    </Button>
                    <Button size="sm" variant="destructive" disabled={busyId === w.id} onClick={() => handleReject(w.id)}>
                      Reddet
                    </Button>
                  </div>
                </Card>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-sm font-semibold">Başarısız / Süresi Dolmuş İşlemler</h2>
        {failedInvoices.length === 0 ? (
          <p className="text-sm text-muted-foreground">Kayıt yok.</p>
        ) : (
          <ul className="flex flex-col gap-2">
            {failedInvoices.map((i) => (
              <li key={i.invoiceId}>
                <Card size="sm">
                  <CardContent>
                    <p className="font-medium">{i.playerId}</p>
                    <p className="flex items-center gap-1.5 text-xs text-muted-foreground">
                      ${i.amountUsd} · <Badge variant="destructive">{i.status}</Badge> · {i.matchId ?? "Para Yatırma"}
                    </p>
                  </CardContent>
                </Card>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
