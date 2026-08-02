"use client";

import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import type { GameConfigDto, MapDto, MatchStateDto } from "@/lib/game/types";

interface ActionPanelProps {
  map: MapDto;
  state: MatchStateDto;
  config: GameConfigDto;
  myPlayerId: string;
  selectedRegionId: string | null;
  onTrainSoldier: (regionId: string) => void;
  onTrainGeneral: (regionId: string) => void;
  onUpgradeNest: (regionId: string) => void;
  onAttack: (fromRegionId: string, toRegionId: string, generalId: string, soldierCount: number) => void;
}

export function ActionPanel({
  map,
  state,
  config,
  myPlayerId,
  selectedRegionId,
  onTrainSoldier,
  onTrainGeneral,
  onUpgradeNest,
  onAttack,
}: ActionPanelProps) {
  const [attackTargetId, setAttackTargetId] = useState<string>("");
  const [soldierCount, setSoldierCount] = useState(1);

  useEffect(() => {
    setAttackTargetId("");
    setSoldierCount(1);
  }, [selectedRegionId]);

  if (!selectedRegionId) {
    return (
      <div className="rounded-md border border-border bg-card px-4 py-6 text-center text-sm text-muted-foreground">
        Aksiyon almak için haritadan bir bölge seçin.
      </div>
    );
  }

  const region = map.regions.find((r) => r.id === selectedRegionId);
  const regionState = state.regions.find((r) => r.id === selectedRegionId);
  const myPlayer = state.players.find((p) => p.id === myPlayerId);

  if (!region || !regionState || !myPlayer) {
    return null;
  }

  const isMine = regionState.ownerId === myPlayerId;
  const ownerName = state.players.find((p) => p.id === regionState.ownerId)?.name;

  if (!isMine) {
    return (
      <div className="flex flex-col gap-2 rounded-md border border-border bg-card px-4 py-4">
        <h3 className="text-sm font-semibold">{region.name}</h3>
        <p className="text-sm text-muted-foreground">
          {regionState.ownerId ? `Sahip: ${ownerName ?? "?"}` : "Nötr bölge"}
          {regionState.nestLevel ? ` · Kale seviye ${regionState.nestLevel}` : ""}
        </p>
        <p className="text-sm text-muted-foreground">
          Garnizon: {regionState.ownerId ? regionState.garrisonSoldiers : regionState.neutralDefenseSoldiers} asker
          {regionState.garrisonArchers > 0 ? `, ${regionState.garrisonArchers} okçu` : ""}
        </p>
        <p className="text-xs text-muted-foreground">Bu bölge size ait değil.</p>
      </div>
    );
  }

  const nestLevel = regionState.nestLevel ?? 1;
  const canUpgrade = nestLevel < config.maxNestLevel;
  const upgradeCost = nestLevel === 1 ? config.nestUpgradeToLevel2Cost : config.nestUpgradeToLevel3Cost;
  const aliveGenerals = state.generals.filter((g) => g.ownerId === myPlayerId && g.status !== "Dead").length;
  const generalHere = state.generals.find(
    (g) => g.ownerId === myPlayerId && g.status === "Garrisoned" && g.currentRegionId === selectedRegionId
  );

  const canAttack = Boolean(
    generalHere && attackTargetId && soldierCount >= 1 && soldierCount <= regionState.garrisonSoldiers
  );

  return (
    <div className="flex flex-col gap-3 rounded-md border border-border bg-card px-4 py-4">
      <div>
        <h3 className="text-sm font-semibold">{region.name}</h3>
        <p className="text-sm text-muted-foreground">
          Kale seviye {nestLevel} · {regionState.garrisonSoldiers} asker
          {regionState.garrisonArchers > 0 ? `, ${regionState.garrisonArchers} okçu` : ""}
        </p>
      </div>

      <div className="flex flex-wrap gap-2">
        <Button
          size="sm"
          variant="secondary"
          disabled={myPlayer.gold < config.soldierCost}
          onClick={() => onTrainSoldier(selectedRegionId)}
        >
          Asker Üret ({config.soldierCost})
        </Button>
        <Button
          size="sm"
          variant="secondary"
          disabled={myPlayer.gold < config.generalCost || aliveGenerals >= config.maxGeneralsPerPlayer}
          onClick={() => onTrainGeneral(selectedRegionId)}
        >
          General Üret ({config.generalCost})
        </Button>
        {canUpgrade ? (
          <Button
            size="sm"
            variant="outline"
            disabled={myPlayer.gold < upgradeCost}
            onClick={() => onUpgradeNest(selectedRegionId)}
          >
            Yükselt ({upgradeCost})
          </Button>
        ) : null}
      </div>

      {generalHere ? (
        <div className="flex flex-col gap-2 border-t border-border pt-3">
          <span className="text-xs font-medium text-muted-foreground">Saldırıya çık</span>
          <div className="flex flex-wrap items-center gap-2">
            <select
              className="h-8 rounded-md border border-input bg-background px-2 text-sm"
              value={attackTargetId}
              onChange={(e) => setAttackTargetId(e.target.value)}
            >
              <option value="">Hedef bölge seçin</option>
              {region.neighborIds.map((neighborId) => {
                const neighbor = map.regions.find((r) => r.id === neighborId);
                return (
                  <option key={neighborId} value={neighborId}>
                    {neighbor?.name ?? neighborId}
                  </option>
                );
              })}
            </select>
            <input
              type="number"
              min={1}
              max={regionState.garrisonSoldiers}
              value={soldierCount}
              onChange={(e) => setSoldierCount(Number(e.target.value))}
              className="h-8 w-20 rounded-md border border-input bg-background px-2 text-sm"
            />
            <Button
              size="sm"
              disabled={!canAttack}
              onClick={() => onAttack(selectedRegionId, attackTargetId, generalHere.id, soldierCount)}
            >
              Saldır
            </Button>
          </div>
        </div>
      ) : (
        <p className="text-xs text-muted-foreground">
          Saldırıya çıkmak için bu bölgede hazır (garnizonlu) bir General gerekir.
        </p>
      )}
    </div>
  );
}
