// ============================================
// Timesheets: monthly entry, submission, and manager review.
// ============================================

function monthStartOf(dateStr) {
    const d = dateStr ? new Date(dateStr + 'T00:00:00') : new Date();
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    return `${y}-${m}-01`;
}

function monthEndOf(dateStr) {
    const start = monthStartOf(dateStr);
    const d = new Date(start + 'T00:00:00');
    d.setMonth(d.getMonth() + 1);
    d.setDate(0);
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const dayNum = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${dayNum}`;
}

// Rejects DateTime.MinValue, which the API can emit for sheets with no real
// period set and which otherwise renders as a confusing "0001-01-01".
function parseIsoDate(value) {
    if (!value) return null;
    const d = new Date(String(value).slice(0, 10) + 'T00:00:00');
    return isNaN(d.getTime()) || d.getFullYear() <= 1 ? null : d;
}

function formatDate(value) {
    const d = parseIsoDate(value);
    return d ? d.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' }) : '—';
}

function formatMonthLabel(start, end) {
    const s = parseIsoDate(start);
    if (!s) return '—';
    const e = parseIsoDate(end);
    if (!e) {
        return s.toLocaleDateString(undefined, { month: 'long', year: 'numeric' });
    }
    return `${s.toLocaleDateString(undefined, { day: 'numeric', month: 'short' })} – ${formatDate(end)}`;
}

function formatFileSize(bytes) {
    const size = Number(bytes) || 0;
    if (size < 1024) return `${size} B`;
    if (size < 1024 * 1024) return `${(size / 1024).toFixed(1)} KB`;
    return `${(size / (1024 * 1024)).toFixed(1)} MB`;
}

function formatMinutes(total) {
    const mins = Math.max(0, Number(total) || 0);
    const h = Math.floor(mins / 60);
    const m = mins % 60;
    return h ? `${h}h ${String(m).padStart(2, '0')}m` : `${m}m`;
}

function timesheetStateBadge(state) {
    const s = String(state || '').toUpperCase();
    const classes = {
        OPEN: 'state-open',
        SUBMITTED: 'state-submitted',
        APPROVED: 'state-approved',
        RETURNED: 'state-returned'
    };
    return `<span class="state-badge ${classes[s] || 'state-open'}">${escapeHtml(s || 'UNKNOWN')}</span>`;
}

function selectedMonthStart() {
    const input = document.getElementById('timesheet-week');
    const monthStart = monthStartOf(input.value);
    input.value = monthStart;
    input.max = monthEndOf();
    return monthStart;
}

async function prepareTimesheets() {
    const input = document.getElementById('timesheet-week');
    if (!input.value) input.value = monthStartOf();
    input.max = monthEndOf();
    selectedMonthStart();
    await Promise.all([loadTimesheetWeek(), loadMyTimesheets(), loadPendingTimesheets()]);
}

async function loadTimesheetWeek() {
    const monthStart = selectedMonthStart();
    const sheet = await fetchApi('/timesheets/week', {}, { weekStart: monthStart });
    const entries = sheet?.entries || [];
    const rangeStart = sheet?.monthStart || sheet?.weekStart || monthStart;
    const rangeEnd = sheet?.monthEnd || sheet?.weekEnd || monthEndOf(monthStart);

    document.getElementById('timesheet-week-summary').innerHTML = `
        <div class="week-summary">
            <div class="week-summary-item">
                <span class="week-summary-label">Status</span>
                ${timesheetStateBadge(sheet?.state)}
            </div>
            <div class="week-summary-item">
                <span class="week-summary-label">Month</span>
                <span class="week-summary-value">${formatMonthLabel(rangeStart, rangeEnd)}</span>
            </div>
            <div class="week-summary-item">
                <span class="week-summary-label">Total logged</span>
                <span class="week-summary-value">${formatMinutes(sheet?.totalMinutes)}</span>
            </div>
            <div class="week-summary-item">
                <span class="week-summary-label">Entries</span>
                <span class="week-summary-value">${entries.length}</span>
            </div>
        </div>`;

    document.getElementById('timesheet-week-detail').innerHTML = `
        <table><thead><tr><th>Entry</th><th>Task</th><th>Duration</th><th>Worked on</th><th>Note</th></tr></thead>
        <tbody>${entries.length ? entries.map(e => `<tr>
            <td>#${e.id}</td><td>${e.taskId}</td><td>${formatMinutes(e.minutes)}</td>
            <td>${formatDate(e.workedOn)}</td><td>${escapeHtml(e.note || '—')}</td>
        </tr>`).join('') : '<tr><td colspan="5" class="empty-cell">No entries this month.</td></tr>'}</tbody></table>`;

    renderTimesheetAttachments(sheet);
}

// Attachments are frozen once the month is with the reviewer, so the file the manager
// downloads is always the one that was submitted.
function attachmentsEditable(state) {
    const s = String(state || 'OPEN').toUpperCase();
    return s === 'OPEN' || s === 'RETURNED';
}

function renderTimesheetAttachments(sheet) {
    const files = sheet?.attachments || [];
    const editable = attachmentsEditable(sheet?.state);
    document.getElementById('timesheet-attachment-upload').style.display = editable ? '' : 'none';

    document.getElementById('timesheet-attachments').innerHTML = files.length
        ? `<table><thead><tr><th>File</th><th>Size</th><th>Uploaded</th><th class="actions-col"></th></tr></thead>
           <tbody>${files.map(f => `<tr>
             <td>${escapeHtml(f.fileName)}</td>
             <td>${formatFileSize(f.sizeBytes)}</td>
             <td>${formatDate(f.uploadedAt)}</td>
             <td class="actions-col">
               <button class="btn-secondary btn-sm" onclick="downloadTimesheetAttachment(${f.id})">Download</button>
               ${editable ? `<button class="btn-danger btn-sm" onclick="deleteTimesheetAttachment(${f.id})">Remove</button>` : ''}
             </td>
           </tr>`).join('')}</tbody></table>`
        : `<p class="empty-state">${editable
            ? 'No files attached yet. Attach supporting documents before you submit the month.'
            : 'No files were attached to this month.'}</p>`;
}

async function uploadTimesheetAttachment() {
    const input = document.getElementById('timesheet-attachment-file');
    const file = input.files && input.files[0];
    if (!file) {
        showError('Choose a file to upload.');
        return;
    }

    const form = new FormData();
    form.append('weekStart', selectedMonthStart());
    form.append('file', file);

    showLoading();
    try {
        // Sent with fetch directly: FormData must set its own multipart Content-Type boundary.
        const response = await fetch(getApiUrl('/timesheets/attachments'), {
            method: 'POST',
            headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` },
            body: form
        });

        if (!response.ok) {
            const problem = await response.json().catch(() => null);
            throw new Error(problem?.error?.message || 'Upload failed.');
        }

        input.value = '';
        showError('✅ File attached.');
        await loadTimesheetWeek();
    } catch (error) {
        showError(error.message);
    } finally {
        hideLoading();
    }
}

async function downloadTimesheetAttachment(attachmentId) {
    showLoading();
    try {
        const response = await fetch(getApiUrl(`/timesheets/attachments/${attachmentId}/download`), {
            headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
        });
        if (!response.ok) throw new Error('Could not download this file.');

        const disposition = response.headers.get('Content-Disposition') || '';
        const match = disposition.match(/filename\*?=(?:UTF-8'')?"?([^\";]+)"?/i);
        const fileName = match ? decodeURIComponent(match[1]) : `attachment-${attachmentId}`;

        const blob = await response.blob();
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        link.remove();
        URL.revokeObjectURL(url);
    } catch (error) {
        showError(error.message);
    } finally {
        hideLoading();
    }
}

async function deleteTimesheetAttachment(attachmentId) {
    if (!confirm('Remove this attachment?')) return;
    await fetchApi(`/timesheets/attachments/${attachmentId}`, { method: 'DELETE' });
    showError('✅ Attachment removed.');
    await loadTimesheetWeek();
}

async function loadMyTimesheets() {
    const rows = await fetchApi('/timesheets/mine') || [];
    document.getElementById('timesheet-mine').innerHTML = rows.length
        ? `<table><thead><tr><th>Month</th><th>State</th><th>Total logged</th><th>Files</th></tr></thead>
           <tbody>${rows.map(s => `<tr>
             <td>${formatMonthLabel(s.monthStart || s.weekStart, s.monthEnd || s.weekEnd)}</td>
             <td>${timesheetStateBadge(s.state)}</td>
             <td>${formatMinutes(s.totalMinutes)}</td>
             <td>${renderPendingAttachmentLinks(s.attachments)}</td>
           </tr>`).join('')}</tbody></table>`
        : '<p class="empty-state">No timesheets yet.</p>';
}

function renderPendingAttachmentLinks(attachments) {
    const files = attachments || [];
    if (!files.length) return '<span class="empty-cell">None</span>';
    return files.map(f =>
        `<button class="btn-secondary btn-sm" onclick="downloadTimesheetAttachment(${f.id})" title="${escapeHtml(f.fileName)} · ${formatFileSize(f.sizeBytes)}">⬇ ${escapeHtml(f.fileName)}</button>`
    ).join(' ');
}

async function loadPendingTimesheets() {
    const rows = await fetchApi('/timesheets/pending') || [];
    document.getElementById('timesheet-pending').innerHTML = rows.length
        ? `<table><thead><tr><th>Employee</th><th>Month</th><th>State</th><th>Attachments</th><th class="actions-col"></th></tr></thead>
           <tbody>${rows.map(s => `<tr>
             <td>${escapeHtml(s.employeeName || ('#' + s.employeeId))}</td>
             <td>${formatMonthLabel(s.monthStart || s.weekStart, s.monthEnd || s.weekEnd)}</td>
             <td>${timesheetStateBadge(s.state)}</td>
             <td>${renderPendingAttachmentLinks(s.attachments)}</td>
             <td class="actions-col">
               <button class="btn-success btn-sm" onclick="reviewTimesheet(${s.id}, true)">Approve</button>
               <button class="btn-secondary btn-sm" onclick="reviewTimesheet(${s.id}, false)">Return</button>
             </td>
           </tr>`).join('')}</tbody></table>`
        : '<p class="empty-state">Nothing pending review.</p>';
}

async function submitTimesheetWeek() {
    const monthStart = selectedMonthStart();
    await fetchApi('/timesheets/submit', {
        method: 'POST',
        body: JSON.stringify({ weekStart: monthStart })
    });
    showSuccess('Timesheet submitted.');
    prepareTimesheets();
}

async function reviewTimesheet(id, approve) {
    const note = approve ? null : (prompt('Return note:') || 'Returned');
    await fetchApi(`/timesheets/${id}/review`, {
        method: 'POST',
        body: JSON.stringify({ approve, note })
    });
    prepareTimesheets();
}
