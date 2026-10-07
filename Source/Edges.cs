using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Poems;

// The previous and next links either side of a poem
static class Edges
{
    const int Tiers = Poem.MaxRating + 1;

    // One pair per rating tier for every poem; css shows the reader's
    public static (string Previous, string Next)[] ForSequence(List<Poem> ordered)
    {
        var previousAt = new int[Tiers][];
        var nextAt = new int[Tiers][];
        for (int tier = 0; tier < Tiers; tier++)
            (previousAt[tier], nextAt[tier]) = NeighbourChain(ordered, tier);

        var edges = new (string Previous, string Next)[ordered.Count];
        for (int i = 0; i < ordered.Count; ++i)
        {
            var previous = new StringBuilder();
            var next = new StringBuilder();
            for (int tier = 0; tier < Tiers; tier++)
            {
                previous.Append(previousAt[tier][i] >= 0
                    ? Link("prev", tier, ordered[previousAt[tier][i]])
                    : Disabled("prev", tier));
                next.Append(nextAt[tier][i] >= 0
                    ? Link("next", tier, ordered[nextAt[tier][i]])
                    : Disabled("next", tier));
            }
            edges[i] = (previous.ToString(), next.ToString());
        }

        return edges;
    }

    // For every position in the sequence, the nearest poem on each side that meets the tier
    static (int[] previous, int[] next) NeighbourChain(List<Poem> ordered, int tier)
    {
        var previous = new int[ordered.Count];
        var next = new int[ordered.Count];

        int seen = -1;
        for (int i = 0; i < ordered.Count; i++)
        {
            previous[i] = seen;
            if (ordered[i].Rating >= tier)
                seen = i;
        }

        seen = -1;
        for (int i = ordered.Count - 1; i >= 0; i--)
        {
            next[i] = seen;
            if (ordered[i].Rating >= tier)
                seen = i;
        }

        return (previous, next);
    }

    static string Chevron(string kind) =>
        $"<svg class=\"edge-mark\" width=\"18\" height=\"18\" viewBox=\"0 0 24 24\" aria-hidden=\"true\">"
        + $"<path d=\"{(kind == "prev" ? "M14.5 5 8 12l6.5 7" : "M9.5 5 16 12l-6.5 7")}\"/></svg>";

    // An id must be unique, so only tier 0 carries one; content.js finds the rest by data-tier
    static string Id(string kind, int tier) => tier == 0
        ? $" id=\"{(kind == "prev" ? "previous" : "next")}\""
        : string.Empty;

    static string Link(string kind, int tier, Poem neighbour)
    {
        string label = $"<span class=\"edge-label\">{WebUtility.HtmlEncode(neighbour.Title)}</span>";
        string inner = kind == "prev" ? Chevron(kind) + label : label + Chevron(kind);
        return $"<a{Id(kind, tier)} class=\"edge edge-{kind}\" data-edge=\"{kind}\" data-tier=\"{tier}\""
            + $" rel=\"{kind}\" href=\"{neighbour.UrlPath}\">{inner}</a>";
    }

    // The chevron stays at the ends of a chain, so the margins keep their shape
    static string Disabled(string kind, int tier) =>
        $"<span{Id(kind, tier)} class=\"edge edge-{kind} edge-disabled\" data-edge=\"{kind}\""
        + $" data-tier=\"{tier}\" aria-hidden=\"true\">{Chevron(kind)}</span>";
}
