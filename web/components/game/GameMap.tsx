"use client";

import { useMemo, useRef, useState } from "react";
import { colorForSlot, NEUTRAL_COLOR, UNEXPLORED_COLOR } from "@/lib/game/colors";
import type { MapDto, MapRegionDto, MatchStateDto } from "@/lib/game/types";
import { RegionNode } from "./RegionNode";

interface GameMapProps {
  map: MapDto;
  state: MatchStateDto;
  myPlayerId: string;
  selectedRegionId: string | null;
  onSelectRegion: (regionId: string) => void;
  onAttack: (fromRegionId: string, toRegionId: string) => void;
}

const VIEW_WIDTH = 610;
const VIEW_HEIGHT = 660;
const REGION_HIT_RADIUS = 26;

// docs/03-game-rules.md Bölüm 6/12: GameConfig.MinGarrisonPerSend — sürüklerken
// geride her zaman en az bu kadar asker kalır, gönderilecek asker yoksa (<=0)
// bölge sürüklenemez.
const MIN_GARRISON_PER_SEND = 1;

export function GameMap({ map, state, myPlayerId, selectedRegionId, onSelectRegion, onAttack }: GameMapProps) {
  const svgRef = useRef<SVGSVGElement>(null);
  const [dragFromRegionId, setDragFromRegionId] = useState<string | null>(null);
  const [dragPointerSvg, setDragPointerSvg] = useState<{ x: number; y: number } | null>(null);
  const [dragHoverTargetId, setDragHoverTargetId] = useState<string | null>(null);

  const slotByPlayerId = useMemo(() => {
    const result = new Map<string, number>();
    state.players.forEach((p) => result.set(p.id, p.slot));
    return result;
  }, [state.players]);

  const regionStateById = useMemo(() => {
    return new Map(state.regions.map((r) => [r.id, r]));
  }, [state.regions]);

  const regionById = useMemo(() => {
    return new Map(map.regions.map((r) => [r.id, r]));
  }, [map.regions]);

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

  function toSvgPoint(clientX: number, clientY: number): { x: number; y: number } | null {
    const svg = svgRef.current;
    if (!svg) return null;
    const ctm = svg.getScreenCTM();
    if (!ctm) return null;
    const point = svg.createSVGPoint();
    point.x = clientX;
    point.y = clientY;
    const transformed = point.matrixTransform(ctm.inverse());
    return { x: transformed.x, y: transformed.y };
  }

  function findRegionAtPoint(point: { x: number; y: number }): string | null {
    for (const region of map.regions) {
      const dx = region.x - point.x;
      const dy = region.y - point.y;
      if (Math.sqrt(dx * dx + dy * dy) <= REGION_HIT_RADIUS) {
        return region.id;
      }
    }
    return null;
  }

  function handleDragStart(regionId: string) {
    return (e: React.PointerEvent) => {
      const regionState = regionStateById.get(regionId);
      const isMine = regionState?.ownerId === myPlayerId;
      if (state.status !== "Playing" || !isMine || (regionState?.soldierCount ?? 0) <= MIN_GARRISON_PER_SEND) {
        return;
      }
      e.currentTarget.setPointerCapture(e.pointerId);
      setDragFromRegionId(regionId);
      const point = toSvgPoint(e.clientX, e.clientY);
      setDragPointerSvg(point);
      setDragHoverTargetId(null);
    };
  }

  function handleDragMove(e: React.PointerEvent) {
    if (!dragFromRegionId) return;
    const point = toSvgPoint(e.clientX, e.clientY);
    if (!point) return;
    setDragPointerSvg(point);
    const hoverId = findRegionAtPoint(point);
    const fromRegion = regionById.get(dragFromRegionId);
    const isValidNeighborHover =
      hoverId !== null && hoverId !== dragFromRegionId && (fromRegion?.neighborIds.includes(hoverId) ?? false);
    setDragHoverTargetId(isValidNeighborHover ? hoverId : null);
  }

  function handleDragEnd(e: React.PointerEvent) {
    if (!dragFromRegionId) return;
    const point = toSvgPoint(e.clientX, e.clientY);
    const hoverId = point ? findRegionAtPoint(point) : null;
    const fromRegion = regionById.get(dragFromRegionId);
    if (hoverId && hoverId !== dragFromRegionId && fromRegion?.neighborIds.includes(hoverId)) {
      onAttack(dragFromRegionId, hoverId);
    }
    setDragFromRegionId(null);
    setDragPointerSvg(null);
    setDragHoverTargetId(null);
  }

  const dragFromRegion = dragFromRegionId ? regionById.get(dragFromRegionId) : undefined;

  return (
    <svg
      ref={svgRef}
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
      {dragFromRegion && dragPointerSvg ? (
        <line
          x1={dragFromRegion.x}
          y1={dragFromRegion.y}
          x2={dragPointerSvg.x}
          y2={dragPointerSvg.y}
          stroke="#111827"
          strokeWidth={2}
          strokeDasharray="4 3"
        />
      ) : null}
      <g>
        {map.regions.map((region) => {
          const regionState = regionStateById.get(region.id);
          const isMine = regionState?.ownerId === myPlayerId;
          const color =
            regionState?.isVisible === false
              ? UNEXPLORED_COLOR
              : regionState?.ownerId
                ? colorForSlot(slotByPlayerId.get(regionState.ownerId) ?? 0)
                : NEUTRAL_COLOR;
          const draggable =
            isMine && state.status === "Playing" && (regionState?.soldierCount ?? 0) > MIN_GARRISON_PER_SEND;
          return (
            <RegionNode
              key={region.id}
              region={region}
              regionState={regionState}
              color={color}
              isMine={isMine}
              isSelected={selectedRegionId === region.id}
              isAttackTarget={attackTargets.has(region.id)}
              isDragSource={dragFromRegionId === region.id}
              isDragHoverTarget={dragHoverTargetId === region.id}
              draggable={draggable}
              onClick={() => onSelectRegion(region.id)}
              onDragStart={handleDragStart(region.id)}
              onDragMove={handleDragMove}
              onDragEnd={handleDragEnd}
            />
          );
        })}
      </g>
    </svg>
  );
}
