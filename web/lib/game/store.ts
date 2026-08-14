"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { GameConnection } from "./signalr-client";
import type { ArmyArrivedEvent, ArmyClashedEvent, ArmyDepartedEvent, MatchStateDto } from "./types";

/**
 * docs/07-pages.md Sayfa Bazlı State Matrisi (/game/[matchId]): Connecting →
 * Synchronizing → Playing → (Reconnecting ↔ Playing) → Finished. Bu store
 * yalnızca bağlantı katmanını temsil eder (Connecting/Connected/Reconnecting/
 * Disconnected) — Synchronizing/Playing/Finished ayrımı `state.status`'tan
 * (MatchStatus) zaten okunabilir, burada tekrarlanmaz.
 */
export type ConnectionStatus = "connecting" | "connected" | "reconnecting" | "disconnected";

/**
 * İstemci tarafı oyun state yönetimi. Redux/Zustand gibi bir bağımlılık yerine
 * hafif bir React hook: SignalR bağlantısını kurar, MatchState yayınlarını
 * dinler ve aksiyon fonksiyonlarını dışa verir. Bağlantı koptuğunda
 * (withAutomaticReconnect) yeniden bağlanınca JoinMatch tekrar çağrılır ki
 * sunucu güncel state'i resync etsin.
 */
export function useGameStore(matchId: string, playerId: string) {
  const [state, setState] = useState<MatchStateDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [connectionStatus, setConnectionStatus] = useState<ConnectionStatus>("connecting");
  const [lobbyTimeoutReached, setLobbyTimeoutReached] = useState(false);
  const [reconnectAttempt, setReconnectAttempt] = useState(0);
  // docs/15-asker-hareketi-performans.md Bölüm 6.3: MatchState resync'e ek olarak,
  // animasyon katmanının (useArmyAnimation) anında tepki verebilmesi için ayrı
  // event'ler — her yeni event yeni bir obje referansı olduğundan tüketen taraf
  // (useArmyAnimation) bunu bir useEffect bağımlılığı olarak kullanabilir.
  const [armyDeparted, setArmyDeparted] = useState<ArmyDepartedEvent | null>(null);
  const [armyClashed, setArmyClashed] = useState<ArmyClashedEvent | null>(null);
  const [armyArrived, setArmyArrived] = useState<ArmyArrivedEvent | null>(null);
  const connectionRef = useRef<GameConnection | null>(null);

  useEffect(() => {
    // playerId, matchId sayfası ilk render'da localStorage'dan henüz okunmadan
    // (undefined) boş string olarak gelebilir; bu durumda bağlantı hiç kurulmaz,
    // playerId gerçek değeriyle geldiğinde effect yeniden çalışır.
    if (!playerId) {
      return;
    }

    let cancelled = false;
    setConnectionStatus("connecting");
    const connection = new GameConnection();
    connectionRef.current = connection;

    connection.onMatchState((matchState) => {
      if (!cancelled) {
        setState(matchState);
      }
    });

    connection.onActionError((message) => {
      if (!cancelled) {
        setError(message);
      }
    });

    connection.onLobbyTimeoutReached(() => {
      if (!cancelled) {
        setLobbyTimeoutReached(true);
      }
    });

    connection.onArmyDeparted((event) => {
      if (!cancelled) {
        setArmyDeparted(event);
      }
    });

    connection.onArmyClashed((event) => {
      if (!cancelled) {
        setArmyClashed(event);
      }
    });

    connection.onArmyArrived((event) => {
      if (!cancelled) {
        setArmyArrived(event);
      }
    });

    connection.onReconnecting(() => {
      if (!cancelled) {
        setConnectionStatus("reconnecting");
      }
    });

    connection.onClose(() => {
      // Sunucu-otoriter maç mimarisi (02-architecture.md): "Disconnected" yalnızca
      // istemcinin senkron olmadığını gösterir, maçın bittiği anlamına gelmez
      // (bkz. 07-pages.md). cancelled ise bu, effect cleanup'ın bilinçli stop()'u
      // — gerçek bir kopma değil, "disconnected" göstermez.
      if (!cancelled) {
        setConnectionStatus("disconnected");
      }
    });

    connection.onReconnected(() => {
      setConnectionStatus("connected");
      connection.joinMatch(matchId).catch((err) => setError(String(err)));
    });

    connection
      .start()
      .then(() => {
        if (cancelled) {
          return;
        }
        return connection.joinMatch(matchId).then(() => {
          if (!cancelled) {
            setConnectionStatus("connected");
          }
        });
      })
      .catch((err) => {
        if (!cancelled) {
          setError(String(err));
          setConnectionStatus("disconnected");
        }
      });

    return () => {
      cancelled = true;
      connectionRef.current = null;
      connection.stop();
    };
  }, [matchId, playerId, reconnectAttempt]);

  /** docs/08-page-content.md Bölüm 3.8: "Disconnected" durumunda kullanıcıya sunulan tek aksiyon. */
  const reconnect = useCallback(() => {
    setReconnectAttempt((n) => n + 1);
  }, []);

  useEffect(() => {
    if (!error) {
      return;
    }
    const timeout = setTimeout(() => setError(null), 4000);
    return () => clearTimeout(timeout);
  }, [error]);

  const leaveLobby = useCallback(() => {
    connectionRef.current?.leaveLobby().catch((err) => setError(String(err)));
  }, []);

  const attackRegion = useCallback((fromRegionId: string, toRegionId: string) => {
    connectionRef.current?.attackRegion(fromRegionId, toRegionId).catch((err) => setError(String(err)));
  }, []);

  const startVipMatchNow = useCallback(() => {
    connectionRef.current?.startVipMatchNow().catch((err) => setError(String(err)));
  }, []);

  return {
    state,
    error,
    connectionStatus,
    lobbyTimeoutReached,
    armyDeparted,
    armyClashed,
    armyArrived,
    leaveLobby,
    attackRegion,
    startVipMatchNow,
    reconnect,
  };
}
