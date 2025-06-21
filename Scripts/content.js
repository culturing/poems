console.log("Like looking under the hood? https://github.com/culturing/poems");

document.onkeydown = function(e) {
    if (e.keyCode == 37) {
        document.getElementById("previous").click();
    } else if (e.keyCode == 39) {
        document.getElementById("next").click();
    }
}