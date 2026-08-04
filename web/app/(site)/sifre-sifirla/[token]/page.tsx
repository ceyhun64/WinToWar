import Link from "next/link";

interface SifreSifirlaPageProps {
  params: Promise<{ token: string }>;
}

/**
 * docs/07-pages.md `/sifre-sifirla/[token]`: bkz. `/sifremi-unuttum` yorumu —
 * mevcut interim kimlik modelinde parola kavramı yok, bu yüzden token burada
 * doğrulanmaz/kullanılmaz (gerçek bir token sistemi henüz kurulmadı). Route,
 * docs'taki tabloyla tutarlılık için var olur; gerçek email/parola auth
 * eklendiğinde token doğrulama + yeni şifre formu burada uygulanır.
 */
export default async function SifreSifirlaPage({ params }: SifreSifirlaPageProps) {
  await params;

  return (
    <div className="mx-auto flex w-full max-w-sm flex-1 flex-col justify-center gap-4 px-4 py-16 text-center">
      <h1 className="text-lg font-semibold">Şifre Sıfırlama</h1>
      <p className="text-sm text-muted-foreground">
        WinToWar şu anda parola tabanlı bir giriş kullanmıyor, bu nedenle sıfırlanacak bir şifre bulunmuyor.
      </p>
      <Link href="/giris" className="text-sm font-medium underline">
        Girişe dön
      </Link>
    </div>
  );
}
