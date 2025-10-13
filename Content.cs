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
    public string UrlPath => $"/{PublicationDate.ToString("yyyy")}/{PublicationDate.ToString("MM")}/{FileName}/";
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