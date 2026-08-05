"use client";

import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
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
        <Card className="w-full max-w-sm">
          <CardHeader>
            <CardTitle>Admin Girişi</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            <Label htmlFor="adminKey">Admin anahtarı</Label>
            <Input
              id="adminKey"
              type="password"
              value={keyInput}
              onChange={(e) => setKeyInput(e.target.value)}
              placeholder="Admin anahtarı"
            />
            <Button disabled={busy || !keyInput.trim()} onClick={handleSubmit}>
              Giriş Yap
            </Button>
            {error ? <p className="text-sm text-destructive">{error}</p> : null}
          </CardContent>
        </Card>
      </div>
    );
  }

  return <>{children}</>;
}
