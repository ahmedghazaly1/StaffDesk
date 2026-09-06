// ============================================
// Departments: CRUD, the department detail view, and the per-department
// triager and default-criteria admin panels.
// ============================================

async function loadDepartments() {
    try {
        const data = await fetchApi('/departments');
        if (data) {
            renderDepartments(data);
        } else {
            renderDepartments([]);
        }
    } catch (error) {
        console.error('Failed to load departments:', error);
        renderDepartments([]);
    }
}

function renderDepartments(departments) {
    const container = document.getElementById('department-list');
    const tt = typeof t === 'function' ? t : (k) => k;
    const tp = typeof translatePhrase === 'function' ? translatePhrase : (s) => s;
    if (!departments || departments.length === 0) {
        container.innerHTML = `<p class="empty-state">${tt('dept.empty')}</p>`;
        return;
    }
    container.innerHTML = departments.map(dept => {
        const name = tp(dept.name || '');
        const location = tp(dept.location || '');
        const manager = dept.managerName ? tp(dept.managerName) : tt('dept.notAssigned');
        const safeName = String(dept.name || '').replace(/'/g, "\\'");
        const safeLoc = String(dept.location || '').replace(/'/g, "\\'");
        return `
        <div class="card">
            <h3>${escapeHtml(name)}</h3>
            <p>📍 ${escapeHtml(location)}</p>
            <p>👤 ${tt('dept.manager', { name: escapeHtml(manager) })}</p>
            <div class="card-actions">
                <span class="badge badge-info" onclick="showDepartmentDetail(${dept.id}, '${safeName}', '${safeLoc}')">
                    ${tt('dept.viewEmployees')}
                </span>
                ${isAdmin() ? `
                <button class="btn-secondary btn-sm" onclick="showEditDepartment(${dept.id}, '${safeName}', '${safeLoc}')">
                    ✏️ ${tt('common.edit')}
                </button>
                <button class="btn-danger btn-sm" onclick="deleteDepartment(${dept.id}, '${safeName}')">
                    🗑️ ${tt('dept.delete')}
                </button>
                ` : ''}
            </div>
        </div>`;
    }).join('');
}

function showCreateDepartment() {
    const form = document.getElementById('create-department-form');
    form.style.display = form.style.display === 'none' ? 'block' : 'none';
    if (form.style.display === 'block') {
        document.getElementById('dept-name').focus();
    }
}

function hideCreateDepartment() {
    document.getElementById('create-department-form').style.display = 'none';
    document.getElementById('dept-error').style.display = 'none';
}

async function createDepartment(event) {
    event.preventDefault();
    hideError();
    const name = document.getElementById('dept-name').value.trim();
    const location = document.getElementById('dept-location').value.trim();

    try {
        await fetchApi('/departments', {
            method: 'POST',
            body: JSON.stringify({ name, location })
        });
        hideCreateDepartment();
        loadDepartments();
        showError('✅ Department created successfully!');
    } catch (error) {
        document.getElementById('dept-error').textContent = error.message;
        document.getElementById('dept-error').style.display = 'block';
    }
}

// ============================================
// Department Edit/Delete Functions
// ============================================

function showEditDepartment(id, name, location) {
    document.getElementById('edit-dept-id').value = id;
    document.getElementById('edit-dept-name').value = name;
    document.getElementById('edit-dept-location').value = location;
    document.getElementById('edit-department-form').style.display = 'block';
    document.getElementById('edit-dept-error').style.display = 'none';
    document.getElementById('edit-dept-name').focus();
}

function hideEditDepartment() {
    document.getElementById('edit-department-form').style.display = 'none';
}

async function updateDepartment(event) {
    event.preventDefault();
    const id = parseInt(document.getElementById('edit-dept-id').value);
    const name = document.getElementById('edit-dept-name').value.trim();
    const location = document.getElementById('edit-dept-location').value.trim();

    try {
        // fetchApi (not a raw fetch) so a 4xx/5xx body is parsed via parseApiErrorBody and
        // error.message carries the real reason from error.error.message, not the stringified
        // {code,message,details,requestId} object a plain `error.error` used to produce.
        await fetchApi(`/departments/${id}`, {
            method: 'PUT',
            body: JSON.stringify({ name, location })
        });

        hideEditDepartment();
        loadDepartments();
        showError('✅ Department updated successfully!');
    } catch (error) {
        document.getElementById('edit-dept-error').textContent = error.message;
        document.getElementById('edit-dept-error').style.display = 'block';
    }
}

async function deleteDepartment(id, name) {
    if (!confirm(`Are you sure you want to delete the department "${name}"?`)) {
        return;
    }

    try {
        // Same fix as updateDepartment above - fetchApi surfaces the real error.message (e.g.
        // "Cannot delete a department that still has employees/tasks/analytics history/...")
        // instead of a raw fetch's stringified error object.
        await fetchApi(`/departments/${id}`, { method: 'DELETE' });

        loadDepartments();
        showError('✅ Department deleted successfully!');
    } catch (error) {
        showError('❌ ' + error.message);
    }
}

// ============================================
// Department Detail
// ============================================
async function loadDepartmentDetail(departmentId) {
    try {
        const dept = await fetchApi(`/departments/${departmentId}`);
        if (dept) {
            document.getElementById('dept-detail-manager').textContent =
                `👤 ${typeof t === 'function' ? t('dept.manager', {
                    name: dept.managerName
                        ? (typeof translatePhrase === 'function' ? translatePhrase(dept.managerName) : dept.managerName)
                        : (typeof t === 'function' ? t('dept.notAssigned') : 'Not assigned')
                }) : ('Manager: ' + (dept.managerName || 'Not assigned'))}`;
            loadDepartmentEmployees(departmentId);
            loadDeptAdminPanels(departmentId);
        }
    } catch (error) {
        console.error('Failed to load department detail:', error);
    }
}

async function loadDepartmentEmployees(departmentId) {
    try {
        const data = await fetchApi(`/departments/${departmentId}/employees`);
        if (data) {
            renderDepartmentEmployees(data);
        } else {
            renderDepartmentEmployees([]);
        }
    } catch (error) {
        console.error('Failed to load department employees:', error);
        renderDepartmentEmployees([]);
    }
}

function renderDepartmentEmployees(employees) {
    const container = document.getElementById('dept-detail-employees');
    const tt = typeof t === 'function' ? t : (k) => k;
    const tp = typeof translatePhrase === 'function' ? translatePhrase : (s) => s;
    if (!employees || employees.length === 0) {
        container.innerHTML = `<p>${tt('ui.no_employees_in_this_department') || 'No employees in this department.'}</p>`;
        return;
    }
    let html = `
        <table>
            <thead>
                <tr>
                    <th>${tt('table.name')}</th>
                    <th>${tt('table.jobTitle')}</th>
                    <th>${tt('table.level')}</th>
                </tr>
            </thead>
            <tbody>
    `;
    employees.forEach(emp => {
        html += `
            <tr>
                <td>${escapeHtml(tp(emp.fullName || ''))}</td>
                <td>${escapeHtml(tp(emp.jobTitle || ''))}</td>
                <td>${escapeHtml(emp.levelName ? tp(emp.levelName) : tt('common.na'))}</td>
            </tr>
        `;
    });
    html += '</tbody></table>';
    container.innerHTML = html;
}

// ============================================
// Department triagers & default DoD
// ============================================
async function loadDeptAdminPanels(departmentId) {
    await Promise.all([loadDeptTriagers(departmentId), loadDeptDefaultCriteria(departmentId)]);
}

async function loadDeptTriagers(departmentId) {
    const panel = document.getElementById('dept-triagers-panel');
    if (!panel) return;
    const triagers = await fetchApi(`/departments/${departmentId}/triagers`, {}, {}, true) || [];
    const addBtn = isAdmin() ? `
        <div class="detail-section-actions"><select id="new-triager-select"><option value="">Add triager…</option></select>
        <button class="btn-primary btn-sm" onclick="addTriager(${departmentId})">Add</button></div>` : '';
    panel.innerHTML = (triagers.length ? `<ul>${triagers.map(t =>
        `<li>${t.employeeName} ${isAdmin() ? `<button class="btn-danger btn-sm" onclick="removeTriager(${departmentId},${t.employeeId})">Remove</button>` : ''}</li>`
    ).join('')}</ul>` : '<p class="empty-state">No triagers assigned.</p>') + addBtn;
    if (isAdmin()) {
        const emps = await fetchApi(`/departments/${departmentId}/employees`, {}, {}, true) || [];
        const sel = document.getElementById('new-triager-select');
        if (sel) sel.innerHTML = '<option value="">Add triager…</option>' + emps.map(e => `<option value="${e.id}">${e.fullName}</option>`).join('');
    }
}

async function addTriager(departmentId) {
    const employeeId = parseInt(document.getElementById('new-triager-select')?.value, 10);
    if (!employeeId) return;
    await fetchApi(`/departments/${departmentId}/triagers`, { method: 'POST', body: JSON.stringify({ employeeId }) });
    loadDeptTriagers(departmentId);
}

async function removeTriager(departmentId, employeeId) {
    await fetchApi(`/departments/${departmentId}/triagers/${employeeId}`, { method: 'DELETE' });
    loadDeptTriagers(departmentId);
}

async function loadDeptDefaultCriteria(departmentId) {
    const panel = document.getElementById('dept-default-criteria-panel');
    if (!panel) return;
    const criteria = await fetchApi(`/departments/${departmentId}/default-criteria`, {}, {}, true) || [];
    panel.innerHTML = (criteria.length ? `<ul>${criteria.map(c =>
        `<li>${escapeHtml(c.text)} <button class="btn-danger btn-sm" onclick="deleteDefaultCriterion(${departmentId},${c.id})">×</button></li>`
    ).join('')}</ul>` : '<p class="empty-state">No default criteria.</p>') + `
        <div class="detail-section-actions"><input type="text" id="new-default-criterion" placeholder="New default criterion" style="flex:2;" />
        <button class="btn-primary btn-sm" onclick="addDefaultCriterion(${departmentId})">Add</button></div>`;
}

async function addDefaultCriterion(departmentId) {
    const text = document.getElementById('new-default-criterion')?.value.trim();
    if (!text) return;
    await fetchApi(`/departments/${departmentId}/default-criteria`, { method: 'POST', body: JSON.stringify({ text }) });
    loadDeptDefaultCriteria(departmentId);
}

async function deleteDefaultCriterion(departmentId, criterionId) {
    await fetchApi(`/departments/${departmentId}/default-criteria/${criterionId}`, { method: 'DELETE' });
    loadDeptDefaultCriteria(departmentId);
}
