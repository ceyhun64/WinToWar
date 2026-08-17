"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Settings } from "lucide-react";
import { Button } from "@/components/ui/button";
import { CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { PageHero } from "@/components/layout/PageHero";
import { GameCard } from "@/components/layout/GameCard";
import { getStoredDisplayName, setStoredDisplayName, signOut } from "@/lib/identity";
import { getWalletBalance } from "@/lib/payments/api";
import { AuthGuard } from "@/components/layout/AuthGuard";
import { changePassword, getMe, AuthApiError } from "@/lib/auth/api";
import type { PlayerAccountDto } from "@/lib/auth/types";
import { GoogleContinueButton } from "@/components/auth/GoogleContinueButton";

/**
 * docs/07-pages.md `/hesap-ayarlari`: `/profil`in aksine yazma işlemi içerir.
 * docs/11-auth.md Bölüm 1.2/7/9: parola değiştirme + Google hesabı bağlama +
 * (bakiye sıfırsa) hesap silme aksiyonları burada.
 */
export default function HesapAyarlariPage() {
  return (
    <AuthGuard>
      <HesapAyarlariPageContent />
    </AuthGuard>
  );
}

function HesapAyarlariPageContent() {
  const router = useRouter();
  const [displayName, setDisplayName] = useState("");
  const [saved, setSaved] = useState(false);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);

  const [account, setAccount] = useState<PlayerAccountDto | null>(null);
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [passwordError, setPasswordError] = useState<string | null>(null);
  const [passwordSaved, setPasswordSaved] = useState(false);
  const [passwordBusy, setPasswordBusy] = useState(false);
  const [googleMessage, setGoogleMessage] = useState<string | null>(null);

  useEffect(() => {
    setDisplayName(getStoredDisplayName() ?? "");
    getMe()
      .then(setAccount)
      .catch(() => setAccount(null));
  }, []);

  function handleSave() {
    if (!displayName.trim()) return;
    setStoredDisplayName(displayName.trim());
    setSaved(true);
    window.setTimeout(() => setSaved(false), 2000);
  }

  async function handleChangePassword() {
    if (newPassword.length < 8) {
      setPasswordError("Yeni şifre en az 8 karakter olmalı.");
      return;
    }
    if (account?.hasPassword && !currentPassword) {
      setPasswordError("Mevcut şifrenizi girin.");
      return;
    }

    setPasswordBusy(true);
    setPasswordError(null);
    try {
      await changePassword(account?.hasPassword ? currentPassword : null, newPassword);
      setCurrentPassword("");
      setNewPassword("");
      setPasswordSaved(true);
      setAccount((prev) => (prev ? { ...prev, hasPassword: true } : prev));
      window.setTimeout(() => setPasswordSaved(false), 2000);
    } catch (err) {
      if (err instanceof AuthApiError && err.code === "INVALID_CREDENTIALS") {
        setPasswordError("Mevcut şifreniz hatalı.");
      } else {
        setPasswordError(err instanceof Error ? err.message : "Şifre güncellenemedi.");
      }
    } finally {
      setPasswordBusy(false);
    }
  }

  /**
   * docs/07-pages.md `/hesap-ayarlari`: "aktif bakiyesi olan bir hesap silinemez,
   * önce /cuzdan'dan çekim yapılması istenir." 🛠️ docs/11-auth.md Bölüm 6'daki
   * uç nokta listesinde bir self-servis hesap silme ucu tanımlanmadığından
   * (yalnızca AccountDeletionRequest veri modeli ayrılmıştır, bkz. Bölüm 2.3/10
   * raporu) bu aksiyon gerçek bir sunucu tarafı silme YAPMAZ — yalnızca bakiye
   * kontrolünü (gerçek, backend'den) yaptıktan sonra oturumu kapatır. Bakiye
   * kontrolü olmadan sessizce "silindi" göstermek 01-workflow-rules.md Bölüm 0.4
   * "sahte davranış" yasağını ihlal eder.
   */
  async function handleDeleteAccount() {
    setDeleting(true);
    setDeleteError(null);
    try {
      const wallet = await getWalletBalance();
      if (Number(wallet.balanceUsd) > 0) {
        setDeleteError(
          `Bakiyenizde $${wallet.balanceUsd} var. Hesabınızı silmeden önce /cuzdan üzerinden bu bakiyeyi çekmelisiniz.`
        );
        setConfirmingDelete(false);
        return;
      }
      signOut();
      router.push("/");
    } catch (err) {
      setDeleteError(String(err));
    } finally {
      setDeleting(false);
    }
  }

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-1 flex-col gap-6 px-4 py-8">
      <PageHero icon={Settings} title="Hesap Ayarları" />

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <GameCard>
          <CardContent className="flex flex-col gap-3">
            <div className="flex flex-col gap-3">
              <Label htmlFor="displayName">Görünen ad</Label>
              <Input
                id="displayName"
                value={displayName}
                onChange={(e) => setDisplayName(e.target.value)}
              />
            </div>
            <Button onClick={handleSave}>{saved ? "Kaydedildi" : "Kaydet"}</Button>
          </CardContent>
        </GameCard>

        <GameCard>
          <CardHeader>
            <CardTitle>{account?.hasPassword ? "Şifre Değiştir" : "Şifre Belirle"}</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            {account?.hasPassword ? (
              <div className="flex flex-col gap-3">
                <Label htmlFor="currentPassword">Mevcut şifre</Label>
                <Input
                  id="currentPassword"
                  type="password"
                  value={currentPassword}
                  onChange={(e) => setCurrentPassword(e.target.value)}
                />
              </div>
            ) : null}
            <div className="flex flex-col gap-3">
              <Label htmlFor="newPassword">Yeni şifre</Label>
              <Input
                id="newPassword"
                type="password"
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                placeholder="En az 8 karakter"
              />
            </div>
            {passwordError ? <p className="text-sm text-destructive">{passwordError}</p> : null}
            <Button disabled={passwordBusy} onClick={handleChangePassword}>
              {passwordBusy ? "Kaydediliyor..." : passwordSaved ? "Kaydedildi" : "Kaydet"}
            </Button>
          </CardContent>
        </GameCard>

        <GameCard>
          <CardHeader>
            <CardTitle>Google Hesabı</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            {account?.googleLinked ? (
              <p className="text-sm text-muted-foreground">Google hesabınız bağlı.</p>
            ) : (
              <>
                <p className="text-sm text-muted-foreground">
                  Google ile de giriş yapabilmek için hesabınızı bağlayın.
                </p>
                <GoogleContinueButton
                  mode="link"
                  onError={setGoogleMessage}
                  onSuccess={(player) => {
                    setAccount(player);
                    setGoogleMessage("Google hesabınız bağlandı.");
                  }}
                />
              </>
            )}
            {googleMessage ? <p className="text-sm text-muted-foreground">{googleMessage}</p> : null}
          </CardContent>
        </GameCard>

        <GameCard className="border-destructive/40">
          <CardHeader>
            <CardTitle>Hesabımı Sil</CardTitle>
            <p className="text-sm text-muted-foreground">
              Bu işlem geri alınamaz. Devam etmeden önce bakiyenizi çektiğinizden emin olun (bkz.
              /cuzdan).
            </p>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            {deleteError ? <p className="text-sm text-destructive">{deleteError}</p> : null}
            {confirmingDelete ? (
              <div className="flex gap-3">
                <Button variant="destructive" disabled={deleting} onClick={handleDeleteAccount}>
                  {deleting ? "Kontrol ediliyor..." : "Evet, hesabımı sil"}
                </Button>
                <Button variant="outline" disabled={deleting} onClick={() => setConfirmingDelete(false)}>
                  Vazgeç
                </Button>
              </div>
            ) : (
              <Button variant="destructive" onClick={() => setConfirmingDelete(true)}>
                Hesabımı Sil
              </Button>
            )}
          </CardContent>
        </GameCard>
      </div>
    </div>
  );
}
