/** Sayfa içi bölüm başlıklarının tek kaynağı — bkz. `lobi/page.tsx`de kurulan desen. */
export function SectionTitle({ children }: { children: React.ReactNode }) {
  return <h2 className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">{children}</h2>;
}
