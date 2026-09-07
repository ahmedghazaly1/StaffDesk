// ============================================
// Notifications: unread badge, panel paging, and read state.
// ============================================

let notificationsLoaded = false;
let notificationsCursor = null;

async function refreshNotificationBadge() {
    try {
        const data = await fetchApi('/notifications', {}, { limit: 1 }, true);
        if (!data) return;
        updateNotifBadge(data.unreadCount || 0);
    } catch (error) {
        console.error('Failed to refresh notifications:', error);
    }
}

function updateNotifBadge(count) {
    const badge = document.getElementById('notif-badge');
    if (count > 0) {
        badge.textContent = count > 99 ? '99+' : count;
        badge.style.display = 'inline-block';
    } else {
        badge.style.display = 'none';
    }
}

async function toggleNotifications(event) {
    event.stopPropagation();
    closeNavMenus();
    const panel = document.getElementById('notif-panel');
    const isOpen = panel.style.display === 'block';
    panel.style.display = isOpen ? 'none' : 'block';
    if (!isOpen) {
        await loadNotifications();
    }
}

async function loadNotifications(append = false) {
    const list = document.getElementById('notif-list');
    if (!append) {
        list.innerHTML = '<p class="empty-state">Loading…</p>';
        notificationsCursor = null;
    }
    try {
        const params = { limit: 20 };
        if (notificationsCursor) params.cursor = notificationsCursor;
        const data = await fetchApi('/notifications', {}, params, true);
        if (!data) return;
        notificationsLoaded = true;
        notificationsCursor = data.nextCursor || null;
        updateNotifBadge(data.unreadCount || 0);
        const items = data.data || [];
        if (append && list.querySelector('.notif-load-more-wrap')) {
            list.querySelector('.notif-load-more-wrap')?.remove();
        }
        if (!append) renderNotifications(items);
        else appendNotifications(items);
        if (notificationsCursor) {
            list.insertAdjacentHTML('beforeend',
                `<div class="notif-load-more-wrap" style="padding:8px;text-align:center;">
                    <button class="btn-secondary btn-sm" onclick="loadNotifications(true)">Load more</button>
                </div>`);
        }
    } catch (error) {
        console.error('Failed to load notifications:', error);
        if (!append) list.innerHTML = '<p class="empty-state">Could not load notifications.</p>';
    }
}

function appendNotifications(notifications) {
    const list = document.getElementById('notif-list');
    const wrap = list.querySelector('.notif-load-more-wrap');
    const html = notifications.map(n => `
        <div class="notif-item ${n.isRead ? '' : 'unread'}" onclick="handleNotificationClick(${n.id}, ${n.taskId ?? 'null'}, ${JSON.stringify(n.type || '')})">
            <div class="notif-message">${n.message}</div>
            <div class="notif-meta">${timeAgo(n.createdAt)}</div>
        </div>
    `).join('');
    if (wrap) wrap.insertAdjacentHTML('beforebegin', html);
    else list.insertAdjacentHTML('beforeend', html);
}

function renderNotifications(notifications) {
    const list = document.getElementById('notif-list');
    if (!notifications || notifications.length === 0) {
        list.innerHTML = '<p class="empty-state">You\'re all caught up!</p>';
        return;
    }

    list.innerHTML = notifications.map(n => `
        <div class="notif-item ${n.isRead ? '' : 'unread'}" onclick="handleNotificationClick(${n.id}, ${n.taskId ?? 'null'}, ${JSON.stringify(n.type || '')})">
            <div class="notif-message">${n.message}</div>
            <div class="notif-meta">${timeAgo(n.createdAt)}</div>
        </div>
    `).join('');
}

async function handleNotificationClick(notificationId, taskId, type) {
    document.getElementById('notif-panel').style.display = 'none';
    try {
        await fetchApi(`/notifications/${notificationId}/read`, { method: 'PATCH' }, {}, true);
    } catch (error) {
        console.error('Failed to mark notification read:', error);
    }
    refreshNotificationBadge();
    if (taskId) {
        showTaskDetail(taskId);
    } else if (type === 'TASK_REQUEST_SUBMITTED') {
        currentView = 'requests';
        showView('requests-view', 'nav-requests', '#requests');
        switchRequestTab('triage');
    }
}

async function markAllNotificationsRead() {
    try {
        await fetchApi('/notifications/read-all', { method: 'POST' });
        updateNotifBadge(0);
        if (notificationsLoaded) {
            loadNotifications();
        }
    } catch (error) {
        showError('Failed to mark notifications as read');
    }
}

document.addEventListener('click', (event) => {
    const wrap = document.querySelector('.notif-wrap');
    const panel = document.getElementById('notif-panel');
    if (wrap && panel && panel.style.display === 'block' && !wrap.contains(event.target)) {
        panel.style.display = 'none';
    }
});
