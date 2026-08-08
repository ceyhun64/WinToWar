"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ensureSessionLoaded, getCurrentRole, isSignedIn } from "@/lib/identity";
import { login } from "@/lib/auth/api";

/**
 * docs/11-auth.md Bölüm 1.9/0.1: paylaşılan X-Admin-Key header'ı kaldırıldı —
 * admin erişimi artık gerçek bir oturum + Player.Role == Admin kontrolüdür
 * (bkz. api/Services/AdminAuthFilter.cs). İki paralel admin yetkilendirme
 * sistemi bırakılmaz (Bölüm 0.1): mevcut giriş formu iskeleti korunur ama artık
 * gerçek e-posta/parola ile `/auth/login`'e bağlanır, `PlayerAccountDto.role`
 * "Admin" değilse erişim reddedilir.
 */
export function AdminGate({ children }: { children: React.ReactNode }) {
  const [authorized, setAuthorized] = useState<boolean | null>(null);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    ensureSessionLoaded().then(() => {
      setAuthorized(isSignedIn() && getCurrentRole() === "Admin");
    });
  }, []);

  async function handleSubmit() {
    setBusy(true);
    setError(null);
    try {
      await login(email.trim(), password);
      if (getCurrentRole() !== "Admin") {
        setError("Bu hesap admin yetkisine sahip değil.");
        setAuthorized(false);
        return;
      }
      setAuthorized(true);
    } catch {
      setError("E-posta veya şifre hatalı.");
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
            <Label htmlFor="adminEmail">E-posta</Label>
            <Input
              id="adminEmail"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="admin@wintowar.local"
            />
            <Label htmlFor="adminPassword">Şifre</Label>
            <Input
              id="adminPassword"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="Şifre"
            />
            <Button disabled={busy || !email.trim() || !password} onClick={handleSubmit}>
              Giriş Yap
            </Button>
            {error ? <p className="text-sm text-destructive">{error}</p> : null}
            <Link href="/giris" className="text-center text-xs text-muted-foreground underline">
              Oyuncu girişine dön
            </Link>
          </CardContent>
        </Card>
      </div>
    );
  }

  return <>{children}</>;
}
