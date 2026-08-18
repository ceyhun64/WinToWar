"use client";

import Link from "next/link";
import { Space_Grotesk } from "next/font/google";
import { motion } from "framer-motion";
import { Play, LogIn, Flag, Trophy, ChevronRight } from "lucide-react";
import { Button, buttonVariants } from "@/components/ui/button";
import { LandingCta } from "@/components/layout/LandingCta";
import { FloatingCards } from "@/components/landing/FloatingCards";

/**
 * `docs/08-page-content.md` Bölüm 3.1 Katman 2'de zaten tanımlı olan "3 adımlık
 * 'nasıl oynanır' özeti (Katıl → Bölge Fethet → Kazan)" — daha önce hiç
 * uygulanmamıştı, kullanıcı geri bildirimi ("oyunda ne yapıyoruz gösteren UI
 * yok") bu belgelenmiş ama eksik kalan içeriği tamamlıyor.
 */
const HOW_TO_PLAY = [
  { icon: LogIn, label: "Katıl" },
  { icon: Flag, label: "Bölge Fethet" },
  { icon: Trophy, label: "Kazan" },
];

/**
 * `docs/04-style.md` Landing İstisnası: yalnızca bu H1 için ek bir "display"
 * font (Space Grotesk) — site genelindeki gövde fontu (Geist/Inter,
 * `app/layout.tsx`) değişmez, kapsam yalnızca Landing başlığı.
 */
const displayFont = Space_Grotesk({ subsets: ["latin"], weight: ["600", "700"], variable: "--font-display" });

/**
 * `docs/08-page-content.md` Bölüm 3.1'deki Landing içerik iskeleti (tek H1,
 * kazanç formülü, tek birincil CTA) korunur — yalnızca görsel dil (büyük/vurucu
 * başlık, oyun HUD'ı hissi veren CTA) `docs/04-style.md` Landing İstisnası'na
 * göre yenilendi. Kullanıcıya görünen metin Türkçe kalır (`CLAUDE.md`).
 */
export function Hero() {
  return (
    <motion.div
      initial={{ opacity: 0, y: 16 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.6, ease: "easeOut" }}
      className="flex min-w-0 flex-col gap-(--landing-gap) text-left"
    >
      <h1
        className={`${displayFont.className} relative text-[clamp(2.25rem,6vw,4rem)] font-bold uppercase leading-[1.02] tracking-tight text-white`}
      >
        Fethet. Savun.
        <br />
        <span className="relative inline-block text-[#F5B942]">
          <span
            aria-hidden="true"
            className="absolute inset-0 -z-10 scale-150 rounded-full bg-[#F5B942]/25 blur-2xl"
          />
          Kazan.
        </span>
      </h1>

      <p className="mx-auto max-w-md text-[clamp(0.9rem,1.4vw,1.05rem)] leading-relaxed text-white/65 lg:mx-0">
        Gerçek zamanlı bölge savaşlarına katıl.
        <br />
         Komşu kaleleri ele geçir,
        ordunu büyüt ve son ayakta kalan komutan ol.
      </p>
      

      <div className="flex flex-wrap items-center gap-3 justify-start">
        <LandingCta
          size="lg"
          className="group h-(--landing-cta-h) rounded-xl bg-[#38BDF8] px-10 text-lg font-bold text-[#070B14] shadow-[0_0_32px_-6px_rgba(56,189,248,0.75)] ring-2 ring-[#F5B942]/0 transition-all duration-150 hover:-translate-y-0.5 hover:bg-[#38BDF8]/90 hover:shadow-[0_0_40px_-2px_rgba(56,189,248,0.9)] hover:ring-[#F5B942]/60 active:translate-y-0"
        />
        <Button
          className={buttonVariants({
            variant: "white",
            size: "lg",
            className: "h-(--landing-cta-h) gap-2 rounded-xl border-white/15 px-7 text-base font-semibold text-black",
          })}
          onClick={() => window.open("/lobi", "_blank")}
        >
          <Play className="size-4 fill-current" aria-hidden="true" />
          Ücretsiz Dene
        </Button>
      </div>

      <div className="flex flex-wrap items-center justify-center gap-1.5 text-xs font-medium text-white/50 lg:justify-start">
        {HOW_TO_PLAY.map(({ icon: Icon, label }, i) => (
          <span key={label} className="flex items-center gap-1.5">
            {i > 0 && <ChevronRight className="size-3.5 text-white/25" aria-hidden="true" />}
            <span className="flex items-center gap-1 rounded-full border border-transparent px-2.5 py-1">
              {label}
            </span>
          </span>
        ))}
      </div>

      <div className="pt-2">
        <FloatingCards variant="compact" />
      </div>
    </motion.div>
  );
}
