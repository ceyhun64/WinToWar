"use client";

import { use, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { PaymentPanel } from "@/components/payments/PaymentPanel";

interface PayPageProps {
  params: Promise<{ matchId: string }>;
}

/**
 * Bölüm 11 (docs/05-payment.md): oyun akışına entegrasyon — lobi bir maça
 * katıldıktan sonra oyuncuyu doğrudan oyuna değil, önce bu ödeme ekranına
 * yönlendirir. Ödeme onaylandığında (SignalR "PaymentConfirmed" veya polling)
 * asıl oyun ekranına ( /game/[matchId] ) geçilir.
 */
export default function PayPage({ params }: PayPageProps) {
  const { matchId } = use(params);
  const router = useRouter();
  const [playerId, setPlayerId] = useState<string | null | undefined>(undefined);

  useEffect(() => {
    setPlayerId(window.localStorage.getItem(`porsuk:match:${matchId}:playerId`));
  }, [matchId]);

  if (playerId === undefined) {
    return null;
  }

  if (playerId === null) {
    return (
      <div className="flex flex-1 flex-col items-center justify-center gap-4 px-4 text-center">
        <p className="text-sm text-muted-foreground">
          Bu maça ait bir oyuncu oturumu bulunamadı. Lütfen lobiden maça katılın.
        </p>
        <button className="text-sm font-medium underline" onClick={() => router.push("/game")}>
          Lobiye dön
        </button>
      </div>
    );
  }

  return (
    <div className="flex flex-1 items-center justify-center px-4 py-8">
      <PaymentPanel matchId={matchId} playerId={playerId} onConfirmed={() => router.push(`/game/${matchId}`)} />
    </div>
  );
}
