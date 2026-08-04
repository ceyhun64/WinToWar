/**
 * docs/07-pages.md `/kosullar`, `/gizlilik`, `/sorumlu-oyun`, `/cerezler`: içerik
 * hukuki/uyumluluk metnidir, Claude tarafından yazılmaz — burada yalnızca
 * "açıkça geçici olduğu belirtilen" bir placeholder bırakılır (bu,
 * `01-workflow-rules.md` Bölüm 0.4'teki placeholder yasağının doküman tarafından
 * açıkça tanımlanmış istisnasıdır).
 */
export function LegalPage({ title }: { title: string }) {
  return (
    <div className="mx-auto flex w-full max-w-2xl flex-1 flex-col gap-4 px-4 py-8">
      <h1 className="text-2xl font-semibold">{title}</h1>
      <p className="text-sm text-muted-foreground">
        İçerik yakında eklenecektir. Bu metin, müşteriden veya hukuk danışmanından gelecek resmi
        içerikle değiştirilene kadar yer tutucudur.
      </p>
    </div>
  );
}
