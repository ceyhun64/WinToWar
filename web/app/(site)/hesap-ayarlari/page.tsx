"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { getStoredDisplayName, isSignedIn, setStoredDisplayName, signOut } from "@/lib/identity";
import { getWalletBalance } from "@/lib/payments/api";

/**
 * docs/07-pages.md `/hesap-ayarlari`: `/profil`in aksine yazma işlemi içerir.
 * 🛠️ Auth henüz email/parola tabanlı olmadığından burada yalnızca görünen ad
 * değişikliği ve hesabı (yerel kimliği) silme aksiyonu vardır.
 */
export default function HesapAyarlariPage() {
  const router = useRouter();
  const [displayName, setDisplayName] = useState("");
  const [saved, setSaved] = useState(false);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    if (!isSignedIn()) {
      router.replace("/giris");
      return;
    }
    setDisplayName(getStoredDisplayName() ?? "");
  }, [router]);

  function handleSave() {
    if (!displayName.trim()) return;
    setStoredDisplayName(displayName.trim());
    setSaved(true);
    window.setTimeout(() => setSaved(false), 2000);
  }

  /**
   * docs/07-pages.md `/hesap-ayarlari`: "aktif bakiyesi olan bir hesap silinemez,
   * önce /cuzdan'dan çekim yapılması istenir." Kimlik (playerId) yalnızca
   * localStorage'da tutulduğundan (bkz. lib/identity.ts) — silme, backend'deki
   * Wallet kaydına erişimin kalıcı olarak kaybedilmesi demektir; bakiye
   * kontrol edilmeden silinirse gerçek USD bakiyesi geri dönülemez şekilde
   * erişilemez hale gelir. Bu yüzden kontrol yalnızca bir UYARI METNİ değil,
   * gerçek bir engelleyici guard'dır.
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
    <div className="mx-auto flex w-full max-w-sm flex-1 flex-col gap-6 px-4 py-8">
      <h1 className="text-lg font-semibold">Hesap Ayarları</h1>

      <div className="flex flex-col gap-3 rounded-md border border-border bg-card p-4">
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium" htmlFor="displayName">
            Görünen ad
          </label>
          <input
            id="displayName"
            className="h-9 rounded-md border border-input bg-background px-3 text-sm"
            value={displayName}
            onChange={(e) => setDisplayName(e.target.value)}
          />
        </div>
        <Button onClick={handleSave}>{saved ? "Kaydedildi" : "Kaydet"}</Button>
      </div>

      <div className="flex flex-col gap-3 rounded-md border border-destructive/40 bg-card p-4">
        <div>
          <h2 className="text-sm font-semibold">Hesabımı Sil</h2>
          <p className="mt-1 text-sm text-muted-foreground">
            Bu işlem geri alınamaz. Devam etmeden önce bakiyenizi çektiğinizden emin olun (bkz.
            /cuzdan).
          </p>
        </div>
        {deleteError ? <p className="text-sm text-destructive">{deleteError}</p> : null}
        {confirmingDelete ? (
          <div className="flex gap-2">
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
      </div>
    </div>
  );
}
