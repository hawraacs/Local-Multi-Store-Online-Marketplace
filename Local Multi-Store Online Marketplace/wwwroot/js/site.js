(function () {
    "use strict";

    let pendingConfirmAction = null;
    let lastFocusedElement = null;

    function getToastContainer() {
        return document.getElementById("appToastContainer");
    }

    function getConfirmElements() {
        return {
            modal: document.getElementById("appConfirmModal"),
            icon: document.getElementById("appConfirmIcon"),
            title: document.getElementById("appConfirmTitle"),
            message: document.getElementById("appConfirmMessage"),
            cancelButton: document.getElementById("appConfirmCancel"),
            submitButton: document.getElementById("appConfirmSubmit")
        };
    }

    function normalizeToastType(type) {
        const allowedTypes = [
            "success",
            "error",
            "warning",
            "info"
        ];

        return allowedTypes.includes(type)
            ? type
            : "info";
    }

    function getToastIcon(type) {
        switch (type) {
            case "success":
                return "✓";

            case "error":
                return "!";

            case "warning":
                return "!";

            default:
                return "i";
        }
    }

    function getToastTitle(type) {
        switch (type) {
            case "success":
                return "Success";

            case "error":
                return "Unable to continue";

            case "warning":
                return "Warning";

            default:
                return "Information";
        }
    }

    window.showAppToast = function (
        message,
        type = "info",
        title = null,
        duration = 5000) {

        const container = getToastContainer();

        if (!container || !message) {
            return;
        }

        const safeType =
            normalizeToastType(type);

        const toast =
            document.createElement("div");

        toast.className =
            `app - toast app - toast - ${ safeType } `;

        toast.setAttribute(
            "role",
            "status");

        const icon =
            document.createElement("span");

        icon.className =
            "app-toast-icon";

        icon.textContent =
            getToastIcon(safeType);

        const content =
            document.createElement("div");

        content.className =
            "app-toast-content";

        const titleElement =
            document.createElement("span");

        titleElement.className =
            "app-toast-title";

        titleElement.textContent =
            title || getToastTitle(safeType);

        const messageElement =
            document.createElement("span");

        messageElement.className =
            "app-toast-message";

        messageElement.textContent =
            String(message);

        const closeButton =
            document.createElement("button");

        closeButton.type =
            "button";

        closeButton.className =
            "app-toast-close";

        closeButton.setAttribute(
            "aria-label",
            "Close notification");

        closeButton.innerHTML =
            "&times;";

        content.appendChild(
            titleElement);

        content.appendChild(
            messageElement);

        toast.appendChild(icon);
        toast.appendChild(content);
        toast.appendChild(closeButton);

        container.appendChild(toast);

        window.requestAnimationFrame(
            function () {
                toast.classList.add(
                    "app-toast-show");
            });

        let removeTimer = null;

        function removeToast() {
            if (!toast.isConnected) {
                return;
            }

            if (removeTimer) {
                window.clearTimeout(
                    removeTimer);
            }

            toast.classList.remove(
                "app-toast-show");

            window.setTimeout(
                function () {
                    toast.remove();
                },
                300);
        }

        closeButton.addEventListener(
            "click",
            removeToast);

        removeTimer =
            window.setTimeout(
                removeToast,
                Math.max(1500, duration));
    };

    window.showAppConfirm = function (options) {
        const elements =
            getConfirmElements();

        if (!elements.modal ||
            !elements.icon ||
            !elements.title ||
            !elements.message ||
            !elements.cancelButton ||
            !elements.submitButton) {

            console.error(
                "Shared confirmation modal is missing.");

            return;
        }

        const settings = {
            title:
                options?.title ||
                "Confirm Action",

            message:
                options?.message ||
                "Are you sure you want to continue?",

            confirmText:
                options?.confirmText ||
                "Confirm",

            cancelText:
                options?.cancelText ||
                "Cancel",

            type:
                options?.type ||
                "default",

            icon:
                options?.icon ||
                "?",

            loadingText:
                options?.loadingText ||
                "Processing...",

            onConfirm:
                typeof options?.onConfirm === "function"
                    ? options.onConfirm
                    : null
        };

        pendingConfirmAction =
            settings.onConfirm;

        lastFocusedElement =
            document.activeElement;

        elements.title.textContent =
            settings.title;

        elements.message.textContent =
            settings.message;

        elements.submitButton.textContent =
            settings.confirmText;

        elements.cancelButton.textContent =
            settings.cancelText;

        elements.icon.textContent =
            settings.icon;

        elements.submitButton.disabled =
            false;

        elements.submitButton.dataset.loadingText =
            settings.loadingText;

        elements.modal.classList.remove(
            "app-confirm-danger",
            "app-confirm-warning");

        if (settings.type === "danger") {
            elements.modal.classList.add(
                "app-confirm-danger");
        }
        else if (settings.type === "warning") {
            elements.modal.classList.add(
                "app-confirm-warning");
        }

        elements.modal.classList.add(
            "app-confirm-open");

        elements.modal.setAttribute(
            "aria-hidden",
            "false");

        document.body.classList.add(
            "app-modal-open");

        window.setTimeout(
            function () {
                elements.cancelButton.focus();
            },
            50);
    };

    window.closeAppConfirm = function () {
        const elements =
            getConfirmElements();

        if (!elements.modal) {
            return;
        }

        elements.modal.classList.remove(
            "app-confirm-open",
            "app-confirm-danger",
            "app-confirm-warning");

        elements.modal.setAttribute(
            "aria-hidden",
            "true");

        document.body.classList.remove(
            "app-modal-open");

        elements.submitButton.disabled =
            false;

        pendingConfirmAction =
            null;

        if (lastFocusedElement &&
            typeof lastFocusedElement.focus === "function") {

            lastFocusedElement.focus();
        }

        lastFocusedElement =
            null;
    };

    window.confirmFormSubmission = function (
        formOrId,
        options) {

        const form =
            typeof formOrId === "string"
                ? document.getElementById(formOrId)
                : formOrId;

        if (!form) {
            console.error(
                "The confirmation form was not found.");

            return;
        }

        window.showAppConfirm({
            ...options,

            onConfirm: function () {
                const elements =
                    getConfirmElements();

                elements.submitButton.disabled =
                    true;

                elements.submitButton.textContent =
                    options?.loadingText ||
                    "Processing...";

                form.requestSubmit();
            }
        });
    };

    window.confirmButtonAction = function (
        button,
        callback,
        options) {

        if (!button ||
            typeof callback !== "function") {
            return;
        }

        window.showAppConfirm({
            ...options,

            onConfirm: async function () {
                const elements =
                    getConfirmElements();

                elements.submitButton.disabled =
                    true;

                elements.submitButton.textContent =
                    options?.loadingText ||
                    "Processing...";

                try {
                    await callback();
                }
                catch (error) {
                    console.error(error);

                    window.closeAppConfirm();

                    window.showAppToast(
                        "An unexpected error occurred.",
                        "error");
                }
            }
        });
    };

    document.addEventListener(
        "DOMContentLoaded",
        function () {

            const elements =
                getConfirmElements();

            if (!elements.modal ||
                !elements.cancelButton ||
                !elements.submitButton) {
                return;
            }

            elements.cancelButton.addEventListener(
                "click",
                function () {
                    window.closeAppConfirm();
                });

            elements.submitButton.addEventListener(
                "click",
                async function () {

                    if (!pendingConfirmAction) {
                        window.closeAppConfirm();
                        return;
                    }

                    const action =
                        pendingConfirmAction;

                    pendingConfirmAction =
                        null;

                    await action();
                });

            elements.modal.addEventListener(
                "click",
                function (event) {

                    if (event.target ===
                        elements.modal) {

                        window.closeAppConfirm();
                    }
                });
        });

    document.addEventListener(
        "keydown",
        function (event) {

            if (event.key !== "Escape") {
                return;
            }

            const elements =
                getConfirmElements();

            if (elements.modal &&
                elements.modal.classList.contains(
                    "app-confirm-open")) {

                window.closeAppConfirm();
            }
        });
})();

