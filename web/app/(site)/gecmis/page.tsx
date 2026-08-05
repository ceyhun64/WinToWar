"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyTitle } from "@/components/ui/empty";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { getInvoiceHistory } from "@/lib/payments/api";
import type { PaymentInvoiceDto } from "@/lib/payments/types";
import { isSignedIn } from "@/lib/identity";

const STATUS_VARIANT: Record<string, "default" | "secondary" | "destructive" | "outline"> = {
  Pending: "outline",
  Confirmed: "default",
  Expired: "destructive",
  Refunded: "secondary",
  Failed: "destructive",
};

const STATUS_LABEL: Record<string, string> = {
  Pending: "Bekliyor",
  Confirmed: "Onaylandı",
  Expired: "Süresi Doldu",
  Refunded: "İade Edildi",
  Failed: "Başarısız",
};

/** docs/07-pages.md `/gecmis`: ödeme/maç geçmişi tablosu. */
export default function GecmisPage() {
  const router = useRouter();
  const [invoices, setInvoices] = useState<PaymentInvoiceDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!isSignedIn()) {
      router.replace("/giris");
      return;
    }
    getInvoiceHistory()
      .then(setInvoices)
      .catch((err) => setError(String(err)));
  }, [router]);

  return (
    <div className="mx-auto flex w-full max-w-2xl flex-1 flex-col gap-4 px-4 py-8">
      <h1 className="text-lg font-semibold">Geçmiş</h1>

      {error ? <p className="text-sm text-destructive">{error}</p> : null}

      {invoices === null ? (
        <p className="text-sm text-muted-foreground">Yükleniyor...</p>
      ) : invoices.length === 0 ? (
        <Empty className="p-6">
          <EmptyHeader>
            <EmptyTitle>Henüz bir işleminiz yok</EmptyTitle>
            <EmptyDescription>Bir odaya katılın veya bakiyenizi yükleyin.</EmptyDescription>
          </EmptyHeader>
          <EmptyContent>
            <Button render={<Link href="/lobi" />}>Lobiye Git</Button>
          </EmptyContent>
        </Empty>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Tarih</TableHead>
              <TableHead>Tür</TableHead>
              <TableHead className="text-right">Tutar (USD)</TableHead>
              <TableHead className="text-right">Durum</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {invoices.map((invoice) => (
              <TableRow key={invoice.invoiceId}>
                <TableCell className="text-xs text-muted-foreground">
                  {new Date(invoice.createdAt).toLocaleDateString("tr-TR")}
                </TableCell>
                <TableCell>{invoice.matchId ? "Maça Giriş" : "Para Yatırma"}</TableCell>
                <TableCell className="text-right tabular-nums">${invoice.amountUsd}</TableCell>
                <TableCell className="text-right">
                  <Badge variant={STATUS_VARIANT[invoice.status] ?? "outline"}>
                    {STATUS_LABEL[invoice.status] ?? invoice.status}
                  </Badge>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  );
}
