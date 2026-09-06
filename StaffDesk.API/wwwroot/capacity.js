// ============================================
// Capacity: the team workload report.
// ============================================

async function prepareCapacityForm() {
    const depts = await fetchApi('/departments') || [];
    const sel = document.getElementById('capacity-department');
    sel.innerHTML = depts.map(d => `<option value="${d.id}">${escapeHtml(d.name)}</option>`).join('');
    const today = new Date();
    const from = new Date(today); from.setDate(today.getDate() - ((today.getDay() + 6) % 7));
    const to = new Date(from); to.setDate(from.getDate() + 13);
    document.getElementById('capacity-from').value = from.toISOString().slice(0, 10);
    document.getElementById('capacity-to').value = to.toISOString().slice(0, 10);
}

async function loadCapacity(event) {
    event.preventDefault();
    const departmentId = parseInt(document.getElementById('capacity-department').value, 10);
    const from = document.getElementById('capacity-from').value;
    const to = document.getElementById('capacity-to').value;
    const overheadPercent = parseFloat(document.getElementById('capacity-overhead').value) || 20;

    const [avail, workload] = await Promise.all([
        fetchApi('/capacity/availability', {}, { departmentId, from, to }),
        fetchApi('/capacity/workload', {}, { departmentId, from, to, overheadPercent })
    ]);

    const availRows = avail?.members || [];
    document.getElementById('capacity-availability').innerHTML = availRows.length
        ? `<table class="data-table"><thead><tr><th>Employee</th><th>Available hours</th><th>Leave days</th></tr></thead>
           <tbody>${availRows.map(m => `<tr>
             <td>${escapeHtml(m.fullName)}</td>
             <td>${m.availableWorkingHours}</td>
             <td>${m.leaveDays}</td>
           </tr>`).join('')}</tbody></table>
           <p class="muted">Calendar: ${escapeHtml(avail.calendarName || '')} · as of ${avail.dataAsOf || ''}</p>`
        : '<p class="empty-state">No members.</p>';

    const loadRows = workload?.members || [];
    document.getElementById('capacity-workload').innerHTML = loadRows.length
        ? `<table class="data-table"><thead><tr>
             <th>Employee</th><th>Capacity (min)</th><th>Committed</th><th>Utilisation %</th><th>Unestimated</th>
           </tr></thead><tbody>${loadRows.map(m => `<tr>
             <td>${escapeHtml(m.fullName)}</td>
             <td>${m.capacityMinutes}</td>
             <td>${m.committedLoadMinutes}</td>
             <td>${m.utilisationPercent ?? '—'}</td>
             <td>${m.unestimatedTaskCount}</td>
           </tr>`).join('')}</tbody></table>`
        : '<p class="empty-state">No workload data.</p>';
}
