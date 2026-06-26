(() => {
    "use strict";

    /*
       Global image fallback for Customer1.

       Your current HTML and JavaScript use /images/no-image.png,
       but that file may not exist. This listener catches broken
       images, including images added later by infinite scroll,
       modal loading, and related-item rendering.
    */

    const fallbackUrl = "/images/product-placeholder.svg";

    document.addEventListener(
        "error",
        event => {
            const image = event.target;

            if (!(image instanceof HTMLImageElement)) {
                return;
            }

            if (image.dataset.realnestFallbackApplied === "true") {
                image.style.objectFit = "contain";
                image.style.background = "#f3f4f6";
                return;
            }

            event.preventDefault();
            event.stopImmediatePropagation();

            image.dataset.realnestFallbackApplied = "true";
            image.onerror = null;
            image.src = fallbackUrl;
            image.style.objectFit = "contain";
            image.style.background = "#f3f4f6";
        },
        true
    );
})();