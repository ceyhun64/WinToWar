import type { Metadata } from "next";
import { Hammer } from "lucide-react";
import { canonicalFor, noIndexFollowMetadata } from "@/lib/metadata";

export const metadata: Metadata = {
  ...noIndexFollowMetadata,
  title: "Bakımdayız — WinToWar",
  description: "WinToWar şu anda kısa bir bakımda; bakiyeniz ve ödemeniz etkilenmez.",
  ...canonicalFor("/bakim"),
};

/** docs/07-pages.md `/bakim`: kritik bir bağımlılık (BTCPay/SignalR) planlı/plansız kapalıyken gösterilir. */
export default function BakimPage() {
  return (
    <div className="flex flex-1 flex-col items-center justify-center gap-4 px-4 py-16 text-center">
      <span className="flex size-12 items-center justify-center rounded-2xl" style={{ backgroundColor: "#F5B94222", color: "#F5B942" }}>
        <Hammer className="size-6" aria-hidden="true" />
      </span>
      <h1 className="text-lg font-semibold">Bakımdayız</h1>
      <p className="max-w-sm text-sm text-muted-foreground">
        Sistem kısa bir bakımda. Kısa süre içinde geri döneceğiz — bakiyeniz/ödemeniz etkilenmez.
      </p>
    </div>
  );
}
