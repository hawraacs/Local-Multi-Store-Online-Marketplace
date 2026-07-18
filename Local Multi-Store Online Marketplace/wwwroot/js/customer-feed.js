/* =============================================================
   CUSTOMER FEED — CLIENT-SIDE BEHAVIOUR
   Local Multi-Store Online Marketplace ("realnest")
   -------------------------------------------------------------
   Responsibilities:
     1. Collapsible "Filters" disclosure in the discovery bar
     2. Per-post "..." overflow menu (open/close, click-outside)
     3. Toast notification auto-dismiss
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
