"use client";

import { Sheet, SheetContent, SheetTitle } from "@/components/ui/sheet";
import { playerFillColor } from "@/lib/game/colors";
import type { GameConfigDto, MapDto, MatchStateDto } from "@/lib/game/types";

interface ActionPanelProps {
  map: MapDto;
  state: MatchStateDto;
  myPlayerId: string;
  selectedRegionId: string | null;
  gameConfig: GameConfigDto;
  onClose: () => void;
}

/**
 * docs/14-game-map-redesign.md Bölüm 0/6: bu panel kalıcı bir sağ sidebar DEĞİL —
 * yalnızca bir bölge seçiliyken açılan kompakt bir bottom-sheet overlay'dir (bkz.
 * `web/components/ui/sheet.tsx`, mevcut UI kütüphanesi). Seçim yokken ekranda hiç
 * yer kaplamaz, harita ana odak kalır.
 *
 * docs/04-style.md Bölüm 10: ActionPanel bir aksiyon değil, BİLGİ panelidir — asker
 * gönderme tamamen GameMap/RegionNode içindeki sürükle-bırak etkileşimindedir.
 *
 * docs/23-game-ui-refresh-v2.md Aşama 4:
 * - Yükseklik tavanı `70vh` → `52dvh`. Görev tanımı "alt aksiyon alanı haritayı
 *   kapatmaz" diyor; %70 pratikte haritanın tamamına yakınını örtüyordu. `vh` yerine
 *   `dvh` çünkü mobilde adres çubuğu açılıp kapandıkça panel zıplıyordu.
 * - Bilgi hiyerarşisi kuruldu: asker sayısı artık panelin de en ağır öğesi (haritadaki
 *   rozetle aynı okuma sırası), sahiplik durumu tek bir renkli satırda, komşu listesi
 *   hizalanmış ve sayıları `tabular-nums`.
 */
export function ActionPanel({ map, state, myPlayerId, selectedRegionId, gameConfig, onClose }: ActionPanelProps) {
  const region = selectedRegionId ? map.regions.find((r) => r.id === selectedRegionId) : undefined;
  const regionState = selectedRegionId ? state.regions.find((r) => r.id === selectedRegionId) : undefined;

  return (
    <Sheet open={!!selectedRegionId} onOpenChange={(open) => !open && onClose()}>
      <SheetContent
        side="bottom"
        className="mx-auto flex max-h-[52dvh] w-full max-w-xl flex-col gap-4 overflow-y-auto border-x px-4 pt-6 pb-6"
      >
        {region && regionState ? (
          <RegionDetails
            map={map}
            state={state}
            myPlayerId={myPlayerId}
            gameConfig={gameConfig}
            region={region}
            regionState={regionState}
          />
        ) : null}
      </SheetContent>
    </Sheet>
  );
}

function RegionDetails({
  map,
  state,
  myPlayerId,
  gameConfig,
  region,
  regionState,
}: {
  map: MapDto;
  state: MatchStateDto;
  myPlayerId: string;
  gameConfig: GameConfigDto;
  region: MapDto["regions"][number];
  regionState: NonNullable<MatchStateDto["regions"][number]>;
}) {
  const isMine = regionState.ownerId === myPlayerId;
  const owner = state.players.find((p) => p.id === regionState.ownerId);

  // docs/03-game-rules.md Bölüm 4 — bkz. Hud.tsx'teki aynı formülün gerekçesi.
  const myRegionCount = state.regions.filter((r) => r.ownerId === myPlayerId).length;
  const myProduction = myRegionCount * gameConfig.baseProductionPerInterval;

  const neighbors = region.neighborIds
    .map((neighborId) => {
      const neighborRegion = map.regions.find((r) => r.id === neighborId);
      const neighborState = state.regions.find((r) => r.id === neighborId);
      if (!neighborRegion || !neighborState) return null;
      const neighborOwner = state.players.find((p) => p.id === neighborState.ownerId);
      return { neighborRegion, neighborState, neighborOwner };
    })
    .filter((n): n is NonNullable<typeof n> => n !== null);

  return (
    <>
      {/* Başlık bloğu: asker sayısı burada da BİRİNCİL — haritadaki rozetle aynı
          okuma sırası. Bölge adı ikincil, sahiplik satırı üçüncül. */}
      <div className="flex items-start justify-between gap-4">
        <div className="flex min-w-0 flex-col gap-1">
          <SheetTitle className="truncate">{region.name}</SheetTitle>
          <OwnerLine state={state} regionState={regionState} owner={owner} isMine={isMine} />
        </div>
        <div className="flex shrink-0 flex-col items-end">
          <span className="text-3xl leading-none font-bold tabular-nums">{regionState.soldierCount}</span>
          <span className="text-xs text-muted-foreground">asker</span>
        </div>
      </div>

      {isMine ? (
        // docs/18-yeni-oyun-ici ui-gelistirme.md Bölüm 25: bkz. Hud.tsx'teki aynı kısaltma.
        <p className="text-xs text-muted-foreground tabular-nums">
          Toplam üretimin: +{myProduction} / {gameConfig.productionIntervalSeconds}s
        </p>
      ) : null}

      <div className="flex flex-col gap-2 border-t border-border pt-3">
        <span className="text-xs font-medium text-muted-foreground">Komşu Bölgeler</span>
        <ul className="flex flex-col gap-1.5 text-sm">
          {neighbors.map(({ neighborRegion, neighborState, neighborOwner }) => (
            <li key={neighborRegion.id} className="flex items-center gap-2">
              <span
                className="size-2.5 shrink-0 rounded-full"
                style={{
                  backgroundColor: playerFillColor({
                    roomType: state.room.type,
                    ownerId: neighborState.ownerId,
                    ownerSlot: neighborOwner?.slot ?? null,
                  }),
                  boxShadow: "0 0 0 1px var(--game-dot-ring)",
                }}
                aria-hidden="true"
              />
              <span className="min-w-0 flex-1 truncate">{neighborRegion.name}</span>
              <span className="shrink-0 truncate text-xs text-muted-foreground">
                {neighborState.ownerId === myPlayerId ? "Sen" : (neighborOwner?.name ?? "Sahipsiz")}
              </span>
              <span className="w-10 shrink-0 text-right font-medium tabular-nums">
                {neighborState.soldierCount}
              </span>
            </li>
          ))}
        </ul>
      </div>

      <p className="text-xs text-muted-foreground">
        Asker göndermek için bu bölgeyi haritada başka bir bölgeye sürükleyip bırakın — hedefin
        komşu olması gerekmez.
      </p>
    </>
  );
}

/** Sahiplik tek bir satırda, renk noktası + metinle — renk körlüğünde de metin okunur. */
function OwnerLine({
  state,
  regionState,
  owner,
  isMine,
}: {
  state: MatchStateDto;
  regionState: NonNullable<MatchStateDto["regions"][number]>;
  owner: MatchStateDto["players"][number] | undefined;
  isMine: boolean;
}) {
  const color = playerFillColor({
    roomType: state.room.type,
    ownerId: regionState.ownerId,
    ownerSlot: owner?.slot ?? null,
  });

  return (
    <span className="flex min-w-0 items-center gap-2 text-sm text-muted-foreground">
      <span
        className="size-2.5 shrink-0 rounded-full"
        style={{ backgroundColor: color, boxShadow: "0 0 0 1px var(--game-dot-ring)" }}
        aria-hidden="true"
      />
      <span className="truncate">
        {isMine ? "Senin bölgen" : regionState.ownerId ? `Sahip: ${owner?.name ?? "?"}` : "Sahipsiz bölge"}
      </span>
    </span>
  );
}
