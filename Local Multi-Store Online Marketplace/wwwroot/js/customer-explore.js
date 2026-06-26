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

    const linkedProductSection = document.getElementById("linkedProductSection");
    const linkedProductLink = document.getElementById("linkedProductLink");
    const linkedProductImage = document.getElementById("linkedProductImage");
    const linkedProductCategory = document.getElementById("linkedProductCategory");
    const linkedProductName = document.getElementById("linkedProductName");
    const linkedProductDescription = document.getElementById("linkedProductDescription");
    const linkedProductPrice = document.getElementById("linkedProductPrice");
    const wishlistProductButton = document.getElementById("wishlistProductButton");
    const cartProductButton = document.getElementById("cartProductButton");
    const outOfStockMessage = document.getElementById("outOfStockMessage");

    const commentForm = document.getElementById("commentForm");
    const commentTextInput = document.getElementById("commentTextInput");
    const modalComments = document.getElementById("modalComments");
    const emptyComments = document.getElementById("emptyComments");

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
                mediaUrl || "/images/no-image.png"
            )}"
                     alt="${escapeHtml(
                item.productName || item.storeName || "Explore item"
            )}"
                     loading="lazy"
                     onerror="this.onerror=null;this.src='/images/no-image.png';" />
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
        if (!hasMore || isLoadingMore) {
            return;
        }

        isLoadingMore = true;
        loader?.classList.remove("hidden");

        try {
            const nextPage = currentPage + 1;
            const category = app.dataset.category || "";

            const response = await fetch(
                `${pageUrl}?handler=ExplorePage` +
                `&page=${nextPage}` +
                `&category=${encodeURIComponent(category)}`,
                {
                    method: "GET",
                    credentials: "same-origin",
                    headers: {
                        Accept: "application/json"
                    }
                }
            );

            const data = await readJsonResponse(response);
            const items = Array.isArray(data.items) ? data.items : [];

            items.forEach(item => {
                grid?.appendChild(createGridItemElement(item));
            });

            currentPage = nextPage;
            hasMore = data.hasMore === true;

            app.dataset.currentPage = String(currentPage);
            app.dataset.hasMore = String(hasMore);

            if (items.length > 0) {
                emptyState?.classList.add("hidden");
            }

            if (!hasMore) {
                endOfFeed?.classList.remove("hidden");
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

    const infiniteObserver = new IntersectionObserver(
        entries => {
            if (entries.some(entry => entry.isIntersecting)) {
                loadMoreItems();
            }
        },
        {
            root: null,
            rootMargin: "650px 0px",
            threshold: 0
        }
    );

    if (sentinel) {
        infiniteObserver.observe(sentinel);
    }

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
            window.location.href =
                `/CustomerProductDetails?id=${encodeURIComponent(
                    tile.dataset.productId
                )}`;
        }
    });

    // =========================================================
    // OPEN / CLOSE MODAL
    // =========================================================
    async function openExplorePost(postId) {
        if (!Number.isFinite(postId) || postId <= 0) {
            return;
        }

        savedScrollPosition = window.scrollY;

        modal?.classList.add("open");
        modal?.setAttribute("aria-hidden", "false");
        document.body.classList.add("modal-open");

        modalLoading?.classList.remove("hidden");
        modalMediaStage?.classList.add("hidden");
        modalInformation?.classList.add("hidden");

        currentPost = null;
        currentMediaIndex = 0;

        try {
            const response = await fetch(
                `${pageUrl}?handler=ExplorePostDetails&id=${postId}`,
                {
                    method: "GET",
                    credentials: "same-origin",
                    headers: {
                        Accept: "application/json"
                    }
                }
            );

            const data = await readJsonResponse(response);
            currentPost = data.post;

            renderExplorePost(currentPost);

            modalLoading?.classList.add("hidden");
            modalMediaStage?.classList.remove("hidden");
            modalInformation?.classList.remove("hidden");
        } catch (error) {
            closeExploreModal();

            showToast(
                error.message || "Could not open the post.",
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
    // RENDER MODAL DETAILS
    // =========================================================
    function renderExplorePost(post) {
        modalStoreName.textContent = post.storeName || "Store";
        modalPostDate.textContent = formatDate(post.createdAt);

        modalStoreLink.href =
            `/StoreCustomerProfile?id=${encodeURIComponent(post.storeID)}`;

        renderStoreAvatar(post);

        modalFollowButton.dataset.storeId = String(post.storeID);
        setFollowButtonState(post.isFollowingStore === true);

        const caption = String(post.caption || "").trim();

        modalCaption.textContent =
            caption || "No caption was added to this post.";

        modalCaption.classList.toggle("empty", !caption);

        modalViewCount.textContent =
            Number(post.viewCount || 0).toLocaleString();

        modalLikeCount.textContent =
            Number(post.likeCount || 0).toLocaleString();

        modalCommentCount.textContent =
            Number(post.commentCount || 0).toLocaleString();

        setLikeButtonState(post.isLikedByCurrentCustomer === true);

        renderMedia(post.media || []);
        renderLinkedProduct(post);
        renderComments(post.comments || []);
        renderRelatedItems(post.relatedItems || []);
    }

    function renderStoreAvatar(post) {
        modalStoreAvatar.textContent = "";
        modalStoreAvatar.style.backgroundImage = "";

        if (post.storeLogoUrl) {
            modalStoreAvatar.style.backgroundImage =
                `url("${String(post.storeLogoUrl)
                    .replaceAll('"', '\\"')}")`;
        } else {
            modalStoreAvatar.textContent =
                getInitial(post.storeName);
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
        const text = modalLikeButton.querySelector("span");

        if (icon) {
            icon.className = liked
                ? "fa-solid fa-heart"
                : "fa-regular fa-heart";
        }

        if (text) {
            text.textContent = liked ? "Liked" : "Like";
        }

        if (currentPost) {
            currentPost.isLikedByCurrentCustomer = liked;
        }
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
                    mediaUrl: "/images/no-image.png"
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
                item.mediaUrl || "/images/no-image.png"
            )}"
                     alt="Explore post media"
                     onerror="this.onerror=null;this.src='/images/no-image.png';" />
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
    // LINKED PRODUCT
    // =========================================================
    function renderLinkedProduct(post) {
        const hasProduct =
            post.productID !== null &&
            post.productID !== undefined;

        linkedProductSection.classList.toggle(
            "hidden",
            !hasProduct
        );

        if (!hasProduct) {
            return;
        }

        linkedProductLink.href =
            `/CustomerProductDetails?id=${encodeURIComponent(
                post.productID
            )}`;

        linkedProductImage.src =
            post.productImageUrl || "/images/no-image.png";

        linkedProductCategory.textContent =
            post.categoryName || "Product";

        linkedProductName.textContent =
            post.productName || "Product";

        linkedProductDescription.textContent =
            post.productDescription || "";

        linkedProductPrice.textContent =
            formatMoney(post.productPrice);

        wishlistProductButton.dataset.productId =
            String(post.productID);

        cartProductButton.dataset.productId =
            String(post.productID);

        const isOutOfStock = post.isOutOfStock === true;

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

        if (!currentPost) {
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

        if (!button || !currentPost) {
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
        if (!currentPost) {
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
                    "ExploreAddWishlist",
                    { productId }
                );

                wishlistProductButton.innerHTML =
                    '<i class="fa-solid fa-heart"></i> Saved';

                showToast(data.message, "success");
            } catch (error) {
                showToast(error.message, "error");
                wishlistProductButton.disabled = false;
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
                previewUrl || "/images/no-image.png"
            )}"
                     alt="${escapeHtml(
                item.productName || item.storeName || "Related item"
            )}"
                     loading="lazy"
                     onerror="this.onerror=null;this.src='/images/no-image.png';" />

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
            window.location.href =
                `/CustomerProductDetails?id=${encodeURIComponent(
                    item.dataset.productId
                )}`;
        }
    });

    // =========================================================
    // SHARE
    // =========================================================
    modalShareButton?.addEventListener("click", async () => {
        if (!currentPost) {
            return;
        }

        const shareUrl =
            `${window.location.origin}${pageUrl}` +
            `#post-${currentPost.explorePostID}`;

        const shareData = {
            title: `${currentPost.storeName} on realnest`,
            text:
                currentPost.caption ||
                "See this Explore post on realnest.",
            url: shareUrl
        };

        try {
            if (navigator.share) {
                await navigator.share(shareData);
                return;
            }

            await navigator.clipboard.writeText(shareUrl);
            showToast("Post link copied.", "success");
        } catch (error) {
            if (error?.name !== "AbortError") {
                showToast(
                    "The post link could not be shared.",
                    "error"
                );
            }
        }
    });

    // =========================================================
    // OPTIONAL HASH SUPPORT
    // =========================================================
    const hashMatch =
        window.location.hash.match(/^#post-(\d+)$/);

    if (hashMatch) {
        openExplorePost(Number(hashMatch[1]));
    }
})();
