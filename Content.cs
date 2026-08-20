using System;
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
    public bool Bold { get; set; } = false;

    // Short plain-text excerpt used for <meta name="description">, Open Graph and the RSS feed
    public string Description { get; set; }

    // Poems published after the cutoff live at /yyyy/MM/dd/slug/, earlier ones at /yyyy/MM/slug/
    public bool HasDayUrl => PublicationDate > Program.DayUrlCutoff;

    // The dated directory a poem lives in, shared by its url and its location under docs/
    public string DatePath => HasDayUrl
        ? $"/{PublicationDate.ToString("yyyy")}/{PublicationDate.ToString("MM")}/{PublicationDate.ToString("dd")}/"
        : $"/{PublicationDate.ToString("yyyy")}/{PublicationDate.ToString("MM")}/";

    public string UrlPath => $"{DatePath}{FileName}/";

    public string Style(bool bestOnly = false)
    {
        string style = string.Empty;
        if (Bold && !bestOnly)
            style += "font-weight: bold;";
        return style;
    }
}

class Analysis : Content
{
    public string UrlPath => $"/analysis/{PublicationDate.ToString("yyyy")}/{PublicationDate.ToString("MM")}/{FileName}/";
}
