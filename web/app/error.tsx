"use client";

import { useEffect } from "react";
import { Button } from "@/components/ui/button";

/**
 * docs/07-pages.md "404 / Error / Bakım Sayfaları": beklenmeyen render/veri
 * hatasında güven verici ama abartısız bir mesaj — ödeme durumu hakkında
 * doğrulanmamış bir tahminde bulunulmaz, yalnızca "durumu kontrol ediyoruz" denir.
 */
export default function GlobalError({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <div className="flex flex-1 flex-col items-center justify-center gap-4 px-4 py-16 text-center">
      <h1 className="text-lg font-semibold">Bir şeyler ters gitti</h1>
      <p className="max-w-sm text-sm text-muted-foreground">
        Durumu kontrol ediyoruz. Bakiyeniz/ödemeniz varsa etkilenmedi — sayfayı yenileyerek tekrar
        deneyebilirsiniz.
      </p>
      <Button onClick={() => reset()}>Tekrar Dene</Button>
    </div>
  );
}
