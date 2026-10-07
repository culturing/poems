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
    // Where the poem was read from; FilePath is where it was written to
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

    public string Description { get; set; }

    public string Opening { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = new List<string>(); // Most salient first

    public bool HasDayUrl => PublicationDate > Program.DayUrlCutoff;

    // Serves as both the url prefix and the directory under docs/
    public string DatePath => HasDayUrl
        ? $"/{PublicationDate.ToString("yyyy")}/{PublicationDate.ToString("MM")}/{PublicationDate.ToString("dd")}/"
        : $"/{PublicationDate.ToString("yyyy")}/{PublicationDate.ToString("MM")}/";

    public string UrlPath => $"{DatePath}{FileName}/";

    // When MaxRating changes, update the ramps in common.css and toc.css
    public string RatingClass => $"rated-{Rating}";
}

class Analysis : Content
{
    public string UrlPath => $"/analysis/{PublicationDate.ToString("yyyy")}/{PublicationDate.ToString("MM")}/{FileName}/";
}
