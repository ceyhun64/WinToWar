"use client";

import type { GameConfigDto, MapDto, MatchStateDto } from "@/lib/game/types";

interface ActionPanelProps {
  map: MapDto;
  state: MatchStateDto;
  myPlayerId: string;
  selectedRegionId: string | null;
  gameConfig: GameConfigDto;
}

/**
 * docs/04-style.md Bölüm 10 (state.io incelemesi sonrası): ActionPanel artık bir
 * aksiyon değil, bilgi panelidir — asker gönderme tamamen GameMap/RegionNode
 * içindeki sürükle-bırak etkileşimine taşındı (bkz. GameMap.tsx). Bu panel yalnızca
 * seçili bölgenin salt-okunur özetini gösterir.
 */
export function ActionPanel({ map, state, myPlayerId, selectedRegionId, gameConfig }: ActionPanelProps) {
  if (!selectedRegionId) {
    return (
      <div className="rounded-md border border-border bg-card px-4 py-6 text-center text-sm text-muted-foreground">
        Bilgi görmek için bir bölge seçin. Asker göndermek için kendi bölgenizi
        doğrudan komşu bir bölgeye sürükleyip bırakın.
      </div>
    );
  }

  const region = map.regions.find((r) => r.id === selectedRegionId);
  const regionState = state.regions.find((r) => r.id === selectedRegionId);

  if (!region || !regionState) {
    return null;
  }

  const isMine = regionState.ownerId === myPlayerId;
  const ownerName = state.players.find((p) => p.id === regionState.ownerId)?.name;

  if (!isMine) {
    return (
      <div className="flex flex-col gap-2 rounded-md border border-border bg-card px-4 py-4">
        <h3 className="text-sm font-semibold">{region.name}</h3>
        <p className="text-sm text-muted-foreground">
          {regionState.ownerId ? `Sahip: ${ownerName ?? "?"}` : "Sahipsiz bölge"}
        </p>
        <p className="text-sm text-muted-foreground">Savunma: {regionState.soldierCount} asker</p>
      </div>
    );
  }

  // docs/03-game-rules.md Bölüm 4 — bkz. Hud.tsx'teki aynı formülün gerekçesi.
  const myRegionCount = state.regions.filter((r) => r.ownerId === myPlayerId).length;
  const myProduction =
    gameConfig.baseProductionPerInterval + Math.max(0, myRegionCount - 1) * gameConfig.productionBonusPerRegion;

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
    <div className="flex flex-col gap-3 rounded-md border border-border bg-card px-4 py-4">
      <div>
        <h3 className="text-sm font-semibold">{region.name}</h3>
        <p className="text-sm text-muted-foreground">{regionState.soldierCount} asker</p>
        <p className="text-xs text-muted-foreground">Üretim: 10sn&apos;de {myProduction} asker</p>
      </div>

      <div className="flex flex-col gap-2 border-t border-border pt-3">
        <span className="text-xs font-medium text-muted-foreground">Komşu Bölgeler</span>
        <ul className="flex flex-col gap-1 text-sm">
          {neighbors.map(({ neighborRegion, neighborState, neighborOwnerName }) => (
            <li key={neighborRegion.id} className="flex items-center justify-between">
              <span>{neighborRegion.name}</span>
              <span className="text-muted-foreground">
                {neighborState.ownerId === myPlayerId
                  ? "Sizin"
                  : (neighborOwnerName ?? "Sahipsiz")}{" "}
                · {neighborState.soldierCount} asker
              </span>
            </li>
          ))}
        </ul>
        <p className="text-xs text-muted-foreground">
          Asker göndermek için bu bölgeyi haritada bir komşusuna sürükleyip bırakın.
        </p>
      </div>
    </div>
  );
}
