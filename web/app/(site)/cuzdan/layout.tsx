import { noIndexMetadata } from "@/lib/metadata";

export const metadata = noIndexMetadata;

export default function CuzdanLayout({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}
