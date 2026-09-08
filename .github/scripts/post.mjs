import { execFileSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import {
  buildRecord,
  createPost,
  externalEmbed,
  graphemes,
  login,
  postedUris,
  truncate,
  uploadThumb
} from "./bluesky.mjs";

const BASE_URL = "https://poems.culturing.net";
const SITE_NAME = "culturing";
const FEED_PATH = "docs/feed.xml";
const SITEMAP_PATH = "docs/sitemap.xml";
const TAGS_PATH = "Other/tags.tsv";
const SKIP_PATH = "Other/bluesky-skip.txt";
const THUMB_PATH = "docs/og-image.png";

// Poems published before this are not posted, and no post older than it is read back.
const SINCE = "2026-09-07";

const POST_LIMIT = 300;
const TAG_COUNT = 2;
const MAX_DIGESTS = 3;
const MIN_RATING = 0;

const ZERO_SHA = "0".repeat(40);
const LIVE_TIMEOUT_MS = 300_000;
const LIVE_INTERVAL_MS = 10_000;
const POEM_PATH = /^\/(\d{4})\/(\d{2})(?:\/(\d{2}))?\/([^/]+)\/$/;

function git(...args) {
  return execFileSync("git", args, { encoding: "utf8", maxBuffer: 64 * 1024 * 1024 });
}

function gitShow(ref, path) {
  try {
    return git("show", `${ref}:${path}`);
  } catch {
    return "";
  }
}

function revExists(ref) {
  try {
    git("rev-parse", "--verify", `${ref}^{commit}`);
    return true;
  } catch {
    return false;
  }
}

function unescapeXml(text) {
  return text
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&#(\d+);/g, (_, code) => String.fromCodePoint(Number(code)))
    .replace(/&amp;/g, "&");
}

function element(item, name) {
  const match = item.match(new RegExp(`<${name}[^>]*>([\\s\\S]*?)</${name}>`));
  return match ? unescapeXml(match[1]).trim() : "";
}

function parseFeed(xml) {
  return [...xml.matchAll(/<item>([\s\S]*?)<\/item>/g)].map(([, item]) => ({
    title: element(item, "title"),
    url: element(item, "guid"),
    description: element(item, "description"),
    pubDate: element(item, "pubDate")
  }));
}

const pathOf = url => url.slice(BASE_URL.length);
const datePathOf = url => pathOf(url).replace(/[^/]+\/$/, "");

// Sitemap order is build order, which keeps a day's poems in file order; the sort puts the
// days themselves oldest first.
function poemUrls() {
  const entries = [];
  for (const [, loc] of readFileSync(SITEMAP_PATH, "utf8").matchAll(/<loc>([^<]+)<\/loc>/g)) {
    const match = pathOf(loc).match(POEM_PATH);
    if (!match || /^\d+$/.test(match[4])) continue;
    entries.push({ url: loc, date: `${match[1]}-${match[2]}-${match[3] ?? "01"}`, index: entries.length });
  }

  return entries
    .filter(entry => entry.date >= SINCE)
    .sort((a, b) => a.date.localeCompare(b.date) || a.index - b.index)
    .map(entry => entry.url);
}

function poemContent(url) {
  const html = readFileSync(`docs${pathOf(url)}index.html`, "utf8");
  const meta = name => {
    const match = html.match(new RegExp(`<meta property="${name}" content="([^"]*)"`));
    return match ? unescapeXml(match[1]) : "";
  };
  return {
    title: meta("og:title").replace(new RegExp(` \\| poems by ${SITE_NAME}$`), ""),
    description: meta("og:description")
  };
}

function loadTags() {
  const tags = new Map();
  if (!existsSync(TAGS_PATH)) return tags;

  for (const line of readFileSync(TAGS_PATH, "utf8").split(/\r?\n/)) {
    if (!line || line.startsWith("#")) continue;
    const [urlPath, list] = line.split("\t");
    if (!list) continue;
    tags.set(urlPath.trim(), list.split(",").map(tag => tag.trim()).filter(Boolean));
  }
  return tags;
}

function loadSkips() {
  if (!existsSync(SKIP_PATH)) return new Set();
  return new Set(
    readFileSync(SKIP_PATH, "utf8")
      .split(/\r?\n/)
      .map(line => line.trim())
      .filter(line => line && !line.startsWith("#"))
  );
}

const hashtag = tag => tag.replace(/[^a-z0-9]/gi, "");

// Poem pages carry no rating; the day archive wraps each link in its rated- class.
function ratingOf(url) {
  const archive = `docs${datePathOf(url)}index.html`;
  if (!existsSync(archive)) return 0;

  const escaped = pathOf(url).replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const match = readFileSync(archive, "utf8").match(new RegExp(`class="rated-(\\d)"><a href="${escaped}"`));
  return match ? Number(match[1]) : 0;
}

function dateLabel(date) {
  return new Date(date).toLocaleDateString("en-GB", {
    day: "2-digit",
    month: "long",
    year: "numeric",
    timeZone: "UTC"
  });
}

function topTags(lists, count) {
  const tally = new Map();
  for (const list of lists) {
    for (const tag of list) tally.set(tag, (tally.get(tag) ?? 0) + 1);
  }
  return [...tally.entries()]
    .sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]))
    .slice(0, count)
    .map(([tag]) => tag);
}

// The body takes whatever room the heading, hashtags and url leave inside the grapheme limit.
function compose({ heading, body, tags, url }) {
  const tagLine = tags.length ? `${tags.map(tag => `#${tag}`).join(" ")}\n` : "";
  const frame = `${heading}\n\n\n\n${tagLine}${url}`;
  return `${heading}\n\n${truncate(body, POST_LIMIT - graphemes(frame))}\n\n${tagLine}${url}`;
}

function poemPost({ url, title, description, tags }, thumb) {
  const hashtags = tags.slice(0, TAG_COUNT).map(hashtag).filter(Boolean);
  return buildRecord({
    text: compose({ heading: title, body: description, tags: hashtags, url }),
    linkUri: url,
    tags: hashtags,
    embed: externalEmbed({
      uri: url,
      title: `${title} | a poem by ${SITE_NAME}`,
      description,
      thumb
    })
  });
}

function digestPost(items, thumb) {
  const label = dateLabel(items[0].pubDate);
  const url = BASE_URL + datePathOf(items[0].url);
  const noun = items.length === 1 ? "poem" : "poems";
  const hashtags = topTags(items.map(item => item.tags ?? []), TAG_COUNT).map(hashtag).filter(Boolean);

  return buildRecord({
    text: compose({
      heading: `${items.length} new ${noun} · ${label}`,
      body: items.map(item => item.title).join(" · "),
      tags: hashtags,
      url
    }),
    linkUri: url,
    tags: hashtags,
    embed: externalEmbed({
      uri: url,
      title: `${label} | poems by ${SITE_NAME}`,
      description: `The ${items.length} ${noun} ${SITE_NAME} published on ${label}.`,
      thumb
    })
  });
}

async function openSession() {
  const identifier = process.env.BLUESKY_HANDLE;
  const password = process.env.BLUESKY_APP_PASSWORD;
  if (!identifier || !password) throw new Error("BLUESKY_HANDLE and BLUESKY_APP_PASSWORD must be set");
  return login(identifier, password);
}

function report(record) {
  const rule = "-".repeat(60);
  console.log(`${rule}\n${record.text}\n${rule}`);
  console.log(`${graphemes(record.text)}/${POST_LIMIT} graphemes`);
  for (const facet of record.facets ?? []) {
    const feature = facet.features[0];
    const kind = feature.$type.split("#")[1];
    console.log(`  facet ${facet.index.byteStart}-${facet.index.byteEnd} ${kind} ${feature.uri ?? feature.tag}`);
  }
  console.log(`  card ${record.embed.external.uri}`);
}

async function waitForLive(url) {
  const deadline = Date.now() + LIVE_TIMEOUT_MS;
  while (Date.now() < deadline) {
    try {
      if ((await fetch(url, { method: "HEAD", cache: "no-store" })).ok) return true;
    } catch {
      // Pages has not published the path yet.
    }
    await new Promise(resolve => setTimeout(resolve, LIVE_INTERVAL_MS));
  }
  return false;
}

function skipRequested(before, after) {
  if (process.env.GITHUB_EVENT_NAME === "workflow_dispatch") return false;
  try {
    return /\[skip (bsky|bluesky)\]/i.test(git("log", `${before}..${after}`, "--format=%B"));
  } catch {
    return false;
  }
}

const isDryRun = args => args["dry-run"] === true || process.env.DRY_RUN === "true";
const sinceIso = () => `${SINCE}T00:00:00.000Z`;

async function digest(args) {
  const dryRun = isDryRun(args);
  const after = args.after ?? process.env.GITHUB_SHA ?? "HEAD";

  let before = args.before ?? process.env.GITHUB_EVENT_BEFORE ?? "";
  if (!before || before === ZERO_SHA || !revExists(before)) before = `${after}^`;
  console.log(`digest ${before}..${after}${dryRun ? " (dry run)" : ""}`);

  if (skipRequested(before, after)) {
    console.log("skip marker in commit message; nothing posted");
    return;
  }

  const known = new Set(parseFeed(gitShow(before, FEED_PATH)).map(item => item.url));
  const fresh = parseFeed(gitShow(after, FEED_PATH)).filter(item => !known.has(item.url));
  if (fresh.length === 0) {
    console.log("no new poems in the feed");
    return;
  }

  const tags = loadTags();
  for (const item of fresh) item.tags = tags.get(pathOf(item.url)) ?? [];

  // The feed runs newest first and keeps file order within a day, so only the days reverse.
  const days = new Map();
  for (const item of fresh) {
    const key = datePathOf(item.url);
    if (!days.has(key)) days.set(key, []);
    days.get(key).push(item);
  }
  const groups = [...days.values()].reverse();
  if (groups.length > MAX_DIGESTS) {
    console.log(`${groups.length} days of new poems; posting the first ${MAX_DIGESTS}`);
  }

  const session = dryRun ? null : await openSession();
  const posted = session ? await postedUris(session, { since: sinceIso() }) : new Set();

  for (const items of groups.slice(0, MAX_DIGESTS)) {
    const url = BASE_URL + datePathOf(items[0].url);
    if (posted.has(url)) {
      console.log(`already posted ${url}`);
      continue;
    }

    const thumb = session ? await uploadThumb(session, THUMB_PATH) : null;
    const record = digestPost(items, thumb);
    report(record);
    if (dryRun) continue;

    if (!(await waitForLive(url))) console.log(`warning: ${url} did not go live within 5 minutes`);
    console.log(`posted ${await createPost(session, record)}`);
  }
}

async function drip(args) {
  const dryRun = isDryRun(args);
  const session = dryRun ? null : await openSession();
  const posted = session ? await postedUris(session, { since: sinceIso() }) : new Set();
  const skipped = loadSkips();
  const tags = loadTags();

  const next = poemUrls().find(url => {
    if (posted.has(url) || skipped.has(pathOf(url))) return false;
    return ratingOf(url) >= MIN_RATING;
  });

  if (!next) {
    console.log("nothing left to post");
    return;
  }

  const entry = { url: next, ...poemContent(next), tags: tags.get(pathOf(next)) ?? [] };
  const thumb = session ? await uploadThumb(session, THUMB_PATH) : null;
  const record = poemPost(entry, thumb);
  report(record);
  if (dryRun) return;

  console.log(`posted ${await createPost(session, record)}`);
}

function parseArgs(argv) {
  const args = {};
  for (let i = 0; i < argv.length; i++) {
    if (!argv[i].startsWith("--")) continue;
    const key = argv[i].slice(2);
    const value = argv[i + 1];
    if (value && !value.startsWith("--")) {
      args[key] = value;
      i++;
    } else {
      args[key] = true;
    }
  }
  return args;
}

const [command, ...rest] = process.argv.slice(2);
const commands = { digest, drip };

if (!commands[command]) {
  console.error("usage: post.mjs digest|drip [--dry-run] [--before <sha>] [--after <sha>]");
  process.exit(1);
}

await commands[command](parseArgs(rest));
