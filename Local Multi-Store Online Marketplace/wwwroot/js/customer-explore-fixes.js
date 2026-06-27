(() => {
    "use strict";

    const fallbackUrl =
        "/images/product-placeholder.svg";

    document.addEventListener(
        "error",
        event => {
            const image = event.target;

            if (
                !(image instanceof HTMLImageElement)
            ) {
                return;
            }

            if (image.closest(".tile-store")) {
                image.style.display = "none";

                const fallback =
                    image.nextElementSibling;

                if (fallback) {
                    fallback.style.display =
                        "flex";
                }

                return;
            }

            if (
                image.dataset
                    .realnestFallbackApplied ===
                "true"
            ) {
                image.style.objectFit =
                    "contain";

                image.style.background =
                    "#f3f4f6";

                return;
            }

            image.dataset
                .realnestFallbackApplied =
                "true";

            image.onerror = null;
            image.src = fallbackUrl;

            image.style.objectFit =
                "contain";

            image.style.background =
                "#f3f4f6";
        },
        true
    );
})();