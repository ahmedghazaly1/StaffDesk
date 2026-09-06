// ============================================
// Audit log: events, chain verification, exports (Admin + Auditor).
// ============================================
(function () {
  let auditCursor = null;
  let queuedExportId = null;

  function auditStatus(msg, ok) {
    const el = document.getElementById('audit-status');
    if (!el) return;
    const text = msg || '';
    el.textContent = text;
    el.hidden = !text;
    el.classList.toggle('is-error', ok === false);
    el.classList.toggle('is-ok', ok === true && !!text);
  }

  function toApiDate(localValue) {
    if (!localValue) return null;
    const d = new Date(localValue);
    return Number.isNaN(d.getTime()) ? null : d.toISOString();
  }

  function defaultRangeInputs() {
    const to = new Date();
    const from = new Date();
    from.setDate(to.getDate() - 30);
    const pad = n => String(n).padStart(2, '0');
    const fmt = d => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
    const setIfEmpty = (id, value) => {
      const el = document.getElementById(id);
      if (el && !el.value) el.value = value;
    };
    setIfEmpty('audit-from', fmt(from));
    setIfEmpty('audit-to', fmt(to));
    setIfEmpty('audit-verify-from', fmt(from));
    setIfEmpty('audit-verify-to', fmt(to));
  }

  function currentFilters() {
    const actorRaw = document.getElementById('audit-actor-id')?.value?.trim();
    return {
      eventType: document.getElementById('audit-event-type')?.value?.trim() || undefined,
      outcome: document.getElementById('audit-outcome')?.value || undefined,
      actorId: actorRaw ? Number(actorRaw) : undefined,
      targetType: document.getElementById('audit-target-type')?.value?.trim() || undefined,
      targetId: document.getElementById('audit-target-id')?.value?.trim() || undefined,
      fromDate: toApiDate(document.getElementById('audit-from')?.value),
      toDate: toApiDate(document.getElementById('audit-to')?.value),
      sourceIp: document.getElementById('audit-source-ip')?.value?.trim() || undefined,
      limit: 50
    };
  }

  window.prepareAuditPage = function prepareAuditPage() {
    if (!isAuditViewer()) return;
    defaultRangeInputs();
    const holdsPanel = document.getElementById('audit-holds-panel');
    if (holdsPanel) holdsPanel.style.display = isAdmin() ? '' : 'none';
    loadAuditEvents();
    loadAuditCount();
    if (isAdmin()) loadAuditLegalHolds();
  };

  window.resetAuditFilters = function resetAuditFilters() {
    ['audit-event-type', 'audit-actor-id', 'audit-target-type', 'audit-target-id', 'audit-source-ip'].forEach(id => {
      const el = document.getElementById(id);
      if (el) el.value = '';
    });
    const outcome = document.getElementById('audit-outcome');
    if (outcome) outcome.value = '';
    ['audit-from', 'audit-to'].forEach(id => {
      const el = document.getElementById(id);
      if (el) el.value = '';
    });
    defaultRangeInputs();
    loadAuditEvents();
  };

  async function loadAuditCount() {
    const el = document.getElementById('audit-count');
    if (!el) return;
    try {
      const filters = currentFilters();
      const data = await fetchApi('/audit/count', {}, {
        fromDate: filters.fromDate,
        toDate: filters.toDate
      }, true);
      el.textContent = `Matching events in range: ${data?.count ?? 0}`;
    } catch {
      el.textContent = '';
    }
  }

  window.loadAuditEvents = async function loadAuditEvents() {
    auditCursor = null;
    await fetchAuditPage(false);
    loadAuditCount();
  };

  window.loadMoreAuditEvents = async function loadMoreAuditEvents() {
    if (!auditCursor) return;
    await fetchAuditPage(true);
  };

  async function fetchAuditPage(append) {
    const el = document.getElementById('audit-events');
    const moreBtn = document.getElementById('audit-load-more');
    if (!el) return;
    if (!append) el.innerHTML = '<p class="empty-state">Loading…</p>';

    try {
      const filters = currentFilters();
      const params = { ...filters };
      if (append && auditCursor) params.cursor = auditCursor;
      const payload = await fetchApi('/audit/events', {}, params, true);
      const rows = payload?.data || [];
      auditCursor = payload?.nextCursor || null;
      if (moreBtn) moreBtn.style.display = auditCursor ? '' : 'none';

      if (!rows.length && !append) {
        el.innerHTML = '<p class="empty-state">No audit events match these filters.</p>';
        auditStatus('No events found.', true);
        return;
      }

      const table = append ? el.querySelector('tbody') : null;
      const bodyHtml = rows.map(e => `<tr>
        <td>${escapeHtml(e.id)}</td>
        <td>${e.occurredAt ? new Date(e.occurredAt).toLocaleString() : '—'}</td>
        <td>${escapeHtml(e.eventType)}</td>
        <td><span class="state-badge ${String(e.outcome).toUpperCase() === 'DENIED' ? 'state-fail' : 'state-pass'}">${escapeHtml(e.outcome || '—')}</span></td>
        <td>${escapeHtml(e.actorLabel || e.actorId || '—')}</td>
        <td>${escapeHtml([e.targetType, e.targetId].filter(Boolean).join(' ') || '—')}</td>
        <td class="actions-col"><button type="button" class="btn-secondary btn-sm" onclick="openAuditEventDetail(${Number(e.id)})">View</button></td>
      </tr>`).join('');

      if (append && table) {
        table.insertAdjacentHTML('beforeend', bodyHtml);
      } else {
        el.innerHTML = `<div class="table-container"><table class="data-table">
          <thead><tr>
            <th>ID</th><th>When</th><th>Type</th><th>Outcome</th><th>Actor</th><th>Target</th><th></th>
          </tr></thead>
          <tbody>${bodyHtml}</tbody>
        </table></div>`;
      }
      auditStatus(`Loaded ${rows.length} event(s).`, true);
    } catch (e) {
      if (!append) el.innerHTML = `<p class="empty-state">${escapeHtml(e.message)}</p>`;
      auditStatus(e.message, false);
    }
  }

  window.openAuditEventDetail = async function openAuditEventDetail(id) {
    const panel = document.getElementById('audit-event-detail');
    const title = document.getElementById('audit-event-detail-title');
    const body = document.getElementById('audit-event-detail-body');
    if (!panel || !body) return;
    panel.style.display = '';
    title.textContent = `Event #${id}`;
    body.textContent = 'Loading…';
    try {
      const row = await fetchApi('/audit/events/' + id, {}, {}, true);
      body.textContent = JSON.stringify(row, null, 2);
    } catch (e) {
      body.textContent = e.message || String(e);
    }
  };

  window.closeAuditEventDetail = function closeAuditEventDetail() {
    const panel = document.getElementById('audit-event-detail');
    if (panel) panel.style.display = 'none';
  };

  window.verifyAuditChain = async function verifyAuditChain() {
    const result = document.getElementById('audit-verify-result');
    const fromDate = toApiDate(document.getElementById('audit-verify-from')?.value);
    const toDate = toApiDate(document.getElementById('audit-verify-to')?.value);
    if (!fromDate || !toDate) {
      showError('Choose a from and to date for verification');
      return;
    }
    if (result) result.innerHTML = '<p class="empty-state">Verifying…</p>';
    try {
      const data = await fetchApi('/audit/verify', {
        method: 'POST',
        body: JSON.stringify({ fromDate, toDate })
      }, {}, true);
      const ok = !!data.isValid;
      if (result) {
        result.innerHTML = `
          <div class="ops-ready-line">Chain ${ok ? '<span class="state-badge state-pass">PASS</span>' : '<span class="state-badge state-fail">FAIL</span>'}</div>
          <p class="ops-metric-note">${ok
            ? 'Hash chain verified for the selected range.'
            : `Broken at event id <strong>${escapeHtml(data.firstBreakIndex)}</strong>.`}</p>
          <p class="ops-metric-note muted">${new Date(data.fromDate).toLocaleString()} → ${new Date(data.toDate).toLocaleString()}</p>`;
      }
      auditStatus(ok ? 'Chain verified.' : `Chain broken at ${data.firstBreakIndex}.`, ok);
    } catch (e) {
      if (result) result.innerHTML = `<p class="empty-state">${escapeHtml(e.message)}</p>`;
      auditStatus(e.message, false);
    }
  };

  window.queueAuditExport = async function queueAuditExport() {
    const status = document.getElementById('audit-export-status');
    const downloadBtn = document.getElementById('audit-export-download');
    if (downloadBtn) downloadBtn.style.display = 'none';
    queuedExportId = null;
    if (status) status.innerHTML = '<p class="empty-state">Queuing export…</p>';

    try {
      const filters = currentFilters();
      const queued = await fetchApi('/audit/exports', { method: 'POST' }, {
        eventType: filters.eventType,
        outcome: filters.outcome,
        actorId: filters.actorId,
        targetType: filters.targetType,
        targetId: filters.targetId,
        fromDate: filters.fromDate,
        toDate: filters.toDate,
        sourceIp: filters.sourceIp
      }, true);

      const exportId = queued?.id ?? queued?.Id;
      if (!exportId) throw new Error('Export did not return an id');
      queuedExportId = exportId;
      if (status) status.innerHTML = `<p class="ops-metric-note">Export #${exportId} queued — waiting for the worker…</p>`;

      const ready = await pollAuditExport(exportId, status);
      if (status) {
        status.innerHTML = `<p class="ops-metric-note">Export #${exportId} ready (${ready.rowCount ?? '—'} rows). Download when you want the JSON file.</p>`;
      }
      if (downloadBtn) downloadBtn.style.display = '';
      auditStatus(`Export #${exportId} ready.`, true);
    } catch (e) {
      if (status) status.innerHTML = `<p class="empty-state">${escapeHtml(e.message)}</p>`;
      auditStatus(e.message, false);
    }
  };

  async function pollAuditExport(exportId, statusEl) {
    const deadline = Date.now() + 90000;
    let attempt = 0;
    while (Date.now() < deadline) {
      attempt += 1;
      const row = await fetchApi('/audit/exports/' + exportId, {}, {}, true);
      const state = String(row?.state || '').toUpperCase();
      if (state === 'SUCCEEDED' || row?.downloadAvailable) return row;
      if (state === 'FAILED') throw new Error(row?.error || 'Export failed');
      if (statusEl) {
        statusEl.innerHTML = `<p class="ops-metric-note">Export #${exportId} ${state.toLowerCase() || 'pending'}… (${attempt})</p>`;
      }
      await new Promise(r => setTimeout(r, 1000));
    }
    throw new Error('Export timed out — check that the worker is running');
  }

  window.downloadQueuedAuditExport = async function downloadQueuedAuditExport() {
    if (!queuedExportId) {
      showError('Queue an export first');
      return;
    }
    try {
      const token = localStorage.getItem('token');
      const res = await fetch(`/v1/audit/exports/${queuedExportId}/download`, {
        headers: token ? { Authorization: `Bearer ${token}` } : {}
      });
      if (res.status === 401) {
        window.location.href = '/login.html';
        return;
      }
      if (!res.ok) {
        let message = 'Download failed';
        try { const body = await res.json(); message = body?.error?.message || body?.message || message; } catch {}
        throw new Error(message);
      }
      const blob = await res.blob();
      let filename = `audit-export-${queuedExportId}.json`;
      const disposition = res.headers.get('Content-Disposition') || '';
      const match = /filename\*?=(?:UTF-8''|")?([^\";]+)/i.exec(disposition);
      if (match) filename = decodeURIComponent(match[1].replace(/"/g, '').trim());
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = filename;
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(url);
      auditStatus(`Downloaded ${filename}.`, true);
    } catch (e) {
      auditStatus(e.message, false);
      showError(e.message);
    }
  };

  async function loadAuditLegalHolds() {
    const el = document.getElementById('audit-holds');
    if (!el || !isAdmin()) return;
    el.innerHTML = '<p class="empty-state">Loading…</p>';
    try {
      const rows = await fetchApi('/governance/legal-holds', {}, {}, true) || [];
      if (!rows.length) {
        el.innerHTML = '<p class="empty-state">No active legal holds.</p>';
        return;
      }
      el.innerHTML = `<div class="table-container"><table class="data-table">
        <thead><tr><th>ID</th><th>Scope</th><th>Subject</th><th>Reason</th><th>Placed</th></tr></thead>
        <tbody>${rows.map(h => `<tr>
          <td>${escapeHtml(h.id)}</td>
          <td>${escapeHtml(h.scopeType || h.ScopeType || '—')}</td>
          <td>${escapeHtml(h.subjectId || h.SubjectId || '—')}</td>
          <td>${escapeHtml(h.reason || h.Reason || '—')}</td>
          <td>${(h.placedAt || h.PlacedAt) ? new Date(h.placedAt || h.PlacedAt).toLocaleString() : '—'}</td>
        </tr>`).join('')}</tbody>
      </table></div>`;
    } catch (e) {
      el.innerHTML = `<p class="empty-state">${escapeHtml(e.message)}</p>`;
    }
  }
})();
