// ============================================
// Scheduled tasks (templates) and recurrence rules, including occurrence generation.
// ============================================

let templatesCache = [];
let recurrenceRulesCache = [];

function toggleCreateTemplateForm() {
    const form = document.getElementById('create-template-form');
    const show = form.style.display === 'none';
    form.style.display = show ? 'block' : 'none';
    if (show) {
        resetTemplateForm();
        loadTemplateDropdowns();
        document.getElementById('template-error').style.display = 'none';
    }
}

function resetTemplateForm() {
    document.getElementById('template-edit-id').value = '';
    document.getElementById('template-form-title').textContent = 'Create Scheduled Task';
    document.getElementById('template-name').value = '';
    document.getElementById('template-description').value = '';
    document.getElementById('template-title-pattern').value = '';
    document.getElementById('template-default-description').value = '';
    document.getElementById('template-priority').value = 'NORMAL';
    document.getElementById('template-assignee').value = '';
    document.getElementById('template-estimate').value = '';
    document.getElementById('template-tags').value = '';
    document.getElementById('template-checklist').value = '';
    document.getElementById('template-acceptance').value = '';
    document.getElementById('template-frequency').value = '';
    document.getElementById('template-days-of-week').value = '';
    document.getElementById('template-day-of-month').value = '';
    document.getElementById('template-rule-start').value = '';
    document.getElementById('template-rule-end').value = '';
    toggleRecurrenceFields();
}

function toggleRecurrenceFields() {
    const freq = document.getElementById('template-frequency')?.value;
    const daysGroup = document.getElementById('template-days-group');
    const dayMonthGroup = document.getElementById('template-day-month-group');
    if (!daysGroup || !dayMonthGroup) return;
    daysGroup.style.display = freq === 'WEEKLY' ? 'block' : 'none';
    dayMonthGroup.style.display = freq === 'MONTHLY' ? 'block' : 'none';
}

async function loadTemplateDropdowns() {
    try {
        const [departments, employees] = await Promise.all([
            fetchApi('/departments'),
            fetchApi('/employees', {}, { limit: 200 })
        ]);

        const deptSelect = document.getElementById('template-department');
        deptSelect.innerHTML = (departments || []).map(d =>
            `<option value="${d.id}">${d.name}</option>`
        ).join('');

        const empSelect = document.getElementById('template-assignee');
        empSelect.innerHTML = '<option value="">None</option>' +
            (employees?.data || []).filter(e => e.isActive).map(emp =>
                `<option value="${emp.id}">${emp.fullName}</option>`
            ).join('');
    } catch (error) {
        console.error('Failed to load template dropdowns:', error);
    }
}

async function loadTemplates() {
    const listContainer = document.getElementById('template-list');
    const rulesContainer = document.getElementById('recurrence-rules-list');
    const occContainer = document.getElementById('occurrences-list');

    listContainer.innerHTML = '<p class="empty-state">Loading…</p>';
    rulesContainer.innerHTML = '<p class="empty-state">Loading…</p>';
    occContainer.innerHTML = '<p class="empty-state">Loading…</p>';

    try {
        const [templates, rules, pendingOcc] = await Promise.all([
            fetchApi('/recurrence/templates'),
            fetchApi('/recurrence/rules'),
            isAdmin() ? fetchApi('/recurrence/occurrences/pending') : Promise.resolve([])
        ]);

        templatesCache = templates || [];
        recurrenceRulesCache = rules || [];
        renderTemplatesTable(listContainer, templatesCache);
        renderRecurrenceRulesTable(rulesContainer, recurrenceRulesCache);
        renderOccurrencesTable(occContainer, pendingOcc || []);
    } catch (error) {
        console.error('Failed to load templates:', error);
        listContainer.innerHTML = '<p class="empty-state">Could not load scheduled tasks.</p>';
    }
}

function renderTemplatesTable(container, templates) {
    if (!templates.length) {
        container.innerHTML = '<p class="empty-state">No scheduled tasks yet. Create one above.</p>';
        return;
    }

    let html = `
        <table>
            <thead>
                <tr>
                    <th>Name</th>
                    <th>Description</th>
                    <th>Priority</th>
                    <th>Department</th>
                    <th>Assignee</th>
                    <th>Active</th>
                    <th>Actions</th>
                </tr>
            </thead>
            <tbody>
    `;

    templates.forEach(t => {
        html += `
            <tr>
                <td><strong>${t.name}</strong></td>
                <td>${t.description || '—'}</td>
                <td><span class="priority-badge priority-${(t.defaultPriority || 'normal').toLowerCase()}">${t.defaultPriority}</span></td>
                <td>${t.departmentName}</td>
                <td>${t.defaultAssigneeName || 'None'}</td>
                <td>${t.isActive ? '🟢' : '🔴'}</td>
                <td class="template-actions">
                    <button class="btn-secondary btn-sm" onclick="editTemplate(${t.id})">✏️ Edit</button>
                    ${isAdmin() ? `<button class="btn-danger btn-sm" onclick="deleteTemplate(${t.id}, '${t.name.replace(/'/g, "\\'")}')">🗑️</button>` : ''}
                </td>
            </tr>
        `;
    });

    html += '</tbody></table>';
    container.innerHTML = html;
}

function renderRecurrenceRulesTable(container, rules) {
    if (!rules.length) {
        container.innerHTML = '<p class="empty-state">No recurrence rules configured.</p>';
        return;
    }

    let html = `
        <table>
            <thead>
                <tr>
                    <th>Scheduled Task</th>
                    <th>Frequency</th>
                    <th>Schedule</th>
                    <th>Start</th>
                    <th>End</th>
                    <th>Paused</th>
                    <th>Last Generated</th>
                    <th>Actions</th>
                </tr>
            </thead>
            <tbody>
    `;

    rules.forEach(r => {
        const schedule = r.frequency === 'WEEKLY' ? `Days: ${r.daysOfWeek}`
            : r.frequency === 'MONTHLY' ? `Day ${r.dayOfMonth}`
            : 'Daily';
        html += `
            <tr class="${r.isPaused ? 'rule-paused' : ''}">
                <td>${r.templateName}</td>
                <td>${r.frequency}</td>
                <td>${schedule}</td>
                <td>${new Date(r.startDate).toLocaleDateString()}</td>
                <td>${r.endDate ? new Date(r.endDate).toLocaleDateString() : '—'}</td>
                <td>${r.isPaused ? '⏸️ Yes' : '▶️ No'}</td>
                <td>${r.lastGeneratedAt ? new Date(r.lastGeneratedAt).toLocaleString() : 'Never'}</td>
                <td class="template-actions">
                    <button class="btn-secondary btn-sm" onclick="loadOccurrences(${r.id})">Occurrences</button>
                    ${r.isPaused
                        ? `<button class="btn-secondary btn-sm" onclick="resumeRecurrenceRule(${r.id})">Resume</button>`
                        : `<button class="btn-secondary btn-sm" onclick="pauseRecurrenceRule(${r.id})">Pause</button>`}
                    ${isAdmin() ? `<button class="btn-danger btn-sm" onclick="deleteRecurrenceRule(${r.id})">🗑️</button>` : ''}
                </td>
            </tr>
        `;
    });

    html += '</tbody></table>';
    container.innerHTML = html;
}

function renderOccurrencesTable(container, occurrences) {
    if (!occurrences.length) {
        container.innerHTML = '<p class="empty-state">No occurrences to show. Click "Occurrences" on a rule or run Generate Now.</p>';
        return;
    }

    let html = `
        <table>
            <thead>
                <tr>
                    <th>Rule</th>
                    <th>Task</th>
                    <th>Occurrence Date</th>
                    <th>State</th>
                    <th>Error</th>
                </tr>
            </thead>
            <tbody>
    `;

    occurrences.forEach(o => {
        html += `
            <tr>
                <td>#${o.ruleId}</td>
                <td>${o.taskKey ? `<a href="#" onclick="showTaskDetail(${o.taskId});return false;">${o.taskKey}</a>` : '—'}</td>
                <td>${new Date(o.occurrenceDate).toLocaleString()}</td>
                <td>${o.state}</td>
                <td>${o.error || '—'}</td>
            </tr>
        `;
    });

    html += '</tbody></table>';
    container.innerHTML = html;
}

async function saveTemplate(event) {
    event.preventDefault();

    const editId = document.getElementById('template-edit-id').value;
    const tags = document.getElementById('template-tags').value.split(',').map(t => t.trim()).filter(Boolean);
    const checklist = document.getElementById('template-checklist').value.split(',').map(t => t.trim()).filter(Boolean);
    const acceptance = document.getElementById('template-acceptance')?.value.split(',').map(t => t.trim()).filter(Boolean) || [];
    const assigneeVal = document.getElementById('template-assignee').value;

    const body = {
        name: document.getElementById('template-name').value.trim(),
        description: document.getElementById('template-description').value.trim() || null,
        titlePattern: document.getElementById('template-title-pattern').value.trim(),
        defaultDescription: document.getElementById('template-default-description').value.trim() || null,
        defaultPriority: document.getElementById('template-priority').value,
        defaultAssigneeId: assigneeVal ? parseInt(assigneeVal, 10) : null,
        defaultEstimateMinutes: document.getElementById('template-estimate').value
            ? parseInt(document.getElementById('template-estimate').value, 10) : null,
        defaultTags: tags.length ? tags : null,
        defaultChecklistItems: checklist.length ? checklist : null,
        defaultAcceptanceCriteria: acceptance.length ? acceptance : null,
        departmentId: parseInt(document.getElementById('template-department').value, 10)
    };

    try {
        let template;
        if (editId) {
            template = await fetchApi(`/recurrence/templates/${editId}`, {
                method: 'PUT',
                body: JSON.stringify({ ...body, isActive: true })
            });
        } else {
            template = await fetchApi('/recurrence/templates', {
                method: 'POST',
                body: JSON.stringify(body)
            });
            const frequency = document.getElementById('template-frequency').value;
            if (frequency) {
                await createRecurrenceRule(template.id);
            }
        }

        toggleCreateTemplateForm();
        loadTemplates();
        showError(`✅ Scheduled task ${editId ? 'updated' : 'created'}!`);
    } catch (error) {
        document.getElementById('template-error').textContent = error.message;
        document.getElementById('template-error').style.display = 'block';
    }
}

async function createRecurrenceRule(templateId) {
    const frequency = document.getElementById('template-frequency').value;
    if (!frequency) return;

    const startVal = document.getElementById('template-rule-start').value;
    const endVal = document.getElementById('template-rule-end').value;

    // date inputs yield "YYYY-MM-DD"; older datetime-local yields "YYYY-MM-DDTHH:mm".
    // Always send UTC midnight calendar dates so ASP.NET can bind StartDate/EndDate.
    const toUtcDate = (val) => {
        if (!val) return null;
        const day = val.includes('T') ? val.slice(0, 10) : val;
        return `${day}T00:00:00.000Z`;
    };

    const body = {
        templateId,
        frequency,
        daysOfWeek: frequency === 'WEEKLY' ? document.getElementById('template-days-of-week').value : null,
        dayOfMonth: frequency === 'MONTHLY' ? String(document.getElementById('template-day-of-month').value) : null,
        startDate: toUtcDate(startVal) || `${new Date().toISOString().slice(0, 10)}T00:00:00.000Z`,
        endDate: toUtcDate(endVal),
        timezone: 'UTC',
        generateOnlyWhenPreviousComplete: false,
        isPaused: false,
        nonWorkingDayPolicy: document.getElementById('template-nwd-policy')?.value || 'NEXT_WORKING_DAY'
    };

    await fetchApi('/recurrence/rules', {
        method: 'POST',
        body: JSON.stringify(body)
    });
}

async function editTemplate(id) {
    try {
        const template = await fetchApi(`/recurrence/templates/${id}`);
        if (!template) return;

        toggleCreateTemplateForm();
        await loadTemplateDropdowns();

        document.getElementById('template-edit-id').value = template.id;
        document.getElementById('template-form-title').textContent = 'Edit Scheduled Task';
        document.getElementById('template-name').value = template.name;
        document.getElementById('template-description').value = template.description || '';
        document.getElementById('template-title-pattern').value = template.titlePattern;
        document.getElementById('template-default-description').value = template.defaultDescription || '';
        document.getElementById('template-priority').value = template.defaultPriority || 'NORMAL';
        document.getElementById('template-department').value = template.departmentId;
        document.getElementById('template-assignee').value = template.defaultAssigneeId || '';
        document.getElementById('template-estimate').value = template.defaultEstimateMinutes || '';
        document.getElementById('template-tags').value = (template.defaultTags || []).join(', ');
        document.getElementById('template-checklist').value = (template.defaultChecklistItems || []).join(', ');
        document.getElementById('template-acceptance').value = (template.defaultAcceptanceCriteria || []).join(', ');
    } catch (error) {
        showError('Failed to load scheduled task for editing');
    }
}

async function deleteTemplate(id, name) {
    if (!confirm(`Delete scheduled task "${name}"?`)) return;
    try {
        await fetchApi(`/recurrence/templates/${id}`, { method: 'DELETE' });
        loadTemplates();
        showError('✅ Scheduled task deleted.');
    } catch (error) {
        showError('❌ ' + error.message);
    }
}

async function deleteRecurrenceRule(id) {
    if (!confirm('Delete this recurrence rule?')) return;
    try {
        await fetchApi(`/recurrence/rules/${id}`, { method: 'DELETE' });
        loadTemplates();
        showError('✅ Recurrence rule deleted.');
    } catch (error) {
        showError('❌ ' + error.message);
    }
}

async function generateOccurrences() {
    if (!isAdmin()) {
        showRestricted();
        return;
    }
    try {
        const result = await fetchApi('/recurrence/generate', { method: 'POST', body: '{}' });
        showError(`✅ Created ${result.generated} task(s) for the scheduled task assignee. ${result.message || ''}`);
        loadTemplates();
    } catch (error) {
        showError('❌ ' + error.message);
    }
}

async function loadOccurrences(ruleId) {
    try {
        const occurrences = await fetchApi(`/recurrence/rules/${ruleId}/occurrences`);
        renderOccurrencesTable(document.getElementById('occurrences-list'), occurrences || []);
    } catch (error) {
        showError('Failed to load occurrences');
    }
}

async function materializeOccurrences() {
    if (!isAdmin()) {
        showRestricted();
        return;
    }
    const res = await fetchApi('/recurrence/materialize', { method: 'POST', body: '{}' });
    showError(`✅ Materialized ${res.created} task(s).`);
    loadTemplates();
}

async function pauseRecurrenceRule(id) {
    await fetchApi(`/recurrence/rules/${id}/pause`, { method: 'POST', body: '{}' });
    loadTemplates();
}

async function resumeRecurrenceRule(id) {
    await fetchApi(`/recurrence/rules/${id}/resume`, { method: 'POST', body: '{}' });
    loadTemplates();
}
