"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { signOut } from "@/lib/identity";

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
    <nav className="flex shrink-0 gap-1 overflow-x-auto border-b border-border bg-card p-3 md:w-48 md:flex-col md:overflow-x-visible md:border-r md:border-b-0">
      {LINKS.map((link) => (
        <Link
          key={link.href}
          href={link.href}
          className={`shrink-0 rounded-md px-3 py-2 text-sm whitespace-nowrap ${
            pathname === link.href ? "bg-muted font-medium text-foreground" : "text-muted-foreground hover:bg-muted"
          }`}
        >
          {link.label}
        </Link>
      ))}
      <button
        className="ml-auto shrink-0 rounded-md px-3 py-2 text-left text-sm whitespace-nowrap text-muted-foreground hover:bg-muted md:mt-auto md:ml-0"
        onClick={() => {
          signOut();
          window.location.reload();
        }}
      >
        Çıkış
      </button>
    </nav>
  );
}
