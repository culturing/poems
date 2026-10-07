// A hub's list is the only thing on the site that scrolls inside the page, and the page
// keys act on whatever holds focus, so the list is given it
(function () {
    var list = document.querySelector(".letters");
    if (!list) {
        var railed = document.querySelector(".railed-body");
        if (railed && railed.querySelector(".theme-columns")) list = railed;
    }
    if (!list) return;

    function take() {
        // The stylesheet decides whether the list scrolls at all; below its breakpoints
        // the page is what scrolls, and focus here would take the keys off it
        if (list.scrollHeight > list.clientHeight) list.focus({ preventScroll: true });
    }

    take();

    // After the click, not during it: the jump that follows puts focus on the body
    var rail = document.querySelector(".year-rail, .rail");
    if (rail) rail.addEventListener("click", function () { setTimeout(take, 0); });
})();
