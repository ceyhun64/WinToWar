"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { GameConnection } from "./signalr-client";
import type { MatchStateDto } from "./types";

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
  const [connected, setConnected] = useState(false);
  const [lobbyTimeoutReached, setLobbyTimeoutReached] = useState(false);
  const connectionRef = useRef<GameConnection | null>(null);

  useEffect(() => {
    // playerId, matchId sayfası ilk render'da localStorage'dan henüz okunmadan
    // (undefined) boş string olarak gelebilir; bu durumda bağlantı hiç kurulmaz,
    // playerId gerçek değeriyle geldiğinde effect yeniden çalışır.
    if (!playerId) {
      return;
    }

    let cancelled = false;
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

    connection.onReconnected(() => {
      connection.joinMatch(matchId, playerId).catch((err) => setError(String(err)));
    });

    connection
      .start()
      .then(() => {
        if (cancelled) {
          return;
        }
        return connection.joinMatch(matchId, playerId).then(() => {
          if (!cancelled) {
            setConnected(true);
          }
        });
      })
      .catch((err) => {
        if (!cancelled) {
          setError(String(err));
        }
      });

    return () => {
      cancelled = true;
      connectionRef.current = null;
      connection.stop();
    };
  }, [matchId, playerId]);

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

  return { state, error, connected, lobbyTimeoutReached, leaveLobby, attackRegion, startVipMatchNow };
}
