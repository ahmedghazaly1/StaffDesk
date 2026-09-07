// ============================================
// Seniority levels: CRUD.
// ============================================

async function loadLevels() {
    try {
        const data = await fetchApi('/seniority-levels');
        if (data) {
            renderLevels(data);
        } else {
            renderLevels([]);
        }
    } catch (error) {
        console.error('Failed to load levels:', error);
        renderLevels([]);
    }
}

function renderLevels(levels) {
    const container = document.getElementById('level-list');
    const tt = typeof t === 'function' ? t : (k) => k;
    if (!levels || levels.length === 0) {
        container.innerHTML = `<p class="empty-state">${tt('levels.empty')}</p>`;
        return;
    }
    container.innerHTML = levels.map(level => `
        <div class="card">
            <h3>${level.name}</h3>
            <p>${tt('levels.rank', { rank: level.rank })}</p>
            <p>${level.description || tt('levels.noDescription')}</p>
            <p>${tt('levels.status', { status: level.isActive ? '🟢 ' + tt('common.active') : '🔴 ' + tt('common.inactive') })}</p>
            ${isAdmin() ? `
            <div class="card-actions">
                <button class="btn-secondary btn-sm" onclick="showEditLevel(${level.id}, '${level.name}', ${level.rank}, '${level.description || ''}', ${level.isActive})">
                    ✏️ ${tt('common.edit')}
                </button>
                <button class="btn-danger btn-sm" onclick="deleteLevel(${level.id}, '${level.name}')">
                    🗑️ ${tt('dept.delete')}
                </button>
            </div>
            ` : ''}
        </div>
    `).join('');
}

function showCreateLevel() {
    const form = document.getElementById('create-level-form');
    form.style.display = form.style.display === 'none' ? 'block' : 'none';
    if (form.style.display === 'block') {
        document.getElementById('level-name').focus();
    }
}

function hideCreateLevel() {
    document.getElementById('create-level-form').style.display = 'none';
    document.getElementById('level-error').style.display = 'none';
}

async function createLevel(event) {
    event.preventDefault();
    const name = document.getElementById('level-name').value.trim();
    const rank = parseInt(document.getElementById('level-rank').value);
    const description = document.getElementById('level-description').value.trim();

    if (!name) {
        showFillBlanks();
        return;
    }

    if (!rank || rank <= 0) {
        showDenied('Rank must be a positive number');
        return;
    }

    try {
        await fetchApi('/seniority-levels', {
            method: 'POST',
            body: JSON.stringify({ name, rank, description: description || null })
        });
        hideCreateLevel();
        loadLevels();
        showError('✅ Level created successfully!');
    } catch (error) {
        showDenied(error.message);
    }
}

function showEditLevel(id, name, rank, description, isActive) {
    document.getElementById('edit-level-id').value = id;
    document.getElementById('edit-level-name').value = name;
    document.getElementById('edit-level-rank').value = rank;
    document.getElementById('edit-level-description').value = description || '';
    document.getElementById('edit-level-active').checked = isActive;
    document.getElementById('edit-level-form').style.display = 'block';
    document.getElementById('edit-level-error').style.display = 'none';
    document.getElementById('edit-level-name').focus();
}

function hideEditLevel() {
    document.getElementById('edit-level-form').style.display = 'none';
}

async function updateLevel(event) {
    event.preventDefault();
    const id = parseInt(document.getElementById('edit-level-id').value);
    const name = document.getElementById('edit-level-name').value.trim();
    const rank = parseInt(document.getElementById('edit-level-rank').value);
    const description = document.getElementById('edit-level-description').value.trim();
    const isActive = document.getElementById('edit-level-active').checked;

    if (!name) {
        showFillBlanks();
        return;
    }

    if (!rank || rank <= 0) {
        showDenied('Rank must be a positive number');
        return;
    }

    try {
        await fetchApi(`/seniority-levels/${id}`, {
            method: 'PUT',
            body: JSON.stringify({ name, rank, description: description || null, isActive })
        });
        hideEditLevel();
        loadLevels();
        showError('✅ Level updated successfully!');
    } catch (error) {
        showDenied(error.message);
    }
}

async function deleteLevel(id, name) {
    if (!confirm(`Are you sure you want to delete the level "${name}"?`)) {
        return;
    }

    try {
        await fetchApi(`/seniority-levels/${id}`, {
            method: 'DELETE'
        });
        loadLevels();
        showError('✅ Level deleted successfully!');
    } catch (error) {
        showError('❌ ' + error.message);
    }
}
