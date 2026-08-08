"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { ensureSessionLoaded, isSignedIn } from "@/lib/identity";

/**
 * docs/07-pages.md "Layout Yapısı": "Auth gerektiren sayfalar... kendi içlerinde
 * bir AuthGuard component'i ile korunur; bu, her sayfanın kendi içine auth
 * kontrolü kopyalamasını önler" (bkz. `06-coding-standards.md` "Kod Tekrarını
 * Önleme"). Önceden 7 sayfada birebir tekrarlanan `isSignedIn()` + redirect
 * bloğunun tek merkezi karşılığıdır.
 *
 * docs/11-auth.md: access token yalnızca memory'de tutulduğundan (Bölüm 1.4)
 * sayfa yenilemesinde `isSignedIn()` senkron olarak yanlış "hayır" dönebilir —
 * `ensureSessionLoaded()` HttpOnly refresh cookie'siyle sessiz bir oturum
 * yenilemesi bitene kadar bekler, ondan sonra karar verilir.
 */
export function AuthGuard({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const [authorized, setAuthorized] = useState(false);

  useEffect(() => {
    let cancelled = false;
    ensureSessionLoaded().then(() => {
      if (cancelled) return;
      if (!isSignedIn()) {
        router.replace("/giris");
        return;
      }
      setAuthorized(true);
    });
    return () => {
      cancelled = true;
    };
  }, [router]);

  if (!authorized) {
    return null;
  }

  return <>{children}</>;
}
