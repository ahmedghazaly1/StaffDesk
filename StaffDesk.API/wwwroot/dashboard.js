// ============================================
// Dashboard: the My Work strip and its filters.
// ============================================

let myWorkTasks = [];
let myWorkActiveFilter = null;

const MYWORK_FILTER_KEYS = {
    assigned: { labelKey: 'dashboard.assigned', icon: '📋', match: () => true },
    overdue: { labelKey: 'dashboard.overdue', icon: '⏰', match: tsk => tsk.isOverdue },
    dueSoon: {
        labelKey: 'dashboard.dueSoon', icon: '📅',
        match: tsk => {
            if (!tsk.dueAt || tsk.isOverdue) return false;
            const due = new Date(tsk.dueAt);
            const in7Days = new Date();
            in7Days.setDate(in7Days.getDate() + 7);
            return due <= in7Days;
        }
    },
    inReview: { labelKey: 'dashboard.inReview', icon: '👀', match: tsk => tsk.status === 'IN_REVIEW' }
};

function myWorkLabel(cfg) {
    return typeof t === 'function' ? t(cfg.labelKey) : cfg.labelKey;
}

async function loadMyWork() {
    const strip = document.getElementById('mywork-strip');
    strip.innerHTML = `<p class="empty-state">${typeof t === 'function' ? t('dashboard.loading') : 'Loading your work…'}</p>`;
    myWorkActiveFilter = null;

    try {
        const data = await fetchApi('/tasks/mine');
        myWorkTasks = Array.isArray(data) ? data : [];
    } catch (error) {
        console.error('Failed to load my work:', error);
        myWorkTasks = [];
        strip.innerHTML = `<p class="empty-state">${typeof t === 'function' ? t('dashboard.loadFailed') : 'Could not load your tasks.'}</p>`;
    }

    renderMyWorkStrip();
    renderMyWorkTaskList();
}

function renderMyWorkStrip() {
    const strip = document.getElementById('mywork-strip');
    strip.innerHTML = Object.entries(MYWORK_FILTER_KEYS).map(([key, cfg]) => {
        const count = myWorkTasks.filter(cfg.match).length;
        const isActive = myWorkActiveFilter === key;
        return `
            <div class="stat-card ${isActive ? 'active' : ''}" onclick="toggleMyWorkFilter('${key}')">
                <div class="stat-icon">${cfg.icon}</div>
                <div class="stat-count">${count}</div>
                <div class="stat-label">${myWorkLabel(cfg)}</div>
            </div>
        `;
    }).join('');
}

function toggleMyWorkFilter(key) {
    myWorkActiveFilter = myWorkActiveFilter === key ? null : key;
    renderMyWorkStrip();
    renderMyWorkTaskList();
}

function renderMyWorkTaskList() {
    const container = document.getElementById('mywork-task-list');
    const cfg = myWorkActiveFilter ? MYWORK_FILTER_KEYS[myWorkActiveFilter] : null;
    const tasks = cfg ? myWorkTasks.filter(cfg.match) : myWorkTasks;
    const tt = typeof t === 'function' ? t : (k) => k;
    const ts = typeof translateStatus === 'function' ? translateStatus : (s) => s;
    const tp = typeof translatePriority === 'function' ? translatePriority : (p) => p;
    const loc = typeof localeTag === 'function' ? localeTag() : 'en';

    if (tasks.length === 0) {
        container.innerHTML = `<p class="empty-state">${cfg
            ? tt('dashboard.emptyFiltered', { filter: myWorkLabel(cfg) })
            : tt('dashboard.emptyAssigned')}</p>`;
        return;
    }

    let html = `
        <table>
            <thead>
                <tr>
                    <th>${tt('table.key')}</th>
                    <th>${tt('table.title')}</th>
                    <th>${tt('table.status')}</th>
                    <th>${tt('table.priority')}</th>
                    <th>${tt('table.sla')}</th>
                    <th>${tt('table.rework')}</th>
                    <th>${tt('table.reopen')}</th>
                    <th>${tt('table.department')}</th>
                    <th>${tt('table.dueDate')}</th>
                </tr>
            </thead>
            <tbody>
    `;
    tasks.forEach(task => {
        const statusClass = `status-${task.status.toLowerCase()}`;
        const priorityClass = `priority-${task.priority.toLowerCase()}`;
        const slaBadge = renderSlaBadge(task);
        const dueLabel = task.dueAt
            ? `<span style="${task.isOverdue ? 'color:#e53e3e;font-weight:600;' : ''}">${new Date(task.dueAt).toLocaleDateString(loc)}</span>`
            : tt('common.na');
        html += `
            <tr onclick="showTaskDetail(${task.id})" style="cursor:pointer;">
                <td><strong>${task.key}</strong></td>
                <td>${task.title}</td>
                <td><span class="status-badge ${statusClass}">${ts(task.status)}</span></td>
                <td><span class="priority-badge ${priorityClass}">${tp(task.priority)}</span></td>
                <td>${slaBadge}</td>
                <td class="count-col">${task.reworkCount || 0}</td>
                <td class="count-col">${task.reopenCount || 0}</td>
                <td>${task.departmentName}</td>
                <td>${dueLabel}</td>
            </tr>
        `;
    });
    html += '</tbody></table>';
    container.innerHTML = html;
}
