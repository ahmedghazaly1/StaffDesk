// ============================================
// Platform and API maturity: API keys and webhooks (PL-11..PL-15).
// ============================================
(function () {
  let pfTab = 'keys';
  let pfSelectedWebhookId = null;

  // Shared HTTP helper for performance.js and the platform UI (wraps fetchApi from app.js).
  window.api = async function api(path, options = {}) {
    const endpoint = path.startsWith('/v1') ? path.slice(3) : path;
    return fetchApi(endpoint, options, {}, options.silent !== false);
  };

  window.showPlatform = function showPlatform() {
    showView('platform-view', 'nav-platform', '#platform');
    showPfTab(pfTab);
    loadPfApiKeys();
    loadPfWebhooks();
  };

  window.showPfTab = function showPfTab(tab) {
    pfTab = tab;
    ['keys', 'webhooks'].forEach(t => {
      const btn = document.getElementById('pf-tab-' + t);
      const panel = document.getElementById('pf-panel-' + t);
      if (btn) btn.classList.toggle('active', t === tab);
      if (panel) panel.style.display = t === tab ? '' : 'none';
    });
  };

  function pfStatus(msg, ok) {
    const el = document.getElementById('pf-status');
    if (!el) return;
    el.textContent = msg || '';
    el.style.color = ok === false ? '#c53030' : '#718096';
  }

  // ── API Keys (PL-11/PL-12) ─────────────────────────────────────────────

  window.togglePfKeyForm = function togglePfKeyForm() {
    const form = document.getElementById('pf-key-form');
    form.style.display = form.style.display === 'none' ? 'block' : 'none';
  };

  window.createPfApiKey = async function createPfApiKey(event) {
    event.preventDefault();
    const label = document.getElementById('pf-key-label').value.trim();
    const scopesRaw = document.getElementById('pf-key-scopes').value.trim();
    const expiresRaw = document.getElementById('pf-key-expires').value.trim();
    const scopes = scopesRaw ? scopesRaw.split(',').map(s => s.trim()).filter(Boolean) : [];
    const body = { label, scopes, ownerId: null, expiresInDays: expiresRaw ? parseInt(expiresRaw, 10) : null };
    try {
      const row = await fetchApi('/api-keys', { method: 'POST', body: JSON.stringify(body) });
      document.getElementById('pf-key-form').style.display = 'none';
      document.getElementById('pf-key-created').style.display = 'block';
      document.getElementById('pf-key-raw').textContent = row.rawKey;
      document.getElementById('pf-key-label').value = '';
      document.getElementById('pf-key-scopes').value = '';
      document.getElementById('pf-key-expires').value = '';
      pfStatus('API key created. Copy the raw key now.', true);
      loadPfApiKeys();
    } catch (e) {
      pfStatus(e.message, false);
    }
  };

  window.copyPfKey = function copyPfKey() {
    const text = document.getElementById('pf-key-raw').textContent;
    navigator.clipboard?.writeText(text).then(() => pfStatus('Copied to clipboard.', true));
  };

  async function loadPfApiKeys() {
    const el = document.getElementById('pf-key-list');
    if (!el) return;
    el.innerHTML = '<p class="empty-state">Loading…</p>';
    try {
      const rows = await fetchApi('/api-keys');
      if (!rows || !rows.length) {
        el.innerHTML = '<p class="empty-state">No API keys yet.</p>';
        return;
      }
      el.innerHTML = `<table class="data-table"><thead><tr>
        <th>Label</th><th>Prefix</th><th>Scopes</th><th>Expires</th><th>Last used</th><th></th>
      </tr></thead><tbody>${rows.map(k => `<tr>
        <td>${escapeHtml(k.label)}</td>
        <td><code>${escapeHtml(k.keyPrefix)}…</code></td>
        <td>${escapeHtml(k.scopes || '—')}</td>
        <td>${k.expiresAt ? new Date(k.expiresAt).toLocaleDateString() : 'Never'}</td>
        <td>${k.lastUsedAt ? timeAgo(k.lastUsedAt) : '—'}</td>
        <td><button class="btn-secondary btn-sm" onclick="revokePfApiKey(${k.id}, '${escapeHtml(k.label).replace(/'/g, "\\'")}')">Revoke</button></td>
      </tr>`).join('')}</tbody></table>`;
    } catch (e) {
      el.innerHTML = `<p class="empty-state">${escapeHtml(e.message)}</p>`;
    }
  }

  window.revokePfApiKey = async function revokePfApiKey(id, label) {
    if (!confirm(`Revoke API key "${label}"?`)) return;
    try {
      await fetchApi(`/api-keys/${id}`, { method: 'DELETE', body: JSON.stringify({ reason: 'Revoked from UI' }) });
      pfStatus('Key revoked.', true);
      loadPfApiKeys();
    } catch (e) {
      pfStatus(e.message, false);
    }
  };

  // ── Webhooks (PL-13/PL-14) ─────────────────────────────────────────────

  window.togglePfWebhookForm = function togglePfWebhookForm() {
    const form = document.getElementById('pf-webhook-form');
    form.style.display = form.style.display === 'none' ? 'block' : 'none';
  };

  window.createPfWebhook = async function createPfWebhook(event) {
    event.preventDefault();
    const label = document.getElementById('pf-wh-label').value.trim();
    const targetUrl = document.getElementById('pf-wh-url').value.trim();
    const eventsRaw = document.getElementById('pf-wh-events').value.trim();
    const eventTypes = eventsRaw.split(',').map(s => s.trim()).filter(Boolean);
    try {
      const row = await fetchApi('/webhooks', {
        method: 'POST',
        body: JSON.stringify({ label: label || targetUrl, targetUrl, eventTypes })
      });
      document.getElementById('pf-webhook-form').style.display = 'none';
      document.getElementById('pf-wh-secret').style.display = 'block';
      document.getElementById('pf-wh-secret-val').textContent = row.signingSecret;
      document.getElementById('pf-wh-label').value = '';
      document.getElementById('pf-wh-url').value = '';
      document.getElementById('pf-wh-events').value = '';
      pfStatus('Webhook created. Copy the signing secret now.', true);
      loadPfWebhooks();
    } catch (e) {
      pfStatus(e.message, false);
    }
  };

  async function loadPfWebhooks() {
    const el = document.getElementById('pf-webhook-list');
    if (!el) return;
    el.innerHTML = '<p class="empty-state">Loading…</p>';
    try {
      const rows = await fetchApi('/webhooks');
      if (!rows || !rows.length) {
        el.innerHTML = '<p class="empty-state">No webhook subscriptions.</p>';
        return;
      }
      const admin = typeof isAdmin === 'function' && isAdmin();
      el.innerHTML = `<table class="data-table"><thead><tr>
        <th>Label</th><th>URL</th><th>Events</th><th>Active</th><th>Failures</th><th></th>
      </tr></thead><tbody>${rows.map(w => {
        let events = w.eventTypesJson;
        try { events = JSON.parse(w.eventTypesJson).join(', '); } catch { /* keep raw */ }
        return `<tr>
          <td>${escapeHtml(w.label)}</td>
          <td style="max-width:200px;overflow:hidden;text-overflow:ellipsis;">${escapeHtml(w.targetUrl)}</td>
          <td style="font-size:0.85rem;">${escapeHtml(events)}</td>
          <td>${w.isActive ? '✓' : '✗'}</td>
          <td>${w.consecutiveFailures}/${w.disableAfterConsecutiveFailures}</td>
          <td>
            <button class="btn-secondary btn-sm" onclick="showPfDeliveries(${w.id}, '${escapeHtml(w.label).replace(/'/g, "\\'")}')">Deliveries</button>
            ${w.isActive ? `<button class="btn-secondary btn-sm" onclick="deletePfWebhook(${w.id})">Disable</button>` : ''}
          </td>
        </tr>`;
      }).join('')}</tbody></table>`;
    } catch (e) {
      el.innerHTML = `<p class="empty-state">${escapeHtml(e.message)}</p>`;
    }
  }

  window.deletePfWebhook = async function deletePfWebhook(id) {
    if (!confirm('Disable this webhook subscription?')) return;
    try {
      await fetchApi(`/webhooks/${id}`, { method: 'DELETE' });
      pfStatus('Webhook disabled.', true);
      hidePfDeliveries();
      loadPfWebhooks();
    } catch (e) {
      pfStatus(e.message, false);
    }
  };

  window.showPfDeliveries = async function showPfDeliveries(id, label) {
    pfSelectedWebhookId = id;
    document.getElementById('pf-wh-del-label').textContent = label;
    document.getElementById('pf-webhook-deliveries').style.display = 'block';
    const el = document.getElementById('pf-delivery-list');
    el.innerHTML = '<p class="empty-state">Loading…</p>';
    try {
      const rows = await fetchApi(`/webhooks/${id}/deliveries`, {}, { limit: 50 });
      if (!rows || !rows.length) {
        el.innerHTML = '<p class="empty-state">No deliveries yet.</p>';
        return;
      }
      const admin = typeof isAdmin === 'function' && isAdmin();
      el.innerHTML = `<table class="data-table"><thead><tr>
        <th>Id</th><th>Event</th><th>State</th><th>HTTP</th><th>When</th>${admin ? '<th></th>' : ''}
      </tr></thead><tbody>${rows.map(d => `<tr>
        <td>${d.id}</td>
        <td>${escapeHtml(d.eventType)}</td>
        <td><span class="badge">${d.state}</span></td>
        <td>${d.responseStatusCode ?? '—'}</td>
        <td>${d.deliveredAt ? timeAgo(d.deliveredAt) : timeAgo(d.createdAt)}</td>
        ${admin ? `<td>${d.state === 'FAILED' ? `<button class="btn-secondary btn-sm" onclick="replayPfDelivery(${d.id})">Replay</button>` : ''}</td>` : ''}
      </tr>`).join('')}</tbody></table>`;
    } catch (e) {
      el.innerHTML = `<p class="empty-state">${escapeHtml(e.message)}</p>`;
    }
  };

  window.hidePfDeliveries = function hidePfDeliveries() {
    document.getElementById('pf-webhook-deliveries').style.display = 'none';
    pfSelectedWebhookId = null;
  };

  window.replayPfDelivery = async function replayPfDelivery(deliveryId) {
    if (!isAdmin()) { pfStatus('Admin only.', false); return; }
    try {
      const res = await fetchApi(`/webhooks/deliveries/${deliveryId}/replay`, { method: 'POST', body: '{}' });
      pfStatus(`Replay queued (job #${res.jobId}).`, true);
      if (pfSelectedWebhookId) {
        const label = document.getElementById('pf-wh-del-label').textContent;
        showPfDeliveries(pfSelectedWebhookId, label);
      }
    } catch (e) {
      pfStatus(e.message, false);
    }
  };

  window.loadPfApiKeys = loadPfApiKeys;
  window.loadPfWebhooks = loadPfWebhooks;
})();
