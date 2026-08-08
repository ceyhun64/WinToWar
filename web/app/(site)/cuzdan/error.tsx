"use client";

import { useEffect } from "react";
import { Button } from "@/components/ui/button";

/**
 * docs/07-pages.md "404 / Error / Bakım Sayfaları": segment-bazlı error.tsx —
 * bu sayfa bakiye/ödeme gösterdiğinden, ödeme durumu hakkında doğrulanmamış
 * bir tahminde bulunulmaz ("ödemeniz alındı" gibi bir ifade kullanılmaz),
 * yalnızca "durumu kontrol ediyoruz" denir (bkz. kök error.tsx ile aynı ilke).
 */
export default function CuzdanError({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <div className="flex flex-1 flex-col items-center justify-center gap-4 px-4 py-16 text-center">
      <h1 className="text-lg font-semibold">Bir şeyler ters gitti</h1>
      <p className="max-w-sm text-sm text-muted-foreground">
        Durumu kontrol ediyoruz. Bakiyeniz etkilenmedi — sayfayı yenileyerek tekrar
        deneyebilirsiniz.
      </p>
      <Button onClick={() => reset()}>Tekrar Dene</Button>
    </div>
  );
}
