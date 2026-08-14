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
      <div className="flex flex-1 flex-col items-center justify-center gap-4 px-4 text-center">
        <p className="text-sm text-muted-foreground">
          Bu maça ait bir oyuncu oturumu bulunamadı. Lütfen lobiden maça katılın.
        </p>
        <button
          className="text-sm font-medium underline"
          onClick={() => router.push("/lobi")}
        >
          Lobiye dön
        </button>
      </div>
    );
  }

  if (loadError) {
    return <div className="flex flex-1 items-center justify-center text-sm text-destructive">{loadError}</div>;
  }

  if (!map || !gameConfig || !store.state) {
    return (
      <div className="flex flex-1 items-center justify-center text-sm text-muted-foreground">
        Maça bağlanılıyor...
      </div>
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

  function handleAttack(fromRegionId: string, toRegionId: string) {
    if (!hasAttacked) setHasAttacked(true);
    store.attackRegion(fromRegionId, toRegionId);
  }

  return (
    <div className="mx-auto flex w-full max-w-6xl flex-1 flex-col gap-4 px-4 py-4">
      <DevFpsOverlay />
      <div className="flex items-center gap-2">
        <div className="flex-1">
          <Hud state={state} myPlayerId={playerId} gameConfig={gameConfig} />
        </div>
        {/* docs/17-oyun-ici-ui-güclendirme.md Bölüm 17 "Pause/Menu": bu gerçek zamanlı,
            sunucu-otoriter maçta klasik bir "Duraklat/Yeniden Başlat" kavramı yok (maç
            durdurulamaz, para riski taşıyan bir maç sıfırlanamaz) ve docs/03-game-rules.md
            Bölüm 10.1 "Pes etme (Surrender)"yi müşteri kararıyla kapsam dışı bırakmış —
            o yüzden burada ayrı bir "Pes Et" aksiyonu YOK. Menü yalnızca zaten var olan,
            state değiştirmeyen kısayolları (kurallar, bağlantı durumu, sekmeden ayrılma)
            tek bir yerde toplar. */}
        <Sheet open={menuOpen} onOpenChange={setMenuOpen}>
          <SheetTrigger render={<Button variant="outline" size="icon-sm" aria-label="Menü" />}>
            <MenuIcon className="size-4" aria-hidden="true" />
          </SheetTrigger>
          <SheetContent side="right" className="gap-0">
            <div className="flex flex-col gap-1.5 p-6">
              <SheetTitle>Menü</SheetTitle>
              <p className="text-sm text-muted-foreground">
                Bağlantı:{" "}
                {store.connectionStatus === "connected"
                  ? "Bağlı"
                  : store.connectionStatus === "reconnecting"
                    ? "Yeniden bağlanıyor…"
                    : store.connectionStatus === "disconnected"
                      ? "Bağlantı kesildi"
                      : "Bağlanıyor…"}
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

      {/* docs/08-page-content.md Bölüm 3.8: bağlantı durumu içeriği — sunucu-otoriter
          mimaride bu bant/uyarı yalnızca istemcinin senkron olmadığını gösterir,
          harita/HUD'u gizlemez, oyun kuralı/ikna metni içermez. */}
      {store.connectionStatus === "reconnecting" ? (
        <div className="rounded-2xl border border-border bg-muted/40 px-4 py-2 text-center text-sm text-muted-foreground">
          Bağlantı kesildi, yeniden bağlanılıyor…
        </div>
      ) : null}

      {store.connectionStatus === "disconnected" ? (
        <div className="flex flex-col items-center gap-3 rounded-2xl border border-destructive/40 bg-card px-4 py-3 text-center text-sm">
          <p>Maçınız sunucuda devam ediyor, bağlantınızı yeniden kurun.</p>
          <Button size="sm" onClick={() => store.reconnect()}>
            Yeniden Bağlan
          </Button>
        </div>
      ) : null}

      {state.status === "Lobby" || state.status === "Countdown" ? (
        <Card>
          <CardContent className="flex flex-col items-center gap-3 text-center text-sm text-muted-foreground">
            {state.status === "Countdown" && state.countdownRemainingSeconds !== null ? (
              <p>{`Lobi doldu, maç ${state.countdownRemainingSeconds}sn içinde başlıyor.`}</p>
            ) : (
              <>
                {/* docs/08-page-content.md Bölüm 1.4/3.4: "Ali / Mehmet · 3/4" gibi somut,
                    dolan/boş slotları isimle gösteren bir liste — jenerik bir sayaç yerine.
                    docs/03-game-rules.md Bölüm 7: bot olan koltuklar burada da (masa
                    listesinde) açıkça "Bot" rozetiyle işaretlenir. */}
                <p className="flex flex-wrap items-center justify-center gap-x-1 gap-y-1 text-foreground">
                  {Array.from({ length: state.room.maxPlayers }, (_, slot) => {
                    const occupant = state.players.find((p) => p.slot === slot);
                    return (
                      <span key={slot} className="inline-flex items-center gap-1">
                        {slot > 0 ? <span className="text-muted-foreground">·</span> : null}
                        <span>{occupant ? occupant.name : "Bekleniyor…"}</span>
                        {occupant?.isBot ? <Badge variant="secondary">Bot</Badge> : null}
                      </span>
                    );
                  })}
                </p>
                <p>{`${state.lobbyConfirmedCount}/${state.room.maxPlayers} oyuncu`}</p>
                {state.room.maxPlayers - state.lobbyConfirmedCount === 1 ? (
                  <p className="font-medium text-foreground">Son oyuncu bekleniyor</p>
                ) : null}
              </>
            )}
            <p>
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
          </CardContent>
        </Card>
      ) : null}

      {state.status === "Cancelled" ? (
        <Card>
          <CardContent className="text-center text-sm font-medium">
            Lobi zaman aşımına uğradı, ödemeniz iade edildi.
          </CardContent>
        </Card>
      ) : null}

      {/* docs/17-oyun-ici-ui-güclendirme.md Bölüm 9 "Victory/Defeat": kazanma ve kaybetme
          net biçimde ayrılır, her ikisinde de bir sonraki adım için açık bir aksiyon
          (maç özeti/ödül dağılımı — zaten var olan `/mac/[matchId]`, yeni bir maç) sunulur.
          Sahte bir ödül/XP/coin sistemi UYDURULMAZ (proje henüz bu sistemlere sahip değil) —
          gerçek ödeme sonucu zaten `/mac/[matchId]`'de (payout özeti) gösteriliyor. */}
      {state.status === "Completed" ? (
        <Card>
          <CardContent className="flex flex-col items-center gap-3 py-2 text-center">
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
          </CardContent>
        </Card>
      ) : null}

      {isEliminatedButWatching ? (
        <div className="rounded-2xl border border-border bg-muted/40 px-4 py-2 text-center text-sm text-muted-foreground">
          Elendiniz — maçın geri kalanını izleyebilir, kimin kazanacağını görebilirsiniz.
        </div>
      ) : null}

      {/* docs/16-state.io-gorsel-referans.md Bölüm 1.1: haritanın hemen üstünde, gerçek
          zamanlı toprak oranı göstergesi — HUD'un bir parçası ama ayrı bir bileşen. */}
      <TerritoryControlBar state={state} myPlayerId={playerId} />

      {/* docs/14-game-map-redesign.md Bölüm 0/6: harita artık kalıcı bir sağ sidebar'la
          bölünmüyor — ekranın ana odağı, container genişliğinin tamamını kullanır.
          Seçili bölge bilgisi yalnızca bir bölge seçiliyken açılan kompakt bir
          bottom-sheet overlay'de gösterilir (bkz. ActionPanel.tsx). */}
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
      {!hasAttacked ? (
        <p className="text-center text-xs text-muted-foreground">
          Bilgi görmek için bir bölgeye dokunun. Asker göndermek için kendi bölgenizi
          haritadaki herhangi bir bölgeye sürükleyip bırakın.
        </p>
      ) : null}
      <ActionPanel
        map={map}
        state={state}
        myPlayerId={playerId}
        selectedRegionId={selectedRegionId}
        gameConfig={gameConfig}
        onClose={() => setSelectedRegionId(null)}
      />

      {store.error ? (
        <div className="fixed bottom-4 left-1/2 -translate-x-1/2 rounded-md border border-destructive/40 bg-card px-4 py-2 text-sm text-destructive shadow-sm">
          {store.error}
        </div>
      ) : null}
    </div>
  );
}
