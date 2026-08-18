import type { Metadata } from "next";
import { AdminGate } from "@/components/admin/AdminGate";
import { AdminSidebar } from "@/components/admin/AdminSidebar";

/** docs/07-pages.md#Metadata/SEO: auth gerektiren sayfalar arama motorunda indexlenmez. */
export const metadata: Metadata = {
  robots: { index: false, follow: false },
};

/** docs/07-pages.md: AdminLayout — kendi minimal header'ı ve Sidebar'ı olan, oyuncu tarafı navigasyonundan tamamen ayrı bir kabuk. */
export default function AdminLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="admin-theme flex min-h-0 flex-1 flex-col bg-background text-foreground">
      <header className="border-b border-border bg-card px-4 py-3">
        <span className="text-sm font-semibold">WinToWar Admin</span>
      </header>
      <AdminGate>
        <div className="flex min-h-0 flex-1 flex-col md:flex-row">
          <AdminSidebar />
          <main className="flex-1 min-h-0 overflow-y-auto p-4">{children}</main>
        </div>
      </AdminGate>
    </div>
  );
}
