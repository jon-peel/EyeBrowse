// Renders the MSIX tile and logo images from Assets/eyebrowse.svg into Packaging/Images.
// Needs Node and Playwright with Chromium:  node Packaging/render-images.js
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

const root = path.join(__dirname, '..');
const svg = fs.readFileSync(path.join(root, 'Assets', 'eyebrowse.svg'), 'utf8');
const out = path.join(__dirname, 'Images');

// [file name, width, height, icon size]: icon size < width centres the icon on a wider tile.
const images = [];
for (const scale of [100, 200, 400]) {
  const s = scale / 100;
  images.push([`StoreLogo.scale-${scale}.png`, 50 * s, 50 * s, 50 * s]);
  images.push([`Square44x44Logo.scale-${scale}.png`, 44 * s, 44 * s, 44 * s]);
  images.push([`Square150x150Logo.scale-${scale}.png`, 150 * s, 150 * s, 110 * s]);
  images.push([`Wide310x150Logo.scale-${scale}.png`, 310 * s, 150 * s, 110 * s]);
}
// Taskbar, Start list and file icons ask for exact pixel sizes.
for (const size of [16, 24, 32, 48, 256])
  images.push([`Square44x44Logo.targetsize-${size}_altform-unplated.png`, size, size, size]);

(async () => {
  fs.mkdirSync(out, { recursive: true });
  const browser = await chromium.launch();
  const page = await browser.newPage();
  for (const [name, width, height, icon] of images) {
    await page.setViewportSize({ width, height });
    await page.setContent(`<html><body style="margin:0;background:transparent;display:flex;
        align-items:center;justify-content:center;width:${width}px;height:${height}px">
        ${svg.replace('width="256" height="256"', `width="${icon}" height="${icon}"`)}</body></html>`);
    await page.screenshot({ path: path.join(out, name), omitBackground: true, clip: { x: 0, y: 0, width, height } });
  }
  await browser.close();
  console.log(`Rendered ${images.length} images into ${out}`);
})();
