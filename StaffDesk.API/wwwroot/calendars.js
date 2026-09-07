// ============================================
// Working calendars and holidays, and assigning them to departments.
// ============================================

function toggleCalendarForm() {
    const form = document.getElementById('calendar-create-form');
    form.style.display = form.style.display === 'none' ? 'block' : 'none';
}

async function loadCalendars() {
    const [cals, depts] = await Promise.all([
        fetchApi('/calendars'),
        fetchApi('/departments')
    ]);
    const deptSel = document.getElementById('cal-dept');
    deptSel.innerHTML = (depts || []).map(d => `<option value="${d.id}">${escapeHtml(d.name)}</option>`).join('');

    const rows = cals || [];
    document.getElementById('calendars-list').innerHTML = rows.length
        ? `<table class="data-table"><thead><tr>
             <th>ID</th><th>Name</th><th>TZ</th><th>Hours</th><th>Default</th><th>Holidays</th>
           </tr></thead><tbody>${rows.map(c => `<tr>
             <td>${c.id}</td>
             <td>${escapeHtml(c.name)}</td>
             <td>${escapeHtml(c.timeZoneId)}</td>
             <td>${c.workStartHour}:00–${c.workEndHour}:00</td>
             <td>${c.isOrganizationDefault ? 'Yes' : ''}</td>
             <td>${(c.holidays || []).map(h => `${h.date} ${escapeHtml(h.name)}${h.recursAnnually ? ' (annual)' : ''}`).join('<br>') || '—'}</td>
           </tr>`).join('')}</tbody></table>`
        : '<p class="empty-state">No calendars.</p>';
}

async function createCalendar(event) {
    event.preventDefault();
    await fetchApi('/calendars', {
        method: 'POST',
        body: JSON.stringify({
            name: document.getElementById('cal-name').value,
            timeZoneId: document.getElementById('cal-tz').value,
            workStartHour: parseInt(document.getElementById('cal-start').value, 10),
            workEndHour: parseInt(document.getElementById('cal-end').value, 10),
            isOrganizationDefault: document.getElementById('cal-default').checked
        })
    });
    toggleCalendarForm();
    loadCalendars();
}

async function addCalendarHoliday(event) {
    event.preventDefault();
    const calId = document.getElementById('holiday-cal-id').value;
    await fetchApi(`/calendars/${calId}/holidays`, {
        method: 'POST',
        body: JSON.stringify({
            date: document.getElementById('holiday-date').value,
            name: document.getElementById('holiday-name').value,
            recursAnnually: document.getElementById('holiday-recur').checked
        })
    });
    loadCalendars();
}

async function setDepartmentCalendar(event) {
    event.preventDefault();
    const departmentId = document.getElementById('cal-dept').value;
    const raw = document.getElementById('cal-dept-cal-id').value;
    await fetchApi(`/calendars/departments/${departmentId}`, {
        method: 'PUT',
        body: JSON.stringify({ calendarId: raw === '' ? null : parseInt(raw, 10) })
    });
    showSuccess('Department calendar updated.');
}
