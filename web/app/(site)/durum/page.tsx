"use client";

import { useEffect, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { API_BASE_URL } from "@/lib/game/api";

type ComponentStatus = "checking" | "up" | "down";

function StatusRow({ label, status }: { label: string; status: ComponentStatus }) {
  const variant = status === "up" ? "default" : status === "down" ? "destructive" : "outline";
  const text = status === "up" ? "Çalışıyor" : status === "down" ? "Kesinti" : "Kontrol ediliyor...";
  return (
    <Card size="sm">
      <CardContent className="flex items-center justify-between">
        <span className="text-sm font-medium">{label}</span>
        <Badge variant={variant}>{text}</Badge>
      </CardContent>
    </Card>
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
    <div className="mx-auto flex w-full max-w-sm flex-1 flex-col gap-4 px-4 py-8">
      <h1 className="text-lg font-semibold">Sistem Durumu</h1>
      <div className="flex flex-col gap-2">
        <StatusRow label="API" status={api} />
        <StatusRow label="Veritabanı" status={database} />
      </div>
    </div>
  );
}
