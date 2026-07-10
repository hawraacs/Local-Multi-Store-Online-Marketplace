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
    const modalPrimaryActions = document.getElementById("modalPrimaryActions");

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
    const writeProductReviewLink = document.getElementById("writeProductReviewLink");
    const modalProductReviews = document.getElementById("modalProductReviews");
    const emptyProductReviews = document.getElementById("emptyProductReviews");

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

            const response = await fetch(
                `${pageUrl}?handler=ExplorePage` +
                `&page=${encodeURIComponent(nextPage)}` +
                `&category=${encodeURIComponent(category)}` +
                `&searchTerm=${encodeURIComponent(searchTerm)}` +
                `&categoryId=${encodeURIComponent(categoryId)}` +
                `&storeId=${encodeURIComponent(storeId)}` +
                `&area=${encodeURIComponent(area)}` +
                `&minPrice=${encodeURIComponent(minPrice)}` +
                `&maxPrice=${encodeURIComponent(maxPrice)}`,
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
                        // Browser can block autoplay; preview remains usable.
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

        if (itemType === "post" && tile.dataset.postId) {
            openExplorePost(Number(tile.dataset.postId));
            return;
        }

        if (tile.dataset.productId) {
            openExploreProduct(Number(tile.dataset.productId));
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

        modalStats?.classList.toggle("hidden", !isPost);
        modalLikeButton?.classList.toggle("hidden", !isPost);
        focusCommentButton?.classList.toggle("hidden", !isPost);
        modalPrimaryActions?.classList.toggle("product-mode", !isPost);
        commentsSection?.classList.toggle("hidden", !isPost);

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
        const textElement = modalLikeButton.querySelector("span");

        if (icon) {
            icon.className = liked
                ? "fa-solid fa-heart"
                : "fa-regular fa-heart";
        }

        if (textElement) {
            textElement.textContent = liked ? "Liked" : "Like";
        }

        if (currentPost) {
            currentPost.isLikedByCurrentCustomer = liked;
        }
    }

    function setWishlistButtonState(saved) {
        wishlistProductButton.classList.toggle("saved", saved);
        wishlistProductButton.disabled = false;

        wishlistProductButton.innerHTML = saved
            ? '<i class="fa-solid fa-heart"></i> Saved'
            : '<i class="fa-regular fa-heart"></i> Save';

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

        writeProductReviewLink.href =
            `/CreateProductReview/${encodeURIComponent(content.productID)}`;

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

        linkedProductSection.classList.toggle(
            "hidden",
            !hasProduct
        );

        if (!hasProduct) {
            return;
        }

        const isProductOnly =
            String(content.contentType || "").toLowerCase() === "product";

        linkedProductLabel.textContent =
            isProductOnly ? "Product details" : "Linked product";

        linkedProductLink.href =
            `/CustomerProductDetails?id=${encodeURIComponent(
                content.productID
            )}`;

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

        wishlistProductButton.dataset.productId =
            String(content.productID);

        cartProductButton.dataset.productId =
            String(content.productID);

        setWishlistButtonState(content.isInWishlist === true);

        const isOutOfStock = content.isOutOfStock === true;

        cartProductButton.disabled = isOutOfStock;
        cartProductButton.innerHTML = isOutOfStock
            ? '<i class="fa-solid fa-ban"></i> Out of stock'
            : '<i class="fa-solid fa-bag-shopping"></i> Add to cart';

        outOfStockMessage.classList.toggle(
            "hidden",
            !isOutOfStock
        );
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

            cartProductButton.innerHTML =
                '<i class="fa-solid fa-check"></i> Added';

            showToast(data.message, "success");
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

        if (
            String(item.dataset.itemType).toLowerCase() ===
            "post" &&
            item.dataset.postId
        ) {
            openExplorePost(Number(item.dataset.postId));
            return;
        }

        if (item.dataset.productId) {
            openExploreProduct(Number(item.dataset.productId));
        }
    });

    // =========================================================
    // SHARE
    // =========================================================
    modalShareButton?.addEventListener("click", async () => {
        if (!currentPost) {
            return;
        }

        const isPost =
            String(currentPost.contentType || "Post").toLowerCase() === "post";

        const hash = isPost
            ? `post-${currentPost.explorePostID}`
            : `product-${currentPost.productID}`;

        const shareUrl =
            `${window.location.origin}${pageUrl}#${hash}`;

        const shareData = {
            title: isPost
                ? `${currentPost.storeName} on realnest`
                : `${currentPost.productName} on realnest`,
            text: currentPost.caption ||
                currentPost.productDescription ||
                "See this item on realnest.",
            url: shareUrl
        };

        try {
            if (navigator.share) {
                await navigator.share(shareData);
                return;
            }

            await navigator.clipboard.writeText(shareUrl);
            showToast("Item link copied.", "success");
        } catch (error) {
            if (error?.name !== "AbortError") {
                showToast(
                    "The item link could not be shared.",
                    "error"
                );
            }
        }
    });

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
})();
