using System;

namespace Poems;

static class Site
{
    public const string BaseUrl = "https://poems.culturing.net";
    public const string Name = "culturing";
    public const string OgImageUrl = BaseUrl + "/og-image.png";

    // Poems after this date live at /yyyy/MM/dd/slug/, earlier ones at /yyyy/MM/slug/
    public static readonly DateTime DayUrlCutoff = new DateTime(2026, 03, 04);

    public static readonly string[] Months = { "", "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
}
