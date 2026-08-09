"use client";

import { Suspense, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { LifeBuoy } from "lucide-react";
import { Button } from "@/components/ui/button";
import { CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { PageHero } from "@/components/layout/PageHero";
import { GameCard } from "@/components/layout/GameCard";
import { API_BASE_URL } from "@/lib/game/api";

function DestekForm() {
  const searchParams = useSearchParams();
  const matchId = searchParams.get("matchId");

  const [subject, setSubject] = useState(matchId ? "Maç itirazı" : "");
  const [description, setDescription] = useState("");
  const [contactEmail, setContactEmail] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sent, setSent] = useState(false);

  async function handleSubmit() {
    if (!subject.trim() || !description.trim() || !contactEmail.trim()) {
      setError("Konu, açıklama ve e-posta gereklidir.");
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const res = await fetch(`${API_BASE_URL}/api/support-tickets`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ subject: subject.trim(), description: description.trim(), contactEmail: contactEmail.trim(), matchId }),
      });
      if (!res.ok) {
        throw new Error(`İstek başarısız oldu (${res.status})`);
      }
      setSent(true);
    } catch (err) {
      setError(String(err));
    } finally {
      setBusy(false);
    }
  }

  if (sent) {
    return (
      <GameCard className="flex-1 min-h-0">
        <CardContent className="h-full overflow-y-auto text-sm">
          Talebiniz alındı. En kısa sürede destek e-postanız üzerinden dönüş yapılacaktır.
        </CardContent>
      </GameCard>
    );
  }

  return (
    <GameCard className="flex-1 min-h-0">
    <CardContent className="flex h-full flex-col gap-4 overflow-y-auto">
      {matchId ? (
        <p className="text-xs text-muted-foreground">
          Maç referansı:{" "}
          <Link href={`/mac/${matchId}`} className="font-mono underline">
            {matchId}
          </Link>
        </p>
      ) : null}

      <div className="flex flex-col gap-3">
        <Label htmlFor="subject">Konu</Label>
        <Input id="subject" value={subject} onChange={(e) => setSubject(e.target.value)} />
      </div>

      <div className="flex flex-col gap-3">
        <Label htmlFor="contactEmail">E-posta</Label>
        <Input
          id="contactEmail"
          type="email"
          value={contactEmail}
          onChange={(e) => setContactEmail(e.target.value)}
        />
      </div>

      <div className="flex flex-col gap-3">
        <Label htmlFor="description">Açıklama</Label>
        <Textarea
          id="description"
          rows={5}
          value={description}
          onChange={(e) => setDescription(e.target.value)}
        />
      </div>

      <Button disabled={busy} onClick={handleSubmit}>
        {busy ? "Gönderiliyor..." : "Gönder"}
      </Button>

      {error ? <p className="text-sm text-destructive">{error}</p> : null}
    </CardContent>
    </GameCard>
  );
}

/** docs/07-pages.md `/destek`: iletişim formu + destek e-postası. */
export default function DestekPage() {
  return (
    <div className="mx-auto flex w-full min-h-0 max-w-sm flex-1 flex-col gap-6 px-4 py-8">
      <PageHero icon={LifeBuoy} title="Destek" subtitle="Sorularınız veya ödeme anlaşmazlıklarınız için bize ulaşın." />
      <Suspense fallback={null}>
        <DestekForm />
      </Suspense>
    </div>
  );
}
