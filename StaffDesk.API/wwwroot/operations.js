// ============================================
// Operations and observability: jobs, alerts, health (OB-1..OB-4).
// ============================================
(function () {
  let pgJobFilter = 'DEAD';

  window.showOperations = function showOperations() {
    if (!isAdmin()) {
      showRestricted();
      return;
    }
    showView('operations-view', 'nav-operations', '#operations');
    loadOperationsSummary();
    loadOperationsJobs(pgJobFilter);
    loadOperationsAlerts();
  };

  function pgStatus(msg, ok) {
    const el = document.getElementById('pg-status');
    if (!el) return;
    const text = msg || '';
    el.textContent = text;
    el.hidden = !text;
    el.classList.toggle('is-error', ok === false);
    el.classList.toggle('is-ok', ok === true && !!text);
  }

  function statusBadge(status) {
    const key = String(status || '').toUpperCase();
    const cls = key === 'PASS' ? 'state-pass'
      : key === 'FAIL' ? 'state-fail'
      : key === 'WARN' ? 'state-warn'
      : 'state-info';
    return `<span class="state-badge ${cls}">${escapeHtml(key || '—')}</span>`;
  }

  function jobStateBadge(state) {
    const key = String(state || '').toUpperCase();
    const cls = key === 'DEAD' ? 'state-dead'
      : key === 'QUEUED' ? 'state-queued'
      : key === 'RUNNING' ? 'state-running'
      : 'state-info';
    return `<span class="state-badge ${cls}">${escapeHtml(key || '—')}</span>`;
  }

  function severityBadge(severity) {
    const key = String(severity || '').toLowerCase();
    const cls = key === 'critical' || key === 'error' || key === 'high' ? 'state-high'
      : key === 'medium' || key === 'warn' || key === 'warning' ? 'state-medium'
      : key === 'low' ? 'state-low'
      : 'state-info';
    return `<span class="state-badge ${cls}">${escapeHtml(severity || '—')}</span>`;
  }

  function formatWhen(value) {
    if (!value) return '—';
    const d = new Date(value);
    return Number.isNaN(d.getTime()) ? '—' : d.toLocaleString();
  }

  function formatAge(seconds) {
    if (seconds == null || Number.isNaN(Number(seconds))) return '—';
    const s = Math.max(0, Math.round(Number(seconds)));
    if (s < 60) return s + 's';
    if (s < 3600) return Math.round(s / 60) + 'm';
    return (s / 3600).toFixed(1) + 'h';
  }

  window.refreshOperations = function refreshOperations() {
    loadOperationsSummary();
    loadOperationsJobs(pgJobFilter);
    loadOperationsAlerts();
    pgStatus('Refreshed.', true);
  };

  window.pgFilterJobs = function pgFilterJobs(state) {
    pgJobFilter = state || '';
    ['all', 'queued', 'running', 'dead'].forEach(s => {
      const btn = document.getElementById('pg-job-tab-' + s);
      if (btn) btn.classList.toggle('active', (s === 'all' && !state) || s === (state || '').toLowerCase());
    });
    loadOperationsJobs(pgJobFilter);
  };

  function renderMetrics(summary) {
    const el = document.getElementById('pg-metrics');
    if (!el) return;

    const j = summary.jobs || {};
    const req = summary.requests || {};
    const roll = summary.rollups || {};
    const chain = summary.auditChain || {};

    const dead = j.dead ?? 0;
    const errorRate = req.errorRatePercent ?? 0;
    const stale = !!roll.isStale;
    const auditOk = !!chain.isValid;

    el.innerHTML = `
      <article class="ops-metric">
        <span class="ops-metric-label">Job queue</span>
        <div class="ops-metric-value">${dead} dead</div>
        <div class="ops-metric-meta">
          <span>Queued <strong>${j.queued ?? 0}</strong></span>
          <span>Running <strong>${j.running ?? 0}</strong></span>
          <span>Oldest <strong>${formatAge(j.oldestQueuedAgeSeconds)}</strong></span>
        </div>
      </article>
      <article class="ops-metric">
        <span class="ops-metric-label">HTTP</span>
        <div class="ops-metric-value">${escapeHtml(String(errorRate))}% errors</div>
        <div class="ops-metric-meta">
          <span>Requests <strong>${req.totalRequests ?? 0}</strong></span>
          <span>5xx <strong>${req.totalErrors ?? 0}</strong></span>
        </div>
      </article>
      <article class="ops-metric">
        <span class="ops-metric-label">Metric rollups</span>
        <div class="ops-metric-value">${roll.stalenessHours != null ? roll.stalenessHours.toFixed(1) + 'h' : '—'}</div>
        <div class="ops-metric-meta">
          <span>${stale ? statusBadge('WARN') : statusBadge('PASS')}</span>
          <span>Latest <strong>${formatWhen(roll.latestComputedAt)}</strong></span>
        </div>
      </article>
      <article class="ops-metric">
        <span class="ops-metric-label">Audit chain</span>
        <div class="ops-metric-value">${auditOk ? statusBadge('PASS') : statusBadge('FAIL')}</div>
        <p class="ops-metric-note">${escapeHtml(chain.detail || (auditOk ? 'Hash chain is intact.' : 'Chain verification failed.'))}</p>
      </article>`;
  }

  async function loadOperationsSummary() {
    const el = document.getElementById('pg-readiness');
    if (!el) return;

    el.innerHTML = '<p class="empty-state">Loading…</p>';
    try {
      const summary = await fetchApi('/operations/summary');
      renderMetrics(summary);

      const r = summary.readiness || {};
      const checks = r.checks || [];
      const readyLine = r.ready ? statusBadge('PASS') : statusBadge('FAIL');
      if (!checks.length) {
        el.innerHTML = `<p class="empty-state">No readiness checks returned. Overall: ${readyLine}</p>`;
      } else {
        el.innerHTML = `
          <p class="ops-ready-line">Overall ready ${readyLine}</p>
          <div class="table-container">
            <table class="data-table">
              <thead><tr><th>Check</th><th>Status</th><th>Detail</th></tr></thead>
              <tbody>${checks.map(c => `<tr>
                <td>${escapeHtml(c.name)}</td>
                <td>${statusBadge(c.status)}</td>
                <td>${escapeHtml(c.detail || '—')}</td>
              </tr>`).join('')}</tbody>
            </table>
          </div>`;
      }

      pgStatus('Summary loaded.', true);
    } catch (e) {
      el.innerHTML = `<p class="empty-state">${escapeHtml(e.message)}</p>`;
      pgStatus(e.message, false);
    }
  }

  async function loadOperationsAlerts() {
    const el = document.getElementById('pg-alerts');
    if (!el) return;
    el.innerHTML = '<p class="empty-state">Loading…</p>';
    try {
      const data = await fetchApi('/operations/alerts');
      const alerts = data.alerts || [];
      if (!alerts.length) {
        el.innerHTML = '<p class="empty-state">No active alerts.</p>';
        return;
      }
      el.innerHTML = `<div class="table-container"><table class="data-table">
        <thead><tr><th>ID</th><th>Severity</th><th>Message</th><th>Runbook</th></tr></thead>
        <tbody>${alerts.map(a => `<tr>
          <td>${escapeHtml(a.id)}</td>
          <td>${severityBadge(a.severity)}</td>
          <td>${escapeHtml(a.message)}</td>
          <td><code>${escapeHtml(a.runbook || '—')}</code></td>
        </tr>`).join('')}</tbody>
      </table></div>`;
    } catch (e) {
      el.innerHTML = `<p class="empty-state">${escapeHtml(e.message)}</p>`;
    }
  }

  async function loadOperationsJobs(state) {
    const el = document.getElementById('pg-jobs-list');
    if (!el) return;
    el.innerHTML = '<p class="empty-state">Loading…</p>';
    try {
      const qs = state ? `?state=${encodeURIComponent(state)}&page=1&limit=50` : '?page=1&limit=50';
      const payload = await fetchApi('/jobs' + qs);
      const rows = payload?.data || (Array.isArray(payload) ? payload : []);
      if (!rows.length) {
        el.innerHTML = '<p class="empty-state">No jobs in this filter.</p>';
        return;
      }
      el.innerHTML = `<div class="table-container"><table class="data-table">
        <thead><tr>
          <th>ID</th><th>Type</th><th>State</th><th>Attempts</th><th>Error</th><th>Updated</th><th class="actions-col"></th>
        </tr></thead>
        <tbody>${rows.map(j => `<tr>
          <td>${escapeHtml(j.id)}</td>
          <td>${escapeHtml(j.type)}</td>
          <td>${jobStateBadge(j.state)}</td>
          <td>${escapeHtml(j.attemptCount)}</td>
          <td class="cell-ellipsis" title="${escapeHtml(j.lastError || '')}">${escapeHtml(j.lastError || '—')}</td>
          <td>${formatWhen(j.updatedAt)}</td>
          <td class="actions-col">${j.state === 'DEAD' ? `<button class="btn-secondary btn-sm" onclick="pgRequeueJob(${Number(j.id)})">Requeue</button>` : ''}</td>
        </tr>`).join('')}</tbody>
      </table></div>`;
    } catch (e) {
      el.innerHTML = `<p class="empty-state">${escapeHtml(e.message)}</p>`;
    }
  }

  window.pgRequeueJob = async function pgRequeueJob(id) {
    if (!isAdmin()) { pgStatus('Admin only.', false); return; }
    try {
      await fetchApi('/jobs/' + id + '/requeue', { method: 'POST' });
      pgStatus('Job ' + id + ' requeued.', true);
      loadOperationsJobs(pgJobFilter);
      loadOperationsSummary();
    } catch (e) {
      pgStatus(e.message, false);
    }
  };

  window.pgProbeHealth = async function pgProbeHealth() {
    const el = document.getElementById('pg-probes');
    if (!el) return;
    el.innerHTML = '<p class="empty-state">Probing…</p>';
    try {
      const liveRes = await fetch('/v1/health/live');
      const readyRes = await fetch('/v1/health/ready');
      const live = await liveRes.json();
      const ready = await readyRes.json();
      const liveOk = liveRes.ok && String(live.status || '').toLowerCase() !== 'fail';
      const readyOk = readyRes.ok && (ready.ready === true || String(ready.status || '').toLowerCase() === 'ready');
      const readyChecks = Array.isArray(ready.checks) ? ready.checks : [];
      el.innerHTML = `<div class="ops-probe-grid">
        <article class="ops-probe-card">
          <div class="ops-metric-label">Live</div>
          <div class="ops-ready-line">${liveOk ? statusBadge('PASS') : statusBadge('FAIL')} HTTP ${liveRes.status}</div>
          <p class="ops-metric-note">${escapeHtml(live.status || 'No status returned.')}</p>
        </article>
        <article class="ops-probe-card">
          <div class="ops-metric-label">Ready</div>
          <div class="ops-ready-line">${readyOk ? statusBadge('PASS') : statusBadge('FAIL')} HTTP ${readyRes.status}</div>
          ${readyChecks.length ? `<ul class="ops-probe-checks">${readyChecks.map(c =>
            `<li><span>${escapeHtml(c.name)}</span>${statusBadge(c.status)}</li>`).join('')}</ul>` : ''}
        </article>
      </div>`;
    } catch (e) {
      el.innerHTML = `<p class="empty-state">${escapeHtml(e.message)}</p>`;
    }
  };
})();
