"use client";

import { createContext, useContext, useEffect, useRef, useState } from "react";
import { ensureSessionLoaded, isSignedIn, subscribeToSession } from "@/lib/identity";
import { getWalletBalance } from "./api";
import { WalletConnection } from "./wallet-signalr-client";

interface WalletContextValue {
  /** null: henüz hiç değer gelmedi (ilk fetch/bağlantı bekleniyor) veya girişsiz kullanıcı. */
  balanceUsd: string | null;
  /** WalletHub bağlantısı şu an açık mı — reconnect sırasında stale veri gösterilmeye devam edilir, bu yalnızca isteğe bağlı bir gösterge içindir. */
  isConnected: boolean;
}

const WalletContext = createContext<WalletContextValue>({ balanceUsd: null, isConnected: false });

/**
 * docs/16-wallet-balance-sync.md: Header, Navbar ve `/cuzdan` dahil bakiyeyi
 * gösteren her yerin okuduğu tek kaynak.
 */
export function useWallet(): WalletContextValue {
  return useContext(WalletContext);
}

/**
 * docs/16-wallet-balance-sync.md Bölüm 2: root layout'a (`app/layout.tsx`) tek
 * bir kez eklenir. Auth durumu bir route'a değil `lib/identity.ts`'teki global
 * oturuma bağlı olduğundan (Header/Navbar'ın zaten yaptığı gibi) WalletHub
 * bağlantısı burada `isSignedIn()`/`subscribeToSession` ile içeriden gate'lenir
 * — girişsiz kullanıcıda hiçbir bağlantı açılmaz, çıkış yapıldığında mevcut
 * bağlantı kapatılır.
 */
export function WalletProvider({ children }: { children: React.ReactNode }) {
  const [balanceUsd, setBalanceUsd] = useState<string | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const connectionRef = useRef<WalletConnection | null>(null);
  // Giriş/çıkış hızlı art arda tetiklenirse (subscribeToSession), gecikmiş bir
  // eski `connect()` çağrısının daha yeni bir `disconnect()`'ten SONRA state'i
  // ezmesini engeller — her connect denemesi kendi kimliğini taşır.
  const attemptIdRef = useRef(0);

  useEffect(() => {
    let cancelled = false;

    function disconnect() {
      attemptIdRef.current += 1;
      connectionRef.current?.stop();
      connectionRef.current = null;
      setIsConnected(false);
      setBalanceUsd(null);
    }

    async function connect() {
      const attemptId = ++attemptIdRef.current;

      try {
        const dto = await getWalletBalance();
        if (!cancelled && attemptId === attemptIdRef.current) {
          setBalanceUsd(dto.balanceUsd);
        }
      } catch {
        // İlk fetch başarısız olsa bile bağlantı denenir; sonraki bir
        // WalletBalanceUpdated event'i bakiyeyi telafi edebilir.
      }

      if (cancelled || attemptId !== attemptIdRef.current) {
        return;
      }

      const connection = new WalletConnection();
      connection.onBalanceUpdated((dto) => {
        if (!cancelled && attemptId === attemptIdRef.current) {
          setBalanceUsd(dto.balanceUsd);
        }
      });
      connection.onReconnecting(() => {
        if (!cancelled && attemptId === attemptIdRef.current) {
          setIsConnected(false);
        }
      });
      connection.onReconnected(() => {
        if (!cancelled && attemptId === attemptIdRef.current) {
          setIsConnected(true);
        }
      });
      connection.onClose(() => {
        if (!cancelled && attemptId === attemptIdRef.current) {
          setIsConnected(false);
        }
      });

      try {
        await connection.start();
      } catch {
        // withAutomaticReconnect ilk başarısız start()'ı kapsamaz — sessizce
        // bırakılır, kullanıcı sayfayı yeniden açtığında/oturum değiştiğinde
        // syncSession tekrar dener.
      }

      if (cancelled || attemptId !== attemptIdRef.current) {
        connection.stop();
        return;
      }

      connectionRef.current = connection;
      setIsConnected(true);
    }

    function syncSession() {
      if (isSignedIn()) {
        if (!connectionRef.current) {
          connect();
        }
      } else {
        disconnect();
      }
    }

    ensureSessionLoaded().then(() => {
      if (!cancelled) {
        syncSession();
      }
    });
    const unsubscribe = subscribeToSession(syncSession);

    return () => {
      cancelled = true;
      unsubscribe();
      disconnect();
    };
  }, []);

  return <WalletContext.Provider value={{ balanceUsd, isConnected }}>{children}</WalletContext.Provider>;
}
