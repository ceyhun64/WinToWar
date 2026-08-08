import { noIndexMetadata } from "@/lib/metadata";

export const metadata = noIndexMetadata;

export default function GecmisLayout({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}
