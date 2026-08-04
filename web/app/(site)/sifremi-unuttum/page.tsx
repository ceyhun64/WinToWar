import Link from "next/link";

/**
 * docs/07-pages.md `/sifremi-unuttum`: yalnızca auth yöntemi email/parola ise
 * anlamlıdır. 🛠️ Auth yöntemi henüz netleşmedi (bkz. `web/app/(site)/giris/page.tsx`
 * yorumu) — mevcut interim kimlik modeli parola içermiyor (tarayıcıya kayıtlı
 * kalıcı bir id + görünen ad). Bu route, docs'taki route tablosuyla tutarlılık
 * için var olur ama gerçek bir e-posta/token akışını sahte şekilde taklit etmez
 * (bkz. `01-workflow-rules.md` Bölüm 0.4 placeholder yasağı) — yalnızca mevcut
 * modeli açıkça belirtir. Gerçek email/parola auth eklendiğinde bu sayfa asıl
 * "e-posta gönder" formuna dönüşür.
 */
export default function SifremiUnuttumPage() {
  return (
    <div className="mx-auto flex w-full max-w-sm flex-1 flex-col justify-center gap-4 px-4 py-16 text-center">
      <h1 className="text-lg font-semibold">Şifremi Unuttum</h1>
      <p className="text-sm text-muted-foreground">
        WinToWar şu anda parola tabanlı bir giriş kullanmıyor — girişiniz tarayıcınıza kayıtlı kalıcı bir
        kimlikle yapılır. Sıfırlanacak bir şifreniz yok; erişiminizi kaybettiyseniz{" "}
        <Link href="/destek" className="underline">
          destek
        </Link>{" "}
        ekibiyle iletişime geçin.
      </p>
      <Link href="/giris" className="text-sm font-medium underline">
        Girişe dön
      </Link>
    </div>
  );
}
