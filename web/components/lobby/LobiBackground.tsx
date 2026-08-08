"use client";

import Image from "next/image";
import { MODES } from "@/components/lobby/GameModeTiles";
import type { RoomType } from "@/lib/game/types";
import { cn } from "@/lib/utils";

/**
 * Kullanıcı talimatı (2026-08-07, düzeltme): ilk sürüm (renk dalgası + dev
 * ikon) yanlış anlaşılmıştı. Asıl istenen — `GameModeTiles` karolarında
 * zaten kullanılan gerçek artwork'un (`MODES[].bgImage`) karonun içinde
 * kalmaya devam ederken AYNI ANDA sayfanın tüm arka planına da yayılması.
 * Görsel dikkat dağıtmasın diye çok soluk (opacity 0.10), bulanık (blur
 * 30px; kenarlarda blur taşması görünmesin diye 1.08 scale) ve üzerine iki
 * koyu katman biner: düz bir overlay + Landing'deki gradient tonuyla aynı
 * lacivert (`--background: #07111f`, bkz. `globals.css` `.dark` bloğu —
 * `/lobi` zaten bu temaya sabit, bkz. `app/(site)/layout.tsx`
 * `DARK_THEME_PATHS`). Sonuç yalnızca bir "atmosfer" hissi verir, resim
 * kendisi tanınabilir/dikkat çekici olmaz. Üç mod da önceden mount edilip
 * yalnızca opaklığı 700ms crossfade yapılır — src değiştirip yeniden
 * yüklemek yerine yumuşak bir geçiş sağlar.
 */
export function LobiBackground({ mode }: { mode: "Practice" | RoomType }) {
  return (
    <div className="pointer-events-none fixed inset-0 -z-10 overflow-hidden">
      {MODES.map((m) =>
        m.bgImage ? (
          <Image
            key={m.key}
            src={m.bgImage}
            alt=""
            fill
            sizes="100vw"
            className={cn(
              "absolute inset-0 object-cover transition-opacity duration-700 ease-out z-20",
              mode === m.key ? "opacity-20" : "opacity-0"
            )}
            style={{ filter: "blur(5px)", transform: "scale(1.08)" }}
          />
        ) : null
      )}
      <div className="absolute inset-0" style={{ backgroundColor: "rgba(7, 17, 31, 0.82)" }} />
      <div
        className="absolute inset-0"
        style={{ background: "linear-gradient(to bottom, rgba(7, 17, 31, 0.70), rgba(7, 17, 31, 0.90))" }}
      />
    </div>
  );
}
