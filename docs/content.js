console.log("Like looking under the hood? https://github.com/culturing/poems");

document.onkeydown = function(e) {
    var id = e.keyCode == 37 ? "previous" : e.keyCode == 39 ? "next" : null;
    if (!id)
        return;

    // Absent at the ends of the sequence, where the slot is a span rather than a link
    var link = document.getElementById(id);
    if (link && link.href)
        link.click();
}
