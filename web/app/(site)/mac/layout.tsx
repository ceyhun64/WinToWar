import { noIndexMetadata } from "@/lib/metadata";

export const metadata = noIndexMetadata;

export default function MacLayout({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}
