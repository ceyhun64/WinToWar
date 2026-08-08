"use client";

import { useEffect, useState } from "react";
import { Activity, Server, Database } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { CardContent } from "@/components/ui/card";
import { PageHero } from "@/components/layout/PageHero";
import { GameCard } from "@/components/layout/GameCard";
import { API_BASE_URL } from "@/lib/game/api";

type ComponentStatus = "checking" | "up" | "down";

function StatusRow({ icon: Icon, label, status }: { icon: typeof Server; label: string; status: ComponentStatus }) {
  const variant = status === "up" ? "default" : status === "down" ? "destructive" : "outline";
  const text = status === "up" ? "Çalışıyor" : status === "down" ? "Kesinti" : "Kontrol ediliyor...";
  return (
    <GameCard size="sm">
      <CardContent className="flex items-center justify-between">
        <span className="flex items-center gap-2.5 text-sm font-medium">
          <Icon className="size-4 text-muted-foreground" aria-hidden="true" />
          {label}
        </span>
        <Badge variant={variant}>{text}</Badge>
      </CardContent>
    </GameCard>
  );
}

/** docs/07-pages.md `/durum`: her bileşen için basit health-check okuması — karmaşık monitoring kurulmaz (YAGNI). */
export default function DurumPage() {
  const [api, setApi] = useState<ComponentStatus>("checking");
  const [database, setDatabase] = useState<ComponentStatus>("checking");

  useEffect(() => {
    fetch(`${API_BASE_URL}/api/health`)
      .then(async (res) => {
        if (!res.ok) throw new Error();
        const body = (await res.json()) as { api: boolean; database: boolean };
        setApi(body.api ? "up" : "down");
        setDatabase(body.database ? "up" : "down");
      })
      .catch(() => {
        setApi("down");
        setDatabase("down");
      });
  }, []);

  return (
    <div className="mx-auto flex w-full max-w-sm flex-1 flex-col gap-6 px-4 py-8">
      <PageHero icon={Activity} title="Sistem Durumu" />
      <div className="flex flex-col gap-3">
        <StatusRow icon={Server} label="API" status={api} />
        <StatusRow icon={Database} label="Veritabanı" status={database} />
      </div>
    </div>
  );
}
