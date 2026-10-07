// Loaded at the top of the body without defer, before any listing has been parsed, so the
// filter is applied in the first paint rather than after one. Delegated from the document
// because the buttons below it do not exist yet when this executes.
(function () {
    var root = document.documentElement;
    root.className += " js";

    var tier = "0";
    try { tier = localStorage.getItem("tier") || "0"; } catch (e) { }
    if (tier !== "1" && tier !== "2") tier = "0";
    root.classList.add("tier-" + tier);

    document.addEventListener("click", function (e) {
        var button = e.target.closest && e.target.closest(".tier-btn");
        if (!button) return;
        var chosen = button.getAttribute("data-tier");
        root.classList.remove("tier-0", "tier-1", "tier-2");
        root.classList.add("tier-" + chosen);
        try { localStorage.setItem("tier", chosen); } catch (e) { }

        // <details> has no idea the choice was made inside it
        var panel = button.closest("details");
        if (panel) panel.open = false;
    });
})();
