import { noIndexMetadata } from "@/lib/metadata";

export const metadata = noIndexMetadata;

export default function HesapAyarlariLayout({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}
