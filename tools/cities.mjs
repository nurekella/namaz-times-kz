// Regenerates cities.json from api.muftyat.kz. Run from repo root: node tools/cities.mjs
const pages = 57, all = [];
for (let i = 1; i <= pages; i += 8) {
  const batch = await Promise.all(Array.from({ length: Math.min(8, pages - i + 1) }, (_, k) =>
    fetch(`https://api.muftyat.kz/cities/?page=${i + k}`).then(r => r.json())));
  batch.forEach(b => all.push(...b.results));
}
const rows = all.map(c => [c.title, c.region ?? "", c.district ?? "", c.lat, c.lng]);
const city = t => /қаласы/.test(t[0]) ? 0 : 1;
rows.sort((a, b) => city(a) - city(b) || a[0].localeCompare(b[0], "kk"));
const fs = await import("fs");
fs.writeFileSync("cities.json", JSON.stringify(rows));
console.log(all.length, new Set(all.map(c => c.id)).size, rows.slice(0, 5));
