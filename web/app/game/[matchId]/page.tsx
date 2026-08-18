"use client";

import { use, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Menu as MenuIcon } from "lucide-react";
import { ActionPanel } from "@/components/game/ActionPanel";
import { DevFpsOverlay } from "@/components/game/DevFpsOverlay";
import { GameMap } from "@/components/game/GameMap";
import { Hud } from "@/components/game/Hud";
import { TerritoryControlBar } from "@/components/game/TerritoryControlBar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Sheet, SheetContent, SheetFooter, SheetTitle, SheetTrigger } from "@/components/ui/sheet";
import { getGameConfig, getMap } from "@/lib/game/api";
import { useGameStore } from "@/lib/game/store";
import type { GameConfigDto, MapDto } from "@/lib/game/types";

interface GamePageProps {
  params: Promise<{ matchId: string }>;
}

/**
 * docs/23-game-ui-refresh-v2.md Aşama 4 — sayfa iskeleti yeniden kuruldu.
 *
 * ÖNCEKİ YAPININ İKİ SOMUT SORUNU:
 *
 * 1. TAŞMA/KIRPILMA. Sayfa tek bir dikey akıştı (`flex-col gap-4`) ve `min-h-0`
 *    taşımıyordu; `body` ise `overflow-hidden` (docs/13-scroll-lock.md). HUD + durum
 *    kartı + kontrol barı + 75vh harita + ipucu satırı toplandığında sütun viewport'u
 *    aşıyor ve taşan kısım SESSİZCE kırpılıyor, erişilemez hale geliyordu.
 *
 * 2. DURUM BLOKLARI HARİTAYI İTİYORDU. "Yeniden bağlanılıyor", "Elendiniz", lobi
 *    kartı, maç sonu kartı — hepsi akışın İÇİNE giriyordu, yani her durum haritayı
 *    aşağı itip küçültüyordu. Bir bağlantı uyarısının harita alanına mal olması yanlış:
 *    öncelik zinciri harita > HUD > dekorasyon diyor.
 *
 * YENİ İSKELET: sabit yükseklikli bir üst kabuk + kalan tüm alanı kaplayan bir harita
 * alanı. Tüm durum göstergeleri harita alanının ÜSTÜNE overlay olarak biner, akışa
 * girmez — harita hiçbir durumda küçülmez. Bloklayıcı durumlar (lobi, maç bitti,
 * bağlantı koptu) ortalanmış bir panel olarak gelir; bloklamayanlar (yeniden
 * bağlanıyor, elendin, ipucu) ince bantlar olarak.
 */
export default function GamePage({ params }: GamePageProps) {
  const { matchId } = use(params);
  const router = useRouter();

  const [playerId, setPlayerId] = useState<string | null | undefined>(undefined);
  const [map, setMap] = useState<MapDto | null>(null);
  const [gameConfig, setGameConfig] = useState<GameConfigDto | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [selectedRegionId, setSelectedRegionId] = useState<string | null>(null);
  // docs/17-oyun-ici-ui-güclendirme.md Bölüm 16 "Tutorial": sabit yardım metni yalnızca
  // oyuncu bu maçta henüz hiç asker göndermediyse gösterilir — ilk gönderimden sonra
  // harita ekranı gereksiz metinden arınır (sürekli görünen bir talimat yerine).
  const [hasAttacked, setHasAttacked] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);

  useEffect(() => {
    setPlayerId(window.localStorage.getItem(`wintowar:match:${matchId}:playerId`));
  }, [matchId]);

  useEffect(() => {
    getMap()
      .then(setMap)
      .catch((err) => setLoadError(String(err)));
    getGameConfig()
      .then(setGameConfig)
      .catch((err) => setLoadError(String(err)));
  }, []);

  const store = useGameStore(matchId, playerId ?? "");

  if (playerId === undefined) {
    return null;
  }

  if (playerId === null) {
    return (
      <GameShell>
        <CenteredMessage>
          <p className="text-sm text-muted-foreground">
            Bu maça ait bir oyuncu oturumu bulunamadı. Lütfen lobiden maça katılın.
          </p>
          <Button size="sm" onClick={() => router.push("/lobi")}>
            Lobiye dön
          </Button>
        </CenteredMessage>
      </GameShell>
    );
  }

  if (loadError) {
    return (
      <GameShell>
        <CenteredMessage>
          <p className="text-sm text-destructive">{loadError}</p>
        </CenteredMessage>
      </GameShell>
    );
  }

  if (!map || !gameConfig || !store.state) {
    return (
      <GameShell>
        <CenteredMessage>
          <p className="text-sm text-muted-foreground">Maça bağlanılıyor…</p>
        </CenteredMessage>
      </GameShell>
    );
  }

  const { state } = store;
  const isWinner = state.status === "Completed" && state.winners.includes(playerId);
  const winnerNames = state.winners
    .map((id) => state.players.find((p) => p.id === id)?.name ?? "Bilinmeyen")
    .join(", ");
  const myPlayer = state.players.find((p) => p.id === playerId);
  // docs/03-game-rules.md Bölüm 8 "Elenen oyuncunun deneyimi": elenen oyuncu maçtan
  // atılmaz, salt-okunur biçimde maçın geri kalanını izler — aksiyon almaya çalışırsa
  // sunucu zaten reddeder (GameHub.HandleAction "Elendiniz, aksiyon alamazsınız."), ama
  // bunu beklemeden burada da açıkça bildirilir (docs/17 Bölüm 9 "Defeat" geri bildirimi).
  const isEliminatedButWatching = state.status === "Playing" && (myPlayer?.isEliminated ?? false);
  const isLobbyPhase = state.status === "Lobby" || state.status === "Countdown";

  function handleAttack(fromRegionId: string, toRegionId: string) {
    if (!hasAttacked) setHasAttacked(true);
    store.attackRegion(fromRegionId, toRegionId);
  }

  return (
    <GameShell>
      <DevFpsOverlay />

      {/* ── Üst kabuk: asla küçülmez, haritayı kapatmaz ────────────────────────── */}
      <header className="shrink-0 px-3 pt-3 pb-2 sm:px-4">
        <div className="mx-auto flex w-full max-w-5xl items-center gap-2">
          <div className="min-w-0 flex-1">
            <Hud state={state} myPlayerId={playerId} gameConfig={gameConfig} />
          </div>
          {/* docs/17-oyun-ici-ui-güclendirme.md Bölüm 17 "Pause/Menu": bu gerçek zamanlı,
              sunucu-otoriter maçta klasik bir "Duraklat/Yeniden Başlat" kavramı yok (maç
              durdurulamaz, para riski taşıyan bir maç sıfırlanamaz) ve docs/03-game-rules.md
              Bölüm 10.1 "Pes etme (Surrender)"yi müşteri kararıyla kapsam dışı bırakmış —
              o yüzden burada ayrı bir "Pes Et" aksiyonu YOK. Menü yalnızca zaten var olan,
              state değiştirmeyen kısayolları tek bir yerde toplar. */}
          <Sheet open={menuOpen} onOpenChange={setMenuOpen}>
            <SheetTrigger render={<Button variant="outline" size="icon-sm" aria-label="Menü" />}>
              <MenuIcon className="size-4" aria-hidden="true" />
            </SheetTrigger>
            <SheetContent side="right" className="gap-0">
              <div className="flex flex-col gap-3 p-6">
                <SheetTitle>Menü</SheetTitle>
                <div className="flex items-center gap-2 text-sm text-muted-foreground">
                  <ConnectionDot status={store.connectionStatus} />
                  <span>{connectionLabel(store.connectionStatus)}</span>
                </div>
                <p className="text-sm text-muted-foreground">
                  Maç kodu: <span className="font-mono">{matchId}</span>
                </p>
              </div>
              <SheetFooter>
                <Button nativeButton={false} variant="outline" render={<Link href="/kurallar" />}>
                  Kurallar
                </Button>
                <Button nativeButton={false} render={<Link href="/lobi" />}>
                  Maçtan Çık
                </Button>
              </SheetFooter>
            </SheetContent>
          </Sheet>
        </div>
        {/* docs/16-state.io-gorsel-referans.md Bölüm 1.1: haritanın hemen üstünde,
            gerçek zamanlı toprak oranı göstergesi — HUD'un bir parçası ama ayrı bileşen. */}
        <div className="mx-auto mt-2.5 w-full max-w-5xl">
          <TerritoryControlBar state={state} myPlayerId={playerId} />
        </div>
      </header>

      {/* ── Harita alanı: kalan TÜM alan. Aşağıdaki katmanların hepsi overlay'dir,
             hiçbiri akışa girip haritayı küçültmez. ─────────────────────────────── */}
      <main className="relative min-h-0 flex-1">
        {/* Harita kabı bilinçli olarak `absolute inset-2`: yüzde yükseklik (`h-full`)
            bir flex öğesinin İÇİNDE, o öğenin yüksekliği flex algoritmasından geliyorken
            bazı tarayıcılarda 0'a çözülür. Kesin kenar konumları (inset) bu belirsizliği
            tamamen ortadan kaldırır — harita ekranın en önemli öğesi, yükseklik hesabının
            tarayıcıya göre değişmesini göze alamayız. */}
        <div className="absolute inset-2">
          <GameMap
            map={map}
            state={state}
            myPlayerId={playerId}
            selectedRegionId={selectedRegionId}
            armyDeparted={store.armyDeparted}
            armyClashed={store.armyClashed}
            armyArrived={store.armyArrived}
            onSelectRegion={setSelectedRegionId}
            onAttack={handleAttack}
          />
        </div>

        {/* docs/08-page-content.md Bölüm 3.8: bağlantı durumu içeriği — sunucu-otoriter
            mimaride bu bant yalnızca istemcinin senkron olmadığını gösterir, harita/HUD'u
            gizlemez, oyun kuralı/ikna metni içermez. */}
        {store.connectionStatus === "reconnecting" ? (
          <StatusBanner>Bağlantı kesildi, yeniden bağlanılıyor…</StatusBanner>
        ) : isEliminatedButWatching ? (
          <StatusBanner>Elendiniz — maçın geri kalanını izleyebilirsiniz.</StatusBanner>
        ) : null}

        {!hasAttacked && state.status === "Playing" ? (
          <div className="pointer-events-none absolute inset-x-0 bottom-1 flex justify-center px-3">
            <p
              className="rounded-(--game-radius-pill) px-3 py-1.5 text-center text-[0.7rem] leading-snug"
              style={{
                background: "var(--game-panel)",
                color: "var(--game-text-muted)",
                boxShadow: "inset 0 0 0 1px var(--game-panel-border)",
              }}
            >
              Bilgi için bir bölgeye dokun · Asker göndermek için kendi bölgeni hedefe sürükle
            </p>
          </div>
        ) : null}

        {/* ── Bloklayıcı durumlar: haritanın üstünde ortalanmış panel ───────────── */}
        {state.status === "Completed" ? (
          /* docs/17-oyun-ici-ui-güclendirme.md Bölüm 9 "Victory/Defeat": kazanma ve
             kaybetme net biçimde ayrılır, her ikisinde de bir sonraki adım için açık bir
             aksiyon sunulur. Sahte bir ödül/XP/coin sistemi UYDURULMAZ — gerçek ödeme
             sonucu zaten `/mac/[matchId]`'de (payout özeti) gösteriliyor. */
          <OverlayPanel>
            <p className="text-lg font-semibold">
              {isWinner
                ? "Kazandınız!"
                : `Kaybettiniz — Kazanan${state.winners.length > 1 ? "lar" : ""}: ${winnerNames}`}
            </p>
            {state.room.type !== "Practice" ? (
              <p className="text-sm text-muted-foreground">
                {isWinner ? "Ödülünüz bakiyenize eklendi." : "Havuzdaki payınız kazananlara dağıtıldı."}
              </p>
            ) : null}
            <div className="flex flex-wrap items-center justify-center gap-3">
              <Button nativeButton={false} variant="outline" render={<Link href={`/mac/${matchId}`} />}>
                Maç Özetini Gör
              </Button>
              <Button nativeButton={false} render={<Link href="/lobi" />}>
                {isWinner ? "Yeni Maça Başla" : "Tekrar Dene"}
              </Button>
            </div>
          </OverlayPanel>
        ) : state.status === "Cancelled" ? (
          <OverlayPanel>
            <p className="text-sm font-medium">Lobi zaman aşımına uğradı, ödemeniz iade edildi.</p>
            <Button nativeButton={false} size="sm" render={<Link href="/lobi" />}>
              Lobiye Dön
            </Button>
          </OverlayPanel>
        ) : store.connectionStatus === "disconnected" ? (
          <OverlayPanel>
            <p className="text-sm">Maçınız sunucuda devam ediyor, bağlantınızı yeniden kurun.</p>
            <Button size="sm" onClick={() => store.reconnect()}>
              Yeniden Bağlan
            </Button>
          </OverlayPanel>
        ) : isLobbyPhase ? (
          <OverlayPanel>
            {state.status === "Countdown" && state.countdownRemainingSeconds !== null ? (
              <p className="text-base font-semibold tabular-nums">
                {`Lobi doldu, maç ${state.countdownRemainingSeconds}sn içinde başlıyor.`}
              </p>
            ) : (
              <>
                {/* docs/08-page-content.md Bölüm 1.4/3.4: dolan/boş slotları isimle gösteren
                    somut bir liste — jenerik bir sayaç yerine. docs/03-game-rules.md Bölüm 7:
                    bot olan koltuklar burada da açıkça "Bot" rozetiyle işaretlenir. */}
                <p className="flex flex-wrap items-center justify-center gap-x-1 gap-y-1 text-sm">
                  {Array.from({ length: state.room.maxPlayers }, (_, slot) => {
                    const occupant = state.players.find((p) => p.slot === slot);
                    return (
                      <span key={slot} className="inline-flex items-center gap-1">
                        {slot > 0 ? <span className="text-muted-foreground">·</span> : null}
                        <span className={occupant ? undefined : "text-muted-foreground"}>
                          {occupant ? occupant.name : "Bekleniyor…"}
                        </span>
                        {occupant?.isBot ? <Badge variant="secondary">Bot</Badge> : null}
                      </span>
                    );
                  })}
                </p>
                <p className="text-sm tabular-nums text-muted-foreground">
                  {`${state.lobbyConfirmedCount}/${state.room.maxPlayers} oyuncu`}
                </p>
                {state.room.maxPlayers - state.lobbyConfirmedCount === 1 ? (
                  <p className="text-sm font-medium">Son oyuncu bekleniyor</p>
                ) : null}
              </>
            )}
            <p className="text-xs text-muted-foreground">
              Maç kodu: <span className="font-mono font-medium">{matchId}</span>
            </p>
            {store.lobbyTimeoutReached ? (
              <p className="text-xs text-muted-foreground">
                Eşleşme süresi doldu — beklemeye devam edebilir ya da ayrılıp ödemenizi iade alabilirsiniz.
              </p>
            ) : null}
            {state.status === "Lobby" ? (
              <div className="flex flex-wrap items-center justify-center gap-3">
                <Button variant="outline" size="sm" onClick={() => store.leaveLobby()}>
                  {store.lobbyTimeoutReached ? "İptal Et / Bakiyeyi İade Et" : "Lobiden Ayrıl"}
                </Button>
                {state.room.type === "Vip" && state.room.creatorPlayerId === playerId ? (
                  <Button size="sm" onClick={() => store.startVipMatchNow()}>
                    Şimdi Başlat
                  </Button>
                ) : null}
              </div>
            ) : null}
          </OverlayPanel>
        ) : null}
      </main>

      {/* docs/14-game-map-redesign.md Bölüm 0/6: seçili bölge bilgisi yalnızca bir bölge
          seçiliyken açılan kompakt bir bottom-sheet overlay'de gösterilir. */}
      <ActionPanel
        map={map}
        state={state}
        myPlayerId={playerId}
        selectedRegionId={selectedRegionId}
        gameConfig={gameConfig}
        onClose={() => setSelectedRegionId(null)}
      />

      {/* z-60: Sheet (ActionPanel/menü) z-50 kullanıyor — hata mesajı bir sheet açıkken
          de görünmeli, yoksa "asker gönderilemedi" gibi bir uyarı panelin altında kalırdı. */}
      {store.error ? (
        <div className="pointer-events-none fixed inset-x-0 bottom-4 z-60 flex justify-center px-4">
          <div className="rounded-(--game-radius-sm) border border-destructive/40 bg-popover px-4 py-2 text-sm text-destructive shadow-lg">
            {store.error}
          </div>
        </div>
      ) : null}
    </GameShell>
  );
}

/**
 * Oyun ekranının dış kabuğu. `h-full min-h-0 flex-col`, çünkü RootLayout'un `body`'si
 * `overflow-hidden` bir flex sütunu (docs/13-scroll-lock.md): `min-h-0` olmadan iç
 * içerik kabı taşırır ve taşan kısım sessizce kırpılır — Aşama 0'da bulunan hata buydu.
 *
 * `env(safe-area-inset-*)`: çentikli/yuvarlak köşeli telefonlarda HUD ve alt ipucu
 * ekran kenarının altına girmesin diye.
 */
function GameShell({ children }: { children: React.ReactNode }) {
  return (
    <div
      className="flex h-full min-h-0 w-full flex-col"
      style={{
        background: "var(--game-bg)",
        color: "var(--game-text)",
        paddingTop: "env(safe-area-inset-top)",
        paddingBottom: "env(safe-area-inset-bottom)",
        paddingLeft: "env(safe-area-inset-left)",
        paddingRight: "env(safe-area-inset-right)",
      }}
    >
      {children}
    </div>
  );
}

function CenteredMessage({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-0 flex-1 flex-col items-center justify-center gap-4 px-4 text-center">
      {children}
    </div>
  );
}

/** Bloklamayan durum bandı — harita alanının üstünde yüzer, akışa girmez (haritayı itmez). */
function StatusBanner({ children }: { children: React.ReactNode }) {
  return (
    <div className="pointer-events-none absolute inset-x-0 top-1 flex justify-center px-3">
      <p
        className="rounded-(--game-radius-pill) px-3 py-1.5 text-center text-xs"
        style={{
          background: "var(--game-panel)",
          color: "var(--game-text-muted)",
          boxShadow: "inset 0 0 0 1px var(--game-panel-border-strong)",
        }}
      >
        {children}
      </p>
    </div>
  );
}

/**
 * Bloklayıcı durum paneli (lobi, maç bitti, iptal, bağlantı koptu). Haritanın ÜSTÜNE
 * biner ve arkasını hafifçe karartır — bu durumlarda harita zaten etkileşime kapalı
 * ya da anlamsız, ama görünür kalması "maç orada duruyor" hissini korur.
 */
function OverlayPanel({ children }: { children: React.ReactNode }) {
  return (
    <div className="absolute inset-0 z-10 flex items-center justify-center px-4">
      <div className="absolute inset-0 bg-black/45 supports-backdrop-filter:backdrop-blur-[2px]" />
      <Card size="sm" className="relative w-full max-w-sm">
        <CardContent className="flex flex-col items-center gap-3 text-center">{children}</CardContent>
      </Card>
    </div>
  );
}

function connectionLabel(status: string): string {
  if (status === "connected") return "Bağlı";
  if (status === "reconnecting") return "Yeniden bağlanıyor…";
  if (status === "disconnected") return "Bağlantı kesildi";
  return "Bağlanıyor…";
}

function ConnectionDot({ status }: { status: string }) {
  const color =
    status === "connected"
      ? "#5FD198"
      : status === "disconnected"
        ? "var(--destructive)"
        : "#EFC05A";
  return <span className="size-2 shrink-0 rounded-full" style={{ backgroundColor: color }} aria-hidden="true" />;
}
