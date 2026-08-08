"use client";

import { motion } from "framer-motion";

/**
 * `docs/04-style.md` Landing İstisnası: eskiden burada oyunla ilgisiz, teknoloji
 * diyagramı gibi duran dekoratif bir "node grafiği" (`StrategyMap`) vardı —
 * kullanıcı geri bildirimi bunun oyun hissini azalttığını, arka plandaki savaş
 * videosuyla çakıştığını belirtti. Bu bileşen onun yerini alır: gerçek oyun
 * haritasıyla (`components/game/GameMap.tsx`, `lib/game/*`) hiçbir bağlantısı
 * olmayan, salt dekoratif, WinToWar'ın "iki taraf bir bölgeyi ele geçirmeye
 * çalışır" mekaniğini basitçe temsil eden bir SVG sahne.
 *
 * İkinci tur geri bildirim ("kamera çok uzak, askerler cansız, derinlik yok"):
 * kaleler/askerler büyütüldü (daha yakın kamera hissi), asker siluetleri
 * eklendi (düz nokta değil), merkezde çarpışma anında kısa bir "vuruş"
 * parıltısı + duman efekti eklendi, sahnenin sol kenarı hero metnine doğru
 * maskeyle söner (iki taraf artık görsel olarak birleşiyor).
 */

function Castle({ x, color }: { x: number; color: string }) {
  return (
    <motion.g
      animate={{ scaleY: [1, 1.008, 1] }}
      transition={{ duration: 4.5, repeat: Infinity, ease: "easeInOut" }}
      style={{ transformOrigin: `${x}px 260px` }}
    >
      <rect x={x - 30} y={168} width={60} height={92} rx={3} fill={color} />
      <rect x={x - 40} y={148} width={16} height={24} fill={color} />
      <rect x={x + 24} y={148} width={16} height={24} fill={color} />
      <rect x={x - 11} y={134} width={22} height={38} fill={color} />
      <rect
        x={x - 30}
        y={168}
        width={60}
        height={12}
        fill="#0B1120"
        opacity={0.22}
      />
      <rect
        x={x - 6}
        y={182}
        width={12}
        height={20}
        rx={2}
        fill="#0B1120"
        opacity={0.35}
      />
      <line x1={x} y1={134} x2={x} y2={112} stroke={color} strokeWidth={2} />
      <motion.path
        d={`M${x} 112 L${x + 22} 118 L${x} 124 Z`}
        fill={color}
        animate={{
          d: [
            `M${x} 112 L${x + 22} 118 L${x} 124 Z`,
            `M${x} 112 L${x + 26} 116 L${x} 124 Z`,
            `M${x} 112 L${x + 22} 118 L${x} 124 Z`,
          ],
        }}
        transition={{ duration: 1.8, repeat: Infinity, ease: "easeInOut" }}
      />
    </motion.g>
  );
}

function Soldier({
  color,
  delay,
  direction,
  lane,
}: {
  color: string;
  delay: number;
  direction: 1 | -1;
  lane: number;
}) {
  const startX = direction === 1 ? 108 : 272;
  const endX = direction === 1 ? 226 : 154;
  const y = 250 + lane * 11;

  return (
    <motion.g
      initial={{ opacity: 0 }}
      animate={{
        x: [startX, endX],
        opacity: [0, 1, 1, 0],
      }}
      transition={{
        duration: 3.4,
        repeat: Infinity,
        repeatDelay: 1.3,
        delay,
        ease: "easeInOut",
      }}
    >
      <motion.g
        animate={{ y: [y, y - 2.5, y] }}
        transition={{ duration: 0.4, repeat: Infinity, ease: "easeInOut" }}
      >
        <circle cx={0} cy={0} r={3.5} fill={color} />
        <rect x={-3} y={2.5} width={6} height={8} rx={1.5} fill={color} />
        <line
          x1={direction === 1 ? 4 : -4}
          y1={4}
          x2={direction === 1 ? 9 : -9}
          y2={-1}
          stroke="#E5E7EB"
          strokeWidth={1.4}
          strokeLinecap="round"
        />
      </motion.g>
    </motion.g>
  );
}

function ClashBurst() {
  return (
    <motion.g
      initial={{ opacity: 0, scale: 0.4 }}
      animate={{ opacity: [0, 0.9, 0], scale: [0.4, 1.3, 1.6] }}
      transition={{
        duration: 1.4,
        repeat: Infinity,
        repeatDelay: 3.6,
        delay: 0.9,
        ease: "easeOut",
      }}
      style={{ transformOrigin: "190px 244px" }}
    >
      {[0, 60, 120, 180, 240, 300].map((angle) => (
        <line
          key={angle}
          x1={190}
          y1={244}
          x2={
            Math.round((190 + Math.cos((angle * Math.PI) / 180) * 16) * 100) /
            100
          }
          y2={
            Math.round((244 + Math.sin((angle * Math.PI) / 180) * 16) * 100) /
            100
          }
          stroke="#F5B942"
          strokeWidth={2}
          strokeLinecap="round"
        />
      ))}
    </motion.g>
  );
}

function Smoke({ cx, delay }: { cx: number; delay: number }) {
  return (
    <motion.circle
      cx={cx}
      r={9}
      fill="#E5E7EB"
      initial={{ opacity: 0 }}
      animate={{ cy: [252, 214], opacity: [0, 0.16, 0], r: [6, 16] }}
      transition={{ duration: 4.5, repeat: Infinity, delay, ease: "easeOut" }}
      style={{ filter: "blur(6px)" }}
    />
  );
}

export function BattleScene() {
  return (
    <div
      className="h-full w-full"
      style={{
        maskImage:
          "linear-gradient(to right, transparent 0%, black 20%, black 80%, transparent 100%)",
        WebkitMaskImage:
          "linear-gradient(to right, transparent 0%, black 20%, black 80%, transparent 100%)",
      }}
    >
      {/* Dikey maske — sahne üstte/altta da söner, yalnızca sol/sağ değil. */}
      <div
        className="h-full w-full"
        style={{
          maskImage:
            "linear-gradient(to bottom, transparent 0%, black 20%, black 80%, transparent 100%)",
          WebkitMaskImage:
            "linear-gradient(to bottom, transparent 0%, black 20%, black 80%, transparent 100%)",
        }}
      >
      <svg
        viewBox="0 90 380 210"
        className="h-full w-full"
        role="img"
        aria-label="Mavi ve kırmızı takımın bir kale için savaştığı animasyonlu sahne önizlemesi"
      >
        <defs>
          <linearGradient id="ground-fade" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="#38BDF8" stopOpacity={0.16} />
            <stop offset="100%" stopColor="#38BDF8" stopOpacity={0} />
          </linearGradient>
          <radialGradient id="scene-vignette" cx="50%" cy="45%" r="65%">
            <stop offset="55%" stopColor="#070B14" stopOpacity={0} />
            <stop offset="100%" stopColor="#070B14" stopOpacity={0.55} />
          </radialGradient>
        </defs>

        <line
          x1={10}
          y1={260}
          x2={370}
          y2={260}
          stroke="#FFFFFF"
          strokeOpacity={0.14}
          strokeWidth={1.5}
        />
        <rect x={10} y={260} width={360} height={30} fill="url(#ground-fade)" />

        <Smoke cx={178} delay={0} />
        <Smoke cx={202} delay={1.8} />

        <Castle x={70} color="#38BDF8" />
        <Castle x={310} color="#F2495C" />

        {/* Merkezdeki bayrak — el değiştiren bölgeyi temsil eder */}
        <line
          x1={190}
          y1={260}
          x2={190}
          y2={196}
          stroke="#E5E7EB"
          strokeOpacity={0.45}
          strokeWidth={2}
        />
        <motion.path
          animate={{
            d: [
              "M190 198 L218 205 L190 212 Z",
              "M190 198 L222 203 L190 212 Z",
              "M190 198 L218 205 L190 212 Z",
            ],
            fill: ["#38BDF8", "#38BDF8", "#F2495C", "#F2495C", "#38BDF8"],
          }}
          transition={{
            d: { duration: 1.6, repeat: Infinity, ease: "easeInOut" },
            fill: {
              duration: 8,
              repeat: Infinity,
              times: [0, 0.45, 0.5, 0.95, 1],
              ease: "linear",
            },
          }}
        />

        {[0, 1, 2].map((i) => (
          <Soldier
            key={`blue-${i}`}
            color="#38BDF8"
            delay={i * 1.15}
            direction={1}
            lane={i}
          />
        ))}
        {[0, 1, 2].map((i) => (
          <Soldier
            key={`red-${i}`}
            color="#F2495C"
            delay={i * 1.15 + 0.55}
            direction={-1}
            lane={i}
          />
        ))}

        <ClashBurst />

        <rect
          x={0}
          y={90}
          width={380}
          height={210}
          fill="url(#scene-vignette)"
        />
      </svg>
      </div>
    </div>
  );
}
