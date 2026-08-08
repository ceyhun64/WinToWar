"use client";

import { useEffect } from "react";
import { Button } from "@/components/ui/button";

/**
 * docs/07-pages.md "404 / Error / Bakım Sayfaları": segment-bazlı error.tsx —
 * oyun sunucu-otoriter olduğundan (02-architecture.md) burada ödeme/maç
 * durumu hakkında doğrulanmamış bir tahminde bulunulmaz, yalnızca "maçınız
 * sunucuda devam ediyor" güveni verilir (bkz. 08-page-content.md Bölüm 3.8
 * bağlantı durumu içeriğiyle aynı ton).
 */
export default function GameError({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <div className="flex flex-1 flex-col items-center justify-center gap-4 px-4 py-16 text-center">
      <h1 className="text-lg font-semibold">Bir şeyler ters gitti</h1>
      <p className="max-w-sm text-sm text-muted-foreground">
        Maçınız sunucuda devam ediyor, bakiyeniz etkilenmedi. Sayfayı yeniden yükleyerek tekrar
        bağlanabilirsiniz.
      </p>
      <Button onClick={() => reset()}>Tekrar Dene</Button>
    </div>
  );
}
