using System;
using System.Collections.Generic;
using System.IO;

namespace Poems;

class Content
{
    public string Title { get; set; }
    public string Link { get; set; }
    public DateTime PublicationDate { get; set; }
    public string FilePath { get; set; }
    public string FileName => Path.GetFileName(Path.GetDirectoryName(FilePath));
    public int Page { get; set; }
}

class Poem : Content
{
    // How highly a poem is rated. A source file opens with one asterisk per level, so the
    // poems marked with a single "*" all arrive as 1. Three levels is what a brightness ramp
    // on this ground actually carries -- see common.css, which also says what a fourth would
    // have to look like if the scale ever needs one.
    public const int MaxRating = 2;

    public int Rating { get; set; } = 0;

    // /best/, the chronology and the pdf's bestOnly filter still ask a yes/no question
    public bool Bold => Rating > 0;

    // Short plain-text excerpt used for <meta name="description">, Open Graph and the RSS feed
    public string Description { get; set; }

    // Theme tags from Other/tags.tsv, most salient first
    public List<string> Tags { get; set; } = new List<string>();

    // Poems published after the cutoff live at /yyyy/MM/dd/slug/, earlier ones at /yyyy/MM/slug/
    public bool HasDayUrl => PublicationDate > Program.DayUrlCutoff;

    // The dated directory a poem lives in, shared by its url and its location under docs/
    public string DatePath => HasDayUrl
        ? $"/{PublicationDate.ToString("yyyy")}/{PublicationDate.ToString("MM")}/{PublicationDate.ToString("dd")}/"
        : $"/{PublicationDate.ToString("yyyy")}/{PublicationDate.ToString("MM")}/";

    public string UrlPath => $"{DatePath}{FileName}/";

    // The rating is carried by the title's own weight and nothing else -- see the ramp in
    // common.css and its paper twin in toc.css. A listing puts this on the row, a poem's own
    // page puts it on the h1. Every one gets a class, unrated included: there is no sensible
    // default for a stylesheet to fall back on.
    public string RatingClass => $"rated-{Rating}";
}

class Analysis : Content
{
    public string UrlPath => $"/analysis/{PublicationDate.ToString("yyyy")}/{PublicationDate.ToString("MM")}/{FileName}/";
}
