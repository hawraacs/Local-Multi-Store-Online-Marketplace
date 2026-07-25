/* =============================================================
   CUSTOMER FEED — CLIENT-SIDE BEHAVIOUR
   Local Multi-Store Online Marketplace ("realnest")
   -------------------------------------------------------------
   Responsibilities:
     1. Collapsible "Filters" disclosure in the discovery bar
     2. Per-post "..." overflow menu (open/close, click-outside)
     3. Toast notification auto-dismiss
     4. NEW — force the story ring color (orange = unviewed,
        purple = viewed) via inline styles with !important priority,
        so it can never lose a specificity fight against any rule
        in stories.css, regardless of load order.
   ============================================================= */

// Toggle the advanced filters panel open/closed.
const filtersToggle = document.getElementById('filtersToggle');
const advancedFilters = document.getElementById('advancedFilters');

if (filtersToggle && advancedFilters) {
    filtersToggle.addEventListener('click', () => {
        const isOpen = !advancedFilters.classList.contains('is-collapsed');
        advancedFilters.classList.toggle('is-collapsed', isOpen);
        filtersToggle.setAttribute('aria-expanded', String(!isOpen));
    });
}

/**
 * Toggles the dropdown menu attached to a post's "..." button.
 * Closes any other open menu first so only one is visible at a time.
 * @param {HTMLElement} btn - The button element that was clicked.
 */
function toggleMenu(btn) {
    const menu = btn.nextElementSibling;

    document.querySelectorAll('.post-menu-dropdown').forEach((openMenu) => {
        if (openMenu !== menu) {
            openMenu.style.display = 'none';
        }
    });

    menu.style.display = menu.style.display === 'block' ? 'none' : 'block';
}

// Close any open post menu when the user clicks outside of it.
document.addEventListener('click', (event) => {
    if (!event.target.closest('.post-menu')) {
        document.querySelectorAll('.post-menu-dropdown').forEach((menu) => {
            menu.style.display = 'none';
        });
    }
});

// Auto-dismiss success/error toast notifications after a few seconds.
window.addEventListener('DOMContentLoaded', () => {
    const TOAST_VISIBLE_MS = 4000;
    const TOAST_FADE_MS = 500;

    setTimeout(() => {
        document.querySelectorAll('.toast').forEach((toast) => {
            toast.style.transition = `opacity ${TOAST_FADE_MS}ms ease, transform ${TOAST_FADE_MS}ms ease`;
            toast.style.opacity = '0';
            toast.style.transform = 'translateY(-10px)';
            setTimeout(() => toast.remove(), TOAST_FADE_MS);
        });
    }, TOAST_VISIBLE_MS);
});

/* =============================================================
   PRODUCT REVIEW COMPOSER — star picker + show more/less reviews
   -------------------------------------------------------------
   Delegated on document so it works for every post's review form
   and review list on the page without needing per-product IDs.
   ============================================================= */
const STAR_RATING_LABELS = ['', 'Poor', 'Fair', 'Good', 'Great', 'Excellent'];

document.addEventListener('click', (event) => {

    // ---- Star picker: tap a star to set the hidden rating value ----
    const starBtn = event.target.closest('.product-review-form-stars .star-pick');
    if (starBtn) {
        const picker = starBtn.closest('.product-review-form-stars');
        const form = starBtn.closest('.product-review-form');
        const ratingInput = form ? form.querySelector('.product-review-rating-input') : null;
        const hint = picker ? picker.querySelector('.product-review-form-hint') : null;
        const value = parseInt(starBtn.dataset.star, 10);

        if (ratingInput) ratingInput.value = String(value);

        if (picker) {
            picker.querySelectorAll('.star-pick').forEach((btn) => {
                const starValue = parseInt(btn.dataset.star, 10);
                const filled = starValue <= value;
                btn.classList.toggle('filled', filled);

                const icon = btn.querySelector('i');
                if (icon) {
                    icon.classList.toggle('fa-solid', filled);
                    icon.classList.toggle('fa-regular', !filled);
                }
            });
        }

        if (hint) {
            hint.textContent = STAR_RATING_LABELS[value] || 'Tap a star';
            hint.classList.toggle('active', value > 0);
        }
        return;
    }

    // ---- Show more / show less reviews (SHEIN-style disclosure) ----
    const toggleBtn = event.target.closest('[data-toggle-reviews]');
    if (toggleBtn) {
        const section = toggleBtn.closest('.comments-section');
        const list = section ? section.querySelector('[data-reviews-list]') : null;
        if (!list) return;

        const isExpanded = toggleBtn.dataset.expanded === 'true';

        list.querySelectorAll('.review-hidden').forEach((review) => {
            review.style.display = isExpanded ? 'none' : 'block';
        });

        toggleBtn.dataset.expanded = String(!isExpanded);
        toggleBtn.textContent = isExpanded
            ? toggleBtn.dataset.showLabel
            : 'Show less';
    }
});

// ---- Require a star rating before letting the review form submit ----
document.addEventListener('submit', (event) => {
    const form = event.target.closest('.product-review-form');
    if (!form) return;

    const ratingInput = form.querySelector('.product-review-rating-input');
    const hint = form.querySelector('.product-review-form-hint');
    const rating = ratingInput ? parseInt(ratingInput.value, 10) : 0;

    if (!rating || rating < 1) {
        event.preventDefault();
        if (hint) {
            hint.textContent = 'Please pick a star rating first';
            hint.style.color = 'var(--error-color)';
        }
    }
});

/* =============================================================
   STORY RING — FORCED VIA JS
   -------------------------------------------------------------
   If stories.css (or anything else) is winning the CSS cascade
   against customer-feed.css's .story-ring rules, this guarantees
   the correct look anyway: inline styles set with "important"
   priority beat any external stylesheet rule, full stop.
   ============================================================= */
(function enforceStoryRingColors() {
    const UNVIEWED_GRADIENT = 'linear-gradient(135deg, #ffd166 0%, #f59e0b 45%, #ea580c 100%)';
    const VIEWED_GRADIENT = 'linear-gradient(135deg, #7c3aed 0%, #6366f1 55%, #4f46e5 100%)';

    function styleRing(ring) {
        const isViewed = ring.classList.contains('story-ring--viewed');
        const gradient = isViewed ? VIEWED_GRADIENT : UNVIEWED_GRADIENT;

        // NOTE: this is now a safety net, not the primary fix. The real bug was
        // that .story-ring (in stories.css) has a fixed 66px box while .story-img
        // was forced to 118px, so the image overflowed past the ring and hid the
        // colored border entirely. That's fixed directly in customer-feed.css by
        // giving .story-ring an explicit width/height that matches the image size
        // plus its padding. This JS just reinforces the background color in case
        // anything else touches these elements later.
        ring.style.setProperty('background', gradient, 'important');
        ring.style.setProperty('border-radius', '50%', 'important');
        ring.style.setProperty('display', 'inline-flex', 'important');
        ring.style.setProperty('border', 'none', 'important');
        ring.style.setProperty('box-shadow', 'none', 'important');
    }

    function applyAll() {
        document.querySelectorAll('#customerStoryBar .story-ring').forEach(styleRing);
    }

    // Run as early as possible, then again once the DOM/page fully finish
    // loading (covers scripts that render stories asynchronously).
    applyAll();
    document.addEventListener('DOMContentLoaded', applyAll);
    window.addEventListener('load', applyAll);

    // IMPORTANT: watch the whole document body for ANY structural change
    // (not just class changes on nodes that already exist). If stories.js
    // tears down and rebuilds the story bar's HTML from
    // window.realnestStoryGroups (a common pattern for interactive story
    // viewers), a narrower observer would miss it entirely - this one
    // re-applies the ring color immediately after every such rebuild.
    if (window.MutationObserver) {
        const observer = new MutationObserver(() => applyAll());
        observer.observe(document.body, {
            childList: true,
            subtree: true,
            attributes: true,
            attributeFilter: ['class']
        });
    }
})();
