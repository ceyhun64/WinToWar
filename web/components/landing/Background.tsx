"use client";

import { motion } from "framer-motion";

/**
 * Yalnızca `/` (Landing) için dekoratif arka plan — oyun haritasıyla veya
 * `components/game/*` ile hiçbir ilişkisi yoktur. Taban katman `public/landing/landing.mp4`
 * (sessiz, döngülü, otomatik oynatılan video); video artık baskın görsel öğe —
 * önceki sürümdeki ağır koyu tint/blur azaltıldı (bkz. `docs/04-style.md` Landing
 * İstisnası — kullanıcı geri bildirimi: "video görünmüyor, sadece doku gibi").
 * Mavi/kırmızı iki hafif glow, sahnedeki "iki takım" hissini videoyla birlikte destekler.
 */
export function Background() {
  return (
    <div className="pointer-events-none absolute inset-0 overflow-hidden bg-[#070B14]">
      <video
        className="absolute inset-0 h-full w-full object-cover opacity-95"
        src="/landing/landing5.mp4"
        autoPlay
        muted
        loop
        playsInline
        preload="auto"
      />
      {/*
        Yönlü (soldan sağa) tint: solda metin okunabilirliği için daha koyu
        (%55), sağda kaleler/savaş sahnesi daha net görünsün diye daha açık
        (%15) — kullanıcı geri bildirimi: "sağ taraf çok karanlık, kuleler
        gölgede kalıyor". Düz/tekdüze tint yerine bu üç durak kullanılıyor.
      */}
      <div
        className="absolute inset-0"
        style={{
          background: "linear-gradient(90deg, rgba(7,11,20,0.55) 0%, rgba(7,11,20,0.30) 50%, rgba(7,11,20,0.15) 100%)",
        }}
      />

      <motion.div
        className="absolute -left-32 top-[8%] size-105 rounded-full blur-[110px]"
        style={{
          background: "radial-gradient(circle, rgba(56,189,248,0.32), transparent 70%)",
        }}
        animate={{ x: [0, 40, 0], y: [0, 24, 0] }}
        transition={{ duration: 22, repeat: Infinity, ease: "easeInOut" }}
      />
      <motion.div
        className="absolute -right-40 bottom-[4%] size-120 rounded-full blur-[120px]"
        style={{
          background: "radial-gradient(circle, rgba(242,73,92,0.24), transparent 70%)",
        }}
        animate={{ x: [0, -30, 0], y: [0, -20, 0] }}
        transition={{ duration: 26, repeat: Infinity, ease: "easeInOut" }}
      />

      <div
        className="absolute inset-0"
        style={{
          background: "linear-gradient(180deg, transparent 0%, rgba(7,11,20,0.55) 100%)",
        }}
      />
    </div>
  );
}
