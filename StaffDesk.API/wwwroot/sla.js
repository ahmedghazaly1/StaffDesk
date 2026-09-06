// ============================================
// SLA: list and detail badges, plus policy administration.
// ============================================

function renderSlaBadge(task) {
    const tt = typeof t === 'function' ? t : (k) => k;
    if (!task.breachState) {
        return `<span class="sla-badge sla-na">${tt('sla.na')}</span>`;
    }

    const state = task.breachState;
    const labels = {
        'ON_TRACK': { label: tt('sla.onTrack'), class: 'sla-on-track' },
        'AT_RISK': { label: tt('sla.atRisk'), class: 'sla-at-risk' },
        'BREACHED': { label: tt('sla.breached'), class: 'sla-breached' }
    };

    const info = labels[state] || { label: state, class: 'sla-na' };
    return `<span class="sla-badge ${info.class}">${info.label}</span>`;
}


function renderSlaDetail(task) {
    const tt = typeof t === 'function' ? t : (k) => k;
    const loc = typeof localeTag === 'function' ? localeTag() : 'en';
    const parts = [];
    if (task.responseTargetAt) parts.push(`${tt('sla.response')}: ${new Date(task.responseTargetAt).toLocaleString(loc)}`);
    if (task.resolutionTargetAt) parts.push(`${tt('sla.resolution')}: ${new Date(task.resolutionTargetAt).toLocaleString(loc)}`);
    if (task.remainingMinutes != null) parts.push(tt('sla.remaining', { min: task.remainingMinutes }));
    if (task.blockedPauseMinutes != null) parts.push(tt('sla.blockedPause', { min: task.blockedPauseMinutes }));
    return parts.length ? parts.join(' · ') : tt('sla.noTargets');
}

// ============================================
// SLA admin
// ============================================
function toggleSlaPolicyForm() {
    const f = document.getElementById('sla-policy-form');
    if (!f) return;
    const show = f.style.display === 'none';
    f.style.display = show ? 'block' : 'none';
    if (show) loadSlaDepartments();
}

async function loadSlaDepartments() {
    await populateDepartmentSelect('sla-dept');
}

async function loadSlaPolicies() {
    const container = document.getElementById('sla-policy-list');
    if (!container) return;
    try {
        const policies = await fetchApi('/sla/policies') || [];
        if (!policies.length) { container.innerHTML = '<p class="empty-state">No SLA policies.</p>'; return; }
        container.innerHTML = `<table><thead><tr><th>Department</th><th>Priority</th><th>Response</th><th>Resolution</th><th>Actions</th></tr></thead><tbody>
            ${policies.map(p => `<tr><td>${p.departmentName}</td><td>${p.priority}</td><td>${p.responseTargetMinutes}m</td><td>${p.resolutionTargetMinutes}m</td>
            <td><button class="btn-danger btn-sm" onclick="deleteSlaPolicy(${p.id})">Delete</button></td></tr>`).join('')}
            </tbody></table>`;
    } catch (error) {
        container.innerHTML = `<p class="empty-state">${error.message}</p>`;
    }
}

async function createSlaPolicy(event) {
    event.preventDefault();
    await fetchApi('/sla/policies', {
        method: 'POST',
        body: JSON.stringify({
            departmentId: parseInt(document.getElementById('sla-dept').value, 10),
            priority: document.getElementById('sla-priority').value,
            responseTargetMinutes: parseInt(document.getElementById('sla-response').value, 10),
            resolutionTargetMinutes: parseInt(document.getElementById('sla-resolution').value, 10)
        })
    });
    toggleSlaPolicyForm();
    loadSlaPolicies();
    showError('✅ Policy created!');
}

async function deleteSlaPolicy(id) {
    if (!confirm('Delete this SLA policy?')) return;
    await fetchApi(`/sla/policies/${id}`, { method: 'DELETE' });
    loadSlaPolicies();
}

async function evaluateAllSla() {
    const res = await fetchApi('/sla/evaluate', { method: 'POST', body: '{}' });
    showError(`✅ SLA evaluation complete — ${res?.evaluated ?? 0} tasks evaluated.`);
}
