"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { clearAdminKey } from "@/lib/admin/api";

const LINKS = [
  { href: "/admin", label: "Özet" },
  { href: "/admin/odemeler", label: "Ödemeler" },
  { href: "/admin/maclar", label: "Maçlar" },
  { href: "/admin/kullanicilar", label: "Kullanıcılar" },
  { href: "/admin/destek", label: "Destek" },
  { href: "/admin/loglar", label: "Loglar" },
];

export function AdminSidebar() {
  const pathname = usePathname();

  return (
    <nav className="flex w-48 shrink-0 flex-col gap-1 border-r border-border bg-card p-3">
      {LINKS.map((link) => (
        <Link
          key={link.href}
          href={link.href}
          className={`rounded-md px-3 py-2 text-sm ${
            pathname === link.href ? "bg-muted font-medium text-foreground" : "text-muted-foreground hover:bg-muted"
          }`}
        >
          {link.label}
        </Link>
      ))}
      <button
        className="mt-auto rounded-md px-3 py-2 text-left text-sm text-muted-foreground hover:bg-muted"
        onClick={() => {
          clearAdminKey();
          window.location.reload();
        }}
      >
        Çıkış
      </button>
    </nav>
  );
}
