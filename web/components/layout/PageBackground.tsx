/**
 * `docs/04-style.md` Landing İstisnası — "diğer sayfalar" pilotu: Landing'in
 * görsel dilini (koyu navy zemin + yumuşak glow + çok hafif taktik harita
 * ızgarası) paylaşır ama video veya büyük illüstrasyon içermez (kullanıcı
 * talimatı — Landing'in imzası video, diğer sayfalarda tekrar edilmez).
 * Yalnızca `.dark` kapsamı içinde (bkz. `app/(site)/layout.tsx`
 * `DARK_THEME_PATHS`) render edilecek şekilde `--background` token'ını
 * kullanır, kendi rengini icat etmez.
 *
 * İkinci tur geri bildirim ("boş hissediyor, savaş haritası çizgileri
 * ekle"): %3-4 opaklıkta ince bir ızgar deseni eklendi — dekoratif, salt
 * atmosfer amaçlı, hiçbir veriyle ilişkili değil (gerçek oyun haritasıyla
 * karıştırılmaması için `components/game/GameMap.tsx` ile hiçbir bağlantısı yok).
 */
export function PageBackground() {
  return (
    <div className="pointer-events-none fixed inset-0 -z-10 overflow-hidden bg-background">
      <div
        className="absolute inset-0 opacity-[0.035]"
        style={{
          backgroundImage:
            "repeating-linear-gradient(0deg, rgba(148,163,184,0.6) 0px, rgba(148,163,184,0.6) 1px, transparent 1px, transparent 64px), repeating-linear-gradient(90deg, rgba(148,163,184,0.6) 0px, rgba(148,163,184,0.6) 1px, transparent 1px, transparent 64px)",
        }}
      />
      <div
        className="absolute -left-40 -top-40 size-120 rounded-full blur-[140px]"
        style={{ background: "radial-gradient(circle, rgba(56,189,248,0.14), transparent 70%)" }}
      />
      <div
        className="absolute -right-40 bottom-0 size-120 rounded-full blur-[140px]"
        style={{ background: "radial-gradient(circle, rgba(245,185,66,0.08), transparent 70%)" }}
      />
    </div>
  );
}
