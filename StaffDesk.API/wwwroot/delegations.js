// ============================================
// Delegations: list, create, and revoke.
// ============================================

function toggleCreateDelegationForm() {
    const form = document.getElementById('create-delegation-form');
    const show = form.style.display === 'none';
    form.style.display = show ? 'block' : 'none';
    if (show) {
        loadDelegationDelegateDropdown();
        const now = new Date();
        const nextWeek = new Date(now.getTime() + 7 * 24 * 60 * 60 * 1000);
        document.getElementById('delegation-start').value = toDatetimeLocalValue(now.toISOString());
        document.getElementById('delegation-end').value = toDatetimeLocalValue(nextWeek.toISOString());
        document.getElementById('delegation-error').style.display = 'none';
    }
}

async function loadDelegationDelegateDropdown() {
    try {
        const data = await fetchApi('/employees', {}, { limit: 200 });
        const select = document.getElementById('delegation-delegate');
        const employees = (data?.data || []).filter(e => e.isActive);
        select.innerHTML = employees.map(emp =>
            `<option value="${emp.id}">${emp.fullName} (${emp.departmentName})</option>`
        ).join('');
    } catch (error) {
        console.error('Failed to load delegates:', error);
    }
}

async function loadDelegations() {
    const mineContainer = document.getElementById('delegation-list-mine');
    const delegateContainer = document.getElementById('delegation-list-delegate');
    mineContainer.innerHTML = '<p class="empty-state">Loading…</p>';
    delegateContainer.innerHTML = '<p class="empty-state">Loading…</p>';

    try {
        const [mine, delegated] = await Promise.all([
            fetchApi('/delegations/active'),
            fetchApi('/delegations/active/delegate')
        ]);
        renderDelegationTable(mineContainer, mine || [], true);
        renderDelegationTable(delegateContainer, delegated || [], false);
    } catch (error) {
        console.error('Failed to load delegations:', error);
        mineContainer.innerHTML = '<p class="empty-state">Could not load delegations.</p>';
        delegateContainer.innerHTML = '<p class="empty-state">Could not load delegations.</p>';
    }
}

function renderDelegationTable(container, delegations, canDelete) {
    if (!delegations.length) {
        container.innerHTML = '<p class="empty-state">No delegations found.</p>';
        return;
    }

    let html = `
        <table>
            <thead>
                <tr>
                    <th>Delegator</th>
                    <th>Delegate</th>
                    <th>Scope</th>
                    <th>Start</th>
                    <th>End</th>
                    <th>Status</th>
                    <th>Reason</th>
                    ${canDelete ? '<th>Actions</th>' : ''}
                </tr>
            </thead>
            <tbody>
    `;

    delegations.forEach(d => {
        html += `
            <tr>
                <td>${d.delegatorName}</td>
                <td>${d.delegateName}</td>
                <td>${d.scope}</td>
                <td>${new Date(d.startDate).toLocaleDateString()}</td>
                <td>${new Date(d.endDate).toLocaleDateString()}</td>
                <td>${d.isActive ? '🟢 Active' : '🔴 Inactive'}</td>
                <td>${d.reason || '—'}</td>
                ${canDelete ? `<td><button class="btn-danger btn-sm" onclick="deleteDelegation(${d.id})">Delete</button></td>` : ''}
            </tr>
        `;
    });

    html += '</tbody></table>';
    container.innerHTML = html;
}

async function createDelegation(event) {
    event.preventDefault();
    const delegateId = parseInt(document.getElementById('delegation-delegate').value, 10);
    const scope = document.getElementById('delegation-scope').value;
    const startDate = document.getElementById('delegation-start').value;
    const endDate = document.getElementById('delegation-end').value;
    const reason = document.getElementById('delegation-reason').value.trim() || null;

    try {
        await fetchApi('/delegations', {
            method: 'POST',
            body: JSON.stringify({
                delegateId,
                scope,
                startDate: new Date(startDate).toISOString(),
                endDate: new Date(endDate).toISOString(),
                reason
            })
        });
        toggleCreateDelegationForm();
        loadDelegations();
        showError('✅ Delegation created!');
    } catch (error) {
        document.getElementById('delegation-error').textContent = error.message;
        document.getElementById('delegation-error').style.display = 'block';
    }
}

async function deleteDelegation(id) {
    if (!confirm('Delete this delegation?')) return;
    try {
        await fetchApi(`/delegations/${id}`, { method: 'DELETE' });
        loadDelegations();
        showError('✅ Delegation deleted.');
    } catch (error) {
        showError('❌ ' + error.message);
    }
}
