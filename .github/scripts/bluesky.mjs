import { readFile } from "node:fs/promises";

const ENTRYWAY = "https://bsky.social";
const POST_COLLECTION = "app.bsky.feed.post";
const WALK_TIMEOUT_MS = 300_000;
const segmenter = new Intl.Segmenter("en", { granularity: "grapheme" });

async function xrpc(host, method, nsid, { body, query, jwt, contentType } = {}) {
  const url = new URL(`/xrpc/${nsid}`, host);
  for (const [key, value] of Object.entries(query ?? {})) url.searchParams.set(key, value);

  const headers = {};
  let payload;
  if (jwt) headers.Authorization = `Bearer ${jwt}`;
  if (body instanceof Uint8Array) {
    headers["Content-Type"] = contentType;
    payload = body;
  } else if (body !== undefined) {
    headers["Content-Type"] = "application/json";
    payload = JSON.stringify(body);
  }

  const response = await fetch(url, { method, headers, body: payload });
  if (!response.ok) throw new Error(`${nsid} ${response.status}: ${await response.text()}`);
  return response.json();
}

// The entryway issues the session; repo writes go to the host named in the did document.
export async function login(identifier, password) {
  const data = await xrpc(ENTRYWAY, "POST", "com.atproto.server.createSession", {
    body: { identifier, password }
  });
  const pds = data.didDoc?.service?.find(s => s.type === "AtprotoPersonalDataServer")?.serviceEndpoint;
  return { did: data.did, jwt: data.accessJwt, host: pds ?? ENTRYWAY };
}

export async function uploadThumb(session, filePath) {
  const bytes = await readFile(filePath);
  const { blob } = await xrpc(session.host, "POST", "com.atproto.repo.uploadBlob", {
    body: bytes,
    contentType: "image/png",
    jwt: session.jwt
  });
  return blob;
}

export function graphemes(text) {
  let count = 0;
  for (const _ of segmenter.segment(text)) count++;
  return count;
}

export function truncate(text, limit) {
  if (graphemes(text) <= limit) return text;
  if (limit <= 1) return "";
  let kept = "";
  let count = 0;
  for (const { segment } of segmenter.segment(text)) {
    if (count >= limit - 1) break;
    kept += segment;
    count++;
  }
  return kept.replace(/[\s,;:.\-—]+$/, "") + "…";
}

// byteStart and byteEnd count utf-8 bytes, not utf-16 code units.
function facetAt(text, needle, feature) {
  const at = text.indexOf(needle);
  if (at < 0) return null;
  return {
    index: {
      byteStart: Buffer.byteLength(text.slice(0, at), "utf8"),
      byteEnd: Buffer.byteLength(text.slice(0, at + needle.length), "utf8")
    },
    features: [feature]
  };
}

export function buildFacets(text, { linkUri, tags = [] }) {
  const facets = [];
  if (linkUri) {
    const facet = facetAt(text, linkUri, { $type: "app.bsky.richtext.facet#link", uri: linkUri });
    if (facet) facets.push(facet);
  }
  for (const tag of tags) {
    const facet = facetAt(text, `#${tag}`, { $type: "app.bsky.richtext.facet#tag", tag });
    if (facet) facets.push(facet);
  }
  return facets;
}

export function externalEmbed({ uri, title, description, thumb }) {
  const external = { uri, title, description };
  if (thumb) external.thumb = thumb;
  return { $type: "app.bsky.embed.external", external };
}

export function buildRecord({ text, linkUri, tags = [], embed }) {
  const record = {
    $type: POST_COLLECTION,
    text,
    createdAt: new Date().toISOString(),
    langs: ["en"]
  };
  const facets = buildFacets(text, { linkUri, tags });
  if (facets.length) record.facets = facets;
  if (tags.length) record.tags = tags.slice(0, 8);
  if (embed) record.embed = embed;
  return record;
}

export async function createPost(session, record) {
  const { uri } = await xrpc(session.host, "POST", "com.atproto.repo.createRecord", {
    body: { repo: session.did, collection: POST_COLLECTION, record },
    jwt: session.jwt
  });
  return uri;
}

// Every url this account has linked back to `since`, from both the card and the link facets.
// Records come newest first, so a record older than `since` ends the walk, and so does a page
// that carries no cursor. A partial set is indistinguishable from a complete one and would
// re-post whatever it missed, so the walk throws rather than return one: a cursor that repeats
// is a pager going nowhere, and the deadline catches one that advances forever.
export async function postedUris(session, { since, timeoutMs = WALK_TIMEOUT_MS } = {}) {
  const uris = new Set();
  const seen = new Set();
  const deadline = Date.now() + timeoutMs;
  let cursor;

  for (;;) {
    const query = { repo: session.did, collection: POST_COLLECTION, limit: "100" };
    if (cursor) query.cursor = cursor;
    const data = await xrpc(session.host, "GET", "com.atproto.repo.listRecords", { query, jwt: session.jwt });

    for (const record of data.records ?? []) {
      const card = record.value?.embed?.external?.uri;
      if (card) uris.add(card);
      for (const facet of record.value?.facets ?? []) {
        for (const feature of facet.features ?? []) {
          if (feature.uri) uris.add(feature.uri);
        }
      }
      if (since && record.value?.createdAt < since) return uris;
    }

    cursor = data.cursor;
    if (!cursor || (data.records ?? []).length === 0) return uris;
    if (seen.has(cursor)) throw new Error(`the post walk stalled: cursor ${cursor} came back twice`);
    seen.add(cursor);
    if (Date.now() > deadline) throw new Error(`the post walk passed ${timeoutMs}ms over ${seen.size} pages`);
  }
}
