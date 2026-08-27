console.log("Like looking under the hood? https://github.com/culturing/poems");

document.onkeydown = function(e) {
    var id = e.keyCode == 37 ? "prev" : e.keyCode == 39 ? "next" : null;
    if (!id)
        return;

    // Every poem carries three pairs, one per rating tier, and only the reader's is shown.
    // The arrow keys have to walk the same sequence the chevrons do, so the tier is read off
    // <html> rather than the pair being found by id -- only the unfiltered pair has one.
    var tier = (document.documentElement.className.match(/tier-(\d)/) || [])[1] || "0";
    var link = document.querySelector('.edge[data-edge="' + id + '"][data-tier="' + tier + '"]');

    // Absent at the ends of the sequence, where the slot is a span rather than a link
    if (link && link.href)
        link.click();
}
