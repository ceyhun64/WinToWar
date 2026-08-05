"use client";

import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
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
      <Card>
        <CardContent className="py-6 text-center text-sm text-muted-foreground">
          Bilgi görmek için bir bölge seçin. Asker göndermek için kendi bölgenizi
          doğrudan komşu bir bölgeye sürükleyip bırakın.
        </CardContent>
      </Card>
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
      <Card>
        <CardHeader>
          <CardTitle>{region.name}</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-1">
          <p className="text-sm text-muted-foreground">
            {regionState.ownerId ? `Sahip: ${ownerName ?? "?"}` : "Sahipsiz bölge"}
          </p>
          <p className="text-sm text-muted-foreground">Savunma: {regionState.soldierCount} asker</p>
        </CardContent>
      </Card>
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
    <Card>
      <CardHeader>
        <CardTitle>{region.name}</CardTitle>
        <p className="text-sm text-muted-foreground">{regionState.soldierCount} asker</p>
        <p className="text-xs text-muted-foreground">Üretim: 10sn&apos;de {myProduction} asker</p>
      </CardHeader>

      <CardContent className="flex flex-col gap-2 border-t border-border pt-3">
        <span className="text-xs font-medium text-muted-foreground">Komşu Bölgeler</span>
        <ul className="flex flex-col gap-1 text-sm">
          {neighbors.map(({ neighborRegion, neighborState, neighborOwnerName }) => (
            <li key={neighborRegion.id} className="flex items-center justify-between">
              <span>{neighborRegion.name}</span>
              <span className="flex items-center gap-1.5 text-muted-foreground">
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
          Asker göndermek için bu bölgeyi haritada bir komşusuna sürükleyip bırakın.
        </p>
      </CardContent>
    </Card>
  );
}
