"use client";

import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { NumberInput } from "@/components/ui/number-input";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";

interface DemoRegion {
  id: string;
  name: string;
  owner: "player" | "neutral";
  soldierCount: number;
}

const INITIAL_REGIONS: DemoRegion[] = [
  { id: "home", name: "Kalen", owner: "player", soldierCount: 6 },
  { id: "b", name: "Bölge B", owner: "neutral", soldierCount: 1 },
  { id: "c", name: "Bölge C", owner: "neutral", soldierCount: 1 },
];

/**
 * docs/03-game-rules.md Bölüm 7 "botsuz tanıtım akışı": tek oyunculu, sabit
 * senaryolu, gerçek bir maça hiç bağlanmayan interaktif bir gösterim — bir
 * Match/Room/PaymentInvoice akışına hiç girmez, "bot" kuralını ihlal etmez.
 * Çözümleme mantığı, docs Bölüm 5'teki "+2 Asker Kuralı" ile birebir aynıdır
 * (sabit gri savunma = 1): A >= d + 1 ise ele geçirilir, 1 asker garrison kalır.
 */
export function PracticeDemo() {
  const [regions, setRegions] = useState<DemoRegion[]>(INITIAL_REGIONS);
  const [soldierCount, setSoldierCount] = useState(2);
  const [target, setTarget] = useState<"b" | "c">("b");
  const [moving, setMoving] = useState(false);
  const [log, setLog] = useState<string[]>([]);

  const home = regions.find((r) => r.id === "home")!;

  function reset() {
    setRegions(INITIAL_REGIONS);
    setLog([]);
    setMoving(false);
  }

  function sendAttack() {
    if (soldierCount < 1 || soldierCount > home.soldierCount || moving) {
      return;
    }

    setMoving(true);
    setLog((prev) => [...prev, `${soldierCount} asker ${target === "b" ? "Bölge B" : "Bölge C"}'ye yola çıktı...`]);

    window.setTimeout(() => {
      setRegions((prev) => {
        const targetRegion = prev.find((r) => r.id === target)!;
        const defense = targetRegion.soldierCount;
        const captured = soldierCount >= defense + 1;

        setLog((current) => [
          ...current,
          captured
            ? `Savunma (${defense}) kırıldı, bölge ele geçirildi! ${soldierCount - defense} asker garnizon oldu.`
            : `Saldırı püskürtüldü — gönderilen ${soldierCount} asker kaybedildi, savunma ${Math.max(0, defense - soldierCount)}'e düştü.`,
        ]);

        return prev.map((r) => {
          if (r.id === "home") {
            return { ...r, soldierCount: r.soldierCount - soldierCount };
          }
          if (r.id === target) {
            return captured
              ? { ...r, owner: "player", soldierCount: soldierCount - defense }
              : { ...r, soldierCount: Math.max(0, defense - soldierCount) };
          }
          return r;
        });
      });
      setMoving(false);
    }, 1200);
  }

  return (
    <Card>
      <CardContent className="flex flex-col gap-4">
      <div className="grid grid-cols-3 gap-3 text-center text-sm">
        {regions.map((region) => (
          <div
            key={region.id}
            className="flex flex-col items-center gap-1 rounded-md border border-border p-3"
            style={{ backgroundColor: region.owner === "player" ? "#7C93B322" : "#D8D2C4" }}
          >
            <span className="font-medium">{region.name}</span>
            <Badge variant={region.owner === "player" ? "default" : "outline"}>
              {region.owner === "player" ? "Sizin" : "Nötr"}
            </Badge>
            <span className="text-xs text-muted-foreground">{region.soldierCount} asker</span>
          </div>
        ))}
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <NativeSelect
          value={target}
          onChange={(e) => setTarget(e.target.value as "b" | "c")}
          disabled={moving}
        >
          <NativeSelectOption value="b">Bölge B&apos;ye saldır</NativeSelectOption>
          <NativeSelectOption value="c">Bölge C&apos;ye saldır</NativeSelectOption>
        </NativeSelect>
        <NumberInput
          min={1}
          max={home.soldierCount}
          value={soldierCount}
          onValueChange={(value) => setSoldierCount((prev) => value ?? prev)}
          className="w-24"
          disabled={moving}
        />
        <Button size="sm" disabled={moving || home.soldierCount === 0} onClick={sendAttack}>
          Gönder
        </Button>
        <Button size="sm" variant="ghost" onClick={reset}>
          Sıfırla
        </Button>
      </div>

      {log.length > 0 ? (
        <div className="flex flex-col gap-1 rounded-md border border-border bg-background p-3 text-xs text-muted-foreground">
          {log.map((line, i) => (
            <span key={i}>{line}</span>
          ))}
        </div>
      ) : (
        <p className="text-xs text-muted-foreground">
          İpucu: sabit savunması 1 olan bir bölgeyi almak için en az 2 asker gerekir (1&apos;i savunmayı
          kırar, 1&apos;i garnizon kalır).
        </p>
      )}
      </CardContent>
    </Card>
  );
}
