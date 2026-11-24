---
name: proof
description: Proofread the day's poems, tag them in Other/tags.tsv, and build the site in review mode. Use when the user types /proof or asks to proofread, tag and review-build new poems.
---

# Proof

Takes an optional date (`yyyy-mm-dd`); without one, use today's.

## 1. Proofread

Read every file in `Poems/yyyy/mm/dd/`. **Never edit a poem.** Not for spelling, markup or whitespace either; the author makes every change.

List suggestions, numbered across all poems (1, 2, 3…) so the author can accept them by number. For each, give the poem's title, the line, the current text and the proposed text, with a few words of why.

Look for:

- misspellings and doubled or dropped words
- a title or epigraph source that is misspelled or misattributed
- punctuation that breaks the sense, such as an unclosed quote or a question with no question mark
- words that look like slips, not choices

Be sparing. Odd words, broken syntax and lowercase may be deliberate. When unsure, ask rather than propose.

## 2. Tag

1. Run `python Tagging/check.py` to list the untagged poems and their urls.
2. Read `Tagging/README.md` for the closed vocabulary and its distinctions (anima/women, longing/desire, the narrow sense of doubt, healing/strength/identity).
3. Append one row per poem to `Other/tags.tsv`: url, a tab, then 2–4 tags, most salient first. Keep its LF line endings.
4. Run `python Tagging/order_tags.py write`, then `python Tagging/check.py`, and confirm nothing is untagged and there are no problems.

If a suggested title change would change a poem's slug, say so, since its tag row would then need a new key.

## 3. Build in review mode

```
pwsh -NoProfile -File Tools/commands/build.ps1 -r
```

Give it a long timeout; it renders the PDF. It waits for other builds and starts the review server on port 5501 if none is running.

## 4. Report

The numbered suggestions, a table of the tags given, and whether the build passed.
