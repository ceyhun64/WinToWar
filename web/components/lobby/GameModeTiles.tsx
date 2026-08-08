"use client";

import Image from "next/image";
import { Swords, Castle, Crown, type LucideIcon } from "lucide-react";
import { cn } from "@/lib/utils";
import type { RoomType } from "@/lib/game/types";

/**
 * `docs/04-style.md` Landing İstisnası pilotu — 3. tur geri bildirim: önceki
 * sürüm "Landing'in stilini taşı" (form + kart + dekoratif SVG) yaklaşımıyla
 * hâlâ bir dashboard gibi okunuyordu. Bu sefer "Lobi'yi oyunun bir parçası
 * gibi tasarla" hedefiyle, sayfanın ilk sorusu artık "hangi savaşa
 * katılacağım" oluyor — üç büyük, seçilebilir oyun modu karosu, küçük
 * sekmeler yerine. Pratik anlık bir aksiyondur (tek tık, kuyruk — bkz.
 * `03-game-rules.md` Bölüm 7), bu yüzden "seçili" durumu yoktur; Standart/VIP
 * ise mevcut `tab` state'ini sürer (bkz. `lobi/page.tsx`), yalnızca sunumu
 * değişti.
 *
 * 4. tur geri bildirim ("kartlar boş kutu gibi duruyor, hafif bir arka plan
 * ekle"): Standart/VIP köşede soluk bir "watermark" ikon + moda özgü glow
 * taşır (mevcut `lucide-react` ikonları büyütülüp soluklaştırıldı). Pratik
 * için ise 5. tur talimatıyla gerçek bir görsel arka plan olarak kullanılıyor
 * — okunabilirlik için üzerine koyu bir gradient biner, metin her zaman aynı
 * kalır.
 *
 * 6. tur geri bildirim ("modlar yukarıdan aşağı, sol tarafta olsun"): üç
 * karo artık `grid-cols-3` yerine dikey bir liste (`flex-col`) — her biri
 * ikon solda, başlık/ipucu sağda olan geniş bir satır. `lobi/page.tsx` bu
 * listeyi solda dar bir kolonda, oda ızgarasını ise sağda gösterir.
 */
interface ModeTile {
  key: "Practice" | RoomType;
  label: string;
  hint: string;
  icon: LucideIcon;
  accent: string;
  bgImage?: string;
}

/** `LobiBackground.tsx` bu diziyi paylaşır — görsel yolları tek yerden tanımlanır, tekrar edilmez. */
export const MODES: ModeTile[] = [
  { key: "Practice", label: "Pratik", hint: "Ücretsiz · tek tık", icon: Swords, accent: "#94A3B8", bgImage: "/lobby/practice4.png" },
  { key: "Standard", label: "Standart", hint: "$1 · 4 oyuncu", icon: Castle, accent: "#38BDF8", bgImage: "/lobby/standard2.png" },
  { key: "Vip", label: "VIP", hint: "Kendi kuralın", icon: Crown, accent: "#F5B942", bgImage: "/lobby/vip2.png" },
];

export function GameModeTiles({
  active,
  busy,
  onSelect,
  onPractice,
}: {
  active: RoomType;
  busy: boolean;
  onSelect: (mode: RoomType) => void;
  onPractice: () => void;
}) {
  return (
    <div className="flex flex-col gap-3">
      {MODES.map((mode) => {
        const isPractice = mode.key === "Practice";
        const isActive = !isPractice && active === mode.key;
        return (
          <button
            key={mode.key}
            type="button"
            disabled={busy}
            onClick={() => (isPractice ? onPractice() : onSelect(mode.key as RoomType))}
            className={cn(
              "relative flex items-center gap-3 overflow-hidden rounded-2xl border px-4 py-4 text-left  disabled:pointer-events-none disabled:opacity-50 transition-all duration-150 hover:-translate-y-0.5 active:translate-y-0 cursor-pointer",
              isActive ? "border-primary bg-primary/10" : "border-border bg-card/60 hover:bg-card"
            )}
          >
            {mode.bgImage ? (
              <>
                <Image src={mode.bgImage} alt="" fill sizes="320px" className="pointer-events-none object-cover" />
                <span className="pointer-events-none absolute inset-0 bg-linear-to-r from-background/90 via-background/60 to-background/20" />
              </>
            ) : (
              <>
                <span
                  aria-hidden="true"
                  className="pointer-events-none absolute inset-0"
                  style={{
                    background: `radial-gradient(circle at 85% 20%, ${mode.accent}${isActive ? "33" : "14"}, transparent 65%)`,
                  }}
                />
                <mode.icon
                  aria-hidden="true"
                  className={cn(
                    "pointer-events-none absolute -bottom-3 -right-3 size-16 blur-[1.5px]",
                    isActive ? "opacity-[0.16]" : "opacity-[0.06]"
                  )}
                  style={{ color: mode.accent }}
                />
              </>
            )}

            <span
              className="relative z-10 flex size-11 shrink-0 items-center justify-center rounded-xl"
              style={{ backgroundColor: `${mode.accent}26`, color: mode.accent }}
            >
              <mode.icon className="size-5" aria-hidden="true" />
            </span>
            <span className="relative z-10 flex flex-col">
              <span className="font-semibold">{mode.label}</span>
              <span className="text-xs text-muted-foreground">{mode.hint}</span>
            </span>
          </button>
        );
      })}
    </div>
  );
}
