// ============================================
// Timesheets: weekly entry, submission, and manager review.
// ============================================

function mondayOf(dateStr) {
    const d = dateStr ? new Date(dateStr + 'T00:00:00') : new Date();
    const day = d.getDay();
    const diff = (day + 6) % 7;
    d.setDate(d.getDate() - diff);
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const dayNum = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${dayNum}`;
}

// Rejects DateTime.MinValue, which the API can emit for sheets with no real
// week set and which otherwise renders as a confusing "0001-01-01".
function parseIsoDate(value) {
    if (!value) return null;
    const d = new Date(String(value).slice(0, 10) + 'T00:00:00');
    return isNaN(d.getTime()) || d.getFullYear() <= 1 ? null : d;
}

function formatDate(value) {
    const d = parseIsoDate(value);
    return d ? d.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' }) : '—';
}

function formatWeekRange(start, end) {
    const s = parseIsoDate(start);
    const e = parseIsoDate(end);
    if (!s) return '—';
    if (!e) return formatDate(start);
    return `${s.toLocaleDateString(undefined, { day: 'numeric', month: 'short' })} – ${formatDate(end)}`;
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

async function prepareTimesheets() {
    document.getElementById('timesheet-week').value = mondayOf();
    await Promise.all([loadTimesheetWeek(), loadMyTimesheets(), loadPendingTimesheets()]);
}

async function loadTimesheetWeek() {
    const weekStart = mondayOf(document.getElementById('timesheet-week').value);
    document.getElementById('timesheet-week').value = weekStart;
    const sheet = await fetchApi('/timesheets/week', {}, { weekStart });
    const entries = sheet?.entries || [];

    document.getElementById('timesheet-week-summary').innerHTML = `
        <div class="week-summary">
            <div class="week-summary-item">
                <span class="week-summary-label">Status</span>
                ${timesheetStateBadge(sheet?.state)}
            </div>
            <div class="week-summary-item">
                <span class="week-summary-label">Week</span>
                <span class="week-summary-value">${formatWeekRange(sheet?.weekStart || weekStart, sheet?.weekEnd)}</span>
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
        </tr>`).join('') : '<tr><td colspan="5" class="empty-cell">No entries this week.</td></tr>'}</tbody></table>`;
}

async function loadMyTimesheets() {
    const rows = await fetchApi('/timesheets/mine') || [];
    document.getElementById('timesheet-mine').innerHTML = rows.length
        ? `<table><thead><tr><th>Week</th><th>State</th><th>Total logged</th></tr></thead>
           <tbody>${rows.map(s => `<tr>
             <td>${formatDate(s.weekStart)}</td>
             <td>${timesheetStateBadge(s.state)}</td>
             <td>${formatMinutes(s.totalMinutes)}</td>
           </tr>`).join('')}</tbody></table>`
        : '<p class="empty-state">No timesheets yet.</p>';
}

async function loadPendingTimesheets() {
    const rows = await fetchApi('/timesheets/pending') || [];
    document.getElementById('timesheet-pending').innerHTML = rows.length
        ? `<table><thead><tr><th>Employee</th><th>Week</th><th>State</th><th class="actions-col"></th></tr></thead>
           <tbody>${rows.map(s => `<tr>
             <td>${escapeHtml(s.employeeName || ('#' + s.employeeId))}</td>
             <td>${formatDate(s.weekStart)}</td>
             <td>${timesheetStateBadge(s.state)}</td>
             <td class="actions-col">
               <button class="btn-success btn-sm" onclick="reviewTimesheet(${s.id}, true)">Approve</button>
               <button class="btn-secondary btn-sm" onclick="reviewTimesheet(${s.id}, false)">Return</button>
             </td>
           </tr>`).join('')}</tbody></table>`
        : '<p class="empty-state">Nothing pending review.</p>';
}

async function submitTimesheetWeek() {
    const weekStart = mondayOf(document.getElementById('timesheet-week').value);
    await fetchApi('/timesheets/submit', {
        method: 'POST',
        body: JSON.stringify({ weekStart })
    });
    showError('Timesheet submitted.');
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
