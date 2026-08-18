#!/usr/bin/env node
// docs/25-responsive-v2.md Bölüm 1 — küçük mobil ekran responsive doğrulama altyapısı.
//
// Tek bir route'u tek bir viewport'ta çekebilir, ya da tüm route × viewport
// matrisini gezip yanında örtüşme (Bölüm 5) ve yatay taşma denetimini
// çalıştırabilir. Kararlılık kuralları (Bölüm 12): reducedMotion=reduce,
// networkidle + document.fonts.ready beklemesi, CSS animasyon/transition'ların
// devre dışı bırakılması. Bunlar olmadan 390/430 baseline piksel
// karşılaştırması gürültülü olur.
//
// Kullanım:
//   node scripts/shot.mjs --list                      route listesini yazdır
//   node scripts/shot.mjs --route / --viewport 375x667
//   node scripts/shot.mjs --all                       tüm route × tüm viewport + denetim
//   node scripts/shot.mjs --all --baseline            yalnızca 390/430, /tmp/shots/baseline altına
//   node scripts/shot.mjs --route /giris --modals     modal/drawer akışları
//
// Playwright bu repoda yalnızca web/node_modules altında kurulu (kök package.json yok).

import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import path from "node:path";
import fs from "node:fs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = path.resolve(__dirname, "..");
const WEB_ROOT = path.join(REPO_ROOT, "web");
const APP_DIR = path.join(WEB_ROOT, "app");

const require = createRequire(path.join(WEB_ROOT, "package.json"));
const { chromium } = require("playwright");

// ---------------------------------------------------------------------------
// Viewport seti — docs/25-responsive-v2.md Bölüm 1/3
// ---------------------------------------------------------------------------

/** Kritik küçük mobil ekranlar (Bölüm 3). */
const SMALL_VIEWPORTS = [
  { width: 320, height: 568 },
  { width: 360, height: 640 },
  { width: 375, height: 667 },
];

/** 🔒 Regresyon referansı — docs/24-responsive-small-screens.md Bölüm 1. */
const BASELINE_VIEWPORTS = [
  { width: 390, height: 844 },
  { width: 430, height: 932 },
];

/** Tablet referansı. */
const TABLET_VIEWPORTS = [{ width: 768, height: 1024 }];

const ALL_VIEWPORTS = [...SMALL_VIEWPORTS, ...BASELINE_VIEWPORTS, ...TABLET_VIEWPORTS];

// ---------------------------------------------------------------------------
// Route keşfi — "route'ları uydurma, source code üzerinden çıkar" (Bölüm 3)
// ---------------------------------------------------------------------------

/**
 * Dinamik segmentler için gerçek değerler. `scripts/shot.mjs --all` çağrısından
 * önce `--fixture <dosya.json>` ile üzerine yazılabilir; böylece API'den
 * üretilen gerçek id'ler (maç, davet, fatura) kullanılabilir.
 */
const DEFAULT_DYNAMIC_PARAMS = {
  matchId: "00000000-0000-0000-0000-000000000000",
  inviteToken: "TESTTOKEN",
  invoiceId: "00000000-0000-0000-0000-000000000000",
  token: "test-reset-token",
};

/** Next.js App Router route group / private folder kuralları. */
function isRouteGroup(segment) {
  return segment.startsWith("(") && segment.endsWith(")");
}

function isDynamic(segment) {
  return segment.startsWith("[") && segment.endsWith("]");
}

function dynamicName(segment) {
  return segment.replace(/^\[+\.*/, "").replace(/\]+$/, "");
}

/** `web/app` altındaki her `page.tsx` bir route'tur. */
function discoverRoutes(params = DEFAULT_DYNAMIC_PARAMS) {
  const routes = [];

  function walk(dir, segments) {
    const entries = fs.readdirSync(dir, { withFileTypes: true });

    if (entries.some((e) => e.isFile() && /^page\.(tsx|ts|jsx|js)$/.test(e.name))) {
      const url = "/" + segments.filter(Boolean).join("/");
      routes.push(url === "//" ? "/" : url);
    }

    for (const entry of entries) {
      if (!entry.isDirectory()) continue;
      if (entry.name.startsWith("_") || entry.name === "api") continue;

      const next = isRouteGroup(entry.name)
        ? segments
        : isDynamic(entry.name)
          ? [...segments, params[dynamicName(entry.name)] ?? dynamicName(entry.name)]
          : [...segments, entry.name];

      walk(path.join(dir, entry.name), next);
    }
  }

  walk(APP_DIR, []);
  routes.sort();
  return routes;
}

// ---------------------------------------------------------------------------
// Dosya adlandırma — `/` `:` `?` gibi karakterler güvenli hale getirilir
// ---------------------------------------------------------------------------

function routeSlug(route, params = null) {
  if (route === "/") return "landing";
  // Dinamik segment değerleri (maç id'si, davet tokeni…) her turda değişir; dosya
  // adında parametre ADI kullanılır ki baseline ile karşılaştırma aynı dosyaya düşsün.
  let normalized = route;
  if (params) {
    for (const [name, value] of Object.entries(params)) {
      if (value) normalized = normalized.split(String(value)).join(name);
    }
  }
  return (
    normalized
      .replace(/^\/+/, "")
      .replace(/[/\\:?#&=]+/g, "-")
      .replace(/[^a-zA-Z0-9._-]/g, "-")
      .replace(/-+/g, "-")
      .replace(/^-|-$/g, "") || "landing"
  );
}

/**
 * Görev talimatı çıktı yolunu POSIX biçiminde (`/tmp/shots/...`) veriyor.
 * Windows'ta bu yol tek başına geçersizdir; makinede karşılığı olan `C:\tmp`
 * kullanılır (bu dizin projenin çalışma dizinlerinden biridir).
 */
function resolveOutDir(dir) {
  if (process.platform !== "win32") return dir;
  const posix = dir.replace(/\\/g, "/");
  if (/^\/tmp(\/|$)/.test(posix)) return path.win32.join("C:\\tmp", posix.slice("/tmp".length));
  return dir;
}

function shotPath(outDir, route, viewport, suffix = "", params = null) {
  const name = `${routeSlug(route, params)}${suffix ? `-${suffix}` : ""}-${viewport.width}x${viewport.height}.png`;
  return path.join(outDir, name);
}

// ---------------------------------------------------------------------------
// Sayfa kararlılığı (Bölüm 12)
// ---------------------------------------------------------------------------

/**
 * CSS animasyon/transition'ları "kaldırmak" yerine süresini sıfıra indirir:
 * eleman animasyonun BİTİŞ durumunda kalır, `animation: none` gibi başlangıç
 * durumunda donup içeriği görünmez bırakmaz.
 */
const FREEZE_CSS = `
*, *::before, *::after {
  animation-delay: 0s !important;
  animation-duration: 1ms !important;
  animation-iteration-count: 1 !important;
  transition-delay: 0s !important;
  transition-duration: 1ms !important;
  scroll-behavior: auto !important;
  caret-color: transparent !important;
}
/* Next.js dev overlay uygulamanın parçası değil; hem screenshot'ı hem hit-test'i kirletir. */
nextjs-portal { display: none !important; }
`;

async function settle(page) {
  try {
    await page.waitForLoadState("networkidle", { timeout: 15000 });
  } catch {
    /* networkidle bazı sayfalarda (SignalR/polling) hiç gelmez — devam et */
  }
  await page.evaluate(async () => {
    if (document.fonts?.ready) await document.fonts.ready;
  });
  // Otomatik oynayan video (Landing arka planı, components/landing/Background.tsx)
  // her turda farklı bir kareye denk gelir ve 390/430 piksel karşılaştırmasını
  // tamamen anlamsız kılar — sabit bir kareye dondurulur.
  await page.evaluate(async () => {
    const videos = [...document.querySelectorAll("video")];
    for (const video of videos) {
      video.pause();
      video.autoplay = false;
      video.loop = false;
      try {
        video.currentTime = 0;
      } catch {
        /* metadata henüz yüklenmemiş olabilir */
      }
    }
    await Promise.all(
      videos.map(
        (video) =>
          new Promise((resolve) => {
            if (video.readyState >= 2 && video.currentTime === 0) resolve();
            else {
              video.addEventListener("seeked", resolve, { once: true });
              setTimeout(resolve, 2000);
            }
          })
      )
    );
  });
  // framer-motion animasyonları CSS değil WAAPI/rAF üzerinden çalışır; süre
  // sıfırlaması onları etkilemez, bu yüzden açıkça bitiş durumuna alınır.
  await page.evaluate(() => {
    for (const animation of document.getAnimations()) {
      try {
        animation.finish();
      } catch {
        try {
          animation.pause();
        } catch {
          /* iptal edilemeyen animasyon — yoksay */
        }
      }
    }
  });
  await page.waitForTimeout(250);
}

// ---------------------------------------------------------------------------
// Denetimler — Bölüm 5 (örtüşme) + yatay taşma
// ---------------------------------------------------------------------------

/**
 * Bölüm 12: "header'ın altındaki ilk içerik bloğu" sezgisi zayıf olduğu için
 * asıl ölçüt hit-test'tir — görünür her metin/etkileşimli elemanın üst-orta
 * noktasında `document.elementFromPoint()` çağrılır; dönen eleman o elemanın
 * kendisi, bir alt öğesi ya da bir üst öğesi değilse ÖRTÜŞME sayılır.
 * (Üst öğe kabul edilir: `[data-slot="button"]::after` gibi dokunma hedefi
 * pseudo-elemanları originating element'i döndürür — bkz. docs/24 Bölüm 3.)
 */
const AUDIT_SCRIPT = () => {
  function describe(el) {
    if (!el) return "null";
    const id = el.id ? `#${el.id}` : "";
    const cls =
      typeof el.className === "string" && el.className
        ? "." + el.className.trim().split(/\s+/).slice(0, 4).join(".")
        : "";
    const text = (el.textContent ?? "").trim().replace(/\s+/g, " ").slice(0, 48);
    return `${el.tagName.toLowerCase()}${id}${cls}${text ? ` «${text}»` : ""}`;
  }

  const vw = document.documentElement.clientWidth;
  const vh = document.documentElement.clientHeight;

  // --- 1. Yatay taşma -------------------------------------------------------
  const docScrollWidth = Math.max(
    document.documentElement.scrollWidth,
    document.body?.scrollWidth ?? 0
  );
  const horizontalOverflow = docScrollWidth > vw + 1;

  /** Bir üst kapta yatayda kırpılıyorsa taşma dokümana yansımaz — dekoratif blur lekeleri gibi. */
  function isClippedByAncestor(el) {
    for (let p = el.parentElement; p && p !== document.documentElement; p = p.parentElement) {
      const ox = getComputedStyle(p).overflowX;
      if (ox === "hidden" || ox === "clip" || ox === "auto" || ox === "scroll") return true;
    }
    return false;
  }

  const overflowingElements = [];
  for (const el of document.querySelectorAll("body *")) {
    if (el.tagName === "NEXTJS-PORTAL") continue;
    const rect = el.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0) continue;
    const style = getComputedStyle(el);
    if (style.visibility === "hidden" || style.display === "none") continue;
    if (style.position === "fixed") continue; // viewport'a sabitlenmiş, doküman genişliğini etkilemez
    if (isClippedByAncestor(el)) continue;
    if (rect.right > vw + 1 || rect.left < -1) {
      overflowingElements.push({
        selector: describe(el),
        left: Math.round(rect.left),
        right: Math.round(rect.right),
      });
    }
  }

  // --- 2. Kaydırılabilir kaplarda yatay taşma -------------------------------
  const scrollableOverflow = [];
  for (const el of document.querySelectorAll("body, body *")) {
    if (el.scrollWidth > el.clientWidth + 1 && el.clientWidth > 0) {
      const style = getComputedStyle(el);
      if (style.overflowX === "auto" || style.overflowX === "scroll") continue; // bilinçli yatay şerit
      scrollableOverflow.push({
        selector: describe(el),
        scrollWidth: el.scrollWidth,
        clientWidth: el.clientWidth,
      });
    }
  }

  // --- 3. Sticky/fixed header ↔ içerik kesişimi (Bölüm 5) -------------------
  const stickyHeaders = [];
  for (const el of document.querySelectorAll("body *")) {
    const style = getComputedStyle(el);
    if (style.position !== "sticky" && style.position !== "fixed") continue;
    // Dekoratif tam-ekran zeminler (PageBackground: `pointer-events-none fixed
    // inset-0 -z-10`) header değildir; içeriğin arkasında ve tıklanamaz durur.
    if (style.pointerEvents === "none") continue;
    if (Number(style.zIndex) < 0) continue;
    const rect = el.getBoundingClientRect();
    if (rect.width < vw * 0.5 || rect.height === 0) continue; // tam genişlikte bir bar değil
    if (rect.top > vh * 0.5) continue; // üstte duran bir header değil
    stickyHeaders.push({ el, rect, position: style.position });
  }

  const headerOverlaps = [];
  for (const header of stickyHeaders) {
    // Yalnızca gerçek içerik (başlık) elemanları — `main` gibi kapsayıcı kaplar
    // sticky bir header'ın altından başladığı için her zaman kesişir ve
    // ölçütü anlamsız kılar (görev talimatı Bölüm 12'nin "sezgi zayıf" notu).
    for (const el of document.querySelectorAll("h1, h2, h3")) {
      if (header.el.contains(el) || el.contains(header.el)) continue;
      const r = el.getBoundingClientRect();
      if (r.width === 0 || r.height === 0) continue;
      const overlaps =
        header.rect.bottom > r.top &&
        header.rect.top < r.bottom &&
        header.rect.right > r.left &&
        header.rect.left < r.right;
      if (overlaps) {
        headerOverlaps.push({
          header: describe(header.el),
          headerPosition: header.position,
          content: describe(el),
          headerBottom: Math.round(header.rect.bottom),
          contentTop: Math.round(r.top),
        });
      }
    }
  }

  // --- 4. Hit-test tabanlı gerçek görsel kapanma (Bölüm 12) -----------------
  const INTERACTIVE = "a,button,input,select,textarea,summary,[role='button'],[role='link'],[tabindex]";
  const candidates = new Set();

  for (const el of document.querySelectorAll("body *")) {
    if (el.tagName === "NEXTJS-PORTAL" || el.closest("nextjs-portal")) continue;
    if (el.closest("svg")) continue; // SVG içi hit-test farklı kurallara tabi
    const interactive = el.matches(INTERACTIVE);
    const hasOwnText = Array.from(el.childNodes).some(
      (n) => n.nodeType === Node.TEXT_NODE && n.textContent.trim().length > 0
    );
    if (interactive || hasOwnText) candidates.add(el);
  }

  const hitOverlaps = [];
  const clipped = [];

  for (const el of candidates) {
    // Bir modal açıkken arka plan `aria-hidden`/`inert` işaretlenir ve backdrop
    // tarafından KASITLI olarak örtülür — bu bir hata değil, modal'ın kendisidir.
    if (el.closest('[aria-hidden="true"], [inert]')) continue;

    const style = getComputedStyle(el);
    if (style.visibility === "hidden" || style.display === "none") continue;
    if (style.pointerEvents === "none") continue;
    if (Number(style.opacity) === 0) continue;

    // Satır içi bir eleman birden çok satıra sarıyorsa sınır kutusu her iki
    // satırı da kapsar ve üst-orta nokta KOMŞU metnin üstüne düşer — sahte
    // örtüşme. Bu durumda ilk satır kutusu ölçülür.
    const clientRects = el.getClientRects();
    const rect = clientRects.length > 1 ? clientRects[0] : el.getBoundingClientRect();
    if (rect.width < 2 || rect.height < 2) continue;

    // GÖRÜNÜR kutu = elemanın kutusu ∩ tüm kırpan atalar ∩ viewport.
    // Bu kesişim olmadan, `overflow-y-auto` bir kabın içinde AŞAĞI kaymış bir
    // eleman hâlâ viewport koordinatlarında footer'ın üstüne düşer ve
    // elementFromPoint footer'ı döndürür — gerçek olmayan bir "örtüşme".
    let top = rect.top;
    let bottom = rect.bottom;
    let left = rect.left;
    let right = rect.right;
    for (let p = el.parentElement; p; p = p.parentElement) {
      const ps = getComputedStyle(p);
      if (ps.overflowX === "visible" && ps.overflowY === "visible") continue;
      const pr = p.getBoundingClientRect();
      if (ps.overflowY !== "visible") {
        top = Math.max(top, pr.top);
        bottom = Math.min(bottom, pr.bottom);
      }
      if (ps.overflowX !== "visible") {
        left = Math.max(left, pr.left);
        right = Math.min(right, pr.right);
      }
    }
    top = Math.max(top, 0);
    bottom = Math.min(bottom, vh);
    left = Math.max(left, 0);
    right = Math.min(right, vw);
    if (bottom - top < 2 || right - left < 2) continue;

    // Elemanın üst kısmı kırpılıyorsa (kabın/viewport'un üstünde kalıyorsa) ayrıca raporla.
    if (top - rect.top > 1 && bottom - top > 2) {
      clipped.push({
        selector: describe(el),
        rectTop: Math.round(rect.top),
        visibleTop: Math.round(top),
        hiddenPx: Math.round(top - rect.top),
      });
    }

    // Tek bir orta nokta yetmez: bir eleman KISMEN örtülebilir (ör. sağ yarısı
    // bir butonun altında kalan nav linki). Üst kenar boyunca üç nokta örneklenir;
    // köşeler (yuvarlatma/gölge yanlış pozitif üretir) bilinçli olarak atlanır.
    const y = Math.round(Math.min(top + Math.min(4, (bottom - top) / 2), bottom - 1));
    const width = right - left;
    const samples = [0.15, 0.5, 0.85].map((f) => Math.round(left + width * f));

    for (const x of samples) {
      const hit = document.elementFromPoint(x, y);
      if (!hit) continue;
      if (hit === el || el.contains(hit) || hit.contains(el)) continue;

      hitOverlaps.push({
        selector: describe(el),
        coveredBy: describe(hit),
        point: { x, y },
        rect: {
          top: Math.round(rect.top),
          left: Math.round(rect.left),
          width: Math.round(rect.width),
          height: Math.round(rect.height),
        },
      });
      break;
    }
  }

  return {
    viewport: { width: vw, height: vh },
    horizontalOverflow,
    docScrollWidth,
    overflowingElements: overflowingElements.slice(0, 20),
    scrollableOverflow: scrollableOverflow.slice(0, 20),
    headerOverlaps: headerOverlaps.slice(0, 20),
    hitOverlaps: hitOverlaps.slice(0, 30),
    clippedAboveViewport: clipped.slice(0, 20),
  };
};

// ---------------------------------------------------------------------------
// Tarayıcı sürücüsü
// ---------------------------------------------------------------------------

async function makeContext(browser, viewport, storage = null) {
  const context = await browser.newContext({
    viewport,
    deviceScaleFactor: 2,
    hasTouch: true,
    isMobile: true,
    reducedMotion: "reduce",
    locale: "tr-TR",
  });
  // Sayfa scripti çalışmadan önce enjekte edilir; ilk boyama bile donmuş olur.
  await context.addInitScript((css) => {
    const style = document.createElement("style");
    style.id = "__shot_freeze__";
    style.textContent = css;
    const attach = () => document.head?.appendChild(style);
    if (document.head) attach();
    else document.addEventListener("DOMContentLoaded", attach);
  }, FREEZE_CSS);

  await context.addInitScript(FETCH_URL_SHIM);

  // `/game/[matchId]` oyuncu kimliğini localStorage'dan okur (bkz. o sayfanın
  // `wintowar:match:<id>:playerId` anahtarı); fixture bunu sağlar.
  if (storage && Object.keys(storage).length) {
    await context.addInitScript((entries) => {
      for (const [key, value] of entries) window.localStorage.setItem(key, value);
    }, Object.entries(storage));
  }

  return context;
}

/**
 * ⚠️ ÜRÜN HATASI TELAFİSİ (yalnızca test altyapısında).
 *
 * `web/lib/admin/api.ts` istek yolunun başına `NEXT_PUBLIC_API_URL` ekliyor,
 * ardından `authFetch` (web/lib/identity.ts) aynı tabanı BİR KEZ DAHA ekliyor.
 * Sonuç `http://host:portHTTP://host:port/api/admin/...` gibi geçersiz bir URL
 * ve her `/admin/*` sayfası veri yerine "TypeError: Failed to parse URL" hatası
 * gösteriyor. Bu, responsive görevin kapsamı dışında bir işlev hatasıdır ve
 * DÜZELTİLMEDİ, raporda ayrıca bildirildi — ancak düzeltilmeden admin sayfaları
 * gerçek içerikle (tablo/liste) hiç render edilemiyor, yani küçük ekran
 * denetimi yapılamıyordu. Burada yalnızca isteğin kendisi düzeltilir; sayfanın
 * CSS/DOM'una dokunulmaz.
 */
const FETCH_URL_SHIM = () => {
  const originalFetch = window.fetch;
  const normalize = (value) => {
    if (typeof value !== "string") return value;
    const second = value.indexOf("http", 5);
    return second > 0 && /^https?:\/\//.test(value.slice(second)) ? value.slice(second) : value;
  };
  window.fetch = (input, init) =>
    originalFetch(typeof input === "string" ? normalize(input) : input, init);
};

async function openPage(context, baseUrl, route) {
  const page = await context.newPage();
  const consoleErrors = [];
  page.on("pageerror", (e) => consoleErrors.push(String(e.message ?? e)));
  const response = await page.goto(baseUrl + route, { waitUntil: "domcontentloaded", timeout: 45000 });
  await settle(page);
  return { page, response, consoleErrors };
}

// ---------------------------------------------------------------------------
// CLI
// ---------------------------------------------------------------------------

function parseArgs(argv) {
  const opts = {
    routes: [],
    viewports: [],
    outDir: "/tmp/shots",
    baseUrl: process.env.SHOT_BASE_URL ?? "http://localhost:3000",
    all: false,
    baseline: false,
    audit: true,
    list: false,
    modals: false,
    auth: false,
    reportPath: null,
    fixture: null,
  };

  for (let i = 2; i < argv.length; i++) {
    const arg = argv[i];
    switch (arg) {
      case "--route":
        opts.routes.push(argv[++i]);
        break;
      case "--viewport": {
        const [w, h] = argv[++i].split("x").map(Number);
        opts.viewports.push({ width: w, height: h });
        break;
      }
      case "--out":
        opts.outDir = argv[++i];
        break;
      case "--base-url":
        opts.baseUrl = argv[++i];
        break;
      case "--report":
        opts.reportPath = argv[++i];
        break;
      case "--fixture":
        opts.fixture = argv[++i];
        break;
      case "--all":
        opts.all = true;
        break;
      case "--baseline":
        opts.baseline = true;
        break;
      case "--small":
        opts.viewports.push(...SMALL_VIEWPORTS);
        break;
      case "--no-audit":
        opts.audit = false;
        break;
      case "--list":
        opts.list = true;
        break;
      case "--modals":
        opts.modals = true;
        break;
      case "--auth":
        opts.auth = true;
        break;
      default:
        throw new Error(`Bilinmeyen argüman: ${arg}`);
    }
  }

  return opts;
}

/**
 * Oturum gerektiren route'lar için: gerçek giriş formu doldurulur (API
 * localhost:5019'da ayakta olmalı). Refresh cookie context'te kalır, bu yüzden
 * aynı context'teki sonraki sayfalar oturumlu render edilir.
 */
async function signIn(context, baseUrl, credentials) {
  const page = await context.newPage();
  await page.goto(baseUrl + "/giris", { waitUntil: "domcontentloaded" });
  // `/giris` bir `<form>` kullanmıyor (bkz. app/(site)/giris/page.tsx) —
  // gönderim düz bir `onClick` handler'ı, bu yüzden butona metniyle erişilir.
  await page.fill("#email", credentials.email);
  await page.fill("#password", credentials.password);
  await page.getByRole("button", { name: /giriş yap/i }).click();
  await page.waitForURL((url) => !url.pathname.startsWith("/giris"), { timeout: 15000 }).catch(() => {});
  await page.waitForTimeout(1500);
  const url = page.url();
  await page.close();
  return !url.includes("/giris");
}

/**
 * docs/25-responsive-v2.md Bölüm 12 — modal/drawer akışları.
 *
 * Router taramasıyla bulunan tüm modal/dialog/sheet kullanım yerleri
 * (`components/ui/*` hariç): `/lobi` → VipCreateDialog, `/game/[matchId]` →
 * menü Sheet'i + bölge seçilince açılan ActionPanel bottom sheet'i,
 * paylaşılan `Header` → kullanıcı DropdownMenu'sü.
 */
function modalFlows(params) {
  return [
    {
      name: "lobi-vip-dialog",
      route: "/lobi",
      async open(page) {
        // "+ Oda Kur" yalnızca VIP sekmesindeyken görünür (bkz. lobi/page.tsx);
        // sekme, GameModeTiles içindeki düz bir <button> ile seçilir.
        await page.getByRole("button", { name: /VIP/ }).first().click();
        await page.getByRole("button", { name: "+ Oda Kur" }).click();
        await page.getByRole("dialog").waitFor({ state: "visible", timeout: 10000 });
      },
    },
    {
      name: "header-user-menu",
      route: "/lobi",
      async open(page) {
        await page.locator('header [data-slot="dropdown-menu-trigger"]').first().click();
        await page.waitForTimeout(400);
      },
    },
    {
      name: "game-menu-sheet",
      route: `/game/${params.matchId}`,
      async open(page) {
        await page.getByRole("button", { name: "Menü" }).click();
        await page.waitForTimeout(600);
      },
    },
    {
      name: "game-action-panel",
      route: `/game/${params.matchId}`,
      async open(page) {
        // ActionPanel yalnızca bir bölge seçiliyken açılır; haritadaki ilk
        // bölge düğümüne dokunulur (sürükleme değil, eşik altı tap).
        await page.locator('[data-game-map-surface] g[role="button"]').first().click();
        await page.waitForTimeout(600);
      },
    },
  ];
}

async function runModalFlows(browser, opts, params, storage, credentials) {
  const outDir = resolveOutDir(opts.outDir);
  fs.mkdirSync(outDir, { recursive: true });
  const results = [];

  for (const viewport of opts.viewports.length ? opts.viewports : [...SMALL_VIEWPORTS, ...BASELINE_VIEWPORTS]) {
    const context = await makeContext(browser, viewport, storage);
    if (credentials) {
      let ok = false;
      for (let attempt = 0; attempt < 3 && !ok; attempt++) {
        if (attempt > 0) await new Promise((r) => setTimeout(r, 20000));
        ok = await signIn(context, opts.baseUrl, credentials);
      }
      if (!ok) console.error(`  ! giriş başarısız (${viewport.width}x${viewport.height})`);
    }

    for (const flow of modalFlows(params)) {
      const entry = { flow: flow.name, route: flow.route, viewport: `${viewport.width}x${viewport.height}` };
      try {
        const { page } = await openPage(context, opts.baseUrl, flow.route);
        await flow.open(page);
        await settle(page);
        const file = path.join(outDir, `${flow.name}-${viewport.width}x${viewport.height}.png`);
        await page.screenshot({ path: file, fullPage: true });
        entry.shot = file;
        entry.audit = await page.evaluate(AUDIT_SCRIPT);
        await page.close();
        console.log(`✓ ${flow.name} @ ${entry.viewport}`);
      } catch (error) {
        entry.error = String(error.message ?? error);
        console.error(`✗ ${flow.name} @ ${entry.viewport}: ${entry.error}`);
      }
      results.push(entry);
    }
    await context.close();
  }

  fs.writeFileSync(path.join(outDir, "modals.json"), JSON.stringify({ results }, null, 2), "utf8");
  console.log(`\nModal raporu: ${path.join(outDir, "modals.json")}`);
}

async function main() {
  const opts = parseArgs(process.argv);

  let params = { ...DEFAULT_DYNAMIC_PARAMS };
  let credentials = null;
  let storage = null;
  if (opts.fixture) {
    const fixture = JSON.parse(fs.readFileSync(resolveOutDir(opts.fixture), "utf8"));
    params = { ...params, ...(fixture.params ?? {}) };
    credentials = fixture.credentials ?? null;
    storage = fixture.localStorage ?? null;
  }

  const routes = opts.routes.length ? opts.routes : discoverRoutes(params);

  if (opts.list) {
    console.log(routes.join("\n"));
    return;
  }

  let viewports = opts.viewports;
  if (!viewports.length) viewports = opts.baseline ? BASELINE_VIEWPORTS : ALL_VIEWPORTS;
  if (opts.baseline) viewports = BASELINE_VIEWPORTS;

  const rootOut = resolveOutDir(opts.outDir);
  const outDir = opts.baseline ? path.join(rootOut, "baseline") : rootOut;
  fs.mkdirSync(outDir, { recursive: true });

  const browser = await chromium.launch();

  if (opts.modals) {
    await runModalFlows(browser, { ...opts, viewports }, params, storage, opts.auth ? credentials : null);
    await browser.close();
    return;
  }

  const report = { baseUrl: opts.baseUrl, outDir, generatedAt: new Date().toISOString(), results: [] };

  for (const viewport of viewports) {
    const context = await makeContext(browser, viewport, storage);

    // 🔒 Oturum viewport'lar arasında storageState ile TAŞINAMAZ: refresh token
    // her sayfa yüklemesinde döner ve API'de yeniden kullanım tespiti tüm
    // oturumları iptal eder (AuthConfig.RevokeAllOnReuseDetected). Bu yüzden her
    // context kendi girişini yapar; giriş dakikada 10 istekle sınırlı olduğu için
    // (LoginRateLimitPerMinute) başarısızlıkta bekleyip yeniden denenir.
    let signedIn = false;
    if (opts.auth && credentials) {
      for (let attempt = 0; attempt < 3 && !signedIn; attempt++) {
        if (attempt > 0) await new Promise((r) => setTimeout(r, 20000));
        signedIn = await signIn(context, opts.baseUrl, credentials);
      }
      if (!signedIn) console.error(`  ! giriş başarısız (${viewport.width}x${viewport.height})`);
    }

    for (const route of routes) {
      const entry = { route, viewport: `${viewport.width}x${viewport.height}`, signedIn };
      try {
        const { page, response, consoleErrors } = await openPage(context, opts.baseUrl, route);
        entry.status = response?.status() ?? null;
        entry.finalUrl = new URL(page.url()).pathname;
        const file = shotPath(outDir, route, viewport, "", params);
        await page.screenshot({ path: file, fullPage: true });
        entry.shot = file;
        if (opts.audit) entry.audit = await page.evaluate(AUDIT_SCRIPT);
        if (consoleErrors.length) entry.pageErrors = consoleErrors.slice(0, 3);
        await page.close();
        console.log(`✓ ${route} @ ${entry.viewport} → ${path.basename(file)}`);
      } catch (error) {
        entry.error = String(error.message ?? error);
        console.error(`✗ ${route} @ ${entry.viewport}: ${entry.error}`);
      }
      report.results.push(entry);
    }

    await context.close();
  }

  await browser.close();

  const reportPath = opts.reportPath ? resolveOutDir(opts.reportPath) : path.join(outDir, "report.json");
  fs.writeFileSync(reportPath, JSON.stringify(report, null, 2), "utf8");
  console.log(`\nRapor: ${reportPath}`);
}

if (process.argv[1] && path.resolve(process.argv[1]) === path.resolve(fileURLToPath(import.meta.url))) {
  main().catch((error) => {
    console.error(error);
    process.exit(1);
  });
}

