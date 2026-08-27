/* Generates the navbar's weathering: a fractured lintel with a vine rooted in the
   fracture. Run `node Tools/weathering.js` and paste the block it prints over the
   generated section at the top of Styles/navbar.css.

   Why generated rather than hand-drawn: the geometry is a few hundred coordinates
   and has to stay reproducible. Every random draw comes from one seeded PRNG, so
   the same SEED always yields the same bar -- change it only to reroll the whole
   thing deliberately.

   Why background images rather than one inline SVG: the navbar is a flex row whose
   width is the viewport's, and an SVG stretched to that width distorts every leaf.
   Backgrounds positioned by percentage never scale and never overflow their box, so
   the growth spreads with the bar while each leaf keeps its shape.

   The one number everything depends on:

     BASE is the distance from the bottom of every generated image to the vine's
     stem. navbar.css hangs the layer at `bottom: -BASE`, so a stem drawn at
     H - BASE lands on the navbar's lower edge in all five images at once, and the
     leaves that lie across the stem hang into the BASE px underneath it. Change it
     here and change `bottom` and `height` on .navbar::after to match. */

const SEED = 6180;
const BAR  = 36;   // 0.5rem padding x2 + the 1.25rem line-height inherited from body
const BASE = 10;   // stem sits this far above each image's bottom edge; a leaf lying
                   // across the stem needs half its length of clearance under it, and at
                   // 6 the largest of them were being cut off square by the image edge
const H    = BAR + BASE + 2;   // 2px of slack above the bar for the tallest tendril

/* Green is the first hue on this site besides the rose in common.css. The two
   alternates below were both drawn and both work: GREY reads as wrought iron and
   keeps the palette strictly neutral, ROSE reads as the growth dried out. */
const GREEN = { stem: '#5c6a51', deep: '#414d3b', leaf: '#4d5b44', lit: '#78866b' };
const GREY  = { stem: '#3a3a3a', deep: '#2b2b2b', leaf: '#343434', lit: '#4e4e4e' };
const ROSE  = { stem: '#7c565a', deep: '#5a3f42', leaf: '#6b4b4f', lit: '#9c6c72' };
const C = GREEN;

/* -- deterministic noise ---------------------------------------------------- */

function mulberry32(a) {
  return function () {
    a |= 0; a = a + 0x6D2B79F5 | 0;
    let t = Math.imul(a ^ a >>> 15, 1 | a);
    t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
    return ((t ^ t >>> 14) >>> 0) / 4294967296;
  };
}

const n = v => Math.round(v * 10) / 10;

/* Catmull-Rom through the points, emitted as cubics. Vines curve; they do not hinge. */
function smooth(pts) {
  if (pts.length < 2) return '';
  let d = `M${n(pts[0][0])},${n(pts[0][1])}`;
  for (let i = 0; i < pts.length - 1; i++) {
    const p0 = pts[i - 1] || pts[i], p1 = pts[i], p2 = pts[i + 1], p3 = pts[i + 2] || pts[i + 1];
    d += `C${n(p1[0] + (p2[0] - p0[0]) / 6)},${n(p1[1] + (p2[1] - p0[1]) / 6)}` +
         ` ${n(p2[0] - (p3[0] - p1[0]) / 6)},${n(p2[1] - (p3[1] - p1[1]) / 6)}` +
         ` ${n(p2[0])},${n(p2[1])}`;
  }
  return d;
}

/* -- the parts -------------------------------------------------------------- */

// A slimmer almond than the first draft: at 0.52 half-width the silhouettes read
// as berries once they overlap.
const LEAF = 'M0,0C0.28,-0.36 0.76,-0.42 1,0C0.76,0.42 0.28,0.36 0,0Z';

function leaf(x, y, size, deg, rnd, alpha = 1) {
  const fill = rnd() < 0.28 ? C.lit : C.leaf;
  const o = n((0.55 + rnd() * 0.35) * alpha);
  return `<path d='${LEAF}' fill='${fill}' opacity='${o}' transform='translate(${n(x)},${n(y)}) rotate(${Math.round(deg)}) scale(${n(size)})'/>`;
}

/* A tendril leaves the stem, climbs, and curls in on itself as it runs out of reach. */
function tendril(x, y, len, curlDir, rnd) {
  const pts = [[x, y]];
  let cx = x, cy = y, ang = -Math.PI / 2 + (rnd() - 0.5) * 0.7;
  const steps = 6, seg = len / steps;
  const curl = (0.13 + rnd() * 0.22) * curlDir;
  for (let i = 0; i < steps; i++) {
    ang += curl * (0.4 + i * 0.42);
    cx += Math.cos(ang) * seg;
    cy += Math.sin(ang) * seg;
    pts.push([cx, cy]);
  }
  return { d: smooth(pts), tip: pts[steps], mid: pts[3] };
}

/* Growth clustered around rootX and thinning with distance from it, because the
   seed entered at one place and has been working outward from it ever since. */
function growth(w, rootX, density, spread, rnd) {
  const y = H - BASE;
  let out = '';

  /* The stem thickens where the vine is oldest. Both of its ends fade to nothing
     over the outer 16%, because the continuous hairline underneath it is a
     repeating tile whose phase at any given viewport width is unknowable -- a hard
     end would show as a step where the two lines meet at slightly different
     heights, which is exactly what the first render did. Faded, the same mismatch
     reads as the stem thickening, which is what a vine does anyway. */
  const seg = [];
  for (let x = -2; x <= w + 2; x += 11) seg.push([x, y + Math.sin(x * 0.09) * 0.85 + (rnd() - 0.5) * 0.7]);
  out += `<defs><linearGradient id='s'>` +
         `<stop offset='0' stop-color='${C.stem}' stop-opacity='0'/>` +
         `<stop offset='0.16' stop-color='${C.stem}' stop-opacity='0.95'/>` +
         `<stop offset='0.84' stop-color='${C.stem}' stop-opacity='0.95'/>` +
         `<stop offset='1' stop-color='${C.stem}' stop-opacity='0'/></linearGradient></defs>`;
  out += `<path d='${smooth(seg)}' fill='none' stroke='url(%23s)' stroke-width='${n(1.1 + density * 0.7)}' stroke-linecap='round'/>`;
  const stemY = px => seg[Math.max(0, Math.min(seg.length - 1, Math.round((px + 2) / 11)))][1];

  /* 14px apart minimum. At 9 the tendrils overlapped into a single blobby mass
     that read as moss rather than as a vine. */
  const count = Math.round(2 + density * 7);
  const used = [];
  for (let i = 0; i < count; i++) {
    let tx = rootX + (rnd() - 0.35) * spread * 2;
    tx = Math.max(4, Math.min(w - 4, tx));
    if (used.some(u => Math.abs(u - tx) < 14)) continue;
    used.push(tx);

    const falloff = 1 - Math.min(1, Math.abs(tx - rootX) / spread);
    const len = (8 + rnd() * 15) * (0.45 + falloff * 0.55) * (0.6 + density * 0.6);
    const t = tendril(tx, stemY(tx), len, rnd() < 0.5 ? -1 : 1, rnd);
    out += `<path d='${t.d}' fill='none' stroke='${C.stem}' stroke-width='0.75' stroke-linecap='round' opacity='${n(0.5 + falloff * 0.4)}'/>`;

    // one leaf at the tip, and only sometimes a second one back down the tendril
    out += leaf(t.tip[0], t.tip[1], (6.5 + rnd() * 4) * (0.7 + density * 0.4), rnd() * 360, rnd, 0.5 + falloff * 0.5);
    if (rnd() < 0.45 + density * 0.3) {
      out += leaf(t.mid[0], t.mid[1], (6 + rnd() * 3.5) * (0.7 + density * 0.4), rnd() * 360, rnd, 0.5 + falloff * 0.5);
    }
  }

  // leaves lying along the stem itself, thickest at the root
  const base = Math.round(1 + density * 4);
  const laid = [];
  for (let k = 0; k < base; k++) {
    let bx = rootX + (rnd() - 0.4) * spread * 1.6;
    bx = Math.max(2, Math.min(w - 2, bx));
    if (laid.some(u => Math.abs(u - bx) < 12)) continue;
    laid.push(bx);
    out += leaf(bx, stemY(bx) + (rnd() - 0.5) * 2, (6.5 + rnd() * 4.5) * (0.7 + density * 0.4),
                (rnd() < 0.5 ? 150 : 30) + (rnd() - 0.5) * 60, rnd, 1);
  }
  return out;
}

/* Midpoint displacement: a fracture is self-similar at every scale, which is why
   it never looks right drawn by hand. */
function crackPoints(a, b, rough, rnd, depth) {
  let pts = [a, b];
  for (let d = 0; d < depth; d++) {
    const next = [pts[0]];
    for (let i = 0; i < pts.length - 1; i++) {
      const p = pts[i], q = pts[i + 1];
      const dx = q[0] - p[0], dy = q[1] - p[1], len = Math.hypot(dx, dy) || 1;
      const off = (rnd() - 0.5) * rough * Math.pow(0.52, d);
      next.push([(p[0] + q[0]) / 2 - dy / len * off, (p[1] + q[1]) / 2 + dx / len * off], q);
    }
    pts = next;
  }
  return pts;
}

const poly = pts => pts.map((p, i) => `${i ? 'L' : 'M'}${n(p[0])},${n(p[1])}`).join('');

/* Drawn twice: the opening in shadow, and half a pixel up-left the chipped edge
   catching what light there is. That offset is the whole illusion. */
function fracture(enterX, exitX, rnd) {
  const main = crackPoints([enterX, 2], [exitX, H - BASE], 8, rnd, 5);
  const paths = [main];

  for (let i = 0; i < 2; i++) {
    const at = main[Math.floor(main.length * (0.28 + rnd() * 0.42))];
    const len = 12 + rnd() * 16;
    const ang = (rnd() < 0.5 ? 0.6 : 2.4) + (rnd() - 0.5) * 0.5;
    paths.push(crackPoints(at, [at[0] + Math.cos(ang) * len, at[1] + Math.abs(Math.sin(ang)) * len * 0.7], 4, rnd, 4));
  }

  let out = '';
  paths.forEach((p, i) => {
    const w = i === 0 ? 1.0 : 0.65;
    out += `<path d='${poly(p.map(q => [q[0] - 0.6, q[1] - 0.6]))}' fill='none' stroke='rgba(245,245,245,0.11)' stroke-width='${n(w * 0.6)}' stroke-linejoin='round'/>`;
    out += `<path d='${poly(p)}' fill='none' stroke='%23000' stroke-width='${w}' stroke-linejoin='round' opacity='0.92'/>`;
  });

  /* A fracture opens wider the further it has run, so the lower stretch is drawn
     again over itself at nearly twice the width. Widening the whole path instead
     just reads as a drawn line. */
  const lower = main.slice(Math.floor(main.length * 0.42));
  out += `<path d='${poly(lower)}' fill='none' stroke='%23000' stroke-width='1.8' stroke-linejoin='round' stroke-linecap='round' opacity='0.9'/>`;
  out += `<path d='${poly(lower.map(q => [q[0] - 0.7, q[1] - 0.7]))}' fill='none' stroke='rgba(245,245,245,0.13)' stroke-width='0.7' stroke-linejoin='round'/>`;

  // spalling: where the stone let go beside the opening
  for (let j = 0; j < 6; j++) {
    const at = main[Math.floor(rnd() * main.length)];
    out += `<ellipse cx='${n(at[0] + (rnd() - 0.5) * 9)}' cy='${n(at[1] + (rnd() - 0.5) * 5)}' rx='${n(0.8 + rnd() * 2)}' ry='${n(0.6 + rnd() * 1.3)}' fill='%23000' opacity='0.55'/>`;
  }
  return out;
}

/* -- assembly --------------------------------------------------------------- */

const rnd = mulberry32(SEED);

/* Inside a quoted CSS url() only three characters actually have to be escaped:
   the closing quote, %, and # (which would start a fragment). Angle brackets are
   left raw -- encoding them as %3C/%3E is the usual habit and it triples the size
   of every tag for nothing. Attributes are single-quoted throughout so the double
   quote never appears at all. */
function svg(w, h, body) {
  const s = `<svg xmlns='http://www.w3.org/2000/svg' width='${w}' height='${h}' viewBox='0 0 ${w} ${h}'>${body}</svg>`;
  return 'url("data:image/svg+xml,' + s
    .replace(/%(?!23)/g, '%25')   // %23 is already the encoded # in the fills above
    .replace(/"/g, '%22')
    .replace(/#/g, '%23') + '")';
}

/* The stem is the rule the navbar never had. One tile, seamless because the wave's
   period divides the tile width exactly. */
function stemTile() {
  const w = 64, h = BASE * 2;
  const pts = [];
  for (let x = 0; x <= w; x += 8) pts.push([x, BASE + Math.sin((x / w) * Math.PI * 2) * 1.1]);
  return svg(w, h, `<path d='${smooth(pts)}' fill='none' stroke='${C.deep}' stroke-width='0.9' stroke-linecap='round' opacity='0.85'/>`);
}

/* Stone: a slab a shade above the paper, with grain. Tiled, so it costs one tile
   whatever the viewport does. */
function grainTile() {
  return svg(180, 180,
    `<filter id='g'><feTurbulence type='fractalNoise' baseFrequency='0.7' numOctaves='4' seed='3'/>` +
    `<feColorMatrix type='saturate' values='0'/></filter>` +
    `<rect width='180' height='180' filter='url(%23g)' opacity='0.055'/>`);
}

/* The fracture and the growth share one image, which is the point of the whole
   design: the vine is not near the crack, it is in it. Rooting them in separate
   layers would let them drift apart at some viewport width and the causation --
   the stone failed, so the seed got in -- would quietly stop reading. */
const ROOT_W = 182, ROOT_X = 76;
const root = svg(ROOT_W, H, fracture(120, ROOT_X, rnd) + growth(ROOT_W, ROOT_X, 1.3, 48, rnd));

/* Three more clusters carry the growth rightward, each thinner than the last.
   Percentages, not pixels, so the spacing opens with the viewport. */
const mid    = svg(132, H, growth(132, 60, 0.62, 56, rnd));
const late   = svg(120, H, growth(120, 52, 0.38, 52, rnd));

/* The runner: one growth that ignores the falloff entirely and reaches past
   everything else, out where the bar is otherwise still just a wall. Asymmetry is
   the whole of it -- a second one at the far left for balance would kill the
   effect outright. */
const runner = svg(96, H, growth(96, 40, 0.26, 46, rnd));

/* A finer flaw for the contact tablet in dropdown.css. It enters at the tablet's
   top edge, under the lintel's own fracture, and runs out partway down -- which is
   why the image is deliberately shorter than the menu it sits in: a crack that
   stopped exactly at the bottom edge would read as a drawn border.

   Drawn last on purpose. Every draw comes off one sequential PRNG, so anything
   inserted above this line would shift every number after it and redraw the whole
   navbar. New parts go here. */
function flaw(w, h, rnd) {
  /* Weighted well under the lintel's fracture. The bar is ~1300px of stone and the
     tablet about 110 -- the same stroke on both makes the small one read as a
     scratch drawn over the labels rather than a flaw inside them. */
  const main = crackPoints([w * 0.62, 0], [w * 0.34, h - 5], 6, rnd, 4);
  let out = `<path d='${poly(main.map(q => [q[0] - 0.5, q[1] - 0.5]))}' fill='none' stroke='rgba(245,245,245,0.075)' stroke-width='0.45' stroke-linejoin='round'/>`;
  out += `<path d='${poly(main)}' fill='none' stroke='%23000' stroke-width='0.6' stroke-linejoin='round' opacity='0.62'/>`;
  const at = main[Math.floor(main.length * 0.55)];
  const br = crackPoints(at, [at[0] + 10, at[1] + 8], 3, rnd, 3);
  out += `<path d='${poly(br)}' fill='none' stroke='%23000' stroke-width='0.45' stroke-linejoin='round' opacity='0.5'/>`;
  for (let j = 0; j < 3; j++) {
    const p = main[Math.floor(rnd() * main.length)];
    out += `<ellipse cx='${n(p[0] + (rnd() - 0.5) * 6)}' cy='${n(p[1] + (rnd() - 0.5) * 4)}' rx='${n(0.5 + rnd())}' ry='${n(0.4 + rnd() * 0.8)}' fill='%23000' opacity='0.32'/>`;
  }
  return out;
}
const stoneFlaw = svg(92, 72, flaw(92, 72, rnd));

const out = `/* -- generated: node Tools/weathering.js -- do not hand-edit ------------------
   The lintel and the vine rooted in its fracture. Geometry is seeded (${SEED}) and
   reproducible; regenerating with the same seed yields byte-identical output.
   --vine-root carries both the fracture and the growth in one image so the two can
   never drift apart. The layer hangs at bottom: -${BASE}px, which is what puts every
   stem exactly on the navbar's lower edge. */
.navbar {
    --stone-grain: ${grainTile()};
    --vine-stem: ${stemTile()};
    --vine-root: ${root};
    --vine-mid: ${mid};
    --vine-late: ${late};
    --vine-runner: ${runner};
    --stone-flaw: ${stoneFlaw};
}
/* -- end generated ----------------------------------------------------------- */
`;

process.stdout.write(out);
process.stderr.write(`\ngenerated ${out.length} bytes of CSS\n`);
