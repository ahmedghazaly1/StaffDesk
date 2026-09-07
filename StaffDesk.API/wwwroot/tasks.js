// ============================================
// Task list: filters, pagination, selection and bulk actions,
// sorting, and task creation.
// ============================================

// Task pagination
let taskCurrentPage = 1;
const TASK_PAGE_SIZE = 10;
let currentTaskFilters = {};
let currentTaskSort = '-createdAt';
// Independent column directions: null = not sorting by this column, 'asc' | 'desc'
let taskSortPriority = null;
let taskSortDueAt = null;

// Phase 3 state
let selectedTaskIds = new Set();
let currentTasksPage = [];
let activeEmployeesCache = [];


function showTaskWarnings(task) {
    if (task && Array.isArray(task.warnings) && task.warnings.length) {
        showError('⚠ ' + task.warnings.join('; '));
    }
}
// ============================================
// TASK MANAGEMENT
// ============================================

async function loadTasks() {
    try {
        const params = {
            page: taskCurrentPage,
            limit: TASK_PAGE_SIZE,
            sort: currentTaskSort,
            ...currentTaskFilters
        };
        const data = await fetchApi('/tasks', {}, params);
        if (data) {
            currentTasksPage = data.data || [];
            renderTasks(data);
            updateTaskPagination(data);
        }
    } catch (error) {
        console.error('Failed to load tasks:', error);
    }
}

async function loadTaskDepartmentFilter() {
    const sel = document.getElementById('filter-department');
    if (!sel || sel.dataset.deptsLoaded === 'true') return;
    const allLabel = typeof t === 'function' ? t('filter.allDepartments') : 'All Departments';
    try {
        const depts = await fetchApi('/departments', {}, {}, true) || [];
        sel.innerHTML = `<option value="">${allLabel}</option>` +
            depts.map(d =>
                `<option value="${d.id}">${escapeHtml(typeof translatePhrase === 'function' ? translatePhrase(d.name) : d.name)}</option>`
            ).join('');
        sel.dataset.deptsLoaded = 'true';
    } catch (error) {
        console.error('Failed to load department filter:', error);
        sel.innerHTML = `<option value="">${allLabel}</option>`;
    }
}

async function loadActiveEmployeesForBulk() {
    try {
        const data = await fetchApi('/employees', {}, { limit: 200 });
        activeEmployeesCache = (data?.data || []).filter(e => e.isActive);
        const select = document.getElementById('bulk-assignee');
        if (!select) return;
        select.innerHTML = `<option value="">${typeof t === 'function' ? t('filter.assignee') : '— Assignee —'}</option>` +
            `<option value="unassigned">${typeof t === 'function' ? t('common.unassigned') : 'Unassigned'}</option>` +
            activeEmployeesCache.map(emp =>
                `<option value="${emp.id}">${emp.fullName} (${emp.departmentName})</option>`
            ).join('');
    } catch (error) {
        console.error('Failed to load employees for bulk actions:', error);
    }
}

function updateBulkBar() {
    const bar = document.getElementById('bulk-actions-bar');
    const countEl = document.getElementById('bulk-selected-count');
    if (!bar || !countEl) return;
    const count = selectedTaskIds.size;
    countEl.textContent = typeof t === 'function' ? t('common.selected', { count }) : `${count} selected`;
    bar.style.display = count > 0 ? 'flex' : 'none';
}

function toggleTaskSelection(taskId, checked) {
    if (checked) {
        selectedTaskIds.add(taskId);
    } else {
        selectedTaskIds.delete(taskId);
    }
    updateBulkBar();
    const selectAll = document.getElementById('select-all-tasks');
    if (selectAll && currentTasksPage.length > 0) {
        selectAll.checked = currentTasksPage.every(t => selectedTaskIds.has(t.id));
    }
}

function selectAllTasks(checked) {
    currentTasksPage.forEach(task => {
        if (checked) {
            selectedTaskIds.add(task.id);
        } else {
            selectedTaskIds.delete(task.id);
        }
    });
    document.querySelectorAll('.task-select-cb').forEach(cb => {
        cb.checked = checked;
    });
    updateBulkBar();
}

async function applyBulkAction() {
    const ids = Array.from(selectedTaskIds);
    if (ids.length === 0) {
        showError('No tasks selected');
        return;
    }

    const status = document.getElementById('bulk-status').value;
    const assigneeVal = document.getElementById('bulk-assignee').value;
    const archiveChecked = document.getElementById('bulk-archive').checked;
    const tagsRaw = document.getElementById('bulk-tags').value.trim();

    if (!status && !assigneeVal && !archiveChecked && !tagsRaw) {
        showError('Select at least one bulk action');
        return;
    }

    const results = [];

    try {
        if (status) {
            const res = await fetchApi('/tasks/bulk/status', {
                method: 'PATCH',
                body: JSON.stringify({ taskIds: ids, status, reason: null })
            });
            if (await handleBulkResponse(res, 'Bulk status')) {
                return;
            }
            results.push(`Status: ${res.totalSuccessful}/${res.totalProcessed} succeeded`);
        }

        if (assigneeVal) {
            const assigneeId = assigneeVal === 'unassigned' ? null : parseInt(assigneeVal, 10);
            const res = await fetchApi('/tasks/bulk/assignee', {
                method: 'PATCH',
                body: JSON.stringify({ taskIds: ids, assigneeId })
            });
            if (await handleBulkResponse(res, 'Bulk assignee')) {
                return;
            }
            results.push(`Assignee: ${res.totalSuccessful}/${res.totalProcessed} succeeded`);
        }

        if (archiveChecked) {
            const res = await fetchApi('/tasks/bulk/archive', {
                method: 'PATCH',
                body: JSON.stringify({ taskIds: ids, isArchived: true })
            });
            results.push(`Archive: ${res.totalSuccessful}/${res.totalProcessed} succeeded`);
        }

        if (tagsRaw) {
            const tags = tagsRaw.split(',').map(t => t.trim()).filter(Boolean);
            const res = await fetchApi('/tasks/bulk/tags', {
                method: 'POST',
                body: JSON.stringify({ taskIds: ids, tags })
            });
            results.push(`Tags: ${res.totalSuccessful}/${res.totalProcessed} succeeded`);
        }

        showError('✅ Bulk action complete — ' + results.join('; '));
        selectedTaskIds.clear();
        document.getElementById('bulk-status').value = '';
        document.getElementById('bulk-assignee').value = '';
        document.getElementById('bulk-archive').checked = false;
        document.getElementById('bulk-tags').value = '';
        updateBulkBar();
        loadTasks();
    } catch (error) {
        showError('❌ ' + error.message);
    }
}

async function applyBulkDelete() {
    if (!isAdmin()) {
        showError('Only admins can bulk delete tasks');
        return;
    }
    const ids = Array.from(selectedTaskIds);
    if (ids.length === 0) return;
    if (!confirm(`Delete ${ids.length} task(s)? This cannot be undone.`)) return;

    try {
        const res = await fetchApi('/tasks/bulk', {
            method: 'DELETE',
            body: JSON.stringify({ taskIds: ids })
        });
        showError(`✅ Deleted ${res.totalSuccessful}/${res.totalProcessed} tasks`);
        selectedTaskIds.clear();
        updateBulkBar();
        loadTasks();
    } catch (error) {
        showError('❌ ' + error.message);
    }
}

function renderTasks(data) {
    const container = document.getElementById('task-list');
    const tt = typeof t === 'function' ? t : (k) => k;
    if (!data.data || data.data.length === 0) {
        container.innerHTML = `<p class="empty-state">${tt('empty.tasks')}</p>`;
        updateBulkBar();
        return;
    }

    const allSelected = data.data.every(task => selectedTaskIds.has(task.id));
    const sortIndicator = (key) => {
        const dir = key === 'priority' ? taskSortPriority : key === 'dueAt' ? taskSortDueAt : null;
        if (dir === 'asc') return ' ▲';
        if (dir === 'desc') return ' ▼';
        return '';
    };
    
    let html = `
        <table>
            <thead>
                <tr>
                    <th class="checkbox-col">
                        <input type="checkbox" id="select-all-tasks" ${allSelected ? 'checked' : ''} onchange="selectAllTasks(this.checked)" />
                    </th>
                    <th>${tt('table.key')}</th>
                    <th>${tt('table.title')}</th>
                    <th>${tt('table.status')}</th>
                    <th class="sortable-th" onclick="sortTasksBy('priority')" title="${tt('table.priority')}">${tt('table.priority')}${sortIndicator('priority')}</th>
                    <th>${tt('table.sla')}</th>
                    <th>${tt('table.rework')}</th>
                    <th>${tt('table.reopen')}</th>
                    <th>${tt('table.assignee')}</th>
                    <th>${tt('table.department')}</th>
                    <th class="sortable-th" onclick="sortTasksBy('dueAt')" title="${tt('table.dueDate')}">${tt('table.dueDate')}${sortIndicator('dueAt')}</th>
                    <th>${tt('table.actions')}</th>
                </tr>
            </thead>
            <tbody>
    `;
    data.data.forEach(task => {
        const statusClass = `status-${task.status.toLowerCase()}`;
        const priorityClass = `priority-${task.priority.toLowerCase()}`;
        const slaBadge = renderSlaBadge(task);
        const checked = selectedTaskIds.has(task.id) ? 'checked' : '';
        
        html += `
            <tr>
                <td class="checkbox-col">
                    <input type="checkbox" class="task-select-cb" data-task-id="${task.id}" ${checked} onchange="toggleTaskSelection(${task.id}, this.checked)" />
                </td>
                <td><strong>${task.key}</strong></td>
                <td>${task.title}</td>
                <td><span class="status-badge ${statusClass}">${typeof translateStatus === 'function' ? translateStatus(task.status) : task.status.replace('_', ' ')}</span></td>
                <td><span class="priority-badge ${priorityClass}">${typeof translatePriority === 'function' ? translatePriority(task.priority) : task.priority}</span></td>
                <td>${slaBadge}</td>
                <td class="count-col">${task.reworkCount || 0}</td>
                <td class="count-col">${task.reopenCount || 0}</td>
                <td>${task.assigneeName || tt('common.unassigned')}</td>
                <td>${task.departmentName}</td>
                <td>${task.dueAt ? new Date(task.dueAt).toLocaleDateString(typeof getStoredLang === 'function' && getStoredLang() === 'ar' ? 'ar' : 'en') : tt('common.na')}</td>
                <td>
                    <button class="btn-secondary btn-sm" onclick="showTaskDetail(${task.id})">👁️ ${tt('common.view')}</button>
                    <button class="btn-secondary btn-sm" onclick="showEditTask(${task.id})">✏️ ${tt('common.edit')}</button>
                </td>
            </tr>
        `;
    });
    html += '</tbody></table>';
    container.innerHTML = html;
    updateBulkBar();
}

function updateTaskPagination(data) {
    const totalPages = data.totalPages || 1;
    const page = data.page || 1;
    document.getElementById('task-page-info').textContent = typeof t === 'function'
        ? t('common.pageOf', { page, total: totalPages })
        : `Page ${page} of ${totalPages}`;
    document.getElementById('task-prev-btn').disabled = page <= 1;
    document.getElementById('task-next-btn').disabled = page >= totalPages;
}

function taskPreviousPage() {
    if (taskCurrentPage > 1) {
        taskCurrentPage--;
        loadTasks();
    }
}

function taskNextPage() {
    taskCurrentPage++;
    loadTasks();
}

function applyTaskFilters() {
    currentTaskFilters = {
        status: document.getElementById('filter-status').value || undefined,
        priority: document.getElementById('filter-priority').value || undefined,
        departmentId: document.getElementById('filter-department').value || undefined,
        search: document.getElementById('task-search').value.trim() || undefined
    };
    taskCurrentPage = 1;
    loadTasks();
}

function resetTaskFilters() {
    document.getElementById('filter-status').value = '';
    document.getElementById('filter-priority').value = '';
    document.getElementById('filter-department').value = '';
    document.getElementById('task-search').value = '';
    currentTaskFilters = {};
    taskSortPriority = null;
    taskSortDueAt = null;
    currentTaskSort = '-createdAt';
    taskCurrentPage = 1;
    loadTasks();
}

function buildTaskSort() {
    const parts = [];
    if (taskSortPriority === 'asc') parts.push('priority');
    if (taskSortPriority === 'desc') parts.push('-priority');
    if (taskSortDueAt === 'asc') parts.push('dueAt');
    if (taskSortDueAt === 'desc') parts.push('-dueAt');
    return parts.length ? parts.join(',') : '-createdAt';
}

function cycleSortDir(current) {
    if (current === null) return 'asc';
    if (current === 'asc') return 'desc';
    return null;
}

function sortTasksBy(field) {
    // Each column is independent — you can set priority and due date together.
    // Click cycles: off → ascending → descending → off.
    if (field === 'priority') {
        taskSortPriority = cycleSortDir(taskSortPriority);
    } else if (field === 'dueAt') {
        taskSortDueAt = cycleSortDir(taskSortDueAt);
    }
    currentTaskSort = buildTaskSort();
    taskCurrentPage = 1;
    loadTasks();
}

// ============================================
// Create Task
// ============================================
function showCreateTask() {
    const form = document.getElementById('create-task-form');
    form.style.display = form.style.display === 'none' ? 'block' : 'none';
    if (form.style.display === 'block') {
        loadTaskDropdowns();
        document.getElementById('task-title').focus();
    }
}

function hideCreateTask() {
    document.getElementById('create-task-form').style.display = 'none';
    document.getElementById('task-error').style.display = 'none';
}

async function loadTaskDropdowns() {
    try {
        const departments = await fetchApi('/departments');
        const deptSelect = document.getElementById('task-department');
        if (departments && departments.length > 0) {
            deptSelect.innerHTML = departments.map(dept =>
                `<option value="${dept.name}">${dept.name}</option>`
            ).join('');
        } else {
            deptSelect.innerHTML = '<option value="">No departments available</option>';
        }

        const employeesData = await fetchApi('/employees', {}, { limit: 100 });
        const empSelect = document.getElementById('task-assignee');
        if (employeesData && employeesData.data) {
            empSelect.innerHTML = `<option value="">${typeof t === 'function' ? t('common.unassigned') : 'Unassigned'}</option>` +
                employeesData.data
                    .filter(e => e.isActive)
                    .map(emp =>
                        `<option value="${emp.id}">${emp.fullName} (${emp.departmentName})</option>`
                    ).join('');
        }
    } catch (error) {
        console.error('Failed to load dropdowns:', error);
    }
}

async function createTask(event) {
    event.preventDefault();
    
    const title = document.getElementById('task-title').value.trim();
    const description = document.getElementById('task-description').value.trim();
    const departmentName = document.getElementById('task-department').value.trim();
    const assigneeId = document.getElementById('task-assignee').value;
    const priority = document.getElementById('task-priority').value;
    const dueAt = document.getElementById('task-due').value;
    const estimateMinutes = document.getElementById('task-estimate').value;
    const tags = document.getElementById('task-tags').value
        .split(',')
        .map(t => t.trim())
        .filter(t => t);

    if (!title) {
        document.getElementById('task-error').textContent = 'Title is required';
        document.getElementById('task-error').style.display = 'block';
        return;
    }

    if (!departmentName) {
        document.getElementById('task-error').textContent = 'Department name is required';
        document.getElementById('task-error').style.display = 'block';
        return;
    }

    try {
        const created = await fetchApi('/tasks', {
            method: 'POST',
            body: JSON.stringify({
                title,
                description: description || null,
                departmentName,
                assigneeId: assigneeId ? parseInt(assigneeId) : null,
                priority,
                dueAt: dueAt ? new Date(dueAt).toISOString() : null,
                estimateMinutes: estimateMinutes ? parseInt(estimateMinutes) : null,
                tags: tags.length > 0 ? tags : null
            })
        });
        hideCreateTask();
        loadTasks();
        showError('✅ Task created successfully!');
        showTaskWarnings(created);
    } catch (error) {
        document.getElementById('task-error').textContent = error.message;
        document.getElementById('task-error').style.display = 'block';
    }
}

// ============================================
// Bulk job polling
// ============================================
async function handleBulkResponse(res, label) {
    if (res?.jobId) {
        showBulkJobStatus(`⏳ ${label} queued (job #${res.jobId})…`);
        await pollBulkJob(res.jobId);
        return true;
    }
    return false;
}

function showBulkJobStatus(msg) {
    const bar = document.getElementById('bulk-job-status');
    if (bar) { bar.style.display = 'block'; bar.textContent = msg; }
}

async function pollBulkJob(jobId) {
    for (let i = 0; i < 60; i++) {
        await new Promise(r => setTimeout(r, 2000));
        const job = await fetchApi(`/jobs/${jobId}`, {}, {}, true);
        if (!job) continue;
        showBulkJobStatus(`Job #${jobId}: ${job.state}${job.lastError ? ' — ' + job.lastError : ''}`);
        if (job.state === 'SUCCEEDED' || job.state === 'DEAD') break;
    }
    loadTasks();
}
