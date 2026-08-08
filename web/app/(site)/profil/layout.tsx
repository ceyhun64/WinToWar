import { noIndexMetadata } from "@/lib/metadata";

export const metadata = noIndexMetadata;

export default function ProfilLayout({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}
