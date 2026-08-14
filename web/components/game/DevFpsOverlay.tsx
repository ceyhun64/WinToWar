"use client";

import { useEffect, useRef, useState } from "react";

/**
 * docs/15-asker-hareketi-performans.md Bölüm 7 "Hedef": requestAnimationFrame bazlı
 * geçici bir dev-only FPS overlay — yalnızca geliştirme ortamında görünür, prod
 * build'de render edilmez (NODE_ENV kontrolü, bkz. aşağıdaki erken return).
 * Önce/sonra performans karşılaştırmasını gözle görülür kılmak için eklendi.
 */
export function DevFpsOverlay() {
  const [fps, setFps] = useState(0);
  const framesRef = useRef(0);
  const lastSampleRef = useRef(performance.now());

  useEffect(() => {
    if (process.env.NODE_ENV === "production") return;

    let rafId = 0;
    const loop = () => {
      framesRef.current += 1;
      const now = performance.now();
      const elapsed = now - lastSampleRef.current;
      if (elapsed >= 500) {
        setFps(Math.round((framesRef.current * 1000) / elapsed));
        framesRef.current = 0;
        lastSampleRef.current = now;
      }
      rafId = requestAnimationFrame(loop);
    };

    rafId = requestAnimationFrame(loop);
    return () => cancelAnimationFrame(rafId);
  }, []);

  if (process.env.NODE_ENV === "production") return null;

  return (
    <div className="pointer-events-none fixed right-3 top-3 z-50 rounded-md bg-black/70 px-2 py-1 font-mono text-xs text-white">
      {fps} FPS
    </div>
  );
}
