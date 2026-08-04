"use client";

import type { MapRegionDto, RegionStateDto } from "@/lib/game/types";

interface RegionNodeProps {
  region: MapRegionDto;
  regionState: RegionStateDto | undefined;
  color: string;
  isMine: boolean;
  isSelected: boolean;
  isAttackTarget: boolean;
  isDragSource: boolean;
  isDragHoverTarget: boolean;
  draggable: boolean;
  onClick: () => void;
  onDragStart: (e: React.PointerEvent) => void;
  onDragMove: (e: React.PointerEvent) => void;
  onDragEnd: (e: React.PointerEvent) => void;
}

const RADIUS = 26;

/**
 * docs/03-game-rules.md Bölüm 6/15, docs/04-style.md Bölüm 10 (state.io incelemesi):
 * asker gönderme artık ayrı bir input/slider yerine bu bölgenin üzerinde doğrudan
 * sürükle-bırak ile yapılır. Masaüstü fare ve mobil dokunmatik aynı pointer-event
 * mantığını kullanır, ayrı bir masaüstü/mobil bileşeni yazılmaz.
 */
export function RegionNode({
  region,
  regionState,
  color,
  isMine,
  isSelected,
  isAttackTarget,
  isDragSource,
  isDragHoverTarget,
  draggable,
  onClick,
  onDragStart,
  onDragMove,
  onDragEnd,
}: RegionNodeProps) {
  const isUnexplored = regionState?.isVisible === false;
  const garrison = regionState?.soldierCount ?? 0;

  return (
    <g
      role="button"
      tabIndex={0}
      onClick={onClick}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") onClick();
      }}
      onPointerDown={draggable ? onDragStart : undefined}
      onPointerMove={isDragSource ? onDragMove : undefined}
      onPointerUp={isDragSource ? onDragEnd : undefined}
      onPointerCancel={isDragSource ? onDragEnd : undefined}
      className={draggable ? "cursor-grab outline-none touch-none" : "cursor-pointer outline-none touch-none"}
    >
      <circle
        cx={region.x}
        cy={region.y}
        r={RADIUS}
        fill={color}
        opacity={isDragSource ? 0.6 : 1}
        stroke={
          isDragHoverTarget
            ? "#111827"
            : isSelected
              ? "#111827"
              : isAttackTarget
                ? "#111827"
                : "rgba(0,0,0,0.25)"
        }
        strokeWidth={isDragHoverTarget ? 4 : isSelected ? 3 : isAttackTarget ? 2 : 1}
        strokeDasharray={isAttackTarget && !isSelected && !isDragHoverTarget ? "4 3" : undefined}
      />
      {isUnexplored ? null : (
        <text x={region.x} y={region.y + 4} textAnchor="middle" fontSize="12" fontWeight={600} fill="#111827">
          {garrison}
        </text>
      )}
      <text
        x={region.x}
        y={region.y + RADIUS + 14}
        textAnchor="middle"
        fontSize="11"
        className="fill-foreground"
      >
        {region.name}
        {isMine ? " •" : ""}
      </text>
    </g>
  );
}
