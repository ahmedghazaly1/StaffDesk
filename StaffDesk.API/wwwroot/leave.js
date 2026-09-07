// ============================================
// Leave requests: submit, approve or decline, and cancel.
// ============================================

function toggleLeaveRequestForm() {
    const form = document.getElementById('leave-request-form');
    form.style.display = form.style.display === 'none' ? 'block' : 'none';
}

async function loadLeave() {
    try {
        const [mine, pending] = await Promise.all([
            fetchApi('/leave'),
            fetchApi('/leave/pending')
        ]);
        renderLeaveTable(document.getElementById('leave-list'), mine || []);
        renderLeavePending(document.getElementById('leave-pending-list'), pending || []);
    } catch (e) {
        showError(e.message || 'Failed to load leave');
    }
}

function renderLeaveTable(container, rows) {
    if (!rows.length) {
        container.innerHTML = '<p class="empty-state">No leave records.</p>';
        return;
    }
    container.innerHTML = `<table class="data-table"><thead><tr>
        <th>Employee</th><th>Type</th><th>Dates</th><th>Partial</th><th>State</th><th></th>
    </tr></thead><tbody>${rows.map(l => `<tr>
        <td>${escapeHtml(l.employeeName || ('#' + l.employeeId))}</td>
        <td>${l.availabilityOnly ? '<em>hidden</em>' : escapeHtml(l.type || '—')}</td>
        <td>${l.startDate} → ${l.endDate}</td>
        <td>${l.isPartialDay ? 'Yes' : 'No'}</td>
        <td><span class="badge">${l.state}</span></td>
        <td>${l.state === 'REQUESTED' || l.state === 'APPROVED'
            ? `<button class="btn-secondary btn-sm" onclick="cancelLeave(${l.id})">Cancel</button>` : ''}</td>
    </tr>`).join('')}</tbody></table>`;
}

function renderLeavePending(container, rows) {
    if (!rows.length) {
        container.innerHTML = '<p class="empty-state">No pending leave approvals.</p>';
        return;
    }
    container.innerHTML = `<table class="data-table"><thead><tr>
        <th>Employee</th><th>Type</th><th>Dates</th><th>Note</th><th></th>
    </tr></thead><tbody>${rows.map(l => `<tr>
        <td>${escapeHtml(l.employeeName || ('#' + l.employeeId))}</td>
        <td>${l.availabilityOnly ? '<em>hidden</em>' : escapeHtml(l.type || '—')}</td>
        <td>${l.startDate} → ${l.endDate}</td>
        <td>${escapeHtml(l.note || '')}</td>
        <td>
            <button class="btn-primary btn-sm" onclick="decideLeave(${l.id}, true)">Approve</button>
            <button class="btn-secondary btn-sm" onclick="decideLeave(${l.id}, false)">Reject</button>
        </td>
    </tr>`).join('')}</tbody></table>`;
}

async function submitLeaveRequest(event) {
    event.preventDefault();
    await fetchApi('/leave', {
        method: 'POST',
        body: JSON.stringify({
            type: document.getElementById('leave-type').value,
            startDate: document.getElementById('leave-start').value,
            endDate: document.getElementById('leave-end').value,
            isPartialDay: document.getElementById('leave-partial').checked,
            note: document.getElementById('leave-note').value || null
        })
    });
    toggleLeaveRequestForm();
    showSuccess('Leave request submitted.');
    loadLeave();
}

async function decideLeave(id, approve) {
    const note = approve ? null : (prompt('Rejection note (optional):') || null);
    await fetchApi(`/leave/${id}/decide`, {
        method: 'POST',
        body: JSON.stringify({ approve, note })
    });
    loadLeave();
}

async function cancelLeave(id) {
    if (!confirm('Cancel this leave request?')) return;
    await fetchApi(`/leave/${id}/cancel`, { method: 'POST', body: '{}' });
    loadLeave();
}
