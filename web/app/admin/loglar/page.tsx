"use client";

import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { getAdminLogs, type AdminLogEntry } from "@/lib/admin/api";

/** docs/07-pages.md `/admin/loglar`: mevcut log altyapısının filtrelenebilir okuma ekranı — yeni bir loglama sistemi kurulmaz (YAGNI). */
export default function AdminLoglarPage() {
  const [logs, setLogs] = useState<AdminLogEntry[]>([]);
  const [level, setLevel] = useState("");
  const [search, setSearch] = useState("");
  const [error, setError] = useState<string | null>(null);

  function refresh() {
    getAdminLogs(level || undefined, search || undefined)
      .then(setLogs)
      .catch((err) => setError(String(err)));
  }

  useEffect(() => {
    refresh();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-lg font-semibold">Loglar</h1>

      <div className="flex flex-wrap gap-3">
        <NativeSelect size="sm" value={level} onChange={(e) => setLevel(e.target.value)}>
          <NativeSelectOption value="">Tüm seviyeler</NativeSelectOption>
          <NativeSelectOption value="Information">Bilgi</NativeSelectOption>
          <NativeSelectOption value="Warning">Uyarı</NativeSelectOption>
          <NativeSelectOption value="Error">Hata</NativeSelectOption>
          <NativeSelectOption value="Critical">Kritik</NativeSelectOption>
        </NativeSelect>
        <Input
          className="h-7 w-auto"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Ara..."
        />
        <Button size="sm" onClick={refresh}>
          Filtrele
        </Button>
      </div>

      {error ? <p className="text-sm text-destructive">{error}</p> : null}

      <Card>
        <CardContent className="flex flex-col gap-1 overflow-x-auto font-mono text-xs">
          {logs.length === 0 ? (
            <p className="text-muted-foreground">Kayıt yok.</p>
          ) : (
            logs.map((log, i) => (
              <div key={i} className="whitespace-nowrap">
                <span className="text-muted-foreground">{new Date(log.timestampUtc).toLocaleTimeString("tr-TR")}</span>{" "}
                <span className={log.level === "Error" || log.level === "Critical" ? "text-destructive" : ""}>[{log.level}]</span>{" "}
                <span className="text-muted-foreground">{log.category}</span> — {log.message}
              </div>
            ))
          )}
        </CardContent>
      </Card>
    </div>
  );
}
