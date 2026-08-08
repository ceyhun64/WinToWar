const sharp = require("sharp");
const path = require("path");

const WEB = __dirname;
const LOGO_MARK = path.join(WEB, "public", "logo", "logo-mark.png");
const LOGO_FULL = path.join(WEB, "public", "logo", "logo.png");
const NAVY = "#07111f";

async function main() {
  const markBuf = await sharp(LOGO_MARK)
    .resize(420, 420, { fit: "inside" })
    .toBuffer();
  await sharp({
    create: { width: 512, height: 512, channels: 4, background: { r: 0, g: 0, b: 0, alpha: 0 } },
  })
    .composite([{ input: markBuf, gravity: "center" }])
    .png()
    .toFile(path.join(WEB, "app", "icon.png"));

  const markForAppleBuf = await sharp(LOGO_MARK)
    .resize(128, 128, { fit: "inside" })
    .toBuffer();
  await sharp({
    create: { width: 180, height: 180, channels: 4, background: NAVY },
  })
    .composite([{ input: markForAppleBuf, gravity: "center" }])
    .png()
    .toFile(path.join(WEB, "app", "apple-icon.png"));

  const faviconSrcBuf = await sharp(LOGO_MARK)
    .resize(220, 220, { fit: "inside" })
    .toBuffer();
  await sharp({
    create: { width: 256, height: 256, channels: 4, background: { r: 0, g: 0, b: 0, alpha: 0 } },
  })
    .composite([{ input: faviconSrcBuf, gravity: "center" }])
    .png()
    .toFile(path.join(WEB, "_favicon-src.png"));

  const ogLogoBuf = await sharp(LOGO_FULL)
    .resize({ height: 560, fit: "inside" })
    .toBuffer();
  await sharp({
    create: { width: 1200, height: 630, channels: 4, background: NAVY },
  })
    .composite([{ input: ogLogoBuf, gravity: "center" }])
    .png()
    .toFile(path.join(WEB, "public", "og-image.png"));

  console.log("done");
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
