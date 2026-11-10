// Builds icons.json from the free Hugeicons set (Stroke Rounded, MIT, https://hugeicons.com).
// Usage: npm pack @hugeicons/core-free-icons && tar xzf hugeicons-core-free-icons-*.tgz
//        node tools/icons.mjs package/dist/cjs
// icons.json: { "Name": [["s"|"f", "<svg path d on a 24x24 grid>"], ...] } — "s" is stroked (1.5 px, round
// caps and joins), "f" is filled. Circles, rects and lines are turned into paths so the app parses one syntax.
import fs from 'fs';
import path from 'path';
import { createRequire } from 'module';

const require = createRequire(import.meta.url);
const dir = path.resolve(process.argv[2]);
const out = path.join(path.dirname(new URL(import.meta.url).pathname.replace(/^\/(\w:)/, '$1')), '..', 'icons.json');

// Every icon the app draws. Keep in step with Icons.Map in Icons.cs.
const names = [
  'Quran01', 'Tasbih', 'Dua', 'Allah', 'PrayerRug01', 'History', 'Zakat', 'Calendar03', 'MoonStar', 'News01',
  'InformationCircle', 'Settings01', 'Mosque01', 'Mosque02', 'Clock01', 'Time04', 'Location01', 'Moon02',
  'ArrowDown01', 'ArrowUp01', 'ArrowLeft01', 'ArrowRight01', 'UnfoldMore', 'Add01', 'MoreHorizontal', 'Pin',
  'Search01', 'Refresh', 'Tick02', 'Maximize01', 'VolumeHigh', 'VolumeMute02', 'Translate', 'SunMoon', 'Droplet',
  'Power', 'NotificationOff01', 'Notification01', 'Eye', 'ArrowReloadHorizontal', 'Download01', 'Upload01',
  'LinkSquare02', 'Archive02', 'Bug01', 'Menu01', 'Cancel01', 'Bookmark02', 'TextFont', 'ZoomIn', 'Sun03',
  'Logout03', 'AppWindow', 'GridView', 'Flash',
];

const n = v => +(+v).toFixed(3);
function toPath(tag, a) {
  switch (tag) {
    case 'path': return a.d;
    case 'circle': {
      const [cx, cy, r] = [n(a.cx), n(a.cy), n(a.r)];
      return `M${cx - r} ${cy}A${r} ${r} 0 1 0 ${cx + r} ${cy}A${r} ${r} 0 1 0 ${cx - r} ${cy}Z`;
    }
    case 'ellipse': {
      const [cx, cy, rx, ry] = [n(a.cx), n(a.cy), n(a.rx), n(a.ry)];
      return `M${cx - rx} ${cy}A${rx} ${ry} 0 1 0 ${cx + rx} ${cy}A${rx} ${ry} 0 1 0 ${cx - rx} ${cy}Z`;
    }
    case 'rect': {
      const [x, y, w, h] = [n(a.x ?? 0), n(a.y ?? 0), n(a.width), n(a.height)];
      const r = Math.min(n(a.rx ?? a.ry ?? 0), w / 2, h / 2);
      if (!r) return `M${x} ${y}H${x + w}V${y + h}H${x}Z`;
      return `M${x + r} ${y}H${x + w - r}A${r} ${r} 0 0 1 ${x + w} ${y + r}V${y + h - r}A${r} ${r} 0 0 1 ${x + w - r} ${y + h}` +
        `H${x + r}A${r} ${r} 0 0 1 ${x} ${y + h - r}V${y + r}A${r} ${r} 0 0 1 ${x + r} ${y}Z`;
    }
    case 'line': return `M${a.x1} ${a.y1}L${a.x2} ${a.y2}`;
    case 'polyline': return 'M' + a.points.trim().replace(/\s+/g, ' ');
    case 'polygon': return 'M' + a.points.trim().replace(/\s+/g, ' ') + 'Z';
    default: throw new Error('unknown element ' + tag);
  }
}

const result = {};
for (const name of names) {
  const icon = require(path.join(dir, `${name}Icon.js`));
  result[name] = icon.map(([tag, a]) => [a.fill && a.fill !== 'none' && !a.stroke ? 'f' : 's', toPath(tag, a)]);
}
fs.writeFileSync(out, JSON.stringify(result));
console.log(`${names.length} icons -> ${out}`);
