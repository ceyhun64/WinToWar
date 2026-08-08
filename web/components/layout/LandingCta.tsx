"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { ensureSessionLoaded, isSignedIn } from "@/lib/identity";

/** docs/07-pages.md `/`: "Giriş yapmamışsa /kayit'e, yapmışsa /lobi'ye yönlendirir." */
export function LandingCta({ className, size }: { className?: string; size?: "default" | "sm" | "lg" }) {
  const router = useRouter();
  const [signedIn, setSignedIn] = useState<boolean | null>(null);

  useEffect(() => {
    ensureSessionLoaded().then(() => setSignedIn(isSignedIn()));
  }, []);

  if (signedIn === null) {
    return (
      <Button disabled size={size} className={className}>
        Yükleniyor...
      </Button>
    );
  }

  return (
    <Button size={size} className={className} onClick={() => router.push(signedIn ? "/lobi" : "/kayit")}>
      
      {signedIn ? "Hemen Oyna" : "Hemen Başla"}
    </Button>
  );
}
