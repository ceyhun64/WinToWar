"use client";

import { Suspense, useState } from "react";
import { useSearchParams } from "next/navigation";
import { Button } from "@/components/ui/button";
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
      <div className="rounded-md border border-border bg-card p-4 text-sm">
        Talebiniz alındı. En kısa sürede destek e-postanız üzerinden dönüş yapılacaktır.
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-4">
      {matchId ? (
        <p className="text-xs text-muted-foreground">Maç referansı: <span className="font-mono">{matchId}</span></p>
      ) : null}

      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-medium" htmlFor="subject">
          Konu
        </label>
        <input
          id="subject"
          className="h-9 rounded-md border border-input bg-background px-3 text-sm"
          value={subject}
          onChange={(e) => setSubject(e.target.value)}
        />
      </div>

      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-medium" htmlFor="contactEmail">
          E-posta
        </label>
        <input
          id="contactEmail"
          type="email"
          className="h-9 rounded-md border border-input bg-background px-3 text-sm"
          value={contactEmail}
          onChange={(e) => setContactEmail(e.target.value)}
        />
      </div>

      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-medium" htmlFor="description">
          Açıklama
        </label>
        <textarea
          id="description"
          rows={5}
          className="rounded-md border border-input bg-background px-3 py-2 text-sm"
          value={description}
          onChange={(e) => setDescription(e.target.value)}
        />
      </div>

      <Button disabled={busy} onClick={handleSubmit}>
        {busy ? "Gönderiliyor..." : "Gönder"}
      </Button>

      {error ? <p className="text-sm text-destructive">{error}</p> : null}
    </div>
  );
}

/** docs/07-pages.md `/destek`: iletişim formu + destek e-postası. */
export default function DestekPage() {
  return (
    <div className="mx-auto flex w-full max-w-sm flex-1 flex-col gap-6 px-4 py-8">
      <div>
        <h1 className="text-lg font-semibold">Destek</h1>
        <p className="text-sm text-muted-foreground">
          Sorularınız veya ödeme anlaşmazlıklarınız için bize ulaşın.
        </p>
      </div>
      <Suspense fallback={null}>
        <DestekForm />
      </Suspense>
    </div>
  );
}
