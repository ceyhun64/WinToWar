import { noIndexMetadata } from "@/lib/metadata";

export const metadata = noIndexMetadata;

export default function OdemeLayout({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}
