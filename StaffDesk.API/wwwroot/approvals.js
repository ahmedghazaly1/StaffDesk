// ============================================
// Task approval steps: the approval panel, approve / reject / skip / reassign,
// and adding an approval to a task.
// ============================================

let pendingApprovalStepIds = new Set();
async function loadApprovalInfo(taskId) {
    let approval;
    try {
        approval = await fetchApi(`/approvals/task/${taskId}`, {}, {}, true);
    } catch {
        return '';
    }
    if (!approval) return '';

    let pendingSteps = [];
    try {
        pendingSteps = await fetchApi('/approvals/pending') || [];
    } catch {
        pendingSteps = [];
    }
    pendingApprovalStepIds = new Set(pendingSteps.map(s => s.id));

    const statusLabel = approval.isRejected ? 'Rejected'
        : approval.isFullyApproved ? 'Approved'
        : 'Pending';

    const statusClass = approval.isRejected ? 'approval-rejected'
        : approval.isFullyApproved ? 'approval-approved'
        : 'approval-pending';

    const stepsHtml = (approval.steps || []).map(step => {
        const stepClass = `approval-${(step.state || 'pending').toLowerCase()}`;
        const canAct = step.state === 'PENDING' && pendingApprovalStepIds.has(step.id);
        const adminActions = isAdmin() && step.state === 'PENDING' ? `
            <button class="btn-secondary btn-sm" onclick="skipApprovalStep(${step.id})">Skip</button>
            <button class="btn-secondary btn-sm" onclick="reassignApprovalStep(${step.id})">Reassign</button>
        ` : '';
        const actions = canAct ? `
            <div class="detail-section-actions" style="margin-top:8px;">
                <input type="text" id="approval-note-${step.id}" placeholder="Decision note (optional)" />
                <button class="btn-success btn-sm" onclick="approveStep(${step.id})">Approve</button>
                <button class="btn-danger btn-sm" onclick="rejectStep(${step.id})">Reject</button>
                ${adminActions}
            </div>
        ` : (adminActions ? `<div class="detail-section-actions" style="margin-top:8px;">${adminActions}</div>` : '');

        return `
            <div class="approval-step">
                <div class="approval-step-header">
                    <strong>Step ${step.order}: ${step.approverName || 'Unknown'}</strong>
                    <span class="approval-status-badge ${stepClass}">${step.state}</span>
                </div>
                <div class="approval-step-meta">
                    ${step.approverLevel ? `Level: ${step.approverLevel} · ` : ''}
                    ${step.decisionAt ? `Decided: ${new Date(step.decisionAt).toLocaleString()}` : 'Awaiting decision'}
                    ${step.decisionNote ? ` · Note: ${step.decisionNote}` : ''}
                </div>
                ${actions}
            </div>
        `;
    }).join('');

    return `
        <div class="detail-section">
            <h4>Approvals — <span class="approval-status-badge ${statusClass}">${statusLabel}</span></h4>
            <p style="font-size:13px;color:#4a5568;margin-bottom:10px;">${approval.name || 'Approval workflow'}${approval.isRequired ? ' (required)' : ''}</p>
            ${stepsHtml || '<p class="empty-state" style="padding:12px;">No approval steps.</p>'}
        </div>
    `;
}

async function approveStep(stepId) {
    const note = document.getElementById(`approval-note-${stepId}`)?.value.trim() || null;
    try {
        await fetchApi(`/approvals/steps/${stepId}`, {
            method: 'PATCH',
            body: JSON.stringify({ state: 'APPROVED', decisionNote: note })
        });
        showError('✅ Step approved!');
        if (currentDetailTaskId) await refreshTaskDetail(currentDetailTaskId);
    } catch (error) {
        showError('❌ ' + error.message);
    }
}

async function rejectStep(stepId) {
    const note = document.getElementById(`approval-note-${stepId}`)?.value.trim() || null;
    try {
        await fetchApi(`/approvals/steps/${stepId}`, {
            method: 'PATCH',
            body: JSON.stringify({ state: 'REJECTED', decisionNote: note })
        });
        showError('✅ Step rejected.');
        if (currentDetailTaskId) await refreshTaskDetail(currentDetailTaskId);
    } catch (error) {
        showError('❌ ' + error.message);
    }
}

async function populateApproverDropdown() {
    const sel = document.getElementById('new-approval-approver');
    if (!sel) return;
    try {
        const emps = await fetchApi('/employees', {}, { limit: 200, page: 1 }, true);
        const list = emps?.data || emps || [];
        sel.innerHTML = '<option value="">Select approver</option>' +
            list.filter(e => e.isActive).map(e => `<option value="${e.id}">${e.fullName}</option>`).join('');
    } catch { /* ignore */ }
}

async function createTaskApproval(taskId) {
    const name = document.getElementById('new-approval-name')?.value.trim() || 'Approval';
    const approverId = parseInt(document.getElementById('new-approval-approver')?.value, 10);
    if (!approverId) { showError('Select an approver'); return; }
    await fetchApi(`/approvals/task/${taskId}`, {
        method: 'POST',
        body: JSON.stringify({ name, isRequired: true, steps: [{ approverId, order: 1 }] })
    });
    await refreshTaskDetail(taskId);
}

// ============================================
// Approval skip / reassign
// ============================================
async function skipApprovalStep(stepId) {
    const reason = prompt('Skip reason:') || null;
    await fetchApi(`/approvals/steps/${stepId}/skip`, { method: 'POST', body: JSON.stringify({ reason }) });
    if (currentDetailTaskId) await refreshTaskDetail(currentDetailTaskId);
    showError('Step skipped.');
}

async function reassignApprovalStep(stepId) {
    const newId = prompt('New approver employee ID:');
    if (!newId) return;
    await fetchApi(`/approvals/steps/${stepId}/reassign`, {
        method: 'POST', body: JSON.stringify({ newApproverId: parseInt(newId, 10), reason: null })
    });
    if (currentDetailTaskId) await refreshTaskDetail(currentDetailTaskId);
    showError('Step reassigned.');
}
