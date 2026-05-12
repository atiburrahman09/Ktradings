document.addEventListener("DOMContentLoaded", function () {
    var toggle = document.querySelector(".menu_toggle");

    if (!toggle) {
        return;
    }

    toggle.addEventListener("click", function () {
        if (window.innerWidth < 992) {
            document.body.classList.toggle("sidebar-open");
            return;
        }

        document.body.classList.toggle("sidebar-collapsed");
    });

    document.addEventListener("click", function (event) {
        if (!document.body.classList.contains("sidebar-open")) {
            return;
        }

        if (event.target.closest(".left_col") || event.target.closest(".menu_toggle")) {
            return;
        }

        document.body.classList.remove("sidebar-open");
    });
});
