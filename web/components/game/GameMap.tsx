"use client";

import { useMemo } from "react";
import { colorForSlot, NEUTRAL_COLOR } from "@/lib/game/colors";
import type { MapDto, MapRegionDto, MatchStateDto } from "@/lib/game/types";
import { RegionNode } from "./RegionNode";

interface GameMapProps {
  map: MapDto;
  state: MatchStateDto;
  myPlayerId: string;
  selectedRegionId: string | null;
  onSelectRegion: (regionId: string) => void;
}

const VIEW_WIDTH = 610;
const VIEW_HEIGHT = 660;

export function GameMap({ map, state, myPlayerId, selectedRegionId, onSelectRegion }: GameMapProps) {
  const slotByPlayerId = useMemo(() => {
    const result = new Map<string, number>();
    state.players.forEach((p) => result.set(p.id, p.slot));
    return result;
  }, [state.players]);

  const regionStateById = useMemo(() => {
    return new Map(state.regions.map((r) => [r.id, r]));
  }, [state.regions]);

  const edges = useMemo(() => {
    const seen = new Set<string>();
    const result: { from: MapRegionDto; to: MapRegionDto }[] = [];
    for (const region of map.regions) {
      for (const neighborId of region.neighborIds) {
        const key = [region.id, neighborId].sort().join("::");
        if (seen.has(key)) continue;
        seen.add(key);
        const neighbor = map.regions.find((r) => r.id === neighborId);
        if (neighbor) {
          result.push({ from: region, to: neighbor });
        }
      }
    }
    return result;
  }, [map.regions]);

  const selectedRegion = selectedRegionId ? regionStateById.get(selectedRegionId) : undefined;
  const selectedIsMine = selectedRegion?.ownerId === myPlayerId;
  const attackTargets = useMemo(() => {
    if (!selectedRegionId || !selectedIsMine) return new Set<string>();
    const region = map.regions.find((r) => r.id === selectedRegionId);
    return new Set(region?.neighborIds ?? []);
  }, [selectedRegionId, selectedIsMine, map.regions]);

  return (
    <svg
      viewBox={`0 0 ${VIEW_WIDTH} ${VIEW_HEIGHT}`}
      className="w-full h-auto max-h-[75vh] rounded-md border border-border bg-card"
      role="img"
      aria-label="Lüksemburg haritası"
    >
      <g>
        {edges.map(({ from, to }) => (
          <line
            key={`${from.id}-${to.id}`}
            x1={from.x}
            y1={from.y}
            x2={to.x}
            y2={to.y}
            stroke="var(--border)"
            strokeWidth={2}
          />
        ))}
      </g>
      <g>
        {map.regions.map((region) => {
          const regionState = regionStateById.get(region.id);
          const color = regionState?.ownerId
            ? colorForSlot(slotByPlayerId.get(regionState.ownerId) ?? 0)
            : NEUTRAL_COLOR;
          return (
            <RegionNode
              key={region.id}
              region={region}
              regionState={regionState}
              color={color}
              isMine={regionState?.ownerId === myPlayerId}
              isSelected={selectedRegionId === region.id}
              isAttackTarget={attackTargets.has(region.id)}
              onClick={() => onSelectRegion(region.id)}
            />
          );
        })}
      </g>
    </svg>
  );
}
