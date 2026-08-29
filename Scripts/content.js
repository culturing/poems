console.log("Like looking under the hood? https://github.com/culturing/poems");

document.onkeydown = function(e) {
    var id = e.keyCode == 37 ? "prev" : e.keyCode == 39 ? "next" : null;
    if (!id)
        return;

    // The tier is read off <html>: only the unfiltered pair carries an id
    var tier = (document.documentElement.className.match(/tier-(\d)/) || [])[1] || "0";
    var link = document.querySelector('.edge[data-edge="' + id + '"][data-tier="' + tier + '"]');

    // Absent at the ends of the sequence, where the slot is a span rather than a link
    if (link && link.href)
        link.click();
}
