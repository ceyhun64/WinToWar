"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { History, ScrollText } from "lucide-react";
import { Button } from "@/components/ui/button";
import { CardContent } from "@/components/ui/card";
import { Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@/components/ui/empty";
import { PageHero } from "@/components/layout/PageHero";
import { GameCard } from "@/components/layout/GameCard";
import { InvoiceRow } from "@/components/payments/InvoiceRow";
import { getInvoiceHistory } from "@/lib/payments/api";
import type { PaymentInvoiceDto } from "@/lib/payments/types";
import { AuthGuard } from "@/components/layout/AuthGuard";

/**
 * docs/07-pages.md `/gecmis`: ödeme/maç geçmişi tablosu.
 * `docs/04-style.md` Landing İstisnası — sitede genelleştirilmiş tasarım
 * sistemi: satırlar `/cuzdan`'daki "Son İşlemler" ile aynı `InvoiceRow`
 * bileşenini paylaşır (kod tekrarı yok, aynı ikon/renk dili). Not: burada
 * gösterilen veri ödeme/fatura geçmişidir — kazanan/kaybeden, harita, süre
 * gibi "maç sonucu" alanları `PaymentInvoiceDto`'da yok (bu bilgi
 * `/mac/[matchId]`'de), bu yüzden burada uydurulmadı.
 */
export default function GecmisPage() {
  return (
    <AuthGuard>
      <GecmisPageContent />
    </AuthGuard>
  );
}

function GecmisPageContent() {
  const [invoices, setInvoices] = useState<PaymentInvoiceDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getInvoiceHistory()
      .then(setInvoices)
      .catch((err) => setError(String(err)));
  }, []);

  return (
    <div className="mx-auto flex w-full max-w-2xl flex-1 flex-col gap-8 px-4 py-8 md:py-10">
      <PageHero icon={History} title="Geçmiş" subtitle="Ödeme ve maç giriş kayıtların." />

      {error ? <p className="text-sm text-destructive">{error}</p> : null}

      {invoices === null ? (
        <p className="text-sm text-muted-foreground">Yükleniyor...</p>
      ) : invoices.length === 0 ? (
        <GameCard className="p-2">
          <Empty className="p-6">
            <EmptyHeader>
              <EmptyMedia variant="icon">
                <ScrollText aria-hidden="true" />
              </EmptyMedia>
              <EmptyTitle>Henüz bir işleminiz yok</EmptyTitle>
              <EmptyDescription>Bir odaya katılın veya bakiyenizi yükleyin.</EmptyDescription>
            </EmptyHeader>
            <EmptyContent>
              <Button render={<Link href="/lobi" />}>Lobiye Git</Button>
            </EmptyContent>
          </Empty>
        </GameCard>
      ) : (
        <GameCard className="p-2">
          <CardContent className="flex flex-col gap-1 px-2">
            {invoices.map((invoice) => (
              <InvoiceRow key={invoice.invoiceId} invoice={invoice} />
            ))}
          </CardContent>
        </GameCard>
      )}
    </div>
  );
}
