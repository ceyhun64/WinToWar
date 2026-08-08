"use client";

import { useEffect, useId, useRef } from "react";
import Script from "next/script";
import { googleAuth, linkGoogle, AuthApiError } from "@/lib/auth/api";
import type { PlayerAccountDto } from "@/lib/auth/types";

const GOOGLE_CLIENT_ID = process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID ?? "";

declare global {
  interface Window {
    google?: {
      accounts: {
        id: {
          initialize: (config: {
            client_id: string;
            callback: (response: { credential: string }) => void;
          }) => void;
          renderButton: (parent: HTMLElement, options: { theme: string; size: string; width?: number; text?: string }) => void;
        };
      };
    };
  }
}

interface GoogleContinueButtonProps {
  onSuccess: (player: PlayerAccountDto) => void;
  onError: (message: string) => void;
  /** docs/11-auth.md Bölüm 1.2/1.8: yalnızca /kayit'ta, ilk kez Google ile hesap oluşturulacaksa gereklidir. */
  consent?: { ageConfirmed: boolean; termsAccepted: boolean };
  /** "link": /hesap-ayarlari'ndan mevcut oturuma Google bağlama (Bölüm 1.2/3.2). */
  mode?: "auth" | "link";
}

/**
 * docs/11-auth.md Bölüm 1.2 🔒: Google Identity Services (GIS) JS SDK ile
 * "Google ile Devam Et" butonu. Ayrı bir npm paketi eklenmez (`@react-oauth/google`
 * vb.) — resmi script doğrudan yüklenir, 06-coding-standards.md Bağımlılık
 * Disiplini'yle tutarlı (YAGNI, tek ihtiyaç credential callback'i).
 */
export function GoogleContinueButton({ onSuccess, onError, consent, mode = "auth" }: GoogleContinueButtonProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const buttonId = useId();

  useEffect(() => {
    if (!GOOGLE_CLIENT_ID || !containerRef.current) {
      return;
    }

    let cancelled = false;

    async function handleCredentialResponse(response: { credential: string }) {
      onError("");
      try {
        if (mode === "link") {
          const player = await linkGoogle(response.credential);
          if (!cancelled) onSuccess(player);
          return;
        }

        const dto = await googleAuth(response.credential, consent);
        if (!cancelled) onSuccess(dto.player);
      } catch (err) {
        if (cancelled) return;
        if (err instanceof AuthApiError && err.code === "EMAIL_EXISTS_LINK_REQUIRED") {
          onError("Bu e-posta zaten kayıtlı. Önce parolanızla giriş yapıp Google'ı hesap ayarlarından bağlayın.");
          return;
        }
        if (err instanceof AuthApiError && err.code === "MISSING_CONSENT") {
          onError("Devam etmek için yaş ve şartlar onay kutularını işaretleyin.");
          return;
        }
        onError(err instanceof Error ? err.message : "Google ile giriş başarısız oldu.");
      }
    }

    function initialize() {
      if (!window.google || !containerRef.current) {
        return;
      }
      window.google.accounts.id.initialize({
        client_id: GOOGLE_CLIENT_ID,
        callback: handleCredentialResponse,
      });
      window.google.accounts.id.renderButton(containerRef.current, {
        theme: "outline",
        size: "large",
        width: 320,
        text: mode === "link" ? "continue_with" : "continue_with",
      });
    }

    if (window.google) {
      initialize();
    } else {
      const interval = window.setInterval(() => {
        if (window.google) {
          window.clearInterval(interval);
          initialize();
        }
      }, 200);
      return () => window.clearInterval(interval);
    }

    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mode]);

  if (!GOOGLE_CLIENT_ID) {
    return null;
  }

  return (
    <>
      <Script src="https://accounts.google.com/gsi/client" strategy="afterInteractive" />
      <div id={buttonId} ref={containerRef} className="flex justify-center" />
    </>
  );
}
