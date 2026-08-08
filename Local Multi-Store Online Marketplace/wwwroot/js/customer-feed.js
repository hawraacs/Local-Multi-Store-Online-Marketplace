/* =============================================================
   CUSTOMER FEED — CLIENT-SIDE BEHAVIOUR
   Local Multi-Store Online Marketplace ("realnest")
   ============================================================= */

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
 */
function toggleMenu(btn) {
    if (!btn) return;

    const wrapper = btn.closest('.menu-wrap, .post-menu');
    const menu = wrapper
        ? wrapper.querySelector('.dropdown, .post-menu-dropdown')
        : btn.nextElementSibling;

    if (!menu) return;

    document.querySelectorAll('.post-menu-dropdown, .dropdown').forEach((openMenu) => {
        if (openMenu !== menu) {
            openMenu.style.display = 'none';
            openMenu.classList.remove('open');
        }
    });

    const isOpen = menu.style.display === 'block' || menu.classList.contains('open');
    menu.style.display = isOpen ? 'none' : 'block';
    menu.classList.toggle('open', !isOpen);
}

document.addEventListener('click', (event) => {
    if (!event.target.closest('.post-menu') && !event.target.closest('.menu-wrap')) {
        document.querySelectorAll('.post-menu-dropdown, .dropdown').forEach((menu) => {
            menu.style.display = 'none';
            menu.classList.remove('open');
        });
    }
});

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
   ============================================================= */
const STAR_RATING_LABELS = ['', 'Poor', 'Fair', 'Good', 'Great', 'Excellent'];

document.addEventListener('click', (event) => {

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
   ============================================================= */
(function enforceStoryRingColors() {
    const UNVIEWED_GRADIENT =
        'linear-gradient(135deg, #7c3aed 0%, #6366f1 55%, #4f46e5 100%)';

    const VIEWED_GRADIENT =
        'linear-gradient(135deg, #d7d7dc 0%, #b9b9c4 100%)';

    function styleRing(ring) {
        const isViewed = ring.classList.contains('story-ring--viewed');
        const gradient = isViewed ? VIEWED_GRADIENT : UNVIEWED_GRADIENT;

        ring.style.setProperty('background', gradient, 'important');
        ring.style.setProperty('border-radius', '50%', 'important');
        ring.style.setProperty('display', 'inline-flex', 'important');
        ring.style.setProperty('border', 'none', 'important');
        ring.style.setProperty('box-shadow', 'none', 'important');
    }

    function applyAll() {
        document.querySelectorAll('#customerStoryBar .story-ring').forEach(styleRing);
    }

    applyAll();
    document.addEventListener('DOMContentLoaded', applyAll);
    window.addEventListener('load', applyAll);

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

/* =============================================================
   NOT INTERESTED — AJAX (was a full-page RedirectToPage() that
   silently dropped ViewMode/filters and made the action look like
   it did nothing when you weren't on the default "Following" view)
   ============================================================= */
document.addEventListener('submit', async (event) => {
    const form = event.target.closest('.ajax-not-interested-form');
    if (!form) return;

    event.preventDefault();

    const post = form.closest('.post');
    const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    const token = tokenInput ? tokenInput.value : '';

    const body = new URLSearchParams();
    body.set('productId', post ? post.dataset.productId : '');
    body.set('__RequestVerificationToken', token);

    try {
        const response = await fetch(form.action, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: body.toString()
        });

        const data = await response.json();

        if (data && data.success && post) {
            post.style.transition = 'opacity .25s ease, transform .25s ease';
            post.style.opacity = '0';
            post.style.transform = 'scale(0.97)';
            setTimeout(() => post.remove(), 260);
        }
    } catch {
        // silent fail — post simply stays visible
    }

    document.querySelectorAll('.post-menu-dropdown').forEach(menu => menu.style.display = 'none');
});

/* =============================================================
   SHARE — WhatsApp / Copy link for feed posts
   ============================================================= */
function shareFeedProductWhatsApp(btn) {
    const post = btn.closest('.post');
    const name = post.dataset.productName;
    const price = post.dataset.price;
    const storeLink = post.querySelector('.store-name');
    const storeId = storeLink ? storeLink.getAttribute('href').split('id=')[1] : '';
    const url = `${window.location.origin}/StoreCustomerProfile?id=${storeId}#product-${post.dataset.productId}`;
    const message = `🛍️ ${name}\nPrice: $${price}\n${url}`;

    window.open('https://wa.me/?text=' + encodeURIComponent(message), '_blank', 'noopener,noreferrer');
}

async function copyFeedProductLink(btn) {
    const post = btn.closest('.post');
    const storeLink = post.querySelector('.store-name');
    const storeId = storeLink ? storeLink.getAttribute('href').split('id=')[1] : '';
    const url = `${window.location.origin}/StoreCustomerProfile?id=${storeId}#product-${post.dataset.productId}`;

    try {
        await navigator.clipboard.writeText(url);
    } catch {
        // silent fail
    }
}