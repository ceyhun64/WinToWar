"use client";

import { useEffect, useState } from "react";
import { getAdminMetrics, type AdminMetrics } from "@/lib/admin/api";

export default function AdminHomePage() {
  const [metrics, setMetrics] = useState<AdminMetrics | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getAdminMetrics()
      .then(setMetrics)
      .catch((err) => setError(String(err)));
  }, []);

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-lg font-semibold">Özet</h1>
      {error ? <p className="text-sm text-destructive">{error}</p> : null}
      {metrics ? (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
          <div className="rounded-md border border-border bg-card p-4">
            <span className="text-xs text-muted-foreground">Bekleyen çekim talebi</span>
            <p className="text-2xl font-semibold tabular-nums">{metrics.pendingWithdrawalCount}</p>
          </div>
          <div className="rounded-md border border-border bg-card p-4">
            <span className="text-xs text-muted-foreground">Aktif maç</span>
            <p className="text-2xl font-semibold tabular-nums">{metrics.activeMatchCount}</p>
          </div>
          <div className="rounded-md border border-border bg-card p-4">
            <span className="text-xs text-muted-foreground">Günlük hacim</span>
            <p className="text-2xl font-semibold tabular-nums">${metrics.dailyVolumeUsd}</p>
          </div>
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">Yükleniyor...</p>
      )}
    </div>
  );
}
