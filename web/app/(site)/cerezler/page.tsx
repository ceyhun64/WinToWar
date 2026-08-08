import type { Metadata } from "next";
import { Cookie } from "lucide-react";
import { LegalPage } from "@/components/layout/LegalPage";
import { canonicalFor } from "@/lib/metadata";

export const metadata: Metadata = {
  title: "Çerez Politikası — WinToWar",
  description: "WinToWar Çerez Politikası: platformda kullanılan çerezler ve tercih yönetimi hakkında bilgilendirme.",
  ...canonicalFor("/cerezler"),
};

export default function CerezlerPage() {
  return <LegalPage title="Çerez Politikası" icon={Cookie} />;
}
