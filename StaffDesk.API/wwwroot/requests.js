// ============================================
// Task requests and the triage queue.
// ============================================

let requestTab = 'mine';

const DECLINE_REASONS = [
    { value: 'duplicate', label: 'Duplicate' },
    { value: 'out_of_scope', label: 'Out of Scope' },
    { value: 'insufficient_information', label: 'Insufficient Information' },
    { value: 'not_now', label: 'Not Now' },
    { value: 'will_not_do', label: 'Will Not Do' }
];
// ============================================
// Task requests
// ============================================
function toggleRequestForm() {
    const f = document.getElementById('create-request-form');
    if (!f) return;
    const show = f.style.display === 'none';
    f.style.display = show ? 'block' : 'none';
    if (show) loadRequestDepartments();
}


async function loadRequestDepartments() {
    await populateDepartmentSelect('request-department');
}

function switchRequestTab(tab) {
    requestTab = tab;
    document.getElementById('req-tab-mine')?.classList.toggle('active', tab === 'mine');
    document.getElementById('req-tab-triage')?.classList.toggle('active', tab === 'triage');
    document.getElementById('triage-controls').style.display = tab === 'triage' ? 'flex' : 'none';
    loadTaskRequests();
}

async function loadTaskRequests() {
    const container = document.getElementById('request-list');
    if (!container) return;
    container.innerHTML = '<p class="empty-state">Loading…</p>';
    try {
        if (requestTab === 'mine') {
            const res = await fetchApi('/task-requests', {}, { limit: 50 });
            renderRequestTable(container, res?.data || [], false);
        } else {
            await loadTriageDepartments();
            await loadTriageQueue();
        }
    } catch (error) {
        container.innerHTML = `<p class="empty-state">Could not load requests: ${error.message}</p>`;
    }
}

async function loadTriageDepartments() {
    await populateDepartmentSelect('triage-dept-select');
}

async function loadTriageQueue() {
    const container = document.getElementById('request-list');
    const deptId = document.getElementById('triage-dept-select')?.value;
    if (!deptId) { container.innerHTML = '<p class="empty-state">Select a department.</p>'; return; }
    const queue = await fetchApi('/task-requests/triage-queue', {}, { departmentId: deptId }, true) || [];
    renderRequestTable(container, queue, true);
}

function renderRequestTable(container, requests, triageMode) {
    if (!requests.length) {
        container.innerHTML = '<p class="empty-state">No requests found.</p>';
        return;
    }
    let html = `<table><thead><tr><th>Title</th><th>Department</th><th>Status</th><th>Submitted</th>`;
    if (triageMode) html += '<th>Age (h)</th><th>Actions</th>';
    html += '</tr></thead><tbody>';
    requests.forEach(r => {
        html += `<tr><td><strong>${escapeHtml(r.title)}</strong><br><small>${escapeHtml(r.description || '')}</small></td>
            <td>${r.departmentName}</td><td>${r.status}</td>
            <td>${new Date(r.submittedAt || r.createdAt).toLocaleString()}</td>`;
        if (triageMode) {
            const age = r.ageHours != null ? r.ageHours.toFixed(1) : '—';
            const actions = ['SUBMITTED', 'UNDER_TRIAGE'].includes(r.status) ? `
                <button class="btn-success btn-sm" onclick="acceptRequest(${r.id})">Accept</button>
                <button class="btn-danger btn-sm" onclick="declineRequest(${r.id})">Decline</button>` : '—';
            html += `<td>${age}</td><td class="template-actions">${actions}</td>`;
        }
        html += '</tr>';
        if (r.createdTaskKey) {
            html += `<tr><td colspan="${triageMode ? 6 : 4}"><a href="#" onclick="showTaskDetail(${r.createdTaskId});return false;">Linked task: ${r.createdTaskKey}</a></td></tr>`;
        }
    });
    html += '</tbody></table>';
    container.innerHTML = html;
}

async function submitTaskRequest(event) {
    event.preventDefault();
    const desiredBy = document.getElementById('request-desired-by')?.value;
    try {
        await fetchApi('/task-requests', {
            method: 'POST',
            body: JSON.stringify({
                title: document.getElementById('request-title').value.trim(),
                description: document.getElementById('request-description').value.trim() || null,
                departmentId: parseInt(document.getElementById('request-department').value, 10),
                businessJustification: document.getElementById('request-justification').value.trim() || null,
                desiredByDate: desiredBy ? new Date(desiredBy).toISOString() : null
            })
        });
        toggleRequestForm();
        loadTaskRequests();
        showError('✅ Request submitted!');
    } catch (error) {
        document.getElementById('request-error').textContent = error.message;
        document.getElementById('request-error').style.display = 'block';
    }
}

async function acceptRequest(id) {
    await fetchApi(`/task-requests/${id}/accept`, { method: 'POST', body: '{}' });
    loadTaskRequests();
    showError('✅ Request accepted — task created.');
}

async function declineRequest(id) {
    const opts = DECLINE_REASONS.map(r => `${r.value}: ${r.label}`).join('\n');
    const cat = prompt(`Decline reason category:\n${opts}`, 'out_of_scope');
    if (!cat) return;
    const note = prompt('Decline note (min 5 chars):', '') || '';
    await fetchApi(`/task-requests/${id}/decline`, {
        method: 'POST', body: JSON.stringify({ reasonCategory: cat.split(':')[0].trim(), note })
    });
    loadTaskRequests();
    showError('Request declined.');
}
