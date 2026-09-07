// ============================================
// Task detail modal: outcome, status transitions, editing, and the
// comment / checklist / time / criteria / dependency panels.
// ============================================

let currentDetailTaskId = null;

const REWORK_CATEGORIES = [
    { value: 'incomplete', label: 'Incomplete' },
    { value: 'defective', label: 'Defective' },
    { value: 'misunderstood', label: 'Misunderstood' },
    { value: 'changed', label: 'Changed' },
    { value: 'quality', label: 'Quality' }
];
// ============================================
// Task Detail
// ============================================
const OUTCOME_OPTIONS = [
    { value: 'DELIVERED', label: 'Delivered' },
    { value: 'DELIVERED_PARTIAL', label: 'Delivered (Partial)' },
    { value: 'SUPERSEDED', label: 'Superseded' },
    { value: 'NOT_REPRODUCIBLE', label: 'Not Reproducible' },
    { value: 'DUPLICATE', label: 'Duplicate' },
    { value: 'WONT_DO', label: "Won't Do" }
];

function formatOutcome(outcome) {
    const opt = OUTCOME_OPTIONS.find(o => o.value === outcome);
    return opt ? opt.label : outcome.replace(/_/g, ' ');
}

async function showTaskDetail(taskId) {
    currentDetailTaskId = taskId;
    await refreshTaskDetail(taskId);
}

async function refreshTaskDetail(taskId) {
    try {
        const task = await fetchApi(`/tasks/${taskId}`);
        if (!task) return;

        currentDetailTaskId = taskId;
        document.getElementById('modal-task-title').textContent = `${task.key}: ${task.title}`;

        const statusClass = `status-${task.status.toLowerCase()}`;
        const priorityClass = `priority-${task.priority.toLowerCase()}`;

        let approvalHtml = '';
        try {
            approvalHtml = await loadApprovalInfo(taskId);
        } catch (err) {
            console.error('Failed to load approvals:', err);
        }

        const html = `
            <div class="task-detail-grid">
                <div class="task-detail-item">
                    <label>Status</label>
                    <div class="value"><span class="status-badge ${statusClass}">${typeof translateStatus === 'function' ? translateStatus(task.status) : task.status.replace('_', ' ')}</span></div>
                </div>
                <div class="task-detail-item">
                    <label>Priority</label>
                    <div class="value"><span class="priority-badge ${priorityClass}">${typeof translatePriority === 'function' ? translatePriority(task.priority) : task.priority}</span></div>
                </div>
                <div class="task-detail-item">
                    <label>Assignee</label>
                    <div class="value">${task.assigneeName || (typeof t === 'function' ? t('common.unassigned') : 'Unassigned')}</div>
                </div>
                <div class="task-detail-item">
                    <label>Department</label>
                    <div class="value">${task.departmentName}</div>
                </div>
                <div class="task-detail-item">
                    <label>Due Date</label>
                    <div class="value">${task.dueAt ? new Date(task.dueAt).toLocaleString(typeof localeTag === 'function' ? localeTag() : undefined) : (typeof t === 'function' ? t('common.na') : 'N/A')}</div>
                </div>
                <div class="task-detail-item">
                    <label>Created</label>
                    <div class="value">${new Date(task.createdAt).toLocaleString()}</div>
                </div>
                <div class="task-detail-item">
                    <label>Outcome</label>
                    <div class="value">${task.outcome ? `<span class="outcome-badge">${formatOutcome(task.outcome)}</span>` : 'Not set'}</div>
                </div>
                <div class="task-detail-item">
                    <label>Closure Required</label>
                    <div class="value ${task.isClosureRequired ? 'closure-required' : ''}">${task.isClosureRequired ? 'Yes' : 'No'}</div>
                </div>
                <div class="task-detail-item" style="grid-column: 1 / -1;">
                    <label>Description</label>
                    <div class="value">${task.description || 'No description'}</div>
                </div>
                <div class="task-detail-item" style="grid-column: 1 / -1;">
                    <label>Tags</label>
                    <div class="value">${task.tags?.join(', ') || 'None'}</div>
                </div>
                <div class="task-detail-item" style="grid-column: 1 / -1;">
                    <label>Checklist</label>
                    <div class="value">${task.checklist?.done || 0} / ${task.checklist?.total || 0} completed</div>
                </div>
                <div class="task-detail-item" style="grid-column: 1 / -1;">
                    <label>SLA</label>
                    <div class="value">${renderSlaBadge(task)}</div>
                </div>
                ${task.responseTargetAt ? `<div class="task-detail-item"><label>Response Target</label><div class="value">${new Date(task.responseTargetAt).toLocaleString()}</div></div>` : ''}
                ${task.resolutionTargetAt ? `<div class="task-detail-item"><label>Resolution Target</label><div class="value">${new Date(task.resolutionTargetAt).toLocaleString()}</div></div>` : ''}
                ${task.remainingMinutes != null ? `<div class="task-detail-item"><label>Remaining</label><div class="value">${task.remainingMinutes} min</div></div>` : ''}
                ${task.blockedPauseMinutes != null ? `<div class="task-detail-item"><label>Blocked Pause</label><div class="value">${task.blockedPauseMinutes} min</div></div>` : ''}
                <div class="task-detail-item">
                    <label>Rework Count</label>
                    <div class="value">${task.reworkCount || 0}</div>
                </div>
                <div class="task-detail-item">
                    <label>Reopen Count</label>
                    <div class="value">${task.reopenCount || 0}</div>
                </div>
            </div>

            ${renderStatusControl(task)}
            ${renderOutcomeSection(task)}
            ${approvalHtml}
            <div id="task-detail-extensions"></div>

            <div style="margin-top:16px;display:flex;gap:8px;flex-wrap:wrap;">
                <button class="btn-secondary btn-sm" onclick="closeTaskDetail()">Close</button>
                <button class="btn-primary btn-sm" onclick="closeTaskDetail();showEditTask(${task.id})">✏️ Edit</button>
            </div>
        `;

        document.getElementById('task-detail-content').innerHTML = html;
        document.getElementById('task-detail-modal').classList.add('active');
        await loadTaskDetailExtensions(taskId, task);
    } catch (error) {
        showError('Failed to load task details');
    }
}

function renderOutcomeSection(task) {
    if (task.status !== 'DONE' && task.status !== 'CANCELLED') return '';

    const options = OUTCOME_OPTIONS.map(o =>
        `<option value="${o.value}" ${task.outcome === o.value ? 'selected' : ''}>${o.label}</option>`
    ).join('');

    const closureBtn = task.status === 'DONE' && task.isClosureRequired
        ? `<button class="btn-primary btn-sm" onclick="processTaskClosure(${task.id})">Process Closure</button>`
        : '';

    return `
        <div class="detail-section">
            <h4>Outcome &amp; Closure</h4>
            <div class="detail-section-actions">
                <select id="outcome-select">${options}</select>
                <input type="text" id="outcome-note" placeholder="Outcome note (optional)" />
                <button class="btn-primary btn-sm" onclick="setTaskOutcome(${task.id})">Apply Outcome</button>
                ${closureBtn}
            </div>
            <div class="detail-section-actions" style="margin-top:8px;">
                <input type="text" id="closure-note" placeholder="Closure note (required if SLA breached or reopened)" style="flex:2;" />
            </div>
        </div>
    `;
}

async function setTaskOutcome(taskId) {
    const outcome = document.getElementById('outcome-select')?.value;
    const note = document.getElementById('outcome-note')?.value.trim() || null;

    if (!outcome) {
        showError('Please select an outcome');
        return;
    }

    try {
        await fetchApi(`/tasks/${taskId}/outcome`, {
            method: 'POST',
            body: JSON.stringify({ outcome, note, externalReference: null, externalReferenceLabel: null })
        });
        showError('✅ Outcome applied!');
        await refreshTaskDetail(taskId);
        loadTasks();
    } catch (error) {
        showError('❌ ' + error.message);
    }
}

async function processTaskClosure(taskId) {
    const closureNote = document.getElementById('closure-note')?.value.trim() || null;

    try {
        await fetchApi(`/tasks/${taskId}/closure`, {
            method: 'POST',
            body: JSON.stringify({ closureNote })
        });
        showError('✅ Closure processed!');
        await refreshTaskDetail(taskId);
        loadTasks();
    } catch (error) {
        showError('❌ ' + error.message);
    }
}

// UI-18: transitions from server; outcome on cancel, rework category on rework/reopen.
function onStatusTransitionChange(currentStatus) {
    const sel = document.getElementById('status-transition-select');
    const target = sel?.value || '';
    const outcomeWrap = document.getElementById('status-outcome-wrap');
    const reworkWrap = document.getElementById('status-rework-wrap');
    if (outcomeWrap) outcomeWrap.style.display = target === 'CANCELLED' ? 'block' : 'none';
    if (reworkWrap) {
        const needsRework = (currentStatus === 'IN_REVIEW' && target === 'IN_PROGRESS')
            || (currentStatus === 'DONE' && target === 'OPEN');
        reworkWrap.style.display = needsRework ? 'block' : 'none';
    }
}

function renderStatusControl(task) {
    const transitions = task.availableTransitions || [];
    if (transitions.length === 0) {
        return `<div class="task-detail-item" style="margin-top:16px;"><label>Status</label><div class="value">No transitions available.</div></div>`;
    }
    const options = transitions.map(s => `<option value="${s}">${s.replace(/_/g, ' ')}</option>`).join('');
    const outcomeOpts = OUTCOME_OPTIONS.map(o => `<option value="${o.value}">${o.label}</option>`).join('');
    const reworkOpts = REWORK_CATEGORIES.map(c => `<option value="${c.value}">${c.label}</option>`).join('');
    return `
        <div class="detail-section">
            <h4>Change Status</h4>
            <div class="detail-section-actions">
                <select id="status-transition-select" onchange="onStatusTransitionChange('${task.status}')">${options}</select>
                <input type="text" id="status-transition-reason" placeholder="Reason (required for some transitions)" style="flex:2;" />
                <button class="btn-primary btn-sm" onclick="changeTaskStatus(${task.id})">Apply</button>
            </div>
            <div id="status-outcome-wrap" class="detail-section-actions" style="display:none;margin-top:8px;">
                <select id="status-transition-outcome"><option value="">— Outcome (required) —</option>${outcomeOpts}</select>
            </div>
            <div id="status-rework-wrap" class="detail-section-actions" style="display:none;margin-top:8px;">
                <select id="status-rework-category">${reworkOpts}</select>
            </div>
        </div>`;
}

async function changeTaskStatus(taskId) {
    const status = document.getElementById('status-transition-select').value;
    const reason = document.getElementById('status-transition-reason').value.trim();
    const outcome = document.getElementById('status-transition-outcome')?.value || null;
    const reworkCategory = document.getElementById('status-rework-category')?.value || null;

    if (status === 'CANCELLED' && !outcome) {
        showError('Outcome is required when cancelling');
        return;
    }

    const body = { status, reason: reason || null };
    if (outcome) body.outcome = outcome;
    if (reworkCategory) body.reworkCategory = reworkCategory;

    try {
        await fetchApi(`/tasks/${taskId}/status`, {
            method: 'PATCH',
            etagKey: `task:${taskId}`,
            body: JSON.stringify(body)
        });
        showError('✅ Status updated!');
        await refreshTaskDetail(taskId);
        loadTasks();
        loadMyWork();
    } catch (error) {
        showError('❌ ' + error.message);
    }
}

function closeTaskDetail() {
    document.getElementById('task-detail-modal').classList.remove('active');
    currentDetailTaskId = null;
}

// ============================================
// Edit Task
// ============================================
async function showEditTask(taskId) {
    closeTaskDetail();
    hideCreateTask();
    try {
        const task = await fetchApi(`/tasks/${taskId}`);
        if (!task) return;

        document.getElementById('edit-task-id').value = task.id;
        document.getElementById('edit-task-title').value = task.title;
        document.getElementById('edit-task-description').value = task.description || '';
        document.getElementById('edit-task-priority').value = task.priority;
        document.getElementById('edit-task-due').value = toDatetimeLocalValue(task.dueAt);
        document.getElementById('edit-task-estimate').value = task.estimateMinutes || '';
        document.getElementById('edit-task-archived').value = task.isArchived;

        await loadEditTaskDropdowns(task.departmentId, task.assigneeId);

        document.getElementById('edit-task-error').style.display = 'none';
        document.getElementById('edit-task-modal').classList.add('active');
        setTimeout(() => document.getElementById('edit-task-title').focus(), 50);
    } catch (error) {
        showError('Failed to load task for editing');
    }
}

function hideEditTask() {
    document.getElementById('edit-task-modal').classList.remove('active');
    document.getElementById('edit-task-error').style.display = 'none';
}

async function loadEditTaskDropdowns(selectedDepartmentId, selectedAssigneeId) {
    try {
        const departments = await fetchApi('/departments');
        const deptSelect = document.getElementById('edit-task-department');
        if (departments && departments.length > 0) {
            deptSelect.innerHTML = departments.map(dept =>
                `<option value="${dept.name}" ${dept.id === selectedDepartmentId ? 'selected' : ''}>${dept.name}</option>`
            ).join('');
        }

        const employeesData = await fetchApi('/employees', {}, { limit: 100 });
        const empSelect = document.getElementById('edit-task-assignee');
        if (employeesData && employeesData.data) {
            empSelect.innerHTML = `<option value="">${typeof t === 'function' ? t('common.unassigned') : 'Unassigned'}</option>` +
                employeesData.data
                    .filter(emp => emp.isActive)
                    .map(emp =>
                        `<option value="${emp.id}" ${emp.id === selectedAssigneeId ? 'selected' : ''}>${emp.fullName} (${emp.departmentName})</option>`
                    ).join('');
        }
    } catch (error) {
        console.error('Failed to load edit dropdowns:', error);
    }
}

async function updateTask(event) {
    event.preventDefault();

    const id = parseInt(document.getElementById('edit-task-id').value);
    const title = document.getElementById('edit-task-title').value.trim();
    const description = document.getElementById('edit-task-description').value.trim();
    const departmentName = document.getElementById('edit-task-department').value;
    const assigneeId = document.getElementById('edit-task-assignee').value;
    const priority = document.getElementById('edit-task-priority').value;
    const dueAt = document.getElementById('edit-task-due').value;
    const estimateMinutes = document.getElementById('edit-task-estimate').value;

    if (!title) {
        document.getElementById('edit-task-error').textContent = 'Title is required';
        document.getElementById('edit-task-error').style.display = 'block';
        return;
    }

    try {
        const updated = await fetchApi(`/tasks/${id}`, {
            method: 'PUT',
            etagKey: `task:${id}`,
            body: JSON.stringify({
                title,
                description: description || null,
                departmentName,
                assigneeId: assigneeId ? parseInt(assigneeId) : null,
                priority,
                dueAt: dueAt ? new Date(dueAt).toISOString() : null,
                estimateMinutes: estimateMinutes ? parseInt(estimateMinutes) : null,
                isArchived: document.getElementById('edit-task-archived').value === 'true'
            })
        });
        hideEditTask();
        loadTasks();
        showError('✅ Task updated successfully!');
        showTaskWarnings(updated);
    } catch (error) {
        document.getElementById('edit-task-error').textContent = error.message;
        document.getElementById('edit-task-error').style.display = 'block';
    }
}
// ============================================
// Task detail extensions
// ============================================
async function loadTaskDetailExtensions(taskId, task) {
    const container = document.getElementById('task-detail-extensions');
    if (!container) return;

    container.innerHTML = '<p class="empty-state">Loading details…</p>';

    const [comments, checklist, activity, timeEntries, intervals, criteria, rework, slaState] = await Promise.all([
        fetchApi(`/tasks/${taskId}/comments`, {}, {}, true).catch(() => []),
        fetchApi(`/tasks/${taskId}/checklist`, {}, {}, true).catch(() => []),
        fetchApi(`/tasks/${taskId}/activity`, {}, {}, true).catch(() => []),
        fetchApi(`/tasks/${taskId}/time-entries`, {}, {}, true).catch(() => []),
        fetchApi(`/tasks/${taskId}/status-intervals`, {}, {}, true).catch(() => []),
        fetchApi(`/tasks/${taskId}/acceptance-criteria`, {}, {}, true).catch(() => []),
        fetchApi(`/rework/tasks/${taskId}`, {}, {}, true).catch(() => []),
        fetchApi(`/sla/task/${taskId}`, {}, {}, true).catch(() => null)
    ]);

    const blockedBy = (task.blockedBy || []).map(b =>
        `<li>${b.key}: ${b.title} <span class="status-badge status-${(b.status || '').toLowerCase()}">${b.status}</span>
         <button class="btn-danger btn-sm" onclick="removeDependency(${taskId},${b.id})">Remove</button></li>`
    ).join('') || '<li class="muted">None</li>';

    const commentsHtml = (comments || []).map(c =>
        `<div class="feed-item"><strong>${c.authorName || 'User'}</strong> <span class="muted">${new Date(c.createdAt).toLocaleString()}</span><p>${escapeHtml(c.body)}</p></div>`
    ).join('') || '<p class="empty-state">No comments yet.</p>';

    const checklistHtml = (checklist || []).map(item =>
        `<label class="checklist-row"><input type="checkbox" ${item.isDone ? 'checked' : ''} onchange="toggleChecklistItem(${item.id}, this.checked)" />
         <span>${escapeHtml(item.label)}</span>
         <button class="btn-danger btn-sm" onclick="deleteChecklistItem(${item.id},${taskId})">×</button></label>`
    ).join('') || '<p class="empty-state">No checklist items.</p>';

    const activityHtml = (activity || []).slice(0, 15).map(a =>
        `<div class="feed-item"><strong>${a.actorName}</strong> ${a.action} ${a.field ? a.field + ': ' : ''}${a.oldValue || ''} → ${a.newValue || ''}
         <span class="muted">${new Date(a.createdAt).toLocaleString()}</span></div>`
    ).join('') || '<p class="empty-state">No activity.</p>';

    const timeHtml = (timeEntries || []).map(t =>
        `<div class="feed-item">${t.minutes} min — ${escapeHtml(t.note || '')} <span class="muted">${new Date(t.workedOn || t.createdAt).toLocaleDateString()}</span>
         <button class="btn-danger btn-sm" onclick="deleteTimeEntry(${t.id},${taskId})">Delete</button></div>`
    ).join('') || '<p class="empty-state">No time logged.</p>';

    const intervalHtml = (intervals || []).map(i =>
        `<tr><td>${i.status}</td><td>${new Date(i.enteredAt).toLocaleString()}</td><td>${i.exitedAt ? new Date(i.exitedAt).toLocaleString() : 'Open'}</td>
         <td>${formatDuration(i.durationSeconds)}</td><td>${formatDuration(i.workingHoursDurationSeconds)}</td><td>${i.actorName || '—'}</td></tr>`
    ).join('') || '<tr><td colspan="6" class="empty-state">No intervals recorded.</td></tr>';

    const criteriaHtml = (criteria || []).map(c =>
        `<label class="checklist-row"><input type="checkbox" ${c.isMet ? 'checked' : ''} onchange="toggleCriterion(${taskId},${c.id}, this.checked, '${escapeHtml(c.text).replace(/'/g, "\\'")}')" />
         <span>${escapeHtml(c.text)}</span>
         <button class="btn-danger btn-sm" onclick="deleteCriterion(${taskId},${c.id})">×</button></label>`
    ).join('') || '<p class="empty-state">No acceptance criteria.</p>';

    const reworkHtml = (rework || []).map(r =>
        `<div class="feed-item"><strong>${r.isReopen ? 'Reopen' : 'Rework'}</strong> — ${r.category}
         <span class="muted">${new Date(r.occurredAt).toLocaleString()}</span>${r.note ? `<p>${escapeHtml(r.note)}</p>` : ''}</div>`
    ).join('') || '<p class="empty-state">No rework history.</p>';

    const watchBtn = task.isWatching
        ? `<button class="btn-secondary btn-sm" onclick="toggleWatch(${taskId}, false)">Unwatch</button>`
        : `<button class="btn-secondary btn-sm" onclick="toggleWatch(${taskId}, true)">Watch</button>`;

    const approvalCreate = isAdmin() ? `
        <div class="detail-section-actions" style="margin-top:8px;">
            <input type="text" id="new-approval-name" placeholder="Workflow name" />
            <select id="new-approval-approver"><option value="">Select approver</option></select>
            <button class="btn-primary btn-sm" onclick="createTaskApproval(${taskId})">Start Approval</button>
        </div>` : '';

    container.innerHTML = `
        <div class="detail-section"><h4>SLA Details</h4><p>${renderSlaDetail(task)}</p>
        ${slaState?.escalations?.length ? `<p>Escalations: ${slaState.escalations.length}</p>` : ''}</div>
        <div class="detail-section"><h4>Acceptance Criteria (${task.acceptanceCriteria?.met || 0}/${task.acceptanceCriteria?.total || 0})</h4>
            ${criteriaHtml}
            <div class="detail-section-actions"><input type="text" id="new-criterion-text" placeholder="New criterion" style="flex:2;" />
            <button class="btn-primary btn-sm" onclick="addCriterion(${taskId})">Add</button></div></div>
        <div class="detail-section"><h4>Checklist</h4>${checklistHtml}
            <div class="detail-section-actions"><input type="text" id="new-checklist-label" placeholder="New item" style="flex:2;" />
            <button class="btn-primary btn-sm" onclick="addChecklistItem(${taskId})">Add</button></div></div>
        <div class="detail-section"><h4>Comments</h4>${commentsHtml}
            <div class="detail-section-actions"><input type="text" id="new-comment-body" placeholder="Add a comment…" style="flex:2;" />
            <button class="btn-primary btn-sm" onclick="addComment(${taskId})">Post</button></div></div>
        <div class="detail-section"><h4>Time Entries (${task.loggedMinutes || 0} min logged)</h4>${timeHtml}
            <div class="detail-section-actions"><input type="number" id="new-time-minutes" placeholder="Minutes" min="1" style="width:100px;" />
            <input type="date" id="new-time-worked-on" value="${new Date().toISOString().slice(0, 10)}" title="Worked on" style="width:150px;" />
            <input type="text" id="new-time-note" placeholder="Note" style="flex:1;" />
            <button class="btn-primary btn-sm" onclick="addTimeEntry(${taskId})">Log</button></div></div>
        <div class="detail-section"><h4>Dependencies (blocked by)</h4><ul class="dep-list">${blockedBy}</ul>
            <div class="detail-section-actions"><input type="text" id="new-blocker-key" placeholder="Blocking task key (e.g. ENG-1)" style="flex:2;" />
            <button class="btn-primary btn-sm" onclick="addDependency(${taskId})">Add</button></div></div>
        <div class="detail-section"><h4>Watchers (${task.watcherCount || 0})</h4>${watchBtn}</div>
        <div class="detail-section"><h4>Status Timeline</h4>
            <table class="mini-table"><thead><tr><th>Status</th><th>Entered</th><th>Exited</th><th>Duration</th><th>Working</th><th>Actor</th></tr></thead>
            <tbody>${intervalHtml}</tbody></table></div>
        <div class="detail-section"><h4>Rework History</h4>${reworkHtml}</div>
        <div class="detail-section"><h4>Activity</h4>${activityHtml}</div>
        ${approvalCreate}`;

    populateApproverDropdown();
}

async function addComment(taskId) {
    const body = document.getElementById('new-comment-body')?.value.trim();
    if (!body) return;
    await fetchApi(`/tasks/${taskId}/comments`, { method: 'POST', body: JSON.stringify({ body }) });
    await refreshTaskDetail(taskId);
}

async function addChecklistItem(taskId) {
    const label = document.getElementById('new-checklist-label')?.value.trim();
    if (!label) return;
    await fetchApi(`/tasks/${taskId}/checklist`, { method: 'POST', body: JSON.stringify({ label }) });
    await refreshTaskDetail(taskId);
}

async function toggleChecklistItem(itemId, isDone) {
    await fetchApi(`/checklist-items/${itemId}`, { method: 'PATCH', body: JSON.stringify({ isDone }) });
    if (currentDetailTaskId) await refreshTaskDetail(currentDetailTaskId);
}

async function deleteChecklistItem(itemId, taskId) {
    await fetchApi(`/checklist-items/${itemId}`, { method: 'DELETE' });
    await refreshTaskDetail(taskId);
}

async function addTimeEntry(taskId) {
    const minutes = parseInt(document.getElementById('new-time-minutes')?.value, 10);
    const workedOn = document.getElementById('new-time-worked-on')?.value;
    const note = document.getElementById('new-time-note')?.value.trim() || null;
    if (!minutes || minutes < 1) { showError('Enter valid minutes'); return; }
    if (!workedOn) { showError('Pick the date you worked on'); return; }
    await fetchApi(`/tasks/${taskId}/time-entries`, {
        method: 'POST',
        body: JSON.stringify({ minutes, workedOn, note })
    });
    await refreshTaskDetail(taskId);
}

async function deleteTimeEntry(entryId, taskId) {
    await fetchApi(`/time-entries/${entryId}`, { method: 'DELETE' });
    await refreshTaskDetail(taskId);
}

async function addCriterion(taskId) {
    const text = document.getElementById('new-criterion-text')?.value.trim();
    if (!text) return;
    await fetchApi(`/tasks/${taskId}/acceptance-criteria`, { method: 'POST', body: JSON.stringify({ text }) });
    await refreshTaskDetail(taskId);
}

async function toggleCriterion(taskId, criterionId, isMet, text) {
    await fetchApi(`/tasks/${taskId}/acceptance-criteria/${criterionId}`, {
        method: 'PATCH', body: JSON.stringify({ text, isMet })
    });
    await refreshTaskDetail(taskId);
}

async function deleteCriterion(taskId, criterionId) {
    await fetchApi(`/tasks/${taskId}/acceptance-criteria/${criterionId}`, { method: 'DELETE' });
    await refreshTaskDetail(taskId);
}

async function addDependency(taskId) {
    const key = document.getElementById('new-blocker-key')?.value.trim();
    if (!key) return;
    const blocker = await fetchApi(`/tasks/key/${encodeURIComponent(key)}`, {}, {}, true);
    if (!blocker) { showError('Task not found'); return; }
    await fetchApi(`/tasks/${taskId}/dependencies`, { method: 'POST', body: JSON.stringify({ blockingTaskId: blocker.id }) });
    await refreshTaskDetail(taskId);
}

async function removeDependency(taskId, blockingTaskId) {
    await fetchApi(`/tasks/${taskId}/dependencies/${blockingTaskId}`, { method: 'DELETE' });
    await refreshTaskDetail(taskId);
}

async function toggleWatch(taskId, watch) {
    if (watch) {
        await fetchApi(`/tasks/${taskId}/watchers`, { method: 'POST', body: '{}' });
    } else {
        const username = localStorage.getItem('username');
        const emps = await fetchApi('/employees', {}, { limit: 200, page: 1, search: username }, true);
        const list = emps?.data || emps || [];
        const me = list.find(e => e.fullName === username || e.email === username);
        if (!me) { showError('Could not resolve your employee record to unwatch'); return; }
        await fetchApi(`/tasks/${taskId}/watchers/${me.id}`, { method: 'DELETE' });
    }
    await refreshTaskDetail(taskId);
}
