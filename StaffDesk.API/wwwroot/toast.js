// ============================================
// Site-wide toast notifications.
// Temporary, non-interruptive, small: Success or Denied (+ reason).
// ============================================

const TOAST_SUCCESS_MS = 3200;
const TOAST_DENIED_MS = 4800;
const TOAST_MAX = 3;

let toastSeq = 0;
let lastToastKey = '';
let lastToastAt = 0;

function toastPhrase(key, fallback) {
    return typeof t === 'function' ? t(key) : fallback;
}

function escapeToastText(str) {
    return String(str ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}

function cleanToastMessage(message) {
    return String(message ?? '').replace(/^[✅❌⚠]\s*/, '').trim();
}

function getToastContainer() {
    let el = document.getElementById('toast-container');
    if (el) return el;
    el = document.createElement('div');
    el.id = 'toast-container';
    el.className = 'toast-container';
    el.setAttribute('aria-live', 'polite');
    el.setAttribute('aria-relevant', 'additions');
    el.setAttribute('aria-atomic', 'false');
    document.body.appendChild(el);
    return el;
}

function dismissToast(node) {
    if (!node || node.classList.contains('toast-out')) return;
    node.classList.add('toast-out');
    const remove = () => node.remove();
    node.addEventListener('animationend', remove, { once: true });
    setTimeout(remove, 280);
}

function dismissAllToasts() {
    const el = document.getElementById('toast-container');
    if (!el) return;
    Array.from(el.children).forEach(dismissToast);
}

function showToast(type, detail) {
    const kind = type === 'success' ? 'success' : 'denied';
    const title = kind === 'success'
        ? toastPhrase('toast.success', 'Success')
        : toastPhrase('toast.denied', 'Denied');
    const reason = cleanToastMessage(detail);

    if (kind === 'denied' && !reason) return;

    const key = kind + '|' + title + '|' + reason;
    const now = Date.now();
    if (key === lastToastKey && now - lastToastAt < 900) return;
    lastToastKey = key;
    lastToastAt = now;

    const container = getToastContainer();
    while (container.children.length >= TOAST_MAX) {
        dismissToast(container.firstElementChild);
    }

    const id = 'toast-' + (++toastSeq);
    const toast = document.createElement('div');
    toast.id = id;
    toast.className = 'toast toast-' + kind;
    toast.setAttribute('role', 'status');

    const reasonHtml = reason
        ? `<p class="toast-reason">${escapeToastText(reason)}</p>`
        : '';

    toast.innerHTML =
        `<span class="toast-icon" aria-hidden="true">${kind === 'success' ? '✓' : '✕'}</span>` +
        `<div class="toast-body">` +
            `<p class="toast-title">${escapeToastText(title)}</p>` +
            reasonHtml +
        `</div>`;

    toast.addEventListener('click', () => dismissToast(toast));
    container.appendChild(toast);

    const lifetime = kind === 'success' ? TOAST_SUCCESS_MS : TOAST_DENIED_MS;
    setTimeout(() => dismissToast(toast), lifetime);
    return toast;
}

function showSuccess(detail) {
    showToast('success', detail);
}

function showDenied(reason) {
    showToast('denied', reason || toastPhrase('toast.denied', 'Denied'));
}

function showRestricted() {
    showDenied(toastPhrase('toast.restricted_access', 'Restricted access'));
}

function showFillBlanks() {
    showDenied(toastPhrase('toast.fill_blanks', 'Please fill the blanks'));
}

(function bindToastValidation() {
    let queued = false;
    const onInvalid = (event) => {
        event.preventDefault();
        if (queued) return;
        queued = true;
        showFillBlanks();
        setTimeout(() => { queued = false; }, 500);
    };
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            document.addEventListener('invalid', onInvalid, true);
        });
    } else {
        document.addEventListener('invalid', onInvalid, true);
    }
})();
