import { Spinner } from "@/components/ui/spinner";

/** docs/04-style.md Bölüm 14: "Spinner + kısa metin" — Next.js loading.tsx dosyalarının ortak içeriği. */
export function PageLoading() {
  return (
    <div className="flex flex-1 items-center justify-center gap-3 py-16 text-sm text-muted-foreground">
      <Spinner />
      Yükleniyor...
    </div>
  );
}
