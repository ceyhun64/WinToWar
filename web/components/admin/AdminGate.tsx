"use client";

import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { getAdminKey, getAdminMetrics, setAdminKey } from "@/lib/admin/api";

/**
 * 🛠️ Bkz. api/AdminConfig.cs gerekçesi: projede henüz bir Role tabanlı auth
 * sistemi yok, admin erişimi paylaşılan bir anahtarla korunur. Anahtar yalnızca
 * oturum boyunca (sessionStorage) tutulur.
 */
export function AdminGate({ children }: { children: React.ReactNode }) {
  const [authorized, setAuthorized] = useState<boolean | null>(null);
  const [keyInput, setKeyInput] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function verify() {
    try {
      await getAdminMetrics();
      setAuthorized(true);
    } catch {
      setAuthorized(false);
    }
  }

  useEffect(() => {
    if (getAdminKey()) {
      verify();
    } else {
      setAuthorized(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function handleSubmit() {
    setBusy(true);
    setError(null);
    setAdminKey(keyInput.trim());
    try {
      await getAdminMetrics();
      setAuthorized(true);
    } catch {
      setError("Anahtar geçersiz.");
      setAuthorized(false);
    } finally {
      setBusy(false);
    }
  }

  if (authorized === null) {
    return null;
  }

  if (!authorized) {
    return (
      <div className="flex flex-1 flex-col items-center justify-center gap-4 px-4 py-16">
        <div className="flex w-full max-w-sm flex-col gap-3">
          <h1 className="text-lg font-semibold">Admin Girişi</h1>
          <input
            type="password"
            className="h-9 rounded-md border border-input bg-background px-3 text-sm"
            value={keyInput}
            onChange={(e) => setKeyInput(e.target.value)}
            placeholder="Admin anahtarı"
          />
          <Button disabled={busy || !keyInput.trim()} onClick={handleSubmit}>
            Giriş Yap
          </Button>
          {error ? <p className="text-sm text-destructive">{error}</p> : null}
        </div>
      </div>
    );
  }

  return <>{children}</>;
}
