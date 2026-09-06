// ============================================
// Profile + theme
// ============================================

function getStoredTheme() {
    try {
        return localStorage.getItem('theme') === 'dark' ? 'dark' : 'light';
    } catch (e) {
        return 'light';
    }
}

function applyTheme(theme) {
    const next = theme === 'dark' ? 'dark' : 'light';
    if (next === 'dark') {
        document.documentElement.setAttribute('data-theme', 'dark');
    } else {
        document.documentElement.removeAttribute('data-theme');
    }
    try {
        localStorage.setItem('theme', next);
    } catch (e) { /* ignore */ }

    const toggle = document.getElementById('theme-toggle');
    if (toggle) toggle.checked = next === 'dark';

    const label = document.getElementById('theme-label');
    const hint = document.getElementById('theme-hint');
    const tt = typeof t === 'function' ? t : (k) => k;
    if (label) label.textContent = next === 'dark' ? tt('profile.themeDark') : tt('profile.themeLight');
    if (hint) {
        hint.textContent = next === 'dark'
            ? tt('profile.themeDarkHint')
            : tt('profile.themeLightHint');
    }
}

function toggleTheme(enabled) {
    applyTheme(enabled ? 'dark' : 'light');
}

function formatSessionExpiry() {
    const raw = localStorage.getItem('expiresAt');
    const tt = typeof t === 'function' ? t : null;
    if (!raw) return tt ? tt('common.notAvailable') : 'Not available';
    const dt = new Date(raw);
    if (Number.isNaN(dt.getTime())) return tt ? tt('common.notAvailable') : 'Not available';
    const lang = typeof getStoredLang === 'function' ? getStoredLang() : 'en';
    return dt.toLocaleString(lang === 'ar' ? 'ar' : 'en');
}

function roleLabel(role) {
    if (typeof translateRole === 'function') return translateRole(role);
    return (role || 'Member').replace(/_/g, ' ');
}

window.showProfile = function showProfile() {
    currentView = 'profile';
    showView('profile-view', null, '#profile');
    renderProfile();
};

function renderProfile() {
    const username = localStorage.getItem('username') || (typeof t === 'function' ? t('common.user') : 'User');
    const role = roleLabel(localStorage.getItem('role'));

    if (typeof applyI18n === 'function') {
        applyI18n(document.getElementById('profile-view'));
    }

    const avatar = document.getElementById('profile-avatar');
    if (avatar) avatar.textContent = username.charAt(0).toUpperCase();

    const nameEl = document.getElementById('profile-username');
    if (nameEl) nameEl.textContent = username;

    const roleBadge = document.getElementById('profile-role');
    if (roleBadge) roleBadge.textContent = role;

    const nameDetail = document.getElementById('profile-username-detail');
    if (nameDetail) nameDetail.textContent = username;

    const roleDetail = document.getElementById('profile-role-detail');
    if (roleDetail) roleDetail.textContent = role;

    const expires = document.getElementById('profile-expires');
    if (expires) expires.textContent = formatSessionExpiry();

    applyTheme(getStoredTheme());
    if (typeof syncLanguageButtons === 'function') syncLanguageButtons();
}

document.addEventListener('DOMContentLoaded', () => {
    applyTheme(getStoredTheme());
});
