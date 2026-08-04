"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { isSignedIn } from "@/lib/identity";

/** docs/07-pages.md `/`: "Giriş yapmamışsa /kayit'e, yapmışsa /lobi'ye yönlendirir." */
export function LandingCta() {
  const router = useRouter();
  const [signedIn, setSignedIn] = useState<boolean | null>(null);

  useEffect(() => {
    setSignedIn(isSignedIn());
  }, []);

  if (signedIn === null) {
    return <Button disabled>Yükleniyor...</Button>;
  }

  return (
    <Button onClick={() => router.push(signedIn ? "/lobi" : "/kayit")}>
      {signedIn ? "Lobiye Git" : "Hemen Başla"}
    </Button>
  );
}
