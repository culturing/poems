console.log("Like looking under the hood? https://github.com/culturing/poems");

document.onkeydown = function(e) {
    if (e.keyCode == 37) {
        document.getElementById("previous").click();
    } else if (e.keyCode == 39) {
        document.getElementById("next").click();
    }
}

var startX = 0;
document.ontouchstart = function(e) {
    if (e.changedTouches.length > 0){
        startX = e.changedTouches[0].screenX;
    }        
}

var swipeThreshold = 300;
document.ontouchend = function(e) {
    if (e.changedTouches.length > 0){
        var changeX = e.changedTouches[0].screenX - startX;

        if (changeX > swipeThreshold) {
            document.getElementById("previous").click();
        } else if (changeX < -swipeThreshold) {
            document.getElementById("next").click();
        }
    }
}