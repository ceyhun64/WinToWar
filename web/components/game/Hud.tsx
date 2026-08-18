"use client";

import { playerFillColor } from "@/lib/game/colors";
import type { GameConfigDto, MatchStateDto, PlayerDto } from "@/lib/game/types";

interface HudProps {
  state: MatchStateDto;
  myPlayerId: string;
  gameConfig: GameConfigDto;
}

/**
 * docs/03-game-rules.md Bölüm 4 (müşteri kararıyla güncellendi): sahip olunan HER
 * bölge aynı oranda (`baseProductionPerInterval`) kendi askerini üretir; toplam üretim
 * bölge sayısı × bu oran. Değerler backend'in tek doğruluk kaynağı olan GameConfig'ten
 * (bkz. lib/game/api.ts getGameConfig) okunur, burada tekrar sabitlenmez.
 *
 * ── docs/23-game-ui-refresh-v2.md Aşama 4 — HUD yeniden kuruldu ─────────────────
 *
 * Önceki hâlin iki somut sorunu vardı:
 *
 * 1. TAŞMA. Oyuncular tek bir `flex-row` içinde, sarma/kaydırma kuralı olmadan yan
 *    yana diziliyordu. 360px genişlikte 4 oyunculu bir Standart odada bile satır
 *    taşıyordu; VIP'te (12 oyuncuya kadar) tamamen kırılıyordu. Çözüm: oyuncu şeridi
 *    kendi içinde YATAY kaydırılabilir bir kap. Bu, docs/13-scroll-lock.md ile
 *    uyumludur — kural viewport'un (html/body) kaymamasıdır, bilinçli olarak
 *    belirlenmiş iç kaplar kayabilir. Böylece hiçbir oyuncu ekrandan düşmez ve
 *    🔒 "bot her zaman açıkça belirtilir" kuralı her oda tipinde korunur.
 *
 * 2. HİYERARŞİ YOKLUĞU. İsim, bölge sayısı ve üretim hep aynı ağırlıktaydı; HUD bir
 *    "oyun HUD'u" değil bir "dashboard satırı" gibi okunuyordu. Artık her çipin içinde
 *    SAYI birincil (ağır, `tabular-nums`), isim ikincil (küçük, soluk); kendi çipin
 *    ayrıca bir yüzey/halka ile öne çıkar. Bu, ekranın genel öncelik zincirini
 *    (asker sayıları > harita > aksiyonlar > HUD > dekorasyon) HUD'un KENDİ içinde
 *    tekrarlar: HUD'un içindeki veri de sayıdır.
 */
export function Hud({ state, myPlayerId, gameConfig }: HudProps) {
  const regionCountByOwner = new Map<string, number>();
  for (const region of state.regions) {
    if (region.ownerId) {
      regionCountByOwner.set(region.ownerId, (regionCountByOwner.get(region.ownerId) ?? 0) + 1);
    }
  }

  const myProduction = (regionCountByOwner.get(myPlayerId) ?? 0) * gameConfig.baseProductionPerInterval;

  // Kendi çipim her zaman başta — sürekli aynı yerde olsun ki kaydırma sonrası da
  // "ben neredeyim" sorusu sorulmasın (şerit her zaman en soldan başlar).
  const orderedPlayers = [...state.players].sort((a, b) => {
    if (a.id === myPlayerId) return -1;
    if (b.id === myPlayerId) return 1;
    return a.slot - b.slot;
  });

  return (
    <div className="flex min-w-0 items-center gap-3">
      {/* Kaydırma çubuğu gizlenir: globals.css tüm sayfada 10px'lik bir scrollbar
          tanımlıyor ve bu, ~30px yüksekliğindeki HUD şeridinin üçte birini yerdi.
          Kaydırma dokunma/trackpad ile zaten yapılabiliyor; şerit taşıyorsa son çipin
          yarısı görünür kalarak "devamı var" ipucunu kendisi verir. */}
      <div className="min-w-0 flex-1 overflow-x-auto scrollbar-none [&::-webkit-scrollbar]:hidden">
        <div className="flex w-max items-center gap-1.5">
          {orderedPlayers.map((player) => (
            <PlayerChip
              key={player.id}
              player={player}
              regionCount={regionCountByOwner.get(player.id) ?? 0}
              isMe={player.id === myPlayerId}
              color={playerFillColor({
                roomType: state.room.type,
                ownerId: player.id,
                ownerSlot: player.slot,
              })}
            />
          ))}
        </div>
      </div>
      <MatchStatus state={state} myProduction={myProduction} gameConfig={gameConfig} />
    </div>
  );
}

function PlayerChip({
  player,
  regionCount,
  isMe,
  color,
}: {
  player: PlayerDto;
  regionCount: number;
  isMe: boolean;
  color: string;
}) {
  return (
    <div
      className="flex shrink-0 items-center gap-1.5 rounded-(--game-radius-pill) py-1 pr-2.5 pl-1.5"
      style={{
        background: isMe ? "var(--game-chip-own)" : "transparent",
        boxShadow: isMe ? "inset 0 0 0 1px var(--game-panel-border-strong)" : undefined,
        opacity: player.isEliminated ? 0.45 : 1,
      }}
      title={player.name}
    >
      <span
        className="size-2.5 shrink-0 rounded-full"
        style={{
          backgroundColor: color,
          // Kimlik noktası koyu HUD zeminine karşı da ayrışsın diye ince bir halka —
          // paletteki en koyu tonda bile nokta yutulmaz.
          boxShadow: "0 0 0 1px var(--game-dot-ring)",
        }}
      />
      <span
        className="max-w-28 truncate text-[0.7rem] leading-none"
        style={{ color: "var(--game-text-muted)" }}
      >
        {isMe ? "Sen" : player.name}
      </span>
      {/* docs/03-game-rules.md Bölüm 7: rakip bot olduğunda her zaman açıkça belirtilir —
          insan olduğu izlenimi hiçbir şekilde verilmez. Kompakt bir etiket, ama metni
          hâlâ açık ("BOT"), kriptik bir sembol değil. */}
      {player.isBot ? (
        <span
          className="rounded-(--game-radius-xs) px-1 text-[0.65rem] leading-[1.35] font-semibold tracking-wide"
          style={{ background: "var(--game-panel-border-strong)", color: "var(--game-text-muted)" }}
        >
          BOT
        </span>
      ) : null}
      {player.isEliminated ? (
        <span className="text-[0.65rem] leading-none" style={{ color: "var(--game-text-muted)" }}>
          elendi
        </span>
      ) : !player.isConnected && !player.isBot ? (
        <span className="text-[0.65rem] leading-none" style={{ color: "var(--game-text-muted)" }}>
          bağlı değil
        </span>
      ) : null}
      <span
        className="text-sm leading-none font-semibold tabular-nums"
        style={{ color: "var(--game-text)" }}
      >
        {regionCount}
      </span>
    </div>
  );
}

/** docs/18-yeni-oyun-ici ui-gelistirme.md Bölüm 25: üretim rakamı/kuralı aynı (yalnızca sunum) — oyun içi bir "+X / Ys" biçimi. */
function MatchStatus({
  state,
  myProduction,
  gameConfig,
}: {
  state: MatchStateDto;
  myProduction: number;
  gameConfig: GameConfigDto;
}) {
  if (state.status === "Completed" || state.status === "Cancelled") {
    return (
      <span className="shrink-0 text-xs font-medium" style={{ color: "var(--game-text-muted)" }}>
        Maç bitti
      </span>
    );
  }

  if (state.status !== "Playing") {
    return (
      <span className="shrink-0 text-xs font-medium tabular-nums" style={{ color: "var(--game-text-muted)" }}>
        {state.lobbyConfirmedCount}/{state.room.maxPlayers} oyuncu
      </span>
    );
  }

  return (
    <div className="flex shrink-0 items-baseline gap-1">
      <span className="text-base leading-none font-bold tabular-nums" style={{ color: "var(--game-text)" }}>
        +{myProduction}
      </span>
      <span className="text-[0.7rem] leading-none" style={{ color: "var(--game-text-muted)" }}>
        /{gameConfig.productionIntervalSeconds}s
      </span>
    </div>
  );
}
