// ============================================
// Security: active sessions and password change (SP-1..SP-3).
// ============================================
(function () {
  window.showSecurity = function showSecurity() {
    showView('security-view', 'nav-security', '#security');
    loadSecuritySessions();
  };

  function setStatus(msg, ok) {
    const el = document.getElementById('sec-status');
    if (!el) return;
    el.textContent = msg || '';
    el.style.color = ok === false ? '#c53030' : '#718096';
  }

  async function loadSecuritySessions() {
    const el = document.getElementById('sec-sessions');
    if (!el) return;
    el.innerHTML = '<p class="empty-state">Loading…</p>';
    try {
      const payload = await fetchApi('/auth/sessions?page=1&limit=50');
      const rows = payload?.data || (Array.isArray(payload) ? payload : []);
      if (!rows.length) {
        el.innerHTML = '<p class="empty-state">No active sessions.</p>';
        return;
      }
      el.innerHTML = `<table class="data-table"><thead><tr>
        <th>Created</th><th>Last seen</th><th>IP</th><th>Client</th><th></th>
      </tr></thead><tbody>${rows.map(s => `<tr>
        <td>${s.createdAt ? new Date(s.createdAt).toLocaleString() : ''}</td>
        <td>${s.lastSeenAt ? new Date(s.lastSeenAt).toLocaleString() : ''}</td>
        <td>${escapeHtml(s.ip || '')}</td>
        <td>${escapeHtml(s.userAgent || '')}</td>
        <td>${s.current ? '<em>this device</em>' :
          `<button class="btn-danger btn-sm" onclick="revokeSession('${s.id}')">Revoke</button>`}</td>
      </tr>`).join('')}</tbody></table>`;
    } catch (e) {
      el.innerHTML = `<p class="empty-state">${escapeHtml(e.message)}</p>`;
    }
  }

  window.revokeSession = async function revokeSession(id) {
    try {
      await fetchApi('/auth/sessions/' + id, { method: 'DELETE' });
      setStatus('Session revoked.', true);
      loadSecuritySessions();
    } catch (e) {
      setStatus(e.message, false);
    }
  };

  window.revokeEmployeeSessions = async function revokeEmployeeSessions(employeeId) {
    if (!isAdmin()) return;
    if (!confirm('Revoke all sessions for this employee? They will have to sign in again.')) return;
    try {
      await fetchApi('/auth/sessions/employee/' + employeeId, { method: 'DELETE' });
      showError('Sessions revoked.');
    } catch (e) {
      showError(e.message);
    }
  };

  window.changePassword = async function changePassword(event) {
    event.preventDefault();
    const currentPassword = document.getElementById('sec-current-password').value;
    const newPassword = document.getElementById('sec-new-password').value;
    try {
      await fetchApi('/auth/password', {
        method: 'POST',
        body: JSON.stringify({ currentPassword, newPassword })
      });
      document.getElementById('sec-current-password').value = '';
      document.getElementById('sec-new-password').value = '';
      alert('Password changed. Please sign in again.');
      logout();
    } catch (e) {
      setStatus(e.message, false);
    }
  };
})();
