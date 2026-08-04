"use client";

import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
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

      <div className="flex flex-wrap gap-2">
        <select
          className="h-8 rounded-md border border-input bg-background px-2 text-sm"
          value={level}
          onChange={(e) => setLevel(e.target.value)}
        >
          <option value="">Tüm seviyeler</option>
          <option value="Information">Bilgi</option>
          <option value="Warning">Uyarı</option>
          <option value="Error">Hata</option>
          <option value="Critical">Kritik</option>
        </select>
        <input
          className="h-8 rounded-md border border-input bg-background px-2 text-sm"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Ara..."
        />
        <Button size="sm" onClick={refresh}>
          Filtrele
        </Button>
      </div>

      {error ? <p className="text-sm text-destructive">{error}</p> : null}

      <div className="flex flex-col gap-1 overflow-x-auto rounded-md border border-border bg-card p-2 font-mono text-xs">
        {logs.length === 0 ? (
          <p className="p-2 text-muted-foreground">Kayıt yok.</p>
        ) : (
          logs.map((log, i) => (
            <div key={i} className="whitespace-nowrap">
              <span className="text-muted-foreground">{new Date(log.timestampUtc).toLocaleTimeString("tr-TR")}</span>{" "}
              <span className={log.level === "Error" || log.level === "Critical" ? "text-destructive" : ""}>[{log.level}]</span>{" "}
              <span className="text-muted-foreground">{log.category}</span> — {log.message}
            </div>
          ))
        )}
      </div>
    </div>
  );
}
