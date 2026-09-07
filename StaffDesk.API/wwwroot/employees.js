// ============================================
// Employees: CRUD, search and pagination, the dropdown loaders its forms
// need, and task handover.
// ============================================

let currentPage = 1;
const PAGE_SIZE = 10;
let currentSearch = '';
// ============================================
// Employees
// ============================================
async function loadEmployees() {
    try {
        const params = {
            page: currentPage,
            limit: PAGE_SIZE
        };
        if (currentSearch) {
            params.search = currentSearch;
        }
        const data = await fetchApi('/employees', {}, params);
        if (data) {
            renderEmployees(data);
            updatePagination(data);
        } else {
            renderEmployees({ data: [], page: 1, totalPages: 1 });
            updatePagination({ page: 1, totalPages: 1 });
        }
    } catch (error) {
        console.error('Failed to load employees:', error);
        renderEmployees({ data: [], page: 1, totalPages: 1 });
        updatePagination({ page: 1, totalPages: 1 });
    }
}

function renderEmployees(data) {
    const container = document.getElementById('employee-list');
    const tt = typeof t === 'function' ? t : (k) => k;
    const tp = typeof translatePhrase === 'function' ? translatePhrase : (s) => s;
    if (!data.data || data.data.length === 0) {
        container.innerHTML = `<p class="empty-state">${tt('empty.employees')}</p>`;
        return;
    }
    let html = `
        <table>
            <thead>
                <tr>
                    <th>${tt('table.name')}</th>
                    <th>${tt('table.jobTitle')}</th>
                    <th>${tt('table.department')}</th>
                    <th>${tt('table.level')}</th>
                    <th>${tt('table.manager')}</th>
                    <th>${tt('table.status')}</th>
                    <th>${tt('table.actions')}</th>
                </tr>
            </thead>
            <tbody>
    `;
    data.data.forEach(emp => {
        const status = emp.isActive
            ? `🟢 ${tt('common.active')}`
            : `🔴 ${tt('common.inactive')}`;
        const name = tp(emp.fullName || '');
        const title = tp(emp.jobTitle || '');
        const dept = tp(emp.departmentName || '');
        const level = emp.levelName ? tp(emp.levelName) : tt('common.na');
        const manager = emp.managerName ? tp(emp.managerName) : tt('label.none');
        html += `
            <tr>
                <td>${escapeHtml(name)}</td>
                <td>${escapeHtml(title)}</td>
                <td>${escapeHtml(dept)}</td>
                <td>${escapeHtml(level)}</td>
                <td>${escapeHtml(manager)}</td>
                <td>${status}</td>
                <td>
                    ${isAdmin() ? `
                    <button class="btn-secondary btn-sm" onclick="showEditEmployee(${emp.id}, '${String(emp.fullName).replace(/'/g, "\\'")}', '${String(emp.jobTitle).replace(/'/g, "\\'")}', ${emp.departmentId}, ${emp.levelId}, ${emp.managerId || 'null'}, ${emp.isActive})">
                        ✏️ ${tt('common.edit')}
                    </button>
                    <button class="btn-danger btn-sm" onclick="deleteEmployee(${emp.id})">
                        🚫 ${tt('common.deactivate')}
                    </button>
                    <button class="btn-secondary btn-sm" onclick="revokeEmployeeSessions(${emp.id})">${tt('common.revokeSessions')}</button>
                    ` : '—'}
                </td>
            </tr>
        `;
    });
    html += '</tbody></table>';
    container.innerHTML = html;
}

function updatePagination(data) {
    const totalPages = data.totalPages || 1;
    const page = data.page || 1;
    document.getElementById('page-info').textContent = typeof t === 'function'
        ? t('common.pageOf', { page, total: totalPages })
        : `Page ${page} of ${totalPages}`;
    document.getElementById('prev-btn').disabled = page <= 1;
    document.getElementById('next-btn').disabled = page >= totalPages;
}

function previousPage() {
    if (currentPage > 1) {
        currentPage--;
        loadEmployees();
    }
}

function nextPage() {
    currentPage++;
    loadEmployees();
}

function searchEmployees() {
    currentSearch = document.getElementById('search-input').value.trim();
    currentPage = 1;
    loadEmployees();
}

// ============================================
// Create Employee
// ============================================
function showCreateEmployee() {
    const form = document.getElementById('create-employee-form');
    form.style.display = form.style.display === 'none' ? 'block' : 'none';
    if (form.style.display === 'block') {
        loadDepartmentDropdown();
        loadLevelDropdown();
        document.getElementById('emp-name').focus();
    }
}

function hideCreateEmployee() {
    document.getElementById('create-employee-form').style.display = 'none';
    document.getElementById('emp-error').style.display = 'none';
}

async function loadDepartmentDropdown() {
    try {
        const departments = await fetchApi('/departments');
        const select = document.getElementById('emp-department');
        const tp = typeof translatePhrase === 'function' ? translatePhrase : (s) => s;
        if (departments && departments.length > 0) {
            select.innerHTML = departments.map(dept =>
                `<option value="${dept.id}">${escapeHtml(tp(dept.name))} (${escapeHtml(tp(dept.location || ''))})</option>`
            ).join('');
        } else {
            select.innerHTML = `<option value="">${typeof t === 'function' ? t('dept.noneAvailable') || t('ui.no_departments_available') : 'No departments available'}</option>`;
        }
    } catch (error) {
        console.error('Failed to load departments for dropdown:', error);
    }
}

async function loadLevelDropdown() {
    try {
        const levels = await fetchApi('/seniority-levels');
        const select = document.getElementById('emp-level');
        const tp = typeof translatePhrase === 'function' ? translatePhrase : (s) => s;
        const tt = typeof t === 'function' ? t : (k, v) => k;
        if (levels && levels.length > 0) {
            select.innerHTML = levels
                .filter(l => l.isActive)
                .map(level =>
                    `<option value="${level.id}">${escapeHtml(tp(level.name))} (${tt('levels.rank', { rank: level.rank })})</option>`
                ).join('');
        }
    } catch (error) {
        console.error('Failed to load levels:', error);
    }
}

async function createEmployee(event) {
    event.preventDefault();
    const fullName = document.getElementById('emp-name').value.trim();
    const jobTitle = document.getElementById('emp-title').value.trim();
    const departmentId = parseInt(document.getElementById('emp-department').value);
    const levelId = parseInt(document.getElementById('emp-level').value);

    if (!departmentId) {
        showFillBlanks();
        return;
    }

    if (!levelId) {
        showFillBlanks();
        return;
    }

    try {
        await fetchApi('/employees', {
            method: 'POST',
            body: JSON.stringify({ fullName, jobTitle, departmentId, levelId })
        });
        hideCreateEmployee();
        loadEmployees();
        showError('✅ Employee created successfully!');
    } catch (error) {
        showDenied(error.message);
    }
}

// ============================================
// Edit Employee
// ============================================
async function showEditEmployee(id, fullName, jobTitle, departmentId, levelId, managerId, isActive) {
    document.getElementById('edit-emp-id').value = id;
    document.getElementById('edit-emp-name').value = fullName;
    document.getElementById('edit-emp-title').value = jobTitle;
    document.getElementById('edit-emp-active').checked = isActive;

    // PL-4: GET to capture ETag before a concurrent PUT.
    try {
        await fetchApi(`/employees/${id}`, {}, {}, true);
    } catch { /* list data still usable */ }
    
    await loadEditDepartmentDropdown(departmentId);
    await loadEditLevelDropdown(levelId);
    await loadEditManagerDropdown(departmentId, managerId);
    
    document.getElementById('edit-employee-form').style.display = 'block';
    document.getElementById('edit-emp-error').style.display = 'none';
    document.getElementById('edit-emp-name').focus();
}

function hideEditEmployee() {
    document.getElementById('edit-employee-form').style.display = 'none';
}

async function loadEditDepartmentDropdown(selectedId) {
    try {
        const departments = await fetchApi('/departments');
        const select = document.getElementById('edit-emp-department');
        const tp = typeof translatePhrase === 'function' ? translatePhrase : (s) => s;
        if (departments && departments.length > 0) {
            select.innerHTML = departments.map(dept =>
                `<option value="${dept.id}" ${dept.id === selectedId ? 'selected' : ''}>
                    ${escapeHtml(tp(dept.name))} (${escapeHtml(tp(dept.location || ''))})
                </option>`
            ).join('');
        }
    } catch (error) {
        console.error('Failed to load departments:', error);
    }
}

async function loadEditLevelDropdown(selectedId) {
    try {
        const levels = await fetchApi('/seniority-levels');
        const select = document.getElementById('edit-emp-level');
        const tp = typeof translatePhrase === 'function' ? translatePhrase : (s) => s;
        const tt = typeof t === 'function' ? t : (k, v) => k;
        if (levels && levels.length > 0) {
            select.innerHTML = levels
                .filter(l => l.isActive)
                .map(level =>
                    `<option value="${level.id}" ${level.id === selectedId ? 'selected' : ''}>
                        ${escapeHtml(tp(level.name))} (${tt('levels.rank', { rank: level.rank })})
                    </option>`
                ).join('');
        }
    } catch (error) {
        console.error('Failed to load levels:', error);
    }
}

async function loadEditManagerDropdown(departmentId, selectedId) {
    try {
        const response = await fetch('/v1/employees', {
            headers: {
                'Authorization': `Bearer ${localStorage.getItem('token')}`
            }
        });
        const data = await response.json();
        const select = document.getElementById('edit-emp-manager');
        const tp = typeof translatePhrase === 'function' ? translatePhrase : (s) => s;
        const tt = typeof t === 'function' ? t : (k) => k;

        const empId = parseInt(document.getElementById('edit-emp-id').value);
        const employees = data.data || [];
        const filtered = employees.filter(e => e.departmentId === departmentId && e.id !== empId);

        select.innerHTML = `<option value="">${tt('label.none')}</option>` +
            filtered.map(emp =>
                `<option value="${emp.id}" ${emp.id === selectedId ? 'selected' : ''}>
                    ${escapeHtml(tp(emp.fullName))} (${escapeHtml(emp.levelName ? tp(emp.levelName) : tt('common.na'))})
                </option>`
            ).join('');
    } catch (error) {
        console.error('Failed to load managers:', error);
    }
}

async function updateEmployee(event) {
    event.preventDefault();
    const id = parseInt(document.getElementById('edit-emp-id').value);
    const fullName = document.getElementById('edit-emp-name').value.trim();
    const jobTitle = document.getElementById('edit-emp-title').value.trim();
    const departmentId = parseInt(document.getElementById('edit-emp-department').value);
    const levelId = parseInt(document.getElementById('edit-emp-level').value);
    const managerId = document.getElementById('edit-emp-manager').value;
    const isActive = document.getElementById('edit-emp-active').checked;

    if (!departmentId) {
        showFillBlanks();
        return;
    }

    try {
        await fetchApi(`/employees/${id}`, {
            method: 'PUT',
            etagKey: `employee:${id}`,
            body: JSON.stringify({
                fullName,
                jobTitle,
                departmentId,
                levelId,
                managerId: managerId ? parseInt(managerId) : null,
                isActive
            })
        });

        hideEditEmployee();
        loadEmployees();
        showError('✅ Employee updated successfully!');
    } catch (error) {
        showDenied(error.message);
    }
}

async function deleteEmployee(id, name) {
    if (!confirm(`Deactivate employee "${name}"? They will stay visible in history but won't be assignable or able to log in.`)) {
        return;
    }

    try {
        const response = await fetch(`/v1/employees/${id}`, {
            method: 'DELETE',
            headers: {
                'Authorization': `Bearer ${localStorage.getItem('token')}`
            }
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.error || 'Deactivation failed');
        }

        loadEmployees();
        showError('✅ Employee deactivated successfully!');
    } catch (error) {
        showError('❌ ' + error.message);
    }
}

// ============================================
// Employee handover
// ============================================
async function showHandoverDialog() {
    const fromId = parseInt(document.getElementById('edit-emp-id')?.value, 10);
    if (!fromId) return;
    const toId = prompt('Enter target employee ID to handover all active tasks to:');
    if (!toId) return;
    try {
        const res = await fetchApi(`/employees/${fromId}/handover`, {
            method: 'POST', body: JSON.stringify({ toEmployeeId: parseInt(toId, 10) })
        });
        showError(`✅ Handover complete — ${res.tasksReassigned} tasks reassigned.`);
    } catch (error) {
        showError('❌ ' + error.message);
    }
}
