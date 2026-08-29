# Design notes

Why the site looks and behaves the way it does. Kept here rather than in comments, so the
code can be read as code. Source of record: `Program.cs` builds the site, `Styles/` sets it,
`Scripts/` adds the two behaviours that need javascript.

---

## Palette and atmosphere

Ground and text are the originals: a neutral near-black and a neutral near-white. Greys stay
neutral with them — a violet-biased grey reads purple next to neutral text. One accent, rose,
and only where it belongs to the poems themselves: links inside a poem, and the breadcrumb
trail above an archive. Everything else — navbar, theme counts, ratings — is grey, so nothing
outside the writing competes with it. The rose is warmed slightly off the mauve it could be,
so it reads as red rather than violet against a ground with no hue of its own.

`--void`, `--loam` and `--whisper` are later additions. Two are ends the ramp already implied:
`--void` is where a fade band lands once `--paper` has run out, and `--whisper` is one step
below `--muted`, dark enough that a line of verse beside a title reads as an aside rather than
a second column. `--loam` sits between paper and raise, for the rare surface that needs to be
off the ground without being lit.

`--glow` and `--shade` are the two atmosphere dials, in one place because they are the whole
look: the strength of the light the page is read by, and how hard the edges close. Both are
alphas, and both are zeroed under print and forced colours.

The page is lit rather than filled. A pool of light sits off centre, over the column that is
actually being read, and a vignette closes the edges; between them the ground stops being one
flat value and starts having a middle and an outside. Both are fixed, so the light stays where
the eye is rather than scrolling away with the text. `body::before` carries light and shadow
together, the vignette painted first so the pool sits on top of it; `body::after` carries the
grain, which has to be its own layer because it multiplies over both. The grain is fractal
noise at 0.75 — fine enough to read as paper tooth rather than static at any likely zoom — at
4.5%, where it stops being visible as noise and only takes the flatness off.

Poem pages switch the pool off (`content.css`, via `:has()` on the root) and keep the vignette.
High-contrast and forced-colours modes get the page with the atmosphere off entirely: a
vignette is decoration, and it is the one thing here that removes contrast.

`body` is a full-height flex column so `#container-parent` can claim exactly the leftover
space between the navbar and the licence badge; it replaced a hardcoded offset that only added
up in quirks mode. `position: relative` is what lets the atmosphere layers anchor to the page
rather than the viewport, and `isolation` opens a stacking context so they sit at z-index 0
and 1 without reaching past the body for anything to paint over.

## Typography

Cinzel sets the navbar and the date labels, and nothing else. It is a Roman inscriptional
face — a page of it would be a monument rather than a poem — and it marks everything on the
site that labels the writing rather than being it, so a reader learns the distinction once.
It has no true lowercase; the small letters are small capitals, which close into a single grey
mark without tracking. 0.26em on nav labels, 0.24em on date headings, 0.44em on the wordmark,
which is read as a name rather than as a word.

Quattrocento sets everything else, poems included. The Greek face is Alegreya's, wearing the
Quattrocento name so it resolves per character on the same line.

Font build notes:

- Cinzel is a static instance built from the upstream variable font with fontTools, pinned at
  the axis value in `fonts.css`, subset, then compressed. Rebuilding means redoing all three
  steps — a differently-pinned instance changes the weight of every nav label. It ships at 400
  only, so a stray `font-weight` elsewhere cannot embolden the bar.
- Quattrocento is a 2048 upem cut carrying its own hinting, **not** built from the v2.000 TTFs
  beside it in `Styles/`, which are upstream's 1000 upem. Measured: at 16px the 1000 upem cut
  renders less evenly, its x-height landing on 7.34px where the hint program rounds
  neighbouring glyphs opposite ways and the line shimmers. 2048 is a power of two, so
  unit-to-pixel conversion adds no rounding of its own. Re-hinting the 1000 upem cut with
  ttfautohint measured worse than either.
- That cut covers 230 codepoints, fewer than the declared ranges. A character inside a range
  but absent from the font drops through to Georgia rather than failing visibly. Nothing
  written so far uses one; the micro sign is the near miss, and it is routed to the Greek face
  on purpose. The real 700 is shipped because otherwise the browser fakes bold from the
  regular. The family ships no italic, so every italic on the site is a computed slant — a
  deliberate trade.
- The Greek face declares two exact weights, never the range `400 700`: a range overlaps the
  regular's exact 400 and, declared later, wins the weight match outright, dropping every 400
  on the site to the browser's default serif while bold carries on working.
- The unicode ranges are generated rather than transcribed, and disjoint by design.
- `Program.cs` names Quattrocento for the PDF folios, which come from the installed font
  rather than from `fonts.css`.

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

### The weathered variant

`Styles/navbar-cinzel.css` and `Styles/dropdown-cinzel.css` are the weathered treatment: a
fractured stone lintel with a vine rooted in the fracture. Generated by
`node Tools/weathering.js`, whose output is pasted over the generated block at the top of
`navbar-cinzel.css`.

- **Generated rather than hand-drawn** because the geometry is a few hundred coordinates and
  has to stay reproducible. Every random draw comes off one seeded PRNG (`SEED = 6180`), so the
  same seed always yields the same bar — byte-identical output. Change it only to reroll the
  whole thing deliberately. Anything inserted above an existing draw shifts every number after
  it and redraws the navbar; new parts go at the end of the file.
- **Background images rather than one inline SVG** because the navbar is a flex row whose width
  is the viewport's, and an SVG stretched to that width distorts every leaf. Backgrounds
  positioned by percentage never scale and never overflow their box, so the growth spreads with
  the bar while each leaf keeps its shape — and none of it can force a scrollbar.
- **`BASE` is the one number everything depends on.** It is the distance from the bottom of
  every generated image to the vine's stem. `navbar-cinzel.css` hangs the layer at
  `bottom: -BASE`, so a stem drawn at `H - BASE` lands on the navbar's lower edge in all five
  images at once, and leaves lying across the stem hang into the `BASE` px underneath. Change
  it in `weathering.js` and change `bottom` and `height` on `.navbar::after` to match. At 6 the
  largest leaves were cut off square by the image edge.
- **The fracture and the growth share one image.** That is the point of the design: the vine is
  not near the crack, it is in it. In separate layers they would drift apart at some viewport
  width and the causation — the stone failed, so the seed got in — would quietly stop reading.
- The stem's ends fade to nothing over the outer 16%, because the continuous hairline
  underneath is a repeating tile whose phase at any viewport width is unknowable; a hard end
  showed as a step where the two lines met at slightly different heights. Faded, the same
  mismatch reads as the stem thickening, which is what a vine does anyway.
- Tendrils are kept 14px apart minimum. At 9 they overlapped into a blobby mass that read as
  moss rather than as a vine.
- The fracture uses midpoint displacement: a crack is self-similar at every scale, which is why
  it never looks right drawn by hand. It is drawn twice — the opening in shadow, and half a
  pixel up-left the chipped edge catching what light there is. That offset is the whole
  illusion. The lower stretch is drawn again over itself at nearly twice the width, because a
  fracture opens wider the further it has run; widening the whole path just reads as a line.
- The runner is one growth that ignores the falloff and reaches out where the bar is otherwise
  still just a wall. The asymmetry is the whole of it — a second one at the far left for
  balance would kill the effect.
- Green is the first hue on the site besides the rose. `GREY` (wrought iron, strictly neutral)
  and `ROSE` (the growth dried out) are drawn and working alternates in `weathering.js`.
- Inside a quoted CSS `url()` only three characters have to be escaped: the closing quote, `%`,
  and `#`. Angle brackets are left raw — encoding them is the usual habit and triples the size
  of every tag for nothing. SVG attributes are single-quoted throughout so the double quote
  never appears.
- The labels are settled rather than straight: each word is a block, the blocks have been in
  the wall a long time, and the wall has moved. Everything is under 0.6deg and under 2px; past
  that it stops reading as settlement and starts reading as a broken stylesheet. Two labels
  have their tracking opened a thousandth as the joint widened. The transforms take effect
  because every `.nav` is a flex item and so blockified — on a bare inline `<a>` a transform is
  ignored. `.dropbtn` stands in for the contact link, since transforming `.dropdown` would
  carry the menu with it.
- `.navbar, .navbar div { padding-bottom: 0 }` exists because `index.css` pads every div by 4px
  to space listing rows and the nav groups nest three deep, so a listing page's bar came out
  48px against a poem page's 36px. That only mattered once the fracture was drawn to a fixed
  36px and appeared to start halfway down the slab on half the site.
- `.navbar { margin-bottom: 1.5rem }` is the 10px of vine overhang plus air. Listing pages
  forced it — `index.css` starts both columns flush to the top, so the first heading came up
  under the leaves. Poem pages absorb it without moving.
- The last layer of `.navbar::after` is an apron: the slab is opaque and hides what scrolls
  under it, but the vine is leaves on nothing, and once the bar went sticky the poem showed
  through the gaps. The layer is transparent for the 38px the slab covers and paper for the
  10px below, so text disappears cleanly at the leaf tips. 38px is not the navbar's height —
  it is the box's own height less the overhang.
- On phones the bar is two rows and the vine has no single baseline to follow, so the growth is
  dropped rather than redrawn. The slab is a gradient and stretches to whatever height the rows
  come to. `display: contents` on the groups also dissolves the settling `nth-child` selectors,
  so the labels stand straight there.
- The dropdown panel is a fragment cut from the same block: same gradient, same grain, same lit
  top edge and shadowed foot, and one fine flaw running down from where it meets the bar. It
  takes the stone's vocabulary and none of the vine's — a second colony on a menu that appears
  and disappears would say the wrong thing about how long any of this took. `--stone-grain` and
  `--stone-flaw` are declared on `.navbar` and reach it by inheritance, so the tablet cannot
  drift out of step with the bar it hangs from. The flaw image is deliberately shorter than the
  menu: a crack stopping exactly at the bottom edge would read as a drawn border. The panel's
  tilts run about half the amplitude of the bar's — three words in a column show a tilt far
  more readily than eight in a row.

## Listings

### The stem

Each date is a node on one continuous line. The line is a border on the node itself rather than
on a wrapper, so consecutive nodes abut into a single stroke and a page with no dated groups —
a day archive, a one-year theme — simply has no stem, rather than a bare line down the side of
a flat list. The dot is filled with `--paper` rather than left hollow, because the border runs
behind it and would otherwise be drawn through its middle. `index.css` draws all of it;
`Program.cs` only emits the nodes.

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

### The fade band

Long lists end in the dark rather than at a hard edge. Fixed, so it is a property of the window
and not of the list, and paired with the padding above it so that scrolling to the end always
leaves the last title clear of it — the band only ever covers ground the list has finished
with. On desktop it is dropped for `/themes/`, where reserving its 7rem on a field that fits in
one screen would only hold the field off centre.

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
read in a pool of light on a black ground needs more room, or the descenders of one line and
the ascenders of the next close the gap and the stanza reads as a block. Unitless, so it
follows a size change, and scoped to `.poem` because the about, 404 and faq pages load the same
stylesheet for prose that should keep body's tighter leading.

The date above the poem and the themes below it are apparatus, not the poem: both muted, both
in Cinzel, both wearing the same `<em><small><small>`. `font-style` is reset because the family
ships no italic and the browser would otherwise compute a slant, which on an inscriptional
roman reads as a mistake rather than as emphasis. Both selectors reach past the `<em>`, since
`font-style` inherits and a declaration has to sit on an element inside it to win.

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
the full height between the navbar and the fade band and the field is centred in it: `/themes/`
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
space alone, and a gap does not say what it is a gap for. The mark is the same Cinzel label the
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

The fade band is fixed to the foot of the window and now lies over the foot of the list rather
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
a page of its own, so it is built as one — stem, dot, Cinzel date — rather than as a serif
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
screen adds — the atmosphere layers, the fixed pagination in the margins — is absent from the
first layout this way.

The navbar, pagination and Creative Commons links are removed from the tree rather than only
hidden: print css already hides them, and the pagination is `position: fixed`, which Chromium
repeats on every page of a paginated document.

Print undoes the screen's flex column, since print stacks poem pages directly in body and
centres each one itself. `position` and `isolation` are undone too, and matter more than they
look: a stacking context on body makes a thousand-page document one composited layer to be
resolved against — on a poem page you would never notice, on the book it is the difference
between a build that finishes and one that does not. The atmosphere layers use `content: none`
rather than `display: none`, so the pseudo-elements are never generated at all; `display: none`
still builds the box and, for the grain, still has an SVG filter attached, tiled and repeated
per page.

The book keeps the date it always had: Quattrocento italic, taking the slant from the `<em>` it
is wrapped in, tracking back to normal. Cinzel small capitals are a screen gesture — they
belong to a navbar and a set of date headings the PDF has none of, and set into a page of verse
they read as a different book's running head. The url becomes visible (it is print-only
apparatus) and the theme links are dropped, since they do not belong in the book. The date goes
black: `--muted` is chosen against a near-black ground and prints faint.

`toc.css` ships to `docs/` because `RenderPdf` loads it as `/toc.css` over the local server.
`review.js` ships only during a review pass.

## Review mode

`dotnet run -- review` adds the rating widget to every poem page and writes
`Output/review-map.json`. The review server has no way back from a poem's url to its source
file, so the build hands it the mapping; it lands in `Output/`, never in `docs/`.

`Scripts/review.js` refuses to run anywhere but localhost and writes nothing itself —
`Tools/review-server.py` does, on port 5501. The whole apparatus is temporary: delete
`Scripts/review.js`, `Tools/` and the `{{review}}` line in `Templates/content.html` when the
pass is done.

## Motion and accessibility

Everything that fades in on this site — the opening line beside a title, a theme's count —
shares one duration. Anyone who has asked their system for less motion gets the end state with
no transition rather than no reveal at all.

`.visually-hidden` content is reachable by crawlers and screen readers and absent from the
visual design.
