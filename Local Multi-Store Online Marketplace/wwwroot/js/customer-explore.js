(() => {
    "use strict";

    const app = document.getElementById("exploreApp");

    if (!app) {
        return;
    }

    const pageUrl = "/Customer1";
    const grid = document.getElementById("exploreGrid");
    const sentinel = document.getElementById("infiniteSentinel");
    const loader = document.getElementById("infiniteLoader");
    const endOfFeed = document.getElementById("endOfFeed");
    const emptyState = document.getElementById("emptyExploreState");
    const toastContainer = document.getElementById("toastContainer");

    const modal = document.getElementById("exploreModal");
    const modalLoading = document.getElementById("modalLoading");
    const modalMediaStage = document.getElementById("modalMediaStage");
    const modalMediaContent = document.getElementById("modalMediaContent");
    const modalInformation = document.getElementById("modalInformation");

    const previousMediaButton = document.getElementById("mediaPreviousButton");
    const nextMediaButton = document.getElementById("mediaNextButton");
    const mediaCounter = document.getElementById("mediaCounter");

    const modalStoreLink = document.getElementById("modalStoreLink");
    const modalStoreAvatar = document.getElementById("modalStoreAvatar");
    const modalStoreName = document.getElementById("modalStoreName");
    const modalPostDate = document.getElementById("modalPostDate");
    const modalFollowButton = document.getElementById("modalFollowButton");

    const modalCaption = document.getElementById("modalCaption");
    const modalViewCount = document.getElementById("modalViewCount");
    const modalLikeCount = document.getElementById("modalLikeCount");
    const modalCommentCount = document.getElementById("modalCommentCount");
    const modalLikeButton = document.getElementById("modalLikeButton");
    const focusCommentButton = document.getElementById("focusCommentButton");
    const modalShareButton = document.getElementById("modalShareButton");
    const modalStats = document.getElementById("modalStats");
    const modalViewStat = document.getElementById("modalViewStat");

    const reelsPlayer = document.getElementById("reelsPlayer");
    const reelsTrack = document.getElementById("reelsTrack");
    const reelsCloseButton = document.getElementById("reelsCloseButton");

    const linkedProductSection = document.getElementById("linkedProductSection");
    const linkedProductLabel = document.getElementById("linkedProductLabel");
    const linkedProductLink = document.getElementById("linkedProductLink");
    const linkedProductImage = document.getElementById("linkedProductImage");
    const linkedProductCategory = document.getElementById("linkedProductCategory");
    const linkedProductName = document.getElementById("linkedProductName");
    const linkedProductDescription = document.getElementById("linkedProductDescription");
    const linkedProductPrice = document.getElementById("linkedProductPrice");
    const wishlistProductButton = document.getElementById("wishlistProductButton");
    const cartProductButton = document.getElementById("cartProductButton");
    const outOfStockMessage = document.getElementById("outOfStockMessage");

    const commentsSection = document.getElementById("commentsSection");
    const commentForm = document.getElementById("commentForm");
    const commentTextInput = document.getElementById("commentTextInput");
    const modalComments = document.getElementById("modalComments");
    const emptyComments = document.getElementById("emptyComments");

    const productReviewsSection = document.getElementById("productReviewsSection");
    const productReviewRating = document.getElementById("productReviewRating");
    const productReviewStars = document.getElementById("productReviewStars");
    const productReviewCount = document.getElementById("productReviewCount");
    const modalProductReviews = document.getElementById("modalProductReviews");
    const emptyProductReviews = document.getElementById("emptyProductReviews");

    const productReviewForm = document.getElementById("productReviewForm");
    const productReviewProductIdInput = document.getElementById("productReviewProductId");
    const productReviewRatingInput = document.getElementById("productReviewRatingInput");
    const productReviewCommentInput = document.getElementById("productReviewCommentInput");
    const productReviewStarPicker = document.getElementById("productReviewStarPicker");
    const productReviewStarHint = document.getElementById("productReviewStarHint");
    const productReviewStarButtons = productReviewStarPicker
        ? Array.from(productReviewStarPicker.querySelectorAll(".star-pick"))
        : [];

    const relatedItemsGrid = document.getElementById("relatedItemsGrid");
    const emptyRelatedItems = document.getElementById("emptyRelatedItems");

    let currentPage = Number(app.dataset.currentPage || "1");
    let hasMore = app.dataset.hasMore === "true";
    let isLoadingMore = false;

    let currentPost = null;
    let currentMediaIndex = 0;
    let savedScrollPosition = 0;

    loader?.classList.add("hidden");

    const antiForgeryToken =
        document.querySelector(
            '#antiForgeryForm input[name="__RequestVerificationToken"]'
        )?.value || "";


    const loadedGridItems = new Set();

    function getGridItemKey(itemType, postId, productId) {
        const type = String(itemType || "").toLowerCase();

        if (type === "post" && postId) {
            return `post-${postId}`;
        }

        if (productId) {
            return `product-${productId}`;
        }

        return "";
    }

    grid?.querySelectorAll("[data-grid-item]").forEach(tile => {
        const key = getGridItemKey(
            tile.dataset.itemType,
            tile.dataset.postId,
            tile.dataset.productId
        );

        if (key) {
            loadedGridItems.add(key);
        }
    });

    // =========================================================
    // HELPERS
    // =========================================================
    function escapeHtml(value) {
        return String(value ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#039;");
    }

    function formatMoney(value) {
        const number = Number(value);

        if (!Number.isFinite(number)) {
            return "";
        }

        return new Intl.NumberFormat("en-US", {
            style: "currency",
            currency: "USD"
        }).format(number);
    }

    function formatDate(value) {
        if (!value) {
            return "";
        }

        const date = new Date(value);

        if (Number.isNaN(date.getTime())) {
            return "";
        }

        return new Intl.DateTimeFormat("en", {
            dateStyle: "medium",
            timeStyle: "short"
        }).format(date);
    }

    function getInitial(value, fallback = "S") {
        const clean = String(value ?? "").trim();
        return clean ? clean.charAt(0).toUpperCase() : fallback;
    }

    function showToast(message, type = "success") {
        if (!toastContainer || !message) {
            return;
        }

        const toast = document.createElement("div");
        toast.className = `app-toast ${type}`;

        toast.innerHTML = `
            <i class="fa-solid ${type === "success"
                ? "fa-circle-check"
                : "fa-circle-exclamation"
            }"></i>

            <span>${escapeHtml(message)}</span>

            <button type="button" aria-label="Close notification">
                <i class="fa-solid fa-xmark"></i>
            </button>
        `;

        const closeButton = toast.querySelector("button");

        closeButton?.addEventListener("click", () => {
            toast.remove();
        });

        toastContainer.appendChild(toast);

        window.setTimeout(() => {
            toast.remove();
        }, 4200);
    }

    async function readJsonResponse(response) {
        let data = null;

        try {
            data = await response.json();
        } catch {
            data = {
                success: false,
                message: "The server returned an invalid response."
            };
        }

        if (response.status === 401) {
            window.location.href =
                "/Identity/Account/Login?returnUrl=%2FCustomer1";

            throw new Error("Authentication required.");
        }

        if (!response.ok || data?.success === false) {
            throw new Error(
                data?.message || "The request could not be completed."
            );
        }

        return data;
    }

    async function postForm(handler, values) {
        const body = new URLSearchParams();

        Object.entries(values).forEach(([key, value]) => {
            if (value !== null && value !== undefined) {
                body.append(key, String(value));
            }
        });

        const response = await fetch(
            `${pageUrl}?handler=${encodeURIComponent(handler)}`,
            {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Content-Type":
                        "application/x-www-form-urlencoded;charset=UTF-8",
                    "RequestVerificationToken": antiForgeryToken
                },
                body
            }
        );

        return readJsonResponse(response);
    }

    // =========================================================
    // GRID ITEM HTML
    // =========================================================
    function createGridItemElement(item) {
        const isPost =
            String(item.gridItemType).toLowerCase() === "post";

        const isVideo =
            String(item.mediaType).toLowerCase() === "video";

        const isReel =
            String(item.postType).toLowerCase() === "reel";

        const isCarousel =
            String(item.postType).toLowerCase() === "carousel";

        const button = document.createElement("button");
        button.type = "button";
        button.className = "explore-tile";
        button.dataset.gridItem = "";
        button.dataset.itemType = item.gridItemType || "Post";
        button.dataset.postType = item.postType || "";

        if (item.explorePostID) {
            button.dataset.postId = String(item.explorePostID);
        }

        if (item.productID) {
            button.dataset.productId = String(item.productID);
        }

        button.setAttribute(
            "aria-label",
            isPost
                ? `Open post from ${item.storeName || "store"}`
                : `Open product ${item.productName || ""}`
        );

        const mediaUrl = isVideo
            ? item.thumbnailUrl || item.mediaUrl
            : item.mediaUrl;

        let mediaMarkup;

        if (isVideo && !item.thumbnailUrl) {
            mediaMarkup = `
                <video muted loop playsinline preload="metadata"
                       data-reel-preview>
                    <source src="${escapeHtml(item.mediaUrl)}" />
                </video>
            `;
        } else {
            mediaMarkup = `
                <img src="${escapeHtml(
                mediaUrl || "/images/product-placeholder.svg"
            )}"
                     alt="${escapeHtml(
                item.productName || item.storeName || "Explore item"
            )}"
                     loading="lazy"
                     onerror="this.onerror=null;this.src='/images/product-placeholder.svg';" />
            `;
        }

        let badgeMarkup = "";

        if (isReel) {
            badgeMarkup = `
                <span class="tile-badge">
                    <i class="fa-solid fa-play"></i>
                    Reel
                </span>
            `;
        } else if (isCarousel) {
            badgeMarkup = `
                <span class="tile-badge">
                    <i class="fa-regular fa-images"></i>
                    ${Number(item.mediaCount || 0)}
                </span>
            `;
        } else if (!isPost) {
            badgeMarkup = `
                <span class="tile-badge">
                    <i class="fa-solid fa-bag-shopping"></i>
                    Product
                </span>
            `;
        }

        const priceMarkup =
            item.productPrice !== null &&
                item.productPrice !== undefined
                ? `<span class="tile-price">${formatMoney(
                    item.productPrice
                )}</span>`
                : "";

        const storeLogoMarkup = item.storeLogoUrl
            ? `
                <img src="${escapeHtml(item.storeLogoUrl)}"
                     alt="${escapeHtml(item.storeName)}"
                     onerror="this.style.display='none';this.nextElementSibling.style.display='flex';" />

                <span class="tile-store-fallback">
                    ${escapeHtml(getInitial(item.storeName))}
                </span>
            `
            : `
                <span class="tile-store-fallback visible">
                    ${escapeHtml(getInitial(item.storeName))}
                </span>
            `;

        button.innerHTML = `
            <span class="tile-media">
                ${mediaMarkup}
            </span>

            <span class="tile-gradient"></span>

            <span class="tile-top">
                ${badgeMarkup}
                ${priceMarkup}
            </span>

            <span class="tile-bottom">
                <span class="tile-store">
                    ${storeLogoMarkup}
                    <span>${escapeHtml(item.storeName || "Store")}</span>
                </span>

                ${item.productName
                ? `<strong>${escapeHtml(item.productName)}</strong>`
                : ""
            }
            </span>
        `;

        observeReelVideos(button);

        return button;
    }

    // =========================================================
    // INFINITE SCROLL
    // =========================================================
    async function loadMoreItems() {
        if (!hasMore || isLoadingMore || !grid) {
            return;
        }

        isLoadingMore = true;
        loader?.classList.remove("hidden");
        endOfFeed?.classList.add("hidden");

        try {
            const nextPage = currentPage + 1;
            const category = app.dataset.category || "";
            const searchTerm = app.dataset.searchTerm || "";
            const categoryId = app.dataset.categoryId || "";
            const storeId = app.dataset.storeId || "";
            const area = app.dataset.area || "";
            const minPrice = app.dataset.minPrice || "";
            const maxPrice = app.dataset.maxPrice || "";
            const typeFilter = app.dataset.typeFilter || "";

            const response = await fetch(
                `${pageUrl}?handler=ExplorePage` +
                `&pageNumber=${encodeURIComponent(nextPage)}` +
                `&category=${encodeURIComponent(category)}` +
                `&searchTerm=${encodeURIComponent(searchTerm)}` +
                `&categoryId=${encodeURIComponent(categoryId)}` +
                `&storeId=${encodeURIComponent(storeId)}` +
                `&area=${encodeURIComponent(area)}` +
                `&minPrice=${encodeURIComponent(minPrice)}` +
                `&maxPrice=${encodeURIComponent(maxPrice)}` +
                `&typeFilter=${encodeURIComponent(typeFilter)}`,
                {
                    method: "GET",
                    credentials: "same-origin",
                    cache: "no-store",
                    headers: {
                        Accept: "application/json"
                    }
                }
            );

            const data = await readJsonResponse(response);
            const returnedItems = Array.isArray(data.items)
                ? data.items
                : [];

            const newItems = returnedItems.filter(item => {
                const key = getGridItemKey(
                    item.gridItemType,
                    item.explorePostID,
                    item.productID
                );

                if (!key || loadedGridItems.has(key)) {
                    return false;
                }

                loadedGridItems.add(key);
                return true;
            });

            if (newItems.length > 0) {
                const fragment = document.createDocumentFragment();

                newItems.forEach(item => {
                    fragment.appendChild(
                        createGridItemElement(item)
                    );
                });

                grid.appendChild(fragment);
                emptyState?.classList.add("hidden");

                appendReelsFromNewItems(newItems);
            }

            currentPage = Number(data.page || nextPage);
            hasMore = data.hasMore === true;

            app.dataset.currentPage = String(currentPage);
            app.dataset.hasMore = String(hasMore);

            if (!hasMore) {
                endOfFeed?.classList.remove("hidden");
                infiniteObserver?.disconnect();
            }
        } catch (error) {
            showToast(
                error.message || "Could not load more items.",
                "error"
            );
        } finally {
            isLoadingMore = false;
            loader?.classList.add("hidden");
        }
    }

    const infiniteObserver = sentinel
        ? new IntersectionObserver(
            entries => {
                if (entries.some(entry => entry.isIntersecting)) {
                    loadMoreItems();
                }
            },
            {
                root: null,
                rootMargin: "300px 0px",
                threshold: 0
            }
        )
        : null;

    if (sentinel && infiniteObserver) {
        infiniteObserver.observe(sentinel);
    }

    let scrollCheckPending = false;

    window.addEventListener(
        "scroll",
        () => {
            if (scrollCheckPending) {
                return;
            }

            scrollCheckPending = true;

            window.requestAnimationFrame(() => {
                const bottomDistance =
                    document.documentElement.scrollHeight -
                    (window.scrollY + window.innerHeight);

                if (bottomDistance <= 450) {
                    loadMoreItems();
                }

                scrollCheckPending = false;
            });
        },
        { passive: true }
    );

    // =========================================================
    // REEL PREVIEWS
    // =========================================================
    const reelObserver = new IntersectionObserver(
        entries => {
            entries.forEach(entry => {
                const video = entry.target;

                if (!(video instanceof HTMLVideoElement)) {
                    return;
                }

                if (entry.isIntersecting) {
                    video.play().catch(() => {
                    });
                } else {
                    video.pause();
                }
            });
        },
        {
            root: null,
            rootMargin: "120px",
            threshold: 0.55
        }
    );

    function observeReelVideos(root = document) {
        root.querySelectorAll?.(
            "video[data-reel-preview]:not([data-observed])"
        ).forEach(video => {
            video.dataset.observed = "true";
            reelObserver.observe(video);
        });
    }

    observeReelVideos();

    // =========================================================
    // GRID CLICK
    // =========================================================
    grid?.addEventListener("click", event => {
        const tile = event.target.closest("[data-grid-item]");

        if (!tile) {
            return;
        }

        const itemType = String(
            tile.dataset.itemType || ""
        ).toLowerCase();

        const postType = String(
            tile.dataset.postType || ""
        ).toLowerCase();

        if (itemType === "post" && postType === "reel" && tile.dataset.postId) {
            openReelsPlayerFromTile(tile);
            return;
        }

        if (itemType === "post" && tile.dataset.postId) {
            openExplorePost(Number(tile.dataset.postId));
            return;
        }

        if (tile.dataset.productId) {
            openExploreProduct(Number(tile.dataset.productId));
        }
    });

    // Trending rail click handling (reuses the same open logic as the
    // main grid; separate listener since the rail lives outside #exploreGrid).
    document.querySelector(".trending-rail-track")?.addEventListener("click", event => {
        const tile = event.target.closest("[data-grid-item]");
        if (!tile) return;

        const postType = String(tile.dataset.postType || "").toLowerCase();

        if (postType === "reel" && tile.dataset.postId) {
            openReelsPlayerFromTile(tile);
            return;
        }

        if (tile.dataset.postId) {
            openExplorePost(Number(tile.dataset.postId));
        }
    });

    // =========================================================
    // REELS PLAYER (full-screen, Instagram-Reels-style)
    // =========================================================
    const reelsDetailsCache = new Map();
    let reelsMuted = true;
    let reelsObserver = null;
    let activeReelSlide = null;

    function buildReelsQueueFromGrid() {
        const tiles = grid
            ? Array.from(grid.querySelectorAll("[data-grid-item]"))
            : [];

        return tiles
            .filter(tile => String(tile.dataset.postType || "").toLowerCase() === "reel")
            .map(tile => Number(tile.dataset.postId))
            .filter(id => Number.isFinite(id) && id > 0);
    }

    function openReelsPlayerFromTile(tile) {
        const queue = buildReelsQueueFromGrid();
        const startId = Number(tile.dataset.postId);
        let startIndex = queue.indexOf(startId);

        if (startIndex === -1) {
            queue.unshift(startId);
            startIndex = 0;
        }

        openReelsPlayer(queue, startIndex);
    }

    function createReelSlide(postId) {
        const slide = document.createElement("div");
        slide.className = "reel-slide";
        slide.dataset.postId = String(postId);

        slide.innerHTML = `
            <div class="reel-loading">
                <span class="loader-spinner large"></span>
            </div>

            <video class="reel-video" loop playsinline preload="metadata" muted></video>

            <div class="reel-video-error hidden">
                <i class="fa-solid fa-triangle-exclamation"></i>
                <span>This video couldn't be loaded.</span>
            </div>

            <button type="button" class="reel-mute-toggle" aria-label="Toggle sound">
                <i class="fa-solid fa-volume-xmark"></i>
            </button>

            <div class="reel-overlay-top">
                <span class="reel-avatar"></span>
                <strong class="reel-store-name"></strong>
                <button type="button" class="reel-follow-button">Follow</button>
            </div>

            <div class="reel-overlay-side">
                <button type="button" class="reel-action reel-like" aria-label="Like">
                    <i class="fa-regular fa-heart"></i>
                    <span class="reel-like-count">0</span>
                </button>

                <button type="button" class="reel-action reel-comment" aria-label="Comment">
                    <i class="fa-regular fa-comment"></i>
                    <span class="reel-comment-count">0</span>
                </button>

                <button type="button" class="reel-action reel-share" aria-label="Share">
                    <i class="fa-regular fa-paper-plane"></i>
                </button>
            </div>

            <div class="reel-caption"></div>

            <button type="button" class="reel-product-chip hidden" aria-label="View linked product">
                <img class="reel-product-chip-img" src="" alt="" />
                <span class="reel-product-chip-text">
                    <strong class="reel-product-chip-name"></strong>
                    <span class="reel-product-chip-price"></span>
                </span>
                <i class="fa-solid fa-chevron-up"></i>
            </button>

            <div class="reel-product-sheet">
                <button type="button" class="reel-product-sheet-close" aria-label="Close product card">
                    <i class="fa-solid fa-xmark"></i>
                </button>

                <button type="button" class="reel-product-sheet-main">
                    <img class="reel-product-sheet-img" src="/images/product-placeholder.svg" alt="" />
                    <span class="reel-product-sheet-info">
                        <small class="reel-product-sheet-category"></small>
                        <strong class="reel-product-sheet-name"></strong>
                        <span class="reel-product-sheet-price"></span>
                    </span>
                </button>

                <div class="reel-product-sheet-actions">
                    <button type="button" class="reel-product-save">
                        <i class="fa-regular fa-heart"></i> Save
                    </button>
                    <button type="button" class="reel-product-cart">
                        <i class="fa-solid fa-bag-shopping"></i> Add to cart
                    </button>
                </div>

                <button type="button" class="reel-product-sheet-details">
                    See full details
                </button>
            </div>
        `;


        return slide;
    }

    async function fetchReelDetails(postId) {
        if (reelsDetailsCache.has(postId)) {
            return reelsDetailsCache.get(postId);
        }

        const response = await fetch(
            `${pageUrl}?handler=ExplorePostDetails&id=${postId}`,
            {
                method: "GET",
                credentials: "same-origin",
                cache: "no-store",
                headers: { Accept: "application/json" }
            }
        );

        const data = await readJsonResponse(response);
        reelsDetailsCache.set(postId, data.post);
        return data.post;
    }

    function populateReelSlide(slide, details) {
        slide.dataset.storeId = String(details.storeID);

        const video = slide.querySelector(".reel-video");
        const errorOverlay = slide.querySelector(".reel-video-error");
        const media = (details.media || []).find(
            m => String(m.mediaType).toLowerCase() === "video"
        ) || details.media?.[0];

        if (video && media) {
            video.src = media.mediaUrl;

            video.addEventListener("error", () => {
                errorOverlay?.classList.remove("hidden");
                slide.querySelector(".reel-loading")?.remove();
            });
        }

        slide.querySelector(".reel-loading")?.remove();

        const avatar = slide.querySelector(".reel-avatar");

        if (details.storeLogoUrl) {
            avatar.style.backgroundImage =
                `url("${String(details.storeLogoUrl).replaceAll('"', '\\"')}")`;
            avatar.textContent = "";
        } else {
            avatar.textContent = getInitial(details.storeName);
        }

        slide.querySelector(".reel-store-name").textContent =
            details.storeName || "Store";
        slide.querySelector(".reel-caption").textContent =
            details.caption || "";

        const followButton = slide.querySelector(".reel-follow-button");
        followButton.classList.toggle("following", details.isFollowingStore === true);
        followButton.textContent = details.isFollowingStore ? "Following" : "Follow";

        const likeButton = slide.querySelector(".reel-like");
        likeButton.classList.toggle("liked", details.isLikedByCurrentCustomer === true);
        likeButton.querySelector("i").className = details.isLikedByCurrentCustomer
            ? "fa-solid fa-heart"
            : "fa-regular fa-heart";
        likeButton.querySelector(".reel-like-count").textContent =
            Number(details.likeCount || 0).toLocaleString();

        slide.querySelector(".reel-comment-count").textContent =
            Number(details.commentCount || 0).toLocaleString();

        populateReelProduct(slide, details);
    }

    function populateReelProduct(slide, details) {
        const chip = slide.querySelector(".reel-product-chip");
        const sheet = slide.querySelector(".reel-product-sheet");

        const hasProduct =
            details.productID !== null && details.productID !== undefined;

        if (!hasProduct) {
            chip?.classList.add("hidden");
            return;
        }

        const imageUrl = details.productImageUrl || "/images/product-placeholder.svg";

        chip?.classList.remove("hidden");
        chip.querySelector(".reel-product-chip-img").src = imageUrl;
        chip.querySelector(".reel-product-chip-name").textContent =
            details.productName || "Product";
        chip.querySelector(".reel-product-chip-price").textContent =
            formatMoney(details.productPrice);

        sheet.querySelector(".reel-product-sheet-img").src = imageUrl;
        sheet.querySelector(".reel-product-sheet-category").textContent =
            details.categoryName || "Product";
        sheet.querySelector(".reel-product-sheet-name").textContent =
            details.productName || "Product";
        sheet.querySelector(".reel-product-sheet-price").textContent =
            formatMoney(details.productPrice);

        const saveButton = sheet.querySelector(".reel-product-save");
        const saved = details.isInWishlist === true;
        saveButton.classList.toggle("saved", saved);
        saveButton.innerHTML = saved
            ? '<i class="fa-solid fa-heart"></i> Saved'
            : '<i class="fa-regular fa-heart"></i> Save';

        const cartButton = sheet.querySelector(".reel-product-cart");
        const isOutOfStock = details.isOutOfStock === true;
        cartButton.disabled = isOutOfStock;
        cartButton.innerHTML = isOutOfStock
            ? '<i class="fa-solid fa-ban"></i> Out of stock'
            : '<i class="fa-solid fa-bag-shopping"></i> Add to cart';
    }

    function updateMuteIcon(button, muted) {
        const icon = button.querySelector("i");

        if (icon) {
            icon.className = muted
                ? "fa-solid fa-volume-xmark"
                : "fa-solid fa-volume-high";
        }
    }

    function wireReelSlideActions(slide, postId) {
        const video = slide.querySelector(".reel-video");
        const muteToggle = slide.querySelector(".reel-mute-toggle");
        const likeButton = slide.querySelector(".reel-like");
        const shareButton = slide.querySelector(".reel-share");
        const commentButton = slide.querySelector(".reel-comment");
        const followButton = slide.querySelector(".reel-follow-button");

        wireReelProductActions(slide, postId);

        video.muted = reelsMuted;
        updateMuteIcon(muteToggle, reelsMuted);

        video.addEventListener("click", () => {
            if (video.paused) {
                video.play().catch(() => { });
            } else {
                video.pause();
            }
        });

        muteToggle.addEventListener("click", event => {
            event.stopPropagation();
            reelsMuted = !reelsMuted;
            video.muted = reelsMuted;
            updateMuteIcon(muteToggle, reelsMuted);
        });

        likeButton.addEventListener("click", async event => {
            event.stopPropagation();
            likeButton.disabled = true;

            try {
                const data = await postForm("ToggleExploreLike", { postId });

                likeButton.classList.toggle("liked", data.liked === true);
                likeButton.querySelector("i").className = data.liked
                    ? "fa-solid fa-heart"
                    : "fa-regular fa-heart";
                likeButton.querySelector(".reel-like-count").textContent =
                    Number(data.likeCount || 0).toLocaleString();

                const cached = reelsDetailsCache.get(postId);

                if (cached) {
                    cached.isLikedByCurrentCustomer = data.liked === true;
                    cached.likeCount = data.likeCount;
                }
            } catch (error) {
                showToast(error.message, "error");
            } finally {
                likeButton.disabled = false;
            }
        });

        commentButton.addEventListener("click", event => {
            event.stopPropagation();
            closeReelsPlayer();

            openExplorePost(postId).then(() => {
                focusCommentButton?.click();
            });
        });

        shareButton.addEventListener("click", async event => {
            event.stopPropagation();

            const shareUrl = `${window.location.origin}${pageUrl}#post-${postId}`;
            const cached = reelsDetailsCache.get(postId);

            const shareData = {
                title: `${cached?.storeName || "Store"} on realnest`,
                text: cached?.caption || "See this reel on realnest.",
                url: shareUrl
            };

            try {
                if (navigator.share) {
                    await navigator.share(shareData);
                } else {
                    await navigator.clipboard.writeText(shareUrl);
                    showToast("Item link copied.", "success");
                }

                const icon = shareButton.querySelector("i");
                shareButton.classList.add("shared");
                icon.className = "fa-solid fa-check";

                window.setTimeout(() => {
                    shareButton.classList.remove("shared");
                    icon.className = "fa-regular fa-paper-plane";
                }, 1500);
            } catch (error) {
                if (error?.name !== "AbortError") {
                    showToast(
                        "The item link could not be shared.",
                        "error"
                    );
                }
            }
        });

        followButton.addEventListener("click", async event => {
            event.stopPropagation();
            const storeId = Number(slide.dataset.storeId);

            if (!Number.isFinite(storeId)) {
                return;
            }

            followButton.disabled = true;

            try {
                const data = await postForm(
                    "ToggleExploreStoreFollow",
                    { storeId }
                );

                followButton.classList.toggle("following", data.following === true);
                followButton.textContent = data.following ? "Following" : "Follow";
            } catch (error) {
                showToast(error.message, "error");
            } finally {
                followButton.disabled = false;
            }
        });
    }

    function wireReelProductActions(slide, postId) {
        const chip = slide.querySelector(".reel-product-chip");
        const sheet = slide.querySelector(".reel-product-sheet");
        const closeButton = slide.querySelector(".reel-product-sheet-close");
        const saveButton = slide.querySelector(".reel-product-save");
        const cartButton = slide.querySelector(".reel-product-cart");
        const sheetMainButton = slide.querySelector(".reel-product-sheet-main");
        const sheetDetailsButton = slide.querySelector(".reel-product-sheet-details");

        chip?.addEventListener("click", event => {
            event.stopPropagation();
            sheet?.classList.add("open");
        });

        closeButton?.addEventListener("click", event => {
            event.stopPropagation();
            sheet?.classList.remove("open");
        });

        function openLinkedProductModal(event) {
            event.stopPropagation();

            const cached = reelsDetailsCache.get(postId);
            const productId = cached?.productID;

            if (productId === null || productId === undefined) {
                return;
            }

            closeReelsPlayer();
            openExploreProduct(productId);
        }

        sheetMainButton?.addEventListener("click", openLinkedProductModal);
        sheetDetailsButton?.addEventListener("click", openLinkedProductModal);

        saveButton?.addEventListener("click", async event => {
            event.stopPropagation();

            const cached = reelsDetailsCache.get(postId);
            const productId = cached?.productID;

            if (productId === null || productId === undefined) {
                return;
            }

            saveButton.disabled = true;

            try {
                const data = await postForm(
                    "ToggleExploreWishlist",
                    { productId }
                );

                const saved = data.saved === true;
                saveButton.classList.toggle("saved", saved);
                saveButton.innerHTML = saved
                    ? '<i class="fa-solid fa-heart"></i> Saved'
                    : '<i class="fa-regular fa-heart"></i> Save';

                if (cached) {
                    cached.isInWishlist = saved;
                }

                showToast(data.message, "success");
            } catch (error) {
                showToast(error.message, "error");
            } finally {
                saveButton.disabled = false;
            }
        });

        cartButton?.addEventListener("click", async event => {
            event.stopPropagation();

            if (cartButton.disabled) {
                return;
            }

            const cached = reelsDetailsCache.get(postId);
            const productId = cached?.productID;

            if (productId === null || productId === undefined) {
                return;
            }

            cartButton.disabled = true;

            try {
                const data = await postForm(
                    "ExploreAddToCart",
                    { productId }
                );

                cartButton.innerHTML =
                    '<i class="fa-solid fa-check"></i> Added';

                showToast(data.message, "success");
                refreshCartBadge();
            } catch (error) {
                showToast(error.message, "error");
                cartButton.disabled = false;
            }
        });
    }

    function appendReelsFromNewItems(newItems) {
        if (!reelsTrack || !reelsPlayer?.classList.contains("open")) {
            return;
        }

        const existingIds = new Set(
            Array.from(reelsTrack.children).map(
                slide => slide.dataset.postId
            )
        );

        newItems
            .filter(item => String(item.postType).toLowerCase() === "reel")
            .forEach(item => {
                const id = String(item.explorePostID);

                if (!id || existingIds.has(id)) {
                    return;
                }

                const slide = createReelSlide(item.explorePostID);
                reelsTrack.appendChild(slide);
                reelsObserver?.observe(slide);
            });
    }

    async function activateReelSlide(slide) {
        if (activeReelSlide === slide) {
            return;
        }

        reelsTrack?.querySelectorAll("video").forEach(video => {
            if (video !== slide.querySelector("video")) {
                video.pause();
            }
        });

        activeReelSlide = slide;

        const slides = Array.from(reelsTrack?.children || []);
        const currentIndex = slides.indexOf(slide);

        if (currentIndex >= slides.length - 2) {
            loadMoreItems();
        }

        const postId = Number(slide.dataset.postId);

        if (slide.dataset.loaded !== "true") {
            slide.dataset.loaded = "true";

            try {
                const details = await fetchReelDetails(postId);
                populateReelSlide(slide, details);
                wireReelSlideActions(slide, postId);
            } catch (error) {
                showToast(
                    error.message || "Could not load this reel.",
                    "error"
                );
            }
        }

        const video = slide.querySelector("video");

        if (video) {
            video.muted = reelsMuted;
            video.play().catch(() => { });
        }
    }

    function openReelsPlayer(postIds, startIndex) {
        if (!reelsPlayer || !reelsTrack || !postIds.length) {
            return;
        }

        reelsTrack.innerHTML = "";

        postIds.forEach(id => {
            reelsTrack.appendChild(createReelSlide(id));
        });

        reelsPlayer.classList.add("open");
        reelsPlayer.setAttribute("aria-hidden", "false");
        document.body.classList.add("modal-open");
        document.body.classList.add("reels-open");
        document.documentElement.style.overflow = "hidden";
        document.body.style.overflow = "hidden";

        const slides = Array.from(reelsTrack.children);
        const startSlide = slides[startIndex] || slides[0];

        startSlide.scrollIntoView({ block: "start" });
        activateReelSlide(startSlide);

        reelsObserver?.disconnect();
        reelsObserver = new IntersectionObserver(
            entries => {
                entries.forEach(entry => {
                    if (entry.isIntersecting && entry.intersectionRatio > 0.6) {
                        activateReelSlide(entry.target);
                    }
                });
            },
            { root: reelsTrack, threshold: [0, 0.6, 1] }
        );

        slides.forEach(slide => reelsObserver.observe(slide));

        window.setTimeout(() => reelsCloseButton?.focus(), 50);
    }

    function closeReelsPlayer() {
        reelsObserver?.disconnect();
        reelsObserver = null;
        activeReelSlide = null;

        reelsTrack?.querySelectorAll("video").forEach(video => {
            video.pause();
            video.removeAttribute("src");
            video.load();
        });

        reelsPlayer?.classList.remove("open");
        reelsPlayer?.setAttribute("aria-hidden", "true");
        document.body.classList.remove("modal-open");
        document.body.classList.remove("reels-open");
        document.documentElement.style.overflow = "";
        document.body.style.overflow = "";

        if (reelsTrack) {
            reelsTrack.innerHTML = "";
        }
    }

    reelsCloseButton?.addEventListener("click", closeReelsPlayer);

    function showAdjacentReelSlide(direction) {
        if (!reelsTrack || !activeReelSlide) {
            return;
        }

        const slides = Array.from(reelsTrack.children);
        const currentIndex = slides.indexOf(activeReelSlide);
        const targetIndex = currentIndex + direction;
        const target = slides[targetIndex];

        target?.scrollIntoView({ block: "start", behavior: "smooth" });
    }

    function getReelsFocusableElements() {
        if (!reelsPlayer) {
            return [];
        }

        return Array.from(
            reelsPlayer.querySelectorAll(
                "button, a[href], video, [tabindex]:not([tabindex='-1'])"
            )
        ).filter(el => el.offsetParent !== null);
    }

    document.addEventListener("keydown", event => {
        if (!reelsPlayer?.classList.contains("open")) {
            return;
        }

        if (event.key === "Escape") {
            closeReelsPlayer();
            return;
        }

        if (event.key === "ArrowUp") {
            event.preventDefault();
            showAdjacentReelSlide(-1);
            return;
        }

        if (event.key === "ArrowDown") {
            event.preventDefault();
            showAdjacentReelSlide(1);
            return;
        }

        if (event.key === "Tab") {
            const focusable = getReelsFocusableElements();

            if (focusable.length === 0) {
                return;
            }

            const first = focusable[0];
            const last = focusable[focusable.length - 1];

            if (event.shiftKey && document.activeElement === first) {
                event.preventDefault();
                last.focus();
            } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first.focus();
            }
        }
    });

    // =========================================================
    // OPEN / CLOSE MODAL
    // =========================================================
    function beginModalOpen() {
        savedScrollPosition = window.scrollY;

        modal?.classList.add("open");
        modal?.setAttribute("aria-hidden", "false");
        document.body.classList.add("modal-open");

        modalLoading?.classList.remove("hidden");
        modalMediaStage?.classList.add("hidden");
        modalInformation?.classList.add("hidden");

        currentPost = null;
        currentMediaIndex = 0;
    }

    function finishModalOpen(content) {
        currentPost = content;
        renderExploreContent(content);

        modalLoading?.classList.add("hidden");
        modalMediaStage?.classList.remove("hidden");
        modalInformation?.classList.remove("hidden");
    }

    async function openExplorePost(postId) {
        if (!Number.isFinite(postId) || postId <= 0) {
            return;
        }

        beginModalOpen();

        try {
            const response = await fetch(
                `${pageUrl}?handler=ExplorePostDetails&id=${postId}`,
                {
                    method: "GET",
                    credentials: "same-origin",
                    cache: "no-store",
                    headers: {
                        Accept: "application/json"
                    }
                }
            );

            const data = await readJsonResponse(response);
            finishModalOpen(data.post);
        } catch (error) {
            closeExploreModal();

            showToast(
                error.message || "Could not open the Explore item.",
                "error"
            );
        }
    }

    async function openExploreProduct(productId) {
        if (!Number.isFinite(productId) || productId <= 0) {
            return;
        }

        beginModalOpen();

        try {
            const response = await fetch(
                `${pageUrl}?handler=ExploreProductDetails&id=${productId}`,
                {
                    method: "GET",
                    credentials: "same-origin",
                    cache: "no-store",
                    headers: {
                        Accept: "application/json"
                    }
                }
            );

            const data = await readJsonResponse(response);
            finishModalOpen(data.product);
        } catch (error) {
            closeExploreModal();

            showToast(
                error.message || "Could not open the product.",
                "error"
            );
        }
    }

    function closeExploreModal() {
        stopModalVideos();

        modal?.classList.remove("open");
        modal?.setAttribute("aria-hidden", "true");
        document.body.classList.remove("modal-open");

        currentPost = null;
        currentMediaIndex = 0;

        window.requestAnimationFrame(() => {
            window.scrollTo(0, savedScrollPosition);
        });
    }

    document.querySelectorAll("[data-close-explore-modal]")
        .forEach(element => {
            element.addEventListener("click", closeExploreModal);
        });

    document.addEventListener("keydown", event => {
        if (event.key === "Escape" && modal?.classList.contains("open")) {
            closeExploreModal();
        }

        if (!modal?.classList.contains("open")) {
            return;
        }

        if (event.key === "ArrowLeft") {
            showPreviousMedia();
        }

        if (event.key === "ArrowRight") {
            showNextMedia();
        }
    });

    // =========================================================
    // RENDER SHARED MODAL DETAILS
    // =========================================================
    function renderExploreContent(content) {
        const isPost =
            String(content.contentType || "Post").toLowerCase() === "post";

        const hasProduct =
            content.productID !== null &&
            content.productID !== undefined;

        // Bigger modal card whenever a product is being shown, whether
        // it's a standalone product or a post's linked product.
        document.querySelector(".explore-modal-card")
            ?.classList.toggle("product-mode", hasProduct);

        modalStoreName.textContent = content.storeName || "Store";
        modalPostDate.textContent = isPost
            ? formatDate(content.createdAt)
            : `${content.categoryName || "Product"} · ${formatDate(content.createdAt)}`;

        modalStoreLink.href =
            `/StoreCustomerProfile?id=${encodeURIComponent(content.storeID)}`;

        renderStoreAvatar(content);

        modalFollowButton.dataset.storeId = String(content.storeID);
        setFollowButtonState(content.isFollowingStore === true);

        const caption = String(
            content.caption || content.productDescription || ""
        ).trim();

        modalCaption.textContent = caption || (
            isPost
                ? "No caption was added to this post."
                : "No product description was added."
        );

        modalCaption.classList.toggle("empty", !caption);

        modalViewStat?.classList.toggle("hidden", !isPost);
        modalLikeButton?.classList.toggle("hidden", !isPost);
        focusCommentButton?.classList.toggle("hidden", !isPost);
        commentsSection?.classList.toggle("hidden", !isPost);

        // Cart/wishlist icons only make sense when a product exists.
        wishlistProductButton?.classList.toggle("hidden", !hasProduct);
        cartProductButton?.classList.toggle("hidden", !hasProduct);

        // "Send in chat" only works for a linked product.
        document.getElementById("modalShareChatButton")
            ?.classList.toggle("hidden", !hasProduct);

        if (isPost) {
            modalViewCount.textContent =
                Number(content.viewCount || 0).toLocaleString();

            modalLikeCount.textContent =
                Number(content.likeCount || 0).toLocaleString();

            modalCommentCount.textContent =
                Number(content.commentCount || 0).toLocaleString();

            setLikeButtonState(
                content.isLikedByCurrentCustomer === true
            );

            renderComments(content.comments || []);
        } else {
            modalComments.innerHTML = "";
            emptyComments.classList.add("hidden");
        }

        renderMedia(content.media || []);
        renderLinkedProduct(content);
        renderProductReviews(
            hasProduct ? content.reviews || [] : [],
            content
        );
        renderRelatedItems(content.relatedItems || []);
    }

    function renderStoreAvatar(content) {
        modalStoreAvatar.textContent = "";
        modalStoreAvatar.style.backgroundImage = "";

        if (content.storeLogoUrl) {
            modalStoreAvatar.style.backgroundImage =
                `url("${String(content.storeLogoUrl)
                    .replaceAll('"', '\\"')}")`;
        } else {
            modalStoreAvatar.textContent =
                getInitial(content.storeName);
        }
    }

    function setFollowButtonState(following) {
        modalFollowButton.classList.toggle("following", following);
        modalFollowButton.textContent =
            following ? "Following" : "Follow";

        if (currentPost) {
            currentPost.isFollowingStore = following;
        }
    }

    function setLikeButtonState(liked) {
        modalLikeButton.classList.toggle("liked", liked);

        const icon = modalLikeButton.querySelector("i");

        if (icon) {
            icon.className = liked
                ? "fa-solid fa-heart"
                : "fa-regular fa-heart";
        }

        if (currentPost) {
            currentPost.isLikedByCurrentCustomer = liked;
        }
    }

    function setWishlistButtonState(saved) {
        wishlistProductButton.classList.toggle("liked", saved);
        wishlistProductButton.disabled = false;

        wishlistProductButton.innerHTML = saved
            ? '<i class="fa-solid fa-heart"></i>'
            : '<i class="fa-regular fa-heart"></i>';

        if (currentPost) {
            currentPost.isInWishlist = saved;
        }
    }

    function createStarMarkup(rating) {
        const safeRating = Math.max(0, Math.min(5, Number(rating) || 0));
        let markup = "";

        for (let index = 1; index <= 5; index += 1) {
            markup += index <= Math.round(safeRating)
                ? '<i class="fa-solid fa-star"></i>'
                : '<i class="fa-regular fa-star"></i>';
        }

        return markup;
    }

    function renderProductReviews(reviews, content) {
        const hasProduct =
            content.productID !== null &&
            content.productID !== undefined;

        productReviewsSection?.classList.toggle("hidden", !hasProduct);

        if (!hasProduct) {
            return;
        }

        const rating = Number(content.productRating || 0);
        const totalRatings = Number(
            content.productTotalRatings || reviews.length || 0
        );

        productReviewRating.textContent = rating.toFixed(1);
        productReviewStars.innerHTML = createStarMarkup(rating);
        productReviewCount.textContent =
            `${totalRatings.toLocaleString()} ${totalRatings === 1 ? "review" : "reviews"}`;

        if (productReviewProductIdInput) {
            productReviewProductIdInput.value = String(content.productID);
        }

        if (productReviewForm) {
            productReviewForm.reset();
        }

        setStarPickerValue(0);

        modalProductReviews.innerHTML = "";

        if (!Array.isArray(reviews) || reviews.length === 0) {
            emptyProductReviews.classList.remove("hidden");
            return;
        }

        emptyProductReviews.classList.add("hidden");

        reviews.forEach(review => {
            modalProductReviews.appendChild(
                createProductReviewElement(review)
            );
        });
    }

    function createProductReviewElement(review) {
        const item = document.createElement("article");
        item.className = "product-review-item";

        item.innerHTML = `
            <span class="comment-avatar">
                ${escapeHtml(getInitial(review.customerName, "C"))}
            </span>

            <div class="product-review-content">
                <div class="product-review-name-row">
                    <strong>${escapeHtml(review.customerName || "Customer")}</strong>
                    ${review.isVerifiedPurchase
                ? '<span class="verified-review">Verified purchase</span>'
                : ""
            }
                </div>

                <div class="review-stars">
                    ${createStarMarkup(review.rating)}
                </div>

                ${review.comment
                ? `<p>${escapeHtml(review.comment)}</p>`
                : ""
            }

                <small>${escapeHtml(formatDate(review.createdAt))}</small>
            </div>
        `;

        return item;
    }

    // =========================================================
    // STAR PICKER (inline product review composer)
    // =========================================================
    function paintStarButtons(value) {
        productReviewStarButtons.forEach(button => {
            const starValue = Number(button.dataset.star);
            const isFilled = starValue <= value;

            button.classList.toggle("filled", isFilled);
            button.setAttribute("aria-pressed", isFilled ? "true" : "false");

            const icon = button.querySelector("i");

            if (icon) {
                icon.className = isFilled
                    ? "fa-solid fa-star"
                    : "fa-regular fa-star";
            }
        });
    }

    function setStarPickerValue(value) {
        const safeValue = Math.max(0, Math.min(5, Number(value) || 0));

        if (productReviewRatingInput) {
            productReviewRatingInput.value = String(safeValue);
        }

        paintStarButtons(safeValue);

        if (productReviewStarHint) {
            const labels = ["Tap a star", "Poor", "Fair", "Good", "Great", "Excellent"];

            productReviewStarHint.textContent = labels[safeValue] || "Tap a star";
            productReviewStarHint.classList.toggle("active", safeValue > 0);
        }
    }

    productReviewStarButtons.forEach(button => {
        const starValue = Number(button.dataset.star);

        button.addEventListener("click", () => {
            setStarPickerValue(starValue);
        });

        button.addEventListener("mouseenter", () => {
            paintStarButtons(starValue);
        });

        button.addEventListener("focus", () => {
            paintStarButtons(starValue);
        });
    });

    productReviewStarPicker?.addEventListener("mouseleave", () => {
        paintStarButtons(Number(productReviewRatingInput?.value || 0));
    });

    productReviewStarPicker?.addEventListener("focusout", event => {
        if (!productReviewStarPicker.contains(event.relatedTarget)) {
            paintStarButtons(Number(productReviewRatingInput?.value || 0));
        }
    });

    // =========================================================
    // INLINE PRODUCT REVIEW SUBMIT (no redirect)
    // =========================================================
    productReviewForm?.addEventListener("submit", async event => {
        event.preventDefault();

        if (
            !currentPost ||
            currentPost.productID === null ||
            currentPost.productID === undefined
        ) {
            return;
        }

        const rating = Number(productReviewRatingInput.value);
        const comment = productReviewCommentInput.value.trim();

        if (!Number.isFinite(rating) || rating < 1 || rating > 5) {
            showToast("Please give a rating between 1 and 5.", "error");
            return;
        }

        if (!comment) {
            showToast("Please write a comment.", "error");
            return;
        }

        const submitButton = productReviewForm.querySelector("button[type='submit']");
        submitButton.disabled = true;

        try {
            const data = await postForm("AddExploreProductReview", {
                productId: currentPost.productID,
                rating,
                comment
            });

            emptyProductReviews.classList.add("hidden");
            modalProductReviews.prepend(createProductReviewElement(data.review));

            productReviewForm.reset();
            if (productReviewProductIdInput) {
                productReviewProductIdInput.value = String(currentPost.productID);
            }
            setStarPickerValue(0);

            const averageRating = Number(data.averageRating || 0);
            const totalRatings = Number(data.totalRatings || 0);

            productReviewRating.textContent = averageRating.toFixed(1);
            productReviewStars.innerHTML = createStarMarkup(averageRating);
            productReviewCount.textContent =
                `${totalRatings.toLocaleString()} ${totalRatings === 1 ? "review" : "reviews"}`;

            if (currentPost) {
                currentPost.productRating = averageRating;
                currentPost.productTotalRatings = totalRatings;
            }

            showToast(data.message, "success");
        } catch (error) {
            showToast(error.message, "error");
        } finally {
            submitButton.disabled = false;
        }
    });

    // =========================================================
    // MEDIA CAROUSEL
    // =========================================================
    function renderMedia(media) {
        currentMediaIndex = 0;

        if (!Array.isArray(media) || media.length === 0) {
            currentPost.media = [
                {
                    mediaType: "Image",
                    mediaUrl: "/images/product-placeholder.svg"
                }
            ];
        }

        showMediaAtIndex(0);
    }

    function showMediaAtIndex(index) {
        const media = currentPost?.media || [];

        if (!media.length) {
            return;
        }

        stopModalVideos();

        if (index < 0) {
            index = media.length - 1;
        }

        if (index >= media.length) {
            index = 0;
        }

        currentMediaIndex = index;

        const item = media[index];
        const isVideo =
            String(item.mediaType).toLowerCase() === "video";

        if (isVideo) {
            modalMediaContent.innerHTML = `
                <video controls
                       autoplay
                       muted
                       playsinline
                       preload="metadata"
                       poster="${escapeHtml(item.thumbnailUrl || "")}">
                    <source src="${escapeHtml(item.mediaUrl)}" />
                    Your browser does not support video playback.
                </video>
            `;
        } else {
            modalMediaContent.innerHTML = `
                <img src="${escapeHtml(
                item.mediaUrl || "/images/product-placeholder.svg"
            )}"
                     alt="Explore post media"
                     onerror="this.onerror=null;this.src='/images/product-placeholder.svg';" />
            `;
        }

        const multiple = media.length > 1;

        previousMediaButton.classList.toggle("hidden", !multiple);
        nextMediaButton.classList.toggle("hidden", !multiple);
        mediaCounter.classList.toggle("hidden", !multiple);

        if (multiple) {
            mediaCounter.textContent =
                `${index + 1} / ${media.length}`;
        }
    }

    function stopModalVideos() {
        modalMediaContent
            ?.querySelectorAll("video")
            .forEach(video => {
                video.pause();
                video.removeAttribute("src");
                video.load();
            });
    }

    function showPreviousMedia() {
        if ((currentPost?.media || []).length > 1) {
            showMediaAtIndex(currentMediaIndex - 1);
        }
    }

    function showNextMedia() {
        if ((currentPost?.media || []).length > 1) {
            showMediaAtIndex(currentMediaIndex + 1);
        }
    }

    previousMediaButton?.addEventListener(
        "click",
        showPreviousMedia
    );

    nextMediaButton?.addEventListener(
        "click",
        showNextMedia
    );

    // =========================================================
    // LINKED / NORMAL PRODUCT
    // =========================================================
    function renderLinkedProduct(content) {
        const hasProduct =
            content.productID !== null &&
            content.productID !== undefined;

        linkedProductSection.classList.toggle("hidden", !hasProduct);

        if (!hasProduct) {
            return;
        }

        const isProductOnly =
            String(content.contentType || "").toLowerCase() === "product";

        linkedProductLabel.textContent =
            isProductOnly ? "Product details" : "Linked product";

        linkedProductImage.src =
            content.productImageUrl || "/images/product-placeholder.svg";

        linkedProductCategory.textContent =
            content.categoryName || "Product";

        linkedProductName.textContent =
            content.productName || "Product";

        linkedProductDescription.textContent =
            content.productDescription || "";

        linkedProductPrice.textContent =
            formatMoney(content.productPrice);

        wishlistProductButton.dataset.productId = String(content.productID);
        cartProductButton.dataset.productId = String(content.productID);

        setWishlistButtonState(content.isInWishlist === true);

        const isOutOfStock = content.isOutOfStock === true;

        cartProductButton.disabled = isOutOfStock;
        cartProductButton.innerHTML = isOutOfStock
            ? '<i class="fa-solid fa-ban"></i>'
            : '<i class="fa-solid fa-bag-shopping"></i>';

        outOfStockMessage.classList.toggle("hidden", !isOutOfStock);
    }

    // =========================================================
    // COMMENTS
    // =========================================================
    function renderComments(comments) {
        modalComments.innerHTML = "";

        if (!Array.isArray(comments) || comments.length === 0) {
            emptyComments.classList.remove("hidden");
            return;
        }

        emptyComments.classList.add("hidden");

        comments.forEach(comment => {
            modalComments.appendChild(
                createCommentElement(comment)
            );
        });
    }

    function createCommentElement(comment) {
        const item = document.createElement("article");
        item.className = "comment-item";
        item.dataset.commentId =
            String(comment.exploreCommentID);

        item.innerHTML = `
            <span class="comment-avatar">
                ${escapeHtml(getInitial(comment.customerName, "C"))}
            </span>

            <div class="comment-content">
                <strong>${escapeHtml(
            comment.customerName || "Customer"
        )}</strong>

                <p>${escapeHtml(comment.commentText || "")}</p>

                <small>${escapeHtml(
            formatDate(comment.createdAt)
        )}</small>
            </div>

            ${comment.canDelete
                ? `
                        <button type="button"
                                class="delete-comment-button"
                                data-delete-comment
                                data-comment-id="${Number(
                    comment.exploreCommentID
                )}"
                                aria-label="Delete comment">
                            <i class="fa-solid fa-trash"></i>
                        </button>
                    `
                : "<span></span>"
            }
        `;

        return item;
    }

    commentForm?.addEventListener("submit", async event => {
        event.preventDefault();

        if (
            !currentPost ||
            String(currentPost.contentType || "").toLowerCase() !== "post"
        ) {
            return;
        }

        const commentText = commentTextInput.value.trim();

        if (!commentText) {
            showToast("Please write a comment.", "error");
            return;
        }

        const submitButton =
            commentForm.querySelector("button[type='submit']");

        submitButton.disabled = true;

        try {
            const data = await postForm(
                "AddExploreComment",
                {
                    postId: currentPost.explorePostID,
                    commentText
                }
            );

            const commentElement =
                createCommentElement(data.comment);

            modalComments.prepend(commentElement);
            emptyComments.classList.add("hidden");
            commentTextInput.value = "";

            currentPost.commentCount =
                Number(data.commentCount || 0);

            modalCommentCount.textContent =
                currentPost.commentCount.toLocaleString();

            showToast(data.message, "success");
        } catch (error) {
            showToast(error.message, "error");
        } finally {
            submitButton.disabled = false;
        }
    });

    modalComments?.addEventListener("click", async event => {
        const button = event.target.closest("[data-delete-comment]");

        if (
            !button ||
            !currentPost ||
            String(currentPost.contentType || "").toLowerCase() !== "post"
        ) {
            return;
        }

        const commentId = Number(button.dataset.commentId);

        if (!Number.isFinite(commentId)) {
            return;
        }

        button.disabled = true;

        try {
            const data = await postForm(
                "DeleteExploreComment",
                {
                    postId: currentPost.explorePostID,
                    commentId
                }
            );

            modalComments
                .querySelector(
                    `[data-comment-id="${commentId}"]`
                )
                ?.remove();

            currentPost.commentCount =
                Number(data.commentCount || 0);

            modalCommentCount.textContent =
                currentPost.commentCount.toLocaleString();

            if (!modalComments.children.length) {
                emptyComments.classList.remove("hidden");
            }

            showToast(data.message, "success");
        } catch (error) {
            button.disabled = false;
            showToast(error.message, "error");
        }
    });

    focusCommentButton?.addEventListener("click", () => {
        commentTextInput?.focus();
        commentTextInput?.scrollIntoView({
            behavior: "smooth",
            block: "center"
        });
    });

    // =========================================================
    // LIKE
    // =========================================================
    modalLikeButton?.addEventListener("click", async () => {
        if (
            !currentPost ||
            String(currentPost.contentType || "").toLowerCase() !== "post"
        ) {
            return;
        }

        modalLikeButton.disabled = true;

        try {
            const data = await postForm(
                "ToggleExploreLike",
                {
                    postId: currentPost.explorePostID
                }
            );

            setLikeButtonState(data.liked === true);

            currentPost.likeCount =
                Number(data.likeCount || 0);

            modalLikeCount.textContent =
                currentPost.likeCount.toLocaleString();
        } catch (error) {
            showToast(error.message, "error");
        } finally {
            modalLikeButton.disabled = false;
        }
    });

    // =========================================================
    // FOLLOW
    // =========================================================
    modalFollowButton?.addEventListener("click", async () => {
        const storeId = Number(
            modalFollowButton.dataset.storeId
        );

        if (!Number.isFinite(storeId)) {
            return;
        }

        modalFollowButton.disabled = true;

        try {
            const data = await postForm(
                "ToggleExploreStoreFollow",
                { storeId }
            );

            setFollowButtonState(data.following === true);
            showToast(data.message, "success");
        } catch (error) {
            showToast(error.message, "error");
        } finally {
            modalFollowButton.disabled = false;
        }
    });

    // =========================================================
    // WISHLIST / CART
    // =========================================================
    wishlistProductButton?.addEventListener(
        "click",
        async () => {
            const productId = Number(
                wishlistProductButton.dataset.productId
            );

            if (!Number.isFinite(productId)) {
                return;
            }

            wishlistProductButton.disabled = true;

            try {
                const data = await postForm(
                    "ToggleExploreWishlist",
                    { productId }
                );

                setWishlistButtonState(data.saved === true);
                showToast(data.message, "success");
            } catch (error) {
                wishlistProductButton.disabled = false;
                showToast(error.message, "error");
            }
        }
    );

    cartProductButton?.addEventListener("click", async () => {
        const productId = Number(
            cartProductButton.dataset.productId
        );

        if (!Number.isFinite(productId)) {
            return;
        }

        cartProductButton.disabled = true;

        try {
            const data = await postForm(
                "ExploreAddToCart",
                { productId }
            );

            cartProductButton.innerHTML = '<i class="fa-solid fa-check"></i>';

            showToast(data.message, "success");

            refreshCartBadge();
        } catch (error) {
            showToast(error.message, "error");

            if (!currentPost?.isOutOfStock) {
                cartProductButton.disabled = false;
            }
        }
    });

    // =========================================================
    // RELATED ITEMS
    // =========================================================
    function renderRelatedItems(items) {
        relatedItemsGrid.innerHTML = "";

        if (!Array.isArray(items) || items.length === 0) {
            emptyRelatedItems.classList.remove("hidden");
            return;
        }

        emptyRelatedItems.classList.add("hidden");

        items.forEach(item => {
            const button = document.createElement("button");
            button.type = "button";
            button.className = "related-item";
            button.dataset.itemType =
                item.gridItemType || "Post";
            button.dataset.postType = item.postType || "";

            if (item.explorePostID) {
                button.dataset.postId =
                    String(item.explorePostID);
            }

            if (item.productID) {
                button.dataset.productId =
                    String(item.productID);
            }

            const isVideo =
                String(item.mediaType).toLowerCase() === "video";

            const previewUrl = isVideo
                ? item.thumbnailUrl || item.mediaUrl
                : item.mediaUrl;

            button.innerHTML = `
                <img src="${escapeHtml(
                previewUrl || "/images/product-placeholder.svg"
            )}"
                     alt="${escapeHtml(
                item.productName || item.storeName || "Related item"
            )}"
                     loading="lazy"
                     onerror="this.onerror=null;this.src='/images/product-placeholder.svg';" />

                <span>
                    ${escapeHtml(
                item.productName || item.storeName || "Explore"
            )}
                    ${item.productPrice !== null &&
                    item.productPrice !== undefined
                    ? ` · ${escapeHtml(
                        formatMoney(item.productPrice)
                    )}`
                    : ""
                }
                </span>
            `;

            relatedItemsGrid.appendChild(button);
        });
    }

    relatedItemsGrid?.addEventListener("click", event => {
        const item = event.target.closest(".related-item");

        if (!item) {
            return;
        }

        const isPost =
            String(item.dataset.itemType).toLowerCase() === "post";

        const isReel =
            String(item.dataset.postType).toLowerCase() === "reel";

        if (isPost && isReel && item.dataset.postId) {
            closeExploreModal();
            openReelsPlayer([Number(item.dataset.postId)], 0);
            return;
        }

        if (isPost && item.dataset.postId) {
            openExplorePost(Number(item.dataset.postId));
            return;
        }

        if (item.dataset.productId) {
            openExploreProduct(Number(item.dataset.productId));
        }
    });

    // =========================================================
    // SHARE — WhatsApp / Chat / Copy link dropdown
    // =========================================================
    function modalShareUrl() {
        if (!currentPost) {
            return window.location.origin + pageUrl;
        }

        const isPost =
            String(currentPost.contentType || "Post").toLowerCase() === "post";

        const hash = isPost
            ? `post-${currentPost.explorePostID}`
            : `product-${currentPost.productID}`;

        return `${window.location.origin}${pageUrl}#${hash}`;
    }

    function closeModalShareDropdown() {
        document.getElementById("modalShareDropdown")?.classList.remove("open");
    }

    window.toggleModalShareMenu = function () {
        const dropdown = document.getElementById("modalShareDropdown");
        if (!dropdown) return;

        const isOpen = dropdown.classList.contains("open");
        document.querySelectorAll(".tile-menu-dropdown.open")
            .forEach(d => d.classList.remove("open"));

        if (!isOpen) dropdown.classList.add("open");
    };

    document.addEventListener("click", event => {
        const dropdown = document.getElementById("modalShareDropdown");
        const trigger = document.getElementById("modalShareButton");

        if (dropdown && !dropdown.contains(event.target) && event.target !== trigger && !trigger?.contains(event.target)) {
            dropdown.classList.remove("open");
        }
    });

    window.shareModalWhatsApp = function () {
        if (!currentPost) return;

        const label = currentPost.productName || currentPost.storeName || "this item";
        const message = `🛍️ ${label}\nStore: ${currentPost.storeName}\n${modalShareUrl()}`;

        window.open(
            "https://wa.me/?text=" + encodeURIComponent(message),
            "_blank",
            "noopener,noreferrer"
        );

        closeModalShareDropdown();
    };

    window.copyModalLink = async function () {
        try {
            await navigator.clipboard.writeText(modalShareUrl());
            showToast("Item link copied.", "success");
        } catch {
            showToast("Could not copy the link.", "error");
        }

        closeModalShareDropdown();
    };

    window.shareModalToChat = async function () {
        closeModalShareDropdown();

        if (!currentPost || currentPost.productID === null || currentPost.productID === undefined) {
            showToast("Only items with a linked product can be sent in chat.", "error");
            return;
        }

        try {
            const data = await postForm("ExploreShareToStore", {
                productId: currentPost.productID,
                storeOwnerId: currentPost.storeOwnerUserID
            });

            showToast(data.message, "success");
        } catch (error) {
            showToast(error.message, "error");
        }
    };

    // =========================================================
    // OPTIONAL HASH SUPPORT
    // =========================================================
    const postHashMatch =
        window.location.hash.match(/^#post-(\d+)$/);

    const productHashMatch =
        window.location.hash.match(/^#product-(\d+)$/);

    if (postHashMatch) {
        openExplorePost(Number(postHashMatch[1]));
    } else if (productHashMatch) {
        openExploreProduct(Number(productHashMatch[1]));
    }

    // =========================================================
    // LIVE CART BADGE UPDATE
    // =========================================================
    async function refreshCartBadge() {
        try {
            const response = await fetch("/api/cart/count", {
                method: "GET",
                credentials: "same-origin",
                cache: "no-store"
            });

            if (!response.ok) {
                return;
            }

            const data = await response.json();
            const count = Number(data.count || 0);

            const cartLink = document.querySelector(
                'a[href="/CustomerCart"]'
            );

            if (!cartLink) {
                return;
            }

            let badge = cartLink.querySelector(".js-cart-badge");

            if (count > 0) {
                if (!badge) {
                    badge = document.createElement("span");
                    badge.className = "js-cart-badge";
                    badge.style.cssText =
                        "position:absolute;top:-4px;right:18px;" +
                        "background:#ef4444;color:white;" +
                        "min-width:16px;height:16px;border-radius:999px;" +
                        "font-size:10px;font-weight:800;display:flex;" +
                        "align-items:center;justify-content:center;" +
                        "padding:0 4px;border:1.5px solid white;" +
                        "line-height:1;z-index:5;";
                    cartLink.appendChild(badge);
                }

                badge.textContent = String(count);
            } else if (badge) {
                badge.remove();
            }
        } catch {
            // Silent fail: badge simply won't update this time.
        }
    }
})();