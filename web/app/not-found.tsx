import Link from "next/link";
import { Button } from "@/components/ui/button";

/** docs/07-pages.md "404 / Error / Bakım Sayfaları": geçersiz route'larda gösterilir. */
export default function NotFound() {
  return (
    <div className="flex flex-1 flex-col items-center justify-center gap-4 px-4 py-16 text-center">
      <h1 className="text-2xl font-semibold">404</h1>
      <p className="text-sm text-muted-foreground">Aradığınız sayfa bulunamadı.</p>
      <Link href="/">
        <Button>Ana Sayfaya Dön</Button>
      </Link>
    </div>
  );
}
