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
    // Where the poem was read from; FilePath is where it was written to. Review mode only
    public string SourcePath { get; set; }
    public string FileName => Path.GetFileName(Path.GetDirectoryName(FilePath));
    public int Page { get; set; }
}

class Poem : Content
{
    // One leading asterisk per level in the source file
    public const int MaxRating = 2;

    public int Rating { get; set; } = 0;

    public bool Bold => Rating > 0;

    // Used for <meta name="description">, Open Graph and the RSS feed
    public string Description { get; set; }

    // The poem's opening line, shown beside its title in a listing on hover
    public string Opening { get; set; } = string.Empty;

    // Theme tags from Other/tags.tsv, most salient first
    public List<string> Tags { get; set; } = new List<string>();

    // Poems after the cutoff live at /yyyy/MM/dd/slug/, earlier ones at /yyyy/MM/slug/
    public bool HasDayUrl => PublicationDate > Program.DayUrlCutoff;

    // Serves as both the url prefix and the directory under docs/
    public string DatePath => HasDayUrl
        ? $"/{PublicationDate.ToString("yyyy")}/{PublicationDate.ToString("MM")}/{PublicationDate.ToString("dd")}/"
        : $"/{PublicationDate.ToString("yyyy")}/{PublicationDate.ToString("MM")}/";

    public string UrlPath => $"{DatePath}{FileName}/";

    // The ramp lives in common.css, and its paper twin in toc.css
    public string RatingClass => $"rated-{Rating}";
}

class Analysis : Content
{
    public string UrlPath => $"/analysis/{PublicationDate.ToString("yyyy")}/{PublicationDate.ToString("MM")}/{FileName}/";
}
