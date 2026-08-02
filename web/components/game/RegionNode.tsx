"use client";

import type { MapRegionDto, RegionStateDto } from "@/lib/game/types";

interface RegionNodeProps {
  region: MapRegionDto;
  regionState: RegionStateDto | undefined;
  color: string;
  isMine: boolean;
  isSelected: boolean;
  isAttackTarget: boolean;
  onClick: () => void;
}

const RADIUS = 26;

export function RegionNode({
  region,
  regionState,
  color,
  isMine,
  isSelected,
  isAttackTarget,
  onClick,
}: RegionNodeProps) {
  const garrison = regionState?.ownerId ? regionState.garrisonSoldiers : regionState?.neutralDefenseSoldiers ?? 0;
  const archers = regionState?.garrisonArchers ?? 0;

  return (
    <g
      role="button"
      tabIndex={0}
      onClick={onClick}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") onClick();
      }}
      className="cursor-pointer outline-none"
    >
      <circle
        cx={region.x}
        cy={region.y}
        r={RADIUS}
        fill={color}
        stroke={isSelected ? "#111827" : isAttackTarget ? "#111827" : "rgba(0,0,0,0.25)"}
        strokeWidth={isSelected ? 3 : isAttackTarget ? 2 : 1}
        strokeDasharray={isAttackTarget && !isSelected ? "4 3" : undefined}
      />
      {regionState?.nestLevel ? (
        <text
          x={region.x}
          y={region.y - 2}
          textAnchor="middle"
          fontSize="11"
          fontWeight={600}
          fill="#ffffff"
        >
          L{regionState.nestLevel}
        </text>
      ) : null}
      <text x={region.x} y={region.y + 10} textAnchor="middle" fontSize="10" fill="#ffffff">
        {garrison}
        {archers > 0 ? ` +${archers}A` : ""}
      </text>
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
