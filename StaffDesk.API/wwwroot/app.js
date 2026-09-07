// ============================================
// Application shell.
//
// Holds only the shared providers every feature depends on -- the API client,
// auth/role checks, loading and toast helpers, formatting utilities -- plus the
// root router and the bootstrap that runs on DOMContentLoaded.
//
// Feature behaviour lives in the modules loaded after this file. They are classic
// scripts sharing one global scope, so a function declared in any of them is
// reachable from the inline handlers in index.html.
// ============================================

const API_BASE = '/v1';
let currentView = 'departments';
let currentDepartmentId = null;

// ============================================
// Auth / Role
// ============================================
// Mirrors the backend's [AdminOnly] gate (Update/Delete on Departments and
// Seniority Levels, Update/Deactivate on Employees) so those controls aren't
// even shown to a caller who'd just get a 403 for using them.
function isAdmin() {
    return localStorage.getItem('role') === 'Admin';
}

function isAuditor() {
    return localStorage.getItem('role') === 'Auditor';
}

// Admin and Auditor share the Audit workspace; everyone else is gated out.
function isAuditViewer() {
    return isAdmin() || isAuditor();
}

function isHrAdmin() {
    return localStorage.getItem('role') === 'HR_ADMIN';
}

function isManagerRole() {
    const role = localStorage.getItem('role');
    return role === 'Manager' || role === 'Admin' || role === 'HR_ADMIN';
}

// ============================================
// Utility Functions
// ============================================
function showLoading() {
    document.getElementById('loading').style.display = 'block';
}

function hideLoading() {
    document.getElementById('loading').style.display = 'none';
}

function showError(message) {
    const raw = String(message ?? '').trim();
    if (!raw) return;
    const detail = typeof cleanToastMessage === 'function' ? cleanToastMessage(raw) : raw.replace(/^[✅❌⚠]\s*/, '').trim();
    if (raw.startsWith('✅') || raw.startsWith('⚠')) {
        showSuccess(detail);
        return;
    }
    showDenied(detail);
}

function hideError() {
    // Inline banners are unused; toasts dismiss themselves.
}

function apiToastReason(status, err) {
    if (status === 403) {
        return typeof toastPhrase === 'function'
            ? toastPhrase('toast.restricted_access', 'Restricted access')
            : 'Restricted access';
    }
    const blob = [err?.message, ...(err?.details || [])].join(' ');
    if (status === 400 && /required|must not be empty|cannot be empty|is required/i.test(blob)) {
        return typeof toastPhrase === 'function'
            ? toastPhrase('toast.fill_blanks', 'Please fill the blanks')
            : 'Please fill the blanks';
    }
    return err?.message || 'Something went wrong';
}

function getApiUrl(endpoint, params = {}) {
    let url = `${API_BASE}${endpoint}`;
    const queryParams = [];
    Object.keys(params).forEach(key => {
        if (params[key] !== undefined && params[key] !== null && params[key] !== '') {
            queryParams.push(`${key}=${encodeURIComponent(params[key])}`);
        }
    });
    if (queryParams.length > 0) {
        url += '?' + queryParams.join('&');
    }
    return url;
}

// ETag cache for optimistic concurrency (PL-4..PL-7), used by platform.js
const resourceEtags = {};

function storeResourceEtag(endpoint, method, etag, explicitKey) {
    if (!etag) return;
    if (explicitKey) {
        resourceEtags[explicitKey] = etag;
        return;
    }
    if (method !== 'GET') return;
    const patterns = [
        [/^\/tasks\/(\d+)$/, (id) => `task:${id}`],
        [/^\/employees\/(\d+)$/, (id) => `employee:${id}`],
        [/^\/reviews\/(\d+)$/, (id) => `review:${id}`],
        [/^\/goals\/(\d+)$/, (id) => `goal:${id}`],
    ];
    for (const [re, keyFn] of patterns) {
        const m = endpoint.match(re);
        if (m) {
            resourceEtags[keyFn(m[1])] = etag;
            break;
        }
    }
}

function parseApiErrorBody(data) {
    if (typeof data?.error === 'object') return data.error;
    if (typeof data?.error === 'string') return { message: data.error, details: [] };
    return { message: 'Something went wrong', details: [] };
}

function tryRefreshAccessToken() {
    const rt = localStorage.getItem('refreshToken');
    if (!rt) return Promise.resolve(false);
    if (window.__staffdeskRefresh) return window.__staffdeskRefresh;
    window.__staffdeskRefresh = fetch('/v1/auth/refresh', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: rt })
    }).then(async res => {
        if (!res.ok) return false;
        const data = await res.json();
        const access = data.accessToken || data.token;
        if (!access) return false;
        localStorage.setItem('token', access);
        if (data.refreshToken) localStorage.setItem('refreshToken', data.refreshToken);
        if (data.expiresAt) localStorage.setItem('expiresAt', data.expiresAt);
        return true;
    }).catch(() => false).finally(() => { window.__staffdeskRefresh = null; });
    return window.__staffdeskRefresh;
}

async function fetchApi(endpoint, options = {}, params = {}, silent = false) {
    const url = getApiUrl(endpoint, params);
    const method = (options.method || 'GET').toUpperCase();
    if (!silent) showLoading();
    try {
        const token = localStorage.getItem('token');
        const headers = {
            'Content-Type': 'application/json',
            ...(token ? { 'Authorization': `Bearer ${token}` } : {}),
            ...(options.headers || {}),
        };

        // PL-1: Idempotency-Key on POST (skip when explicitly disabled, e.g. idempotent replay).
        if (method === 'POST' && options.skipIdempotency !== true && !headers['Idempotency-Key']) {
            headers['Idempotency-Key'] = options.idempotencyKey || crypto.randomUUID();
        }

        // PL-5/PL-6: If-Match for optimistic concurrency on protected resources.
        const etagKey = options.etagKey;
        if (etagKey && resourceEtags[etagKey] && !headers['If-Match']) {
            headers['If-Match'] = resourceEtags[etagKey];
        }
        if (options.ifMatch) headers['If-Match'] = options.ifMatch;

        const response = await fetch(url, { ...options, method, headers });

        // PL-15: surface deprecation when the API marks an endpoint as deprecated.
        const deprecation = response.headers.get('Deprecation');
        if (deprecation && !silent) {
            const sunset = response.headers.get('Sunset') || 'see changelog';
            console.warn(`Deprecated endpoint ${endpoint}; sunset ${sunset}`);
        }

        if (response.status === 401) {
            const isAuthCall = endpoint.indexOf('/auth/login') !== -1 || endpoint.indexOf('/auth/refresh') !== -1;
            if (!isAuthCall && !options._retriedAfterRefresh) {
                const ok = await tryRefreshAccessToken();
                if (ok) {
                    return fetchApi(endpoint, { ...options, _retriedAfterRefresh: true }, params, silent);
                }
            }
            localStorage.removeItem('token');
            localStorage.removeItem('refreshToken');
            localStorage.removeItem('username');
            localStorage.removeItem('role');
            window.location.href = '/login.html';
            return null;
        }

        if (response.status === 403) {
            throw new Error(apiToastReason(403, {}));
        }

        // PL-9: rate limit with Retry-After.
        if (response.status === 429) {
            const retryAfter = response.headers.get('Retry-After') || '60';
            throw new Error(`Too many requests. Try again in ${retryAfter} seconds.`);
        }

        storeResourceEtag(endpoint, method, response.headers.get('ETag'), etagKey);

        const contentLength = response.headers.get('content-length');
        if (response.status === 204 || contentLength === '0') {
            if (!response.ok) throw new Error(apiToastReason(response.status, {}));
            return null;
        }

        const rawText = await response.text();
        let data;
        try {
            data = rawText ? JSON.parse(rawText) : null;
        } catch {
            if (response.ok) return null;
            throw new Error('Something went wrong');
        }

        if (!response.ok) {
            const err = parseApiErrorBody(data);
            // PL-7: 412 includes current ETag — refresh cache so the next retry can succeed.
            if (response.status === 412 && Array.isArray(err.details)) {
                const etagDetail = err.details.find(d => String(d).startsWith('currentETag:'));
                if (etagDetail && etagKey) {
                    resourceEtags[etagKey] = String(etagDetail).slice('currentETag:'.length);
                }
            }
            const message = apiToastReason(response.status, err);
            throw new Error(message);
        }
        return data;
    } catch (error) {
        if (!silent && error.message !== 'Something went wrong') {
            showError(error.message);
        }
        throw error;
    } finally {
        if (!silent) hideLoading();
    }
}

// ============================================
// Logout
// ============================================
function logout() {
    const token = localStorage.getItem('token');
    if (token) {
        fetch('/v1/auth/logout', {
            method: 'POST',
            headers: { 'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json' }
        }).catch(() => {});
    }
    localStorage.removeItem('token');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('username');
    localStorage.removeItem('role');
    localStorage.removeItem('expiresAt');
    window.location.href = '/login.html';
}

// ============================================
// Navigation
// ============================================
const ALL_VIEWS = ['dashboard-view', 'departments-view', 'employees-view', 'levels-view', 'tasks-view', 'requests-view', 'delegations-view', 'templates-view', 'leave-view', 'capacity-view', 'timesheets-view', 'analytics-view', 'performance-view', 'platform-view', 'operations-view', 'audit-view', 'security-view', 'profile-view', 'calendars-view', 'sla-view', 'department-detail'];
const ALL_NAV_TABS = ['nav-dashboard', 'nav-departments', 'nav-employees', 'nav-levels', 'nav-tasks', 'nav-requests', 'nav-delegations', 'nav-templates', 'nav-leave', 'nav-capacity', 'nav-timesheets', 'nav-analytics', 'nav-performance', 'nav-platform', 'nav-operations', 'nav-audit', 'nav-security', 'nav-calendars', 'nav-sla'];

const HASH_ROUTES = {
    '#dashboard': () => showDashboard(),
    '#departments': () => showDepartments(),
    '#employees': () => showEmployees(),
    '#levels': () => showLevels(),
    '#tasks': () => showTasks(),
    '#requests': () => showRequests(),
    '#delegations': () => showDelegations(),
    '#templates': () => showTemplates(),
    '#leave': () => showLeave(),
    '#capacity': () => showCapacity(),
    '#timesheets': () => showTimesheets(),
    '#analytics': () => showAnalytics(),
    '#performance': () => showPerformance(),
    '#platform': () => showPlatform(),
    '#operations': () => showOperations(),
    '#audit': () => showAudit(),
    '#security': () => showSecurity(),
    '#profile': () => showProfile(),
    '#calendars': () => showCalendars(),
    '#sla': () => showSla()
};

function showView(viewId, navTabId, hash) {
    ALL_VIEWS.forEach(id => {
        const el = document.getElementById(id);
        if (el) el.style.display = id === viewId ? 'block' : 'none';
    });
    ALL_NAV_TABS.forEach(id => {
        const el = document.getElementById(id);
        if (el) el.classList.toggle('active', id === navTabId);
    });
    syncNavGroups();
    closeNavMenus();
    if (hash && window.location.hash !== hash) {
        history.replaceState(null, '', hash);
    }
    if (typeof scheduleLocalize === 'function') {
        const view = document.getElementById(viewId);
        scheduleLocalize(view || document.body);
    }
}

// ============================================
// Grouped navigation (dropdown menus)
// ============================================

// A group button is highlighted when the active item lives inside it.
function syncNavGroups() {
    document.querySelectorAll('.nav-group').forEach(group => {
        const btn = group.querySelector('.nav-group-btn');
        if (!btn) return;
        btn.classList.toggle('active', !!group.querySelector('.nav-item.active'));
    });
}

function closeNavMenus(except) {
    document.querySelectorAll('.nav-group.open').forEach(group => {
        if (group === except) return;
        group.classList.remove('open');
        const btn = group.querySelector('.nav-group-btn');
        if (btn) btn.setAttribute('aria-expanded', 'false');
    });
}

function closeNotifPanel() {
    const panel = document.getElementById('notif-panel');
    if (panel) panel.style.display = 'none';
}

function toggleNavGroup(event, key) {
    event.stopPropagation();
    const group = document.querySelector(`.nav-group[data-nav-group="${key}"]`);
    if (!group) return;
    const willOpen = !group.classList.contains('open');
    closeNavMenus(group);
    closeNotifPanel();
    group.classList.toggle('open', willOpen);
    const btn = group.querySelector('.nav-group-btn');
    if (btn) btn.setAttribute('aria-expanded', willOpen ? 'true' : 'false');
}

function toggleNavMenu(event) {
    if (event) event.stopPropagation();
    const list = document.getElementById('nav-list');
    const burger = document.getElementById('nav-burger');
    if (!list) return;
    const open = list.classList.toggle('open');
    if (burger) burger.setAttribute('aria-expanded', open ? 'true' : 'false');
    closeNotifPanel();
    if (!open) closeNavMenus();
}

// Hide a whole group when every entry inside it is role-gated away.
function syncNavGroupVisibility() {
    document.querySelectorAll('.nav-group').forEach(group => {
        const items = Array.from(group.querySelectorAll('.nav-item'));
        const anyVisible = items.some(item => item.style.display !== 'none');
        group.style.display = anyVisible ? '' : 'none';
    });
}

document.addEventListener('click', (event) => {
    if (!event.target.closest('.nav-group')) closeNavMenus();
    const list = document.getElementById('nav-list');
    if (list && list.classList.contains('open') && !event.target.closest('.app-nav')) {
        list.classList.remove('open');
        const burger = document.getElementById('nav-burger');
        if (burger) burger.setAttribute('aria-expanded', 'false');
    }
});

document.addEventListener('keydown', (event) => {
    if (event.key !== 'Escape') return;
    closeNavMenus();
    const panel = document.getElementById('notif-panel');
    if (panel) panel.style.display = 'none';
});

function showDashboard() {
    currentView = 'dashboard';
    showView('dashboard-view', 'nav-dashboard', '#dashboard');
    loadMyWork();
}

function showDepartments() {
    currentView = 'departments';
    showView('departments-view', 'nav-departments', '#departments');
    const newDeptBtn = document.getElementById('btn-new-department');
    if (newDeptBtn) newDeptBtn.style.display = isAdmin() ? 'inline-flex' : 'none';
    loadDepartments();
}

function showEmployees() {
    currentView = 'employees';
    showView('employees-view', 'nav-employees', '#employees');
    loadEmployees();
}

function showLevels() {
    currentView = 'levels';
    showView('levels-view', 'nav-levels', '#levels');
    document.getElementById('btn-new-level').style.display = isAdmin() ? 'inline-flex' : 'none';
    loadLevels();
}

function showTasks() {
    currentView = 'tasks';
    showView('tasks-view', 'nav-tasks', '#tasks');
    document.getElementById('bulk-delete-btn').style.display = isAdmin() ? 'inline-flex' : 'none';
    loadActiveEmployeesForBulk();
    loadTaskDepartmentFilter();
    loadTasks();
}

function showRequests() {
    currentView = 'requests';
    showView('requests-view', 'nav-requests', '#requests');
    loadTaskRequests();
}

function showSla() {
    if (!isAdmin()) {
        showRestricted();
        return;
    }
    currentView = 'sla';
    showView('sla-view', 'nav-sla', '#sla');
    loadSlaPolicies();
}

function showDelegations() {
    currentView = 'delegations';
    showView('delegations-view', 'nav-delegations', '#delegations');
    loadDelegations();
}

function showTemplates() {
    currentView = 'templates';
    showView('templates-view', 'nav-templates', '#templates');
    loadTemplates();
}

function showLeave() {
    currentView = 'leave';
    showView('leave-view', 'nav-leave', '#leave');
    loadLeave();
}

function showCapacity() {
    currentView = 'capacity';
    showView('capacity-view', 'nav-capacity', '#capacity');
    prepareCapacityForm();
}

function showTimesheets() {
    currentView = 'timesheets';
    showView('timesheets-view', 'nav-timesheets', '#timesheets');
    prepareTimesheets();
}

function showAnalytics() {
    currentView = 'analytics';
    showView('analytics-view', 'nav-analytics', '#analytics');
    prepareAnalyticsForm();
}

function showCalendars() {
    if (!isAdmin()) {
        showRestricted();
        return;
    }
    currentView = 'calendars';
    showView('calendars-view', 'nav-calendars', '#calendars');
    loadCalendars();
}

function showAudit() {
    if (!isAuditViewer()) {
        showRestricted();
        return;
    }
    currentView = 'audit';
    showView('audit-view', 'nav-audit', '#audit');
    if (typeof prepareAuditPage === 'function') prepareAuditPage();
}

function showDepartmentDetail(departmentId, departmentName, location) {
    currentView = 'detail';
    currentDepartmentId = departmentId;
    ALL_VIEWS.forEach(id => {
        const el = document.getElementById(id);
        if (el) el.style.display = 'none';
    });
    document.getElementById('department-detail').style.display = 'block';
    const tp = typeof translatePhrase === 'function' ? translatePhrase : (s) => s;
    document.getElementById('dept-detail-name').textContent = tp(departmentName || '');
    document.getElementById('dept-detail-location').textContent = `📍 ${tp(location || '')}`;
    loadDepartmentDetail(departmentId);
}

function backToDepartments() {
    showDepartments();
}
// ============================================
// Shared formatting and form helpers
// ============================================
function formatDuration(seconds) {
    if (seconds == null) return '—';
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    return h > 0 ? `${h}h ${m}m` : `${m}m`;
}
function escapeHtml(str) {
    if (!str) return '';
    return String(str).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function timeAgo(isoString) {
    const seconds = Math.floor((Date.now() - new Date(isoString).getTime()) / 1000);
    const tt = typeof t === 'function' ? t : null;
    if (seconds < 60) return tt ? tt('ui.just_now') : 'just now';
    const minutes = Math.floor(seconds / 60);
    if (minutes < 60) return tt ? tt('ui.minutes_ago', { n: minutes }) : `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return tt ? tt('ui.hours_ago', { n: hours }) : `${hours}h ago`;
    const days = Math.floor(hours / 24);
    return tt ? tt('ui.days_ago', { n: days }) : `${days}d ago`;
}

function toDatetimeLocalValue(isoString) {
    if (!isoString) return '';
    const d = new Date(isoString);
    const pad = n => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

// Department pickers are filled asynchronously while their form is already on
// screen, so the select has to say something meaningful in the gap and must not
// be left silently empty when the request fails. A failed load is not cached,
// so reopening the form retries.
async function populateDepartmentSelect(selectId) {
    const sel = document.getElementById(selectId);
    if (!sel || sel.dataset.deptsLoaded === 'true') return;

    sel.innerHTML = '<option value="">Loading departments…</option>';
    try {
        const depts = await fetchApi('/departments', {}, {}, true) || [];
        if (!depts.length) {
            sel.innerHTML = '<option value="">No departments available</option>';
            return;
        }
        sel.innerHTML = depts.map(d =>
            `<option value="${d.id}">${escapeHtml(typeof translatePhrase === 'function' ? translatePhrase(d.name) : d.name)}</option>`).join('');
        sel.dataset.deptsLoaded = 'true';
    } catch (error) {
        sel.innerHTML = '<option value="">Could not load departments — reopen to retry</option>';
        console.error(`Failed to load departments for #${selectId}:`, error);
    }
}

// ============================================
// Initialize
// ============================================
document.addEventListener('DOMContentLoaded', () => {
    if (typeof applyI18n === 'function') applyI18n(document);
    if (typeof scheduleLocalize === 'function') scheduleLocalize(document.body);

    // Re-localize after async UI updates (tables, panels, modals).
    if (typeof MutationObserver === 'function' && typeof scheduleLocalize === 'function') {
        let timer = null;
        let suppress = false;
        const orig = window.localizeTree;
        if (typeof orig === 'function') {
            window.localizeTree = function (root) {
                suppress = true;
                try { orig(root); } finally { suppress = false; }
            };
        }
        const obs = new MutationObserver(() => {
            if (suppress) return;
            clearTimeout(timer);
            timer = setTimeout(() => scheduleLocalize(document.body), 80);
        });
        obs.observe(document.body, { childList: true, subtree: true });
    }

    const username = localStorage.getItem('username') || (typeof t === 'function' ? t('common.user') : 'User');
    const userInfo = document.getElementById('user-info');
    const userAvatar = document.getElementById('user-avatar');

    if (userInfo) {
        userInfo.textContent = username;
    }
    if (userAvatar) {
        userAvatar.textContent = username.charAt(0).toUpperCase();
    }
    const userRole = document.getElementById('user-role');
    if (userRole) {
        userRole.textContent = typeof translateRole === 'function'
            ? translateRole(localStorage.getItem('role'))
            : ((localStorage.getItem('role') || '').replace('_', ' ') || 'Member');
    }

    if (isAdmin()) {
        ['nav-sla', 'nav-calendars', 'nav-operations'].forEach(id => {
            const el = document.getElementById(id);
            if (el) el.style.display = '';
        });
    }

    // Audit workspace: Admin + Auditor. Auditor is otherwise limited to /v1/audit/*
    // by the API middleware, so hide the business nav groups for that role.
    const auditNav = document.getElementById('nav-audit');
    if (auditNav) auditNav.style.display = isAuditViewer() ? '' : 'none';
    if (isAuditor()) {
        document.querySelectorAll('.nav-group').forEach(group => {
            const key = group.getAttribute('data-nav-group');
            if (key && key !== 'system') group.style.display = 'none';
        });
        ['nav-dashboard', 'nav-platform', 'nav-operations', 'nav-calendars', 'nav-sla'].forEach(id => {
            const el = document.getElementById(id);
            if (el) el.style.display = 'none';
        });
        const notifWrap = document.querySelector('.notif-wrap');
        if (notifWrap) notifWrap.style.display = 'none';
    }

    syncNavGroupVisibility();
    syncNavGroups();

    const handoverBtn = document.getElementById('btn-handover');
    if (handoverBtn) handoverBtn.style.display = isAdmin() ? 'inline-flex' : 'none';

    const route = HASH_ROUTES[window.location.hash];
    if (route) {
        route();
    } else if (isAuditor()) {
        showAudit();
    } else {
        showDashboard();
    }

    if (!isAuditor()) {
        refreshNotificationBadge();
        setInterval(refreshNotificationBadge, 60000);
    }
});
