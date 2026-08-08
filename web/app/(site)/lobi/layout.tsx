import { noIndexMetadata } from "@/lib/metadata";

export const metadata = noIndexMetadata;

export default function LobiLayout({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}
