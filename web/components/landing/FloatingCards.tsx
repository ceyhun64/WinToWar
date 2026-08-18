"use client";

import { Coins, Trophy, Zap, Users } from "lucide-react";
import { motion } from "framer-motion";
import { cn } from "@/lib/utils";

/**
 * Gerçek oyun sabitlerine dayanır (uydurma pazarlama sayısı değil):
 * - Giriş ücreti: `api/GameConfig.cs` StandardRoomEntryFeeUsd = $1
 * - Kazanç payı: `api/PaymentConfig.cs` CommissionRate = %10 → kazanana %90
 * - Maç süresi: docs/04-style.md Bölüm 9 hedef süre (10-15 dk)
 * - Oyuncu sayısı: `api/GameConfig.cs` VipRoomMinPlayers/MaxPlayers = 2-12
 *
 * `docs/04-style.md` Landing İstisnası: ilk turda "havada duran genel UI kartı"
 * eleştirisi aldı — `floating` varyantı artık sahnedeki kale/bayrak
 * konumlarına göre yerleştirilmiş, küçük bir "işaretçi" ile bağlandığı öğeye
 * bakan birer banner (state.io/Clash Royale'deki kale üstü rozetler gibi).
 */
const STATS = [
  {
    icon: Coins,
    label: "Giriş",
    value: "$1",
    accent: "#38BDF8",
    style: { left: "10%", bottom: "11%" },
    pointer: "top" as const,
  },
  {
    icon: Trophy,
    label: "Kazanç Payı",
    value: "%90",
    accent: "#F5B942",
    style: { left: "30%", bottom: "11%" },
    pointer: "top" as const,
  },
  {
    icon: Zap,
    label: "Maç Süresi",
    value: "10-15 Dk",
    accent: "#38BDF8",
    style: { right: "30%", bottom: "11%" },
    pointer: "top" as const,
  },
  {
    icon: Users,
    label: "Oyuncu",
    value: "2-12",
    accent: "#F5B942",
    style: { right: "10%", bottom: "11%" },
    pointer: "top" as const,
  },
];

/**
 * `compact`: hero altında, `lg` altındaki genişliklerde görünen 2x2 grid.
 * `floating`: yalnızca `lg`+ genişlikte, savaş sahnesindeki kale/bayrak
 * konumlarına iğnelenmiş banner'lar. İkisi aynı anda render edilmez.
 */
export function FloatingCards({ variant }: { variant: "compact" | "floating" }) {
  if (variant === "compact") {
    return (
      <div className="grid grid-cols-2 gap-3 lg:hidden">
        {STATS.map(({ icon: Icon, label, value, accent }) => (
          <div
            key={label}
            className="flex min-w-0 items-center gap-2.5 rounded-xl bg-transparent px-3 py-2.5"
          >
            {/* docs/24-responsive-small-screens.md Bölüm 4: hücrenin en dar hâli
                (ikon + "10-15 Dk") 320px'te iki sütuna sığmıyordu. İkon kutusu
                akışkan hâle getirildi ve metin sütununa `min-w-0` verildi —
                390px ve üzerinde ikon yine 2.5rem, yani görünüm değişmez. */}
            <span className="flex size-(--landing-stat-icon) shrink-0 items-center justify-center rounded-lg">
              <Icon className="size-5" aria-hidden="true" />
            </span>
            <span className="flex min-w-0 flex-col leading-tight">
              <span className="truncate text-sm font-semibold tabular-nums text-white">{value}</span>
              <span className="truncate text-[11px] text-white/55">{label}</span>
            </span>
          </div>
        ))}
      </div>
    );
  }

  return (
    <div className="hidden lg:absolute lg:inset-0 lg:block">
      {STATS.map(({ icon: Icon, label, value, accent, style, pointer }) => (
        <motion.div
          key={label}
          whileHover={{ y: -3 }}
          transition={{ duration: 0.15 }}
          className="lg:absolute lg:flex lg:flex-col lg:items-center"
          style={style}
        >
          {pointer === "top" && <span className="mb-0.5 size-2 rotate-45 " />}
          <div className="flex items-center gap-2 rounded-lg bg-transparent px-2.5 ">
           
            <span className="flex flex-col leading-tight">
              <span className="text-xs font-semibold tabular-nums text-white">{value}</span>
              <span className="text-[10px] text-white/55">{label}</span>
            </span>
          </div>
          {pointer === "top" && (
            <span className="mt-0.5 size-2 rotate-45 border-b border-r border-white/10 bg-[#0B1120]/90" />
          )}
        </motion.div>
      ))}
    </div>
  );
}
