# Design notes

Why the site looks and behaves the way it does. Kept here rather than in comments, so the
code can be read as code. Source of record: `Source/` builds the site, `Styles/` sets it,
`Scripts/` adds the behaviours that need javascript.

---

## Palette

Ground and text are the originals: a neutral near-black and a neutral near-white. Greys stay
neutral with them — a violet-biased grey reads purple next to neutral text. One accent, rose,
and only where it belongs to the poems themselves: links inside a poem, and the breadcrumb
trail above an archive. Everything else — navbar, theme counts, ratings — is grey, so nothing
outside the writing competes with it. The rose is warmed slightly off the mauve it could be,
so it reads as red rather than violet against a ground with no hue of its own.

`--loam` is a later addition. It sits between paper and raise, for the rare
surface that needs to be off the ground without being lit. A `--whisper` grey below `--muted`
once held the opening lines, theme counts and index letters; it was dropped because a grey that
dim on this ground strained the eye, and those now take `--muted`.

`body` is a full-height flex column so `#container-parent` can claim exactly the leftover
space between the navbar and the licence badge; it replaced a hardcoded offset that only added
up in quirks mode.

## Typography

Alegreya sets the whole site: poems, titles, navbar and labels. It replaced a pairing of
Quattrocento for the writing and Cinzel for everything that labels it; the trials are the
"Poems by culturing font trials" canvas, and the Index and Chronology boards there are the
spec. One family with a true italic does the work the second face did: labels — the navbar's
rating filter, dates, year marks, the alphabet rail, letter marks — are set in italic and
muted, so a reader still learns once what labels the writing rather than being it, without an
inscriptional face that reads as a monument beside a poem.

Lowercase nav labels at 0.875rem and 0.06em, the wordmark at 0.94rem and 0.12em, since it is
read as a name rather than a word. Titles in a listing are 1.06rem, a step above body text,
as on the canvas. On screen the root is 17px rather than 16px, so every rem is a step larger
than on the canvas; print keeps 16px, so the book's pagination does not move.

The date and themes on a poem page are 0.8rem on screen. `<small><small>` alone takes Alegreya
to about 11px, which in grey on black is too small to read. The book keeps the nested smalls.

Alegreya's default figures are old-style. Columns of numbers — the year rails, the book's
contents — ask for lining, tabular figures, so a year sits level with the one above it.

Font build notes:

- The three files are static instances of upstream's variable fonts (`google/fonts`,
  `ofl/alegreya`, v2.009): roman at `wght` 400 and 700, italic at 400. Each is instanced with
  fontTools, `usWeightClass` set to match, then subset and saved as woff2 and woff. There is no
  bold italic; a bold inside an italic is synthesized.
- The subset is Latin-1, Latin Extended-A, Greek and polytonic Greek, and the usual
  punctuation, with layout features `kern liga ccmp locl mark mkmk onum lnum tnum pnum`.
  `lnum` and `tnum` are kept for the year rails and the book's contents.
- The `unicode-range` in `fonts.css` is the subset's own cmap, generated, so a declared range
  never promises a glyph the file lacks. Greek is in the same file; the separate Greek face
  that used to wear the Quattrocento name is gone.
- 1000 upem, unhinted, as upstream ships it.
- Cinzel stays in `fonts.css` for "Song of Sophia", which sets Wisdom's voice in it inline.
  Nothing preloads it.
- `Source/PdfBook.cs` names Alegreya for the PDF folios, which come from the installed font rather
  than from `fonts.css`. The build machine needs Alegreya installed: static Regular, Bold,
  Italic and Bold Italic instanced from the same variable fonts, unsubset.

## The navbar

The wordmark is the home link, so "home" leaves the row: a site's name in the top left corner
already means home, and the slot it frees is what "index" occupies.

Sticky, with the paper carried up with it — the bar sits on a transparent ground otherwise and
titles scroll through the labels. The fill is inset by the same 1rem the body pads by, so the
band and the content underneath share an edge. The fill is a gradient rather than flat for the
same reason the licence band at the foot is: a line of poetry passing under it fades out over
the last 12px instead of being cut through the middle of its letters. `z-index` keeps it over
the stem and the fixed pagination, both of which would otherwise cross it.

`--bar` (3.25rem) is the height anything scrolled to by anchor has to clear: 1rem of the bar's
own top padding over a 2.25rem row — a 1.25rem line box with 0.5rem above and below. It moves
only if the nav's padding or the body's line-height does. Landing an anchor exactly there puts
the heading flush against the bar, with whatever precedes it hidden behind the solid part
rather than showing through the 12px the fill spends fading out.

Hover on nav labels is grey, not rose: the accent belongs to the poems and the breadcrumbs, and
the navbar sits on every page.

On phones the wordmark centres over the row — five labels and a name do not fit on 390px, and
the name is the one that can afford to move. 0.9rem of vertical padding on a 0.69rem label is a
44px thumb target; the horizontal padding goes, since `space-between` spaces them. `#pdf` is
dropped: the 2MB book is not what anyone downloads on a phone, and it stays reachable from
every desktop page and from the browser menu. Two rows of navigation is most of a phone screen
before a poem starts, so the bar gives up the top edge and scrolls away with the page.

### The dropdown

A `<details>` rather than a hover panel. Hover was wrong twice over: a touch device has no
pointer and fires a synthetic one on tap, so the panel opened and shut on the same gesture; and
on a mouse it closed on the diagonal everyone actually moves in — out of the button and down
towards the item they want, which briefly leaves both. Click to open, click to close, the same
everywhere. `<details>` also brings the keyboard for free: `<summary>` is focusable and toggles
on Enter and Space, which the old `<div>` could not do at all.

The disclosure triangle is hidden twice, because the two engines hide it differently and
neither alone is enough. The label is the affordance; a marker beside it would be the only
glyph of its kind in the bar.

The panel is right-aligned — contact is the last item in the row, and a panel hanging left from
it would open off the edge of a narrow window. `--loam` rather than `--paper` so it is legibly
a surface in front of the page and not a hole in it. Buttons and links share the panel: the
filter's items do something rather than go somewhere, but they are the same object to a reader.

## Listings

### The stem

Each date is a node on one continuous line. The line is a border on the node itself rather than
on a wrapper, so consecutive nodes abut into a single stroke and a page with no dated groups —
a day archive, a one-year theme — simply has no stem, rather than a bare line down the side of
a flat list. The dot is filled with `--paper` rather than left hollow, because the border runs
behind it and would otherwise be drawn through its middle. `index.css` draws all of it;
the C# only emits the nodes.

### The opening line

The first line of verse, shown beside a title on hover. It is a lure back into the poem, not a
summary — `Description` already carries the summary — so it is cut hard at a length that still
fits the row on a 1440 screen. Out of flow entirely: it takes no width, so nothing moves when
it arrives and the column stays the width of its titles. A poem opening on an epigraph or a
blank line falls back to the first line carrying words.

It rides along only on the pages that list one column of titles — the home chronology and the
date archives — where there is an empty half of the page to hold it. The title index and the
theme hubs list three columns and have no empty half; there the line could only be read across
whatever is beside it, so those two pages go without.

`fit-content` on a row keeps it as wide as its title, so the hover target is the title and not
the empty half of the column beside it.

On phones the opening line and the theme counts are dropped: no pointer means no way to ask for
them, and both would sit there permanently invisible. The theme count comes back as ordinary
text, since the field alone is a coarser signal on one column.

### The rating ramp

A poem's rating is spent on the one thing already on the row rather than on a mark beside it.
Nothing is drawn: the poems worth reading first are simply the ones that come forward.

Three levels, because three is what the channel carries. Below about `#b0b0b0` a title reads as
dimmed rather than quiet, and `--ink` is the ceiling, which leaves 56 steps of grey to spend.
Five levels put 14 between neighbours, under the point where two greys a line apart can be told
apart; three put 28 there and are legible without comparing anything. A fourth level would have
to come from a second channel rather than a thinner slice of this one — `.rated-3` at `--ink`
and `font-weight: 700`, with the three below unchanged. `Poem.MaxRating` in `Content.cs` is the
scale; source files carry one leading asterisk per level.

The ramp is pinned to `--ink` at the top and `#b0b0b0` at the floor, so moving `--ink` moves the
middle step with it.

Listings only. A poem's own page has one title and no column to read it against, so it stays at
`--ink` whatever the rating. `index.css` loads after `common.css` and puts the accent hover
last, which is what lets a hover beat the rating underneath it.

`toc.css` carries the same ramp inverted for paper. Half those rows sit on a grey stripe, so
the floor stays a solid reading grey rather than going as light as the screen's does.

Every poem gets a `rated-N` class, unrated included: a stylesheet has no sensible default to
fall back on.

### The foot band

Listing pages carry a band at the foot of the window that mirrors the navbar: the same height
(`--bar`), the same paper, and the same 12px fade, turned to face up, so a title passing under
it fades rather than being cut. It replaced a 7rem gradient that ran down into `--void`; that
long dimming across the bottom of every list was tiring to read against. Fixed, so it is a
property of the window and not of the list, and the list keeps 7rem of padding below it, more
than the band is tall, so scrolling to the end always leaves the last title clear. On desktop
the padding is dropped for `/themes/`, where reserving it on a field that fits in one screen
would only hold the field off centre.

Listing pages underline nothing: they are almost entirely links, and underlining every one
turns the page into a grid of rules.

`#container-parent` uses `overflow-x: clip` rather than `hidden`, because a hidden axis would
make it a scroll container and the sticky year rail inside would start measuring itself against
that box instead of the window. The clip box is pulled a few pixels wider than the content it
holds, since the stem's dot sits outside the node it belongs to and on a phone the node begins
at the clip edge, which sliced every dot down the middle.

## The rating filter

`/best/` used to be a page of its own — a whole second tree built for one setting. It is now
one of three settings of a filter, and the url still resolves, redirecting to the chronology it
used to be a subset of.

Every listing row already carried its own rating, so the filter is a class on `<html>` and
nothing else. No page is fetched and no page is built per tier. A date group whose poems have
all gone would otherwise stay as a heading with nothing under it, so a group is hidden when
none of its rows survive.

The control sits beside the wordmark rather than among the links on the right, because it is
not a place you can go — it changes what every other link leads to, and belongs with the name
of the thing it is changing. Only the setting you are in is named on the bar; the other two are
inside the panel, the current one at full strength and the others as what you could switch to.
It is hidden until the script driving it has run, so it is never a control that does nothing;
without javascript the site is simply unfiltered, which is the honest fallback.

The poem itself never filters — you can read anything at any setting. The filter only decides
which sequence the chevrons walk.

## Pagination

Out of the navbar and into the margins the poem was leaving empty anyway. The gain is the
neighbour's name: "prev" told you a poem existed, this tells you which one, which is the
difference between paging and following a sequence. The chevron is drawn rather than typed — a
glyph would take the body face's weight and read as punctuation.

Fixed and vertically centred, so they sit level with the poem however long it is, and dark
enough at rest to be found only when looked for. The name is the reveal: the chevron is always
there, the title arrives when you approach it. Never wrapped — a two-line title in a 15% margin
is a paragraph in the corner of the page. `padding-right` buys room for the italic overhang: a
serif italic leans past its advance width and `overflow: hidden` cuts at the box edge, not at
the ink, so a title ending in one lost the tail of its last letter. `min-width: 0` lets the
label shrink below its content when a long title really does need the ellipsis.

All three tiers ship with every page and css shows the reader's — switching filter is a class
on `<html>`, never a request. `NeighbourChain` is written for positions rather than for
members, because a poem below the tier is still shown normally: an unrated poem read while
filtering to two stars still needs the two-star poems either side of it to page to. At two
stars the chain is 215 poems long, not 1132.

At the ends of a chain the slot becomes a span, so no link points nowhere; the chevron stays so
the margins keep their shape, at a grey that reads as absent rather than as unvisited. Only the
unfiltered pair carries an id — `review.js` drives the arrow keys off `#previous` and `#next`,
and three elements answering to one id is three answers to `getElementById`. `content.js` finds
the others by tier.

Below 1100px the margins are too narrow to hold a title beside the poem, so the label goes.
Below 512px there are no margins at all: the chevrons rejoin the flow under the poem, ordered
there rather than moved — they sit before `#container-parent` in the markup so a screen reader
meets the pagination with the poem, not after the licence badge. Both carry their titles, since
there is no hover on a phone and a chevron alone would never say where it goes.

## Poem pages

Verse wants more air between lines than a listing does: 1.62 rather than the 1.3 it was. A poem
read on a black ground needs more room, or the descenders of one line and
the ascenders of the next close the gap and the stanza reads as a block. Unitless, so it
follows a size change, and scoped to `.poem` because the about, 404 and faq pages load the same
stylesheet for prose that should keep body's tighter leading.

The date above the poem and the themes below it are apparatus, not the poem: both muted, both
italic, both wearing the same `<em><small><small>`.

The theme chips are what link the hubs from every poem; without them `/themes/` is reachable
from the navbar alone.

Titles are written out as `<h1>` rather than left to markdown's `# `, so a title can never be
read as markup. An untitled poem still gets an `<h1>`, hidden, for structure.

The epigraph caption is set away from the quotation and muted, so it does not read as the
epigraph's last line.

On phones the poem does not wrap — it scrolls sideways, so the poet's line breaks survive a
390px screen. The one thing missing was any sign that it does, so the long lines fade out at
the right edge instead of being cut, which is the same gesture the licence band makes at the
foot of the page.

The fade needs somewhere to go, though: without it the longest line ends flush with the right
edge and its last word sits under the gradient at the far end of the scroll, permanently dimmed
with no way to read it. So the lines carry the fade's own width as trailing padding. It is on
the lines rather than on the scroll box because a scroll container's end padding is left out of
the scrollable width, and `min-width: max-content` is what makes each line's box as wide as its
text, so that padding is width the reader can actually reach. The rule is the exception: its
measure is the screen, not the poem.

An epigraph is the exception to the exception. It is prose, not verse, so its line breaks are
the browser's and nothing is lost by moving them: on a phone it wraps to the screen rather than
joining the sideways scroll, where a long quotation would otherwise be one line the reader has
to drag through. The figure's default indents come off so the wrapped measure is the full
screen, and the cap is `100vw` less the body's padding rather than a percentage, because the
percentage would resolve against the scroll container, which is as wide as the poem's longest
line and not as wide as the screen.

No scrollbar on the poem either. A horizontal bar under a poem is a control on a page that has
none, it would sit against the licence band, and the fade already says the same thing more
quietly.

## The licence band

The other end of the sticky pair: the navbar holds the top of the window, this holds the foot,
and only the poem between them moves. It is the last flex item in the column, so it stretches
to the full content width and the paper fill becomes a band the text disappears behind —
without it the badge is an 88px image with the poem sliding past on either side.

The fill is a gradient so a line of verse passing under it fades out over 14px instead of being
cut through the middle of its letters. Invisible at rest: the band sits on paper and starts as
paper. No rule along its top either — at rest that would be a new line on every page, and the
band is only ever a floor while you are scrolling. Nothing is lost behind it, since sticky
returns the badge to the flow at the end.

The negative side margins are the navbar's trick at the other end: the body pads by 1rem and
the band pulls back out through it, so the fill reaches the window's edges while everything the
padding positions keeps the padding it was built on. The padding goes straight back on the
inside, so the badge does not move.

## Themes

Theme hubs live at `/themes/<tag>/` and are the pages that can answer a search like "poems
about grief", which an individual poem never can. Tags come from `Other/tags.tsv`, keyed by url,
most salient first. A renamed poem orphans its row, which fails the build loudly rather than
dropping the poem out of its theme pages — the fix is to update the key and add a line to
`Other/redirects.txt`. Below `MinimumThemePoems` a hub would be thin content, so the tag is
held back from `/themes/`; it still travels in the poem's schema.

The field on `/themes/` carries each theme's weight in its size and its grey, so the shape of
the collection is legible before a single number is read — nine steps, on a log scale. Linear
would be useless: love has 384 poems and beauty 21, so on a straight ramp everything from myth
downwards lands in the bottom two steps and the field flattens into one size with an outlier.
Ordered alphabetically rather than by size — sorted, it would be a staircase, and the point is
that it looks grown. The counts stay in the markup for crawlers and screen readers and come
forward only under the pointer, out of flow so a number appearing never reflows the field.

A field wants air on all four sides, not only the two the column gives it, so the row grows to
the full height between the navbar and the foot band and the field is centred in it: `/themes/`
reads as one shape hanging in the window rather than as a list that started under the heading
and stopped. Safe centring, so a field taller than the window still starts at the top.

A hub gathers a hundred-odd poems spanning fifteen years, which is more than a flat list can
hold. The years become a rail beside it and the poems group under year headings, so the rail is
a shape as well as a set of jumps: a theme's history shows in which years are bright. Greys
only, no size — a column of numbers that changed size would never settle into a straight edge
to read down. One year is no rail; the flat list already says everything it would.

The rail links anchors rather than `/2026/`, which would be a promise the year archive does not
keep — it holds that year's poems, not this theme's. The year marks in the body are plain text
for the same reason. They exist at all because the sections are otherwise separated by white
space alone, and a gap does not say what it is a gap for. The mark is the same italic label the
stem puts on a date, with a rule carrying the eye across the full width of the columns beneath.

A hub is a reference, not a reading column: three columns of titles beside the rail rather than
one column three times as long, and no stem in the body — the rail is the heading, and a second
set of years beside it would say it twice.

A hub is the one page that does not scroll. The page is exactly the window and the list
alone moves inside it, so the title saying which theme this is and the rail saying which years
it holds are both still there at the foot of a hundred titles. A rail that scrolls away is a
rail you have to scroll back for, and a set of jumps you have to go and find is most of the way
to not having them. It was sticky before, which held it against the navbar but let the title go
and left the rail's own position dependent on how far down the page you were.

The foot band is fixed to the foot of the window and now lies over the foot of the list rather
than the foot of the page, which is the same thing to look at and says the same thing: there is
more below. The 7rem the page reserved to end clear of it moves onto the list. The scrollbar is
the one piece of chrome this cannot suppress, so it is thinned and put in the grey of the rules,
rather than left as a lit strip down a dark page.

Short windows keep the ordinary page scroll: divided, they would leave the list a slot of two
or three rows, which is worse than a title that scrolls away.

The page keys act on whatever holds focus, and a page that does not scroll is a page where they
do nothing at all. So the list takes focus as soon as it has something to scroll, and takes it
back after a rail jump, which hands it to the body on the way past. It is not in the tab order:
the list is nothing but links, and tabbing through them scrolls it anyway. Focus here is a
place for the keys to land rather than something asked for, so it is not drawn; a ring the size
of the page would be the loudest thing on it.

Rail anchors land flush against the top of whatever is scrolling. In the window that is the top
of the list, with nothing above it to clear. In the short-window fallback it is the navbar,
exactly: less and the date lands behind the bar, more and the gap fills with the tail of the
group above. The node carries 2rem of bottom padding, so at flush that padding sits under the
bar and the previous title is above it.

On phones the rail goes entirely. Laid flat it was two rows of years above every list, which is
most of a phone screen spent on navigation before a single title — and jump-to-year matters
least on the device where the whole page is one column anyway.

## The title index

`/index/` used to be a column of 1128 titles beside the chronology on the home page. It is a
wall next to a tree, and the home page is the tree; on a page of its own it can carry an
alphabet rail and the home page can be only the chronology. It is the one page that wants to be
wider than a column of verse.

Every letter is present whether or not it has poems: the rail is a fixed shape you learn once,
and a missing L would move every letter after it. `space-between` rather than a fixed gap,
because the rule beneath the rail runs the full measure and an alphabet stopping short of it
read as a list that had run out; the gap is now a floor the letters never close below. Wrapped,
on a phone, `space-between` would justify the last row across the page as well, which reads as
a gappy line rather than as the end of the alphabet — so it falls back to `flex-start` there.

On a phone the letter leaves the gutter and heads its own row, ruled across in the way a year
is ruled across a hub. A single character reserving a column costs the titles the width of two
or three words, which on 390px is the difference between a title on one line and a title on
two, and the whole page is titles.

Sorting drops punctuation, so "A poem" and "A Platonist declares" sort together and "Am I
Right?" lands under its own letter rather than after it. A title opening on a digit still needs
a home: "other" collects them, and a tilde key sorts it last without needing a second pass.

## Chronology

Seventeen years is a long way to scroll to reach 2010, so the home page carries the same year
rail the theme hubs do — a jump to each year, bright where the writing was dense, newest first
to match the order of everything beside it. The first node of each year carries that year's
anchor, so the rail lands on the date the year opens with rather than on a heading of its own.

`Poems/Prologue` is named by title rather than by date, so archive sorts are explicit;
reversing before `OrderByDescending` leaves poems sharing a date in the order the chronology
puts them, since the sort is stable.

## Urls, breadcrumbs and metadata

Poems published after `DayUrlCutoff` live at `/yyyy/MM/dd/slug/`; earlier ones at
`/yyyy/MM/slug/`. Archives and breadcrumbs honour the same split, and a month can straddle the
cutoff, so each date decides for itself whether it has an archive of its own to link to.

Slugs separate on characters filenames cannot carry rather than deleting them. An all-numeric
slug would sit alongside the day directories under `/yyyy/MM/` and could shadow one, so it is
prefixed.

Year, month and day archives exist because without them `/2026/` and `/2026/08/` are dead ends
and the homepage is the only path into any poem. A day archive is one node of the chronology on
a page of its own, so it is built as one — stem, dot, italic date — rather than as a serif
heading over a bare list. The `<h1>` stays for the outline and for search and steps out of the
way, since the node's own date says the same words directly beneath it.

The date under a poem's title doubles as the breadcrumb, linking its archives; on a day archive
the day itself is not linked, since it is the page you are standing on.

Every page is titled `<what this page is> | poems by culturing`, and the home page is the second
half of that on its own. `titleText` is how a page whose visible heading is a sentence gets a
name instead: "Poems from 2026" and "Poems about love" are right over the page but say "poems"
twice in a title that already carries it, so year pages pass "2026" and a hub passes "Love".

The visible trail is what is above this page and nothing else. Two crumbs never earn their
place: the site name, because the wordmark in the navbar is the same link a few words to its
left; and the page itself, because the heading directly beneath already says it — a day archive
was reading "2026 › August › 17 August 2026" over an `<h1>` of "17 August 2026". Dropping both
leaves every crumb a link to somewhere you are not, which is the only thing a trail is for, and
leaves the year archives and the hubs with no trail at all rather than a trail of one. Both
crumbs stay in the `BreadcrumbList`, which wants the whole chain, root and leaf.

Schema.NET has no `Poem` class, so poem pages carry `AdditionalType: schema.org/Poem` to narrow
the type for consumers that look.

GitHub Pages cannot serve a 301, so a renamed url gets a stub carrying a meta refresh and a
`rel=canonical` to its new home; the list is `Other/redirects.txt`. A later poem may
legitimately have claimed an old url back, so an existing page is never overwritten by a stub.

`culturing.pdf` is deliberately absent from the sitemap: it reproduces every poem on the site,
so submitting it competes with the pages themselves. It stays linked from the navbar.

`docs/.nojekyll` stops GitHub Pages running the content through Jekyll, which drops
`_`-prefixed paths. `og-image.png` is the card link previews use; 1920x1080 is near enough the
1.91:1 Open Graph ratio.

Stylesheet and script urls carry `?v=<hash>`, eight hex characters of the file's SHA-256, added
to the templates as they are read. The site sits behind Cloudflare, which holds `/common.css`
for four hours on the origin's `max-age`; the html is revalidated far sooner, so a deploy that
changed a stylesheet without changing its url served new markup against the old css until the
edge expired. Fingerprinting the url means changed bytes are a new url and a guaranteed miss,
and unchanged bytes keep the cache. The fonts are left alone deliberately: their urls appear
both in the `@font-face` rules and in the `<link rel=preload>` above them, and a version on one
and not the other would preload a file the css never asks for. This assumes Cloudflare caches
on the full url, which is the default — a rule that ignores query strings would defeat it.

## The book

The PDF is built through the same stylesheets, over a local server, by Playwright. Playwright
and ffmpeg run last in the build: the html and the sitemap must not depend on them.

Print media is emulated *before* navigation rather than after. The page is three megabytes of
poems and lays out to something over a thousand pages; arriving in screen media and switching
afterwards lays the whole book out twice, once in a form nobody will ever see. Everything the
screen adds, such as the fixed pagination in the margins, is absent from the first layout this
way.

The navbar, pagination and Creative Commons links are removed from the tree rather than only
hidden: print css already hides them, and the pagination is `position: fixed`, which Chromium
repeats on every page of a paginated document.

Print undoes the screen's flex column, since print stacks poem pages directly in body and
centres each one itself. Body carries no stacking context, screen or print: it makes a
thousand-page document one composited layer to be resolved against — on a poem page you would
never notice, on the book it is the difference between a build that finishes and one that does
not.

The book sets the date as the screen does, Alegreya italic from the `<em>` it is wrapped in.
The url becomes visible (it is print-only
apparatus) and the theme links are dropped, since they do not belong in the book. The date goes
black: `--muted` is chosen against a near-black ground and prints faint.

`toc.css` ships to `docs/` because `PdfBook` loads it as `/toc.css` over the local server.
`review.js` ships only during a review pass.

## Review mode

`dotnet run -- review` adds the rating widget to every poem page and writes
`Output/review-map.json`. The review server has no way back from a poem's url to its source
file, so the build hands it the mapping; it lands in `Output/`, never in `docs/`.

`Scripts/review.js` refuses to run anywhere but localhost and writes nothing itself —
`Tools/review-server.py` does, on port 5501. The whole apparatus is temporary: delete
`Scripts/review.js`, `Tools/` and the `{{review}}` line in `Templates/content.html` when the
pass is done.

## Accessibility

Nothing on this site moves. The reveals — the opening line beside a title, a theme's count, a
pagination label — go straight to their end state on hover, so there is no motion for
`prefers-reduced-motion` to reduce and no rule answering it.

`.visually-hidden` content is reachable by crawlers and screen readers and absent from the
visual design.

## Bluesky

Poems arrive in batches: 959 of them across 101 drops, a mean of 9.5 a drop and over half
carrying ten or more. A post per poem on push would fire seventeen at once, so the account
carries two kinds of post instead. A drop gets one digest linking that day's archive, and a
cron job posts one individual poem a day.

Neither job writes to the repository. What has been posted is read back from the account
itself: `com.atproto.repo.listRecords` walks the posts back to `ACCOUNT_START` and collects
every url they link, and the drip posts the oldest poem absent from that set. A ledger file
would have meant a commit a day, and the account is the more reliable record anyway — a post
that succeeded while its commit failed would have left the two disagreeing.

The drip takes its candidates from `docs/sitemap.xml`, whose order is build order and so keeps
a day's poems in file order; the days themselves are sorted oldest first. Title and description
come from the chosen poem's own `og:` tags.

`SINCE` splits those candidates into two queues. Output runs at about 1.1 poems a day, so the
poems from `SINCE` on usually keep the daily slot filled; when they do not, the drip falls back
to the backlog behind `SINCE` and posts the oldest poem there, walking forward from 2010 until
that queue empties too. New work always takes the slot ahead of the backlog, so reaching back
to 2010 costs the present nothing. A backlog post carries its year in the heading and in the
card title, so it does not read as new work.

The read-back has its own constant. `ACCOUNT_START` is the account's first post and holds the
walk's floor; `SINCE` only divides the queues. They are the same date today, but bounding the
read-back by the divider would mean that moving the divider forward hid earlier posts and
re-posted the backlog poems among them.

Because nothing predates `ACCOUNT_START`, that floor never actually stops the walk — it runs to
the end of the record set every time. A partial set is indistinguishable from a complete one and
would re-post whatever the missing pages held, so `postedUris` throws rather than return one. A
failed job asks to be looked at; a quietly duplicated post does not.

What bounds the walk is the pager rather than a page count. A count would have had to be raised
as the account grew, and the raise would have been due the one time nobody was watching. The two
guards instead describe the ways a pager fails: a cursor that comes back twice is going nowhere,
and a wall-clock deadline catches one that advances forever. Both are clear of any history this
account will hold, so neither needs revisiting.

Reading every post each run is the price of keying on urls, and worth it. Resuming from the
newest drip post instead would make the walk a single page, but three things put a poem behind
that mark and expect it to still post: a url released from `bluesky-skip.txt`, a retitle that
changes a slug, and a lowered `MIN_RATING`. The url set handles all three by construction.

The digest instead diffs `docs/feed.xml` between the pushed commits and takes the guids only
the newer side has. The feed already holds the finished url, title and description, so nothing
reimplements `Slugify` or `BuildDescription` outside C#. A rebuild that adds no poems changes
`lastBuildDate` and the page bodies but no guids, and posts nothing.

Hashtags come from `Other/tags.tsv`, written in the same commit as the poems and ordered most
salient first; the first two become the post's tags. Ratings come from the day archive, which
wraps each link in its `rated-` class — a poem page carries no rating of its own. `MIN_RATING`
gates both queues and sits at 0. At 1 it would drop the third of poems that are unrated, which
the backlog now has the depth to absorb.

`[skip bsky]` in any commit message of a push suppresses that push's digest; dispatching a
workflow by hand ignores the marker. `Other/bluesky-skip.txt`, one url path a line, holds a
poem back from the drip. Neither helps with a retitle: the slug changes, so the poem reads as
one that has never been posted.

The scripts are node with no dependencies. `Poems.csproj` globs `**/*.cs`, so a C# helper
anywhere in the tree would be compiled into the site build.
