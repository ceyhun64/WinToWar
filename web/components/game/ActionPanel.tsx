"use client";

import { Badge } from "@/components/ui/badge";
import { Sheet, SheetContent, SheetTitle } from "@/components/ui/sheet";
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
 * docs/14-game-map-redesign.md Bölüm 0/6: bu panel artık kalıcı bir sağ sidebar
 * DEĞİL — yalnızca bir bölge seçiliyken açılan kompakt bir bottom-sheet overlay'dir
 * (bkz. `web/components/ui/sheet.tsx`, mevcut UI kütüphanesi — yeni bir pattern icat
 * edilmedi). Seçim yokken ekranda hiç yer kaplamaz, harita ana odak kalır.
 *
 * docs/04-style.md Bölüm 10 (state.io incelemesi sonrası): ActionPanel bir aksiyon
 * değil, bilgi panelidir — asker gönderme tamamen GameMap/RegionNode içindeki
 * sürükle-bırak etkileşimine taşındı (bkz. GameMap.tsx).
 */
export function ActionPanel({ map, state, myPlayerId, selectedRegionId, gameConfig, onClose }: ActionPanelProps) {
  const region = selectedRegionId ? map.regions.find((r) => r.id === selectedRegionId) : undefined;
  const regionState = selectedRegionId ? state.regions.find((r) => r.id === selectedRegionId) : undefined;

  return (
    <Sheet open={!!selectedRegionId} onOpenChange={(open) => !open && onClose()}>
      <SheetContent
        side="bottom"
        className="mx-auto flex max-h-[70vh] w-full max-w-xl flex-col gap-3 overflow-y-auto border-x px-4 pt-6 pb-6"
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
  const ownerName = state.players.find((p) => p.id === regionState.ownerId)?.name;

  if (!isMine) {
    return (
      <>
        <SheetTitle>{region.name}</SheetTitle>
        <div className="flex flex-col gap-1">
          <p className="text-sm text-muted-foreground">
            {regionState.ownerId ? `Sahip: ${ownerName ?? "?"}` : "Sahipsiz bölge"}
          </p>
          <p className="text-sm text-muted-foreground">Savunma: {regionState.soldierCount} asker</p>
        </div>
      </>
    );
  }

  // docs/03-game-rules.md Bölüm 4 — bkz. Hud.tsx'teki aynı formülün gerekçesi.
  const myRegionCount = state.regions.filter((r) => r.ownerId === myPlayerId).length;
  const myProduction = myRegionCount * gameConfig.baseProductionPerInterval;

  const neighbors = region.neighborIds
    .map((neighborId) => {
      const neighborRegion = map.regions.find((r) => r.id === neighborId);
      const neighborState = state.regions.find((r) => r.id === neighborId);
      if (!neighborRegion || !neighborState) return null;
      const neighborOwnerName = state.players.find((p) => p.id === neighborState.ownerId)?.name;
      return { neighborRegion, neighborState, neighborOwnerName };
    })
    .filter((n): n is NonNullable<typeof n> => n !== null);

  return (
    <>
      <div className="flex flex-col gap-1.5">
        <SheetTitle>{region.name}</SheetTitle>
        <p className="text-sm text-muted-foreground">{regionState.soldierCount} asker</p>
        {/* docs/18-yeni-oyun-ici ui-gelistirme.md Bölüm 25: bkz. Hud.tsx'teki aynı kısaltma. */}
        <p className="text-xs text-muted-foreground">
          +{myProduction} / {gameConfig.productionIntervalSeconds}s
        </p>
      </div>

      <div className="flex flex-col gap-3 border-t border-border pt-3">
        <span className="text-xs font-medium text-muted-foreground">Komşu Bölgeler</span>
        <ul className="flex flex-col gap-1 text-sm">
          {neighbors.map(({ neighborRegion, neighborState, neighborOwnerName }) => (
            <li key={neighborRegion.id} className="flex items-center justify-between">
              <span>{neighborRegion.name}</span>
              <span className="flex items-center gap-3 text-muted-foreground">
                {neighborState.ownerId === myPlayerId ? (
                  <Badge variant="secondary">Sizin</Badge>
                ) : (
                  (neighborOwnerName ?? "Sahipsiz")
                )}{" "}
                · {neighborState.soldierCount} asker
              </span>
            </li>
          ))}
        </ul>
        <p className="text-xs text-muted-foreground">
          Asker göndermek için bu bölgeyi haritada başka bir bölgeye sürükleyip bırakın —
          hedefin komşu olması gerekmez.
        </p>
      </div>
    </>
  );
}
