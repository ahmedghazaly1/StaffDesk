// ============================================
// Analytics dashboards (A7.3).
// ============================================

let analyticsTab = 'me';

function ax(key, vars) {
    return typeof t === 'function' ? t(key, vars) : key;
}

function axPhrase(english) {
    if (typeof translatePhrase === 'function') return translatePhrase(english);
    return english;
}

function axCol(col) {
    return axPhrase(col);
}

function axDeptName(name) {
    return axPhrase(name || '');
}

async function prepareAnalyticsForm() {
    try {
        const depts = await fetchApi('/departments');
        const opts = (depts || []).map(d =>
            `<option value="${d.id}">${escapeHtml(axDeptName(d.name))}</option>`).join('');
        const sel = document.getElementById('analytics-dept');
        const other = document.getElementById('analytics-other-dept');
        if (sel) sel.innerHTML = opts;
        if (other) other.innerHTML = opts;
        const to = new Date();
        const from = new Date();
        from.setDate(to.getDate() - 13);
        document.getElementById('analytics-from').value = toIsoDateLocal(from);
        document.getElementById('analytics-to').value = toIsoDateLocal(to);

        const mgr = typeof isManagerRole === 'function' ? isManagerRole() : false;
        const admin = typeof isAdmin === 'function' ? isAdmin() : false;
        document.getElementById('analytics-tab-dept').style.display = (mgr || admin) ? '' : 'none';
        document.getElementById('analytics-tab-flow').style.display = (mgr || admin) ? '' : 'none';
        document.getElementById('analytics-tab-org').style.display = admin ? '' : 'none';
        document.getElementById('analytics-rebuild-btn').style.display = admin ? '' : 'none';

        showAnalyticsTab(admin || mgr ? 'department' : 'me');
    } catch (e) {
        showError(e.message || axPhrase('Failed to prepare analytics'));
    }
}

function toIsoDateLocal(d) {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
}

function showAnalyticsTab(tab) {
    analyticsTab = tab;
    ['me', 'department', 'flow', 'organisation'].forEach(t => {
        const panel = document.getElementById('analytics-panel-' + (t === 'organisation' ? 'organisation' : t));
        if (panel) panel.style.display = t === tab ? 'block' : 'none';
        const btn = document.getElementById('analytics-tab-' + (t === 'organisation' ? 'org' : t === 'department' ? 'dept' : t === 'flow' ? 'flow' : 'me'));
        if (btn) btn.classList.toggle('btn-primary', t === tab);
    });
    const other = document.getElementById('analytics-other-dept-group');
    if (other) other.style.display = tab === 'organisation' ? '' : 'none';
}

function analyticsQuery() {
    const departmentId = document.getElementById('analytics-dept').value;
    const from = document.getElementById('analytics-from').value;
    const to = document.getElementById('analytics-to').value;
    const basis = document.getElementById('analytics-basis').value;
    const granularity = document.getElementById('analytics-granularity').value;
    const overhead = document.getElementById('analytics-overhead').value || 20;
    return { departmentId, from, to, basis, granularity, overhead,
        q: `departmentId=${departmentId}&from=${from}&to=${to}` };
}

async function loadAnalytics(event) {
    if (event) event.preventDefault();
    const { departmentId, from, to, basis, granularity, overhead, q } = analyticsQuery();
    document.getElementById('analytics-status').textContent = axPhrase('Loading…');
    try {
        if (analyticsTab === 'me') {
            const [me, tasks] = await Promise.all([
                fetchApi(`/analytics/me?from=${from}&to=${to}`),
                fetchApi(`/tasks?assigneeId=me&status=OPEN,IN_PROGRESS,BLOCKED,IN_REVIEW&limit=50`)
            ]);
            document.getElementById('analytics-asof').textContent = `${axPhrase('dataAsOf')}: ${me.dataAsOf || '—'}`;
            renderAnalyticsMe(me);
            renderAnalyticsMeTasks(tasks?.items || tasks || []);
        } else if (analyticsTab === 'department') {
            const [throughput, wip, sla, quality, workload, dq] = await Promise.all([
                fetchApi(`/analytics/throughput?${q}&granularity=${granularity}`),
                fetchApi(`/analytics/wip?${q}&granularity=${granularity}`),
                fetchApi(`/analytics/sla?${q}&granularity=${granularity}`),
                fetchApi(`/analytics/quality?${q}`),
                fetchApi(`/analytics/workload?${q}&overheadPercent=${overhead}`),
                fetchApi(`/analytics/data-quality?${q}`)
            ]);
            document.getElementById('analytics-asof').textContent = `${axPhrase('dataAsOf')}: ${throughput.dataAsOf || '—'}`;
            renderAnalyticsSeries('analytics-throughput', throughput.series || [], ['throughput', 'arrivals', 'backlog']);
            renderAnalyticsWip(wip);
            renderAnalyticsSla(sla);
            renderAnalyticsQuality(quality);
            renderAnalyticsSeries('analytics-workload', workload.series || [], ['capacityWorkingMinutes', 'committedLoadMinutes', 'utilisationPercent', 'unestimatedTaskCount']);
            renderAnalyticsDq(dq);
        } else if (analyticsTab === 'flow') {
            const flow = await fetchApi(`/analytics/flow?${q}&basis=${encodeURIComponent(basis)}&granularity=${granularity}`);
            document.getElementById('analytics-asof').textContent = `${axPhrase('dataAsOf')}: ${flow.dataAsOf || '—'}`;
            renderAnalyticsFlow(flow);
            renderAnalyticsSeries('analytics-flow-series', flow.series || [], ['n', 'throughput']);
            renderAnalyticsTis(flow.overall?.timeInStatus || {});
        } else {
            const otherId = document.getElementById('analytics-other-dept').value;
            const [org, compare] = await Promise.all([
                fetchApi(`/analytics/organisation?from=${from}&to=${to}&basis=${encodeURIComponent(basis)}`),
                fetchApi(`/analytics/compare?${q}&mode=department&otherDepartmentId=${otherId}&basis=${encodeURIComponent(basis)}`)
            ]);
            document.getElementById('analytics-asof').textContent = `${axPhrase('dataAsOf')}: ${org.dataAsOf || '—'}`;
            renderAnalyticsOrg(org);
            renderAnalyticsCompare(compare);
        }
        document.getElementById('analytics-status').textContent = axPhrase('Loaded.');
        if (typeof scheduleLocalize === 'function') scheduleLocalize(document.getElementById('analytics-view'));
    } catch (e) {
        document.getElementById('analytics-status').textContent = '';
        showError(e.message || axPhrase('Failed to load analytics'));
    }
}

function fmtPct(p) {
    if (!p || p.status === 'insufficient_data') return axPhrase('insufficient_data');
    return `p50=${p.p50 ?? '—'} / p85=${p.p85 ?? '—'} / p95=${p.p95 ?? '—'} (n=${p.n})`;
}

function fmtRatio(r) {
    if (!r) return '—';
    if (r.status === 'insufficient_data') return `${axPhrase('insufficient_data')} (${r.numerator}/${r.denominator})`;
    return `${r.value ?? '—'}% (${r.numerator}/${r.denominator})`;
}

function renderAnalyticsMe(me) {
    const el = document.getElementById('analytics-me');
    el.innerHTML = `<p class="muted">${escapeHtml(me.caveat || '')}</p>
        <p>${axPhrase('My throughput')}: <strong>${me.myThroughput ?? 0}</strong></p>
        <p>${axPhrase('My rework rate')}: ${fmtRatio(me.myReworkRate)} <em>${axPhrase('(system indicator — interpret carefully)')}</em></p>
        <p>${axPhrase('My reopen rate')}: ${fmtRatio(me.myReopenRate)}</p>` +
        (me.series?.length
            ? `<table class="data-table"><thead><tr><th>${axCol('Date')}</th><th>${axCol('throughput')}</th><th>${axCol('rework')}</th><th>${axCol('reopen')}</th></tr></thead>
               <tbody>${me.series.map(r => `<tr><td>${r.date}</td><td>${r.throughput}</td><td>${r.reworkTransitions}</td><td>${r.reopenTransitions}</td></tr>`).join('')}</tbody></table>`
            : '');
}

function renderAnalyticsMeTasks(items) {
    const el = document.getElementById('analytics-me-tasks');
    const rows = Array.isArray(items) ? items : [];
    if (!rows.length) {
        el.innerHTML = `<p class="empty-state">${axPhrase('No active assigned tasks.')}</p>`;
        return;
    }
    const byPri = {};
    rows.forEach(task => {
        const p = task.priority || 'NORMAL';
        byPri[p] = (byPri[p] || 0) + 1;
    });
    const atRisk = rows.filter(task => task.breachState === 'AT_RISK' || task.breachState === 'BREACHED');
    const priLabel = Object.entries(byPri).map(([k, v]) =>
        `${typeof translatePriority === 'function' ? translatePriority(k) : axPhrase(k)}=${v}`).join(', ');
    el.innerHTML = `<p>${axPhrase('By priority')}: ${priLabel}</p>
        <p>${axPhrase('At risk / breached')}: <strong>${atRisk.length}</strong></p>
        <table class="data-table"><thead><tr>
            <th>${axPhrase('Key')}</th><th>${axPhrase('Title')}</th><th>${axPhrase('Status')}</th>
            <th>${axPhrase('Priority')}</th><th>${axPhrase('SLA')}</th>
        </tr></thead>
        <tbody>${rows.slice(0, 30).map(task => `<tr>
            <td>${escapeHtml(task.key || '')}</td>
            <td>${escapeHtml(task.title || '')}</td>
            <td>${escapeHtml(typeof translateStatus === 'function' ? translateStatus(task.status) : (task.status || ''))}</td>
            <td>${escapeHtml(typeof translatePriority === 'function' ? translatePriority(task.priority) : (task.priority || ''))}</td>
            <td>${escapeHtml(task.breachState ? axPhrase(task.breachState === 'AT_RISK' ? 'At Risk' : task.breachState === 'BREACHED' ? 'Breached' : task.breachState === 'ON_TRACK' ? 'On Track' : task.breachState) : '—')}</td>
        </tr>`).join('')}</tbody></table>`;
}

function renderAnalyticsFlow(flow) {
    const el = document.getElementById('analytics-flow');
    const o = flow.overall || {};
    el.innerHTML = `<p><strong>${axPhrase('Lead')}</strong> ${fmtPct(o.lead)}</p>
        <p><strong>${axPhrase('Cycle')}</strong> ${fmtPct(o.cycle)}</p>
        <p><strong>${axPhrase('Reaction')}</strong> ${fmtPct(o.reaction)}</p>
        <p><strong>${axPhrase('Triage latency')}</strong> ${fmtPct(o.triageLatency)}</p>
        <p><strong>${axPhrase('Flow efficiency')}</strong> ${fmtPct(o.flowEfficiency)}</p>
        <p><strong>${axPhrase('Blocked')}</strong> ${fmtPct(o.blocked)}</p>
        <p class="muted">basis=${escapeHtml(axPhrase(flow.basis || '') || flow.basis || '')}${flow.includesBackfilled ? ' · ' + axPhrase('includes backfilled intervals') : ''}</p>`;
}

function renderAnalyticsTis(tis) {
    const el = document.getElementById('analytics-tis');
    const keys = Object.keys(tis || {});
    if (!keys.length) {
        el.innerHTML = `<p class="empty-state">${axPhrase('No time-in-status samples.')}</p>`;
        return;
    }
    el.innerHTML = `<table class="data-table"><thead><tr><th>${axPhrase('Status')}</th><th>${axPhrase('Percentiles')}</th></tr></thead>
        <tbody>${keys.map(k => `<tr><td>${escapeHtml(typeof translateStatus === 'function' ? translateStatus(k) : axPhrase(k))}</td><td>${fmtPct(tis[k])}</td></tr>`).join('')}</tbody></table>`;
}

function renderAnalyticsSeries(containerId, series, cols) {
    const el = document.getElementById(containerId);
    if (!el) return;
    if (!series.length) {
        el.innerHTML = `<p class="empty-state">${axPhrase('No snapshot data in range. Run rebuild or wait for nightly rollup.')}</p>`;
        return;
    }
    const periodKey = series[0].date != null ? 'date' : 'period';
    el.innerHTML = `<table class="data-table"><thead><tr><th>${axCol(periodKey)}</th>${cols.map(c => `<th>${axCol(c)}</th>`).join('')}</tr></thead>
        <tbody>${series.map(row => `<tr><td>${row[periodKey] ?? row.date ?? row.period}</td>${cols.map(c => `<td>${row[c] ?? '—'}</td>`).join('')}</tr>`).join('')}</tbody></table>`;
}

function renderAnalyticsWip(wip) {
    const a = wip.agingBuckets || {};
    const el = document.getElementById('analytics-wip');
    const series = wip.series || [];
    const periodKey = series[0]?.date != null ? 'date' : 'period';
    const cols = ['open', 'inProgress', 'blocked', 'inReview', 'total'];
    el.innerHTML = `<p>${axPhrase('Aging')}: 0–1d=${a.d0_1 ?? 0}, 1–3d=${a.d1_3 ?? 0}, 3–7d=${a.d3_7 ?? 0}, 7–14d=${a.d7_14 ?? 0}, 14d+=${a.d14_plus ?? 0}</p>` +
        (series.length
            ? `<table class="data-table"><thead><tr><th>${axCol(periodKey)}</th>${cols.map(c => `<th>${axCol(c)}</th>`).join('')}</tr></thead>
               <tbody>${series.map(row => `<tr><td>${row[periodKey] ?? row.date ?? row.period}</td>${cols.map(c => `<td>${row[c] ?? '—'}</td>`).join('')}</tr>`).join('')}</tbody></table>`
            : `<p class="empty-state">${axPhrase('No WIP snapshot data.')}</p>`);
}

function renderAnalyticsSla(sla) {
    const el = document.getElementById('analytics-sla');
    const tot = sla.totals || {};
    el.innerHTML = `<p>${axPhrase('Attainment')}: ${fmtRatio(sla.attainment)}</p>
        <p>${axPhrase('Peak breached')}: <strong>${tot.breachedPeak ?? 0}</strong> · ${axPhrase('Peak at-risk')}: <strong>${tot.atRiskPeak ?? 0}</strong></p>`;
    renderAnalyticsSeries('analytics-sla-series', sla.series || [], ['breached', 'atRisk', 'onTrack']);
}

function renderAnalyticsQuality(q) {
    const el = document.getElementById('analytics-quality');
    const cats = q.reworkByCategory || {};
    el.innerHTML = `<p>${axPhrase('Completed')}: ${q.completed ?? 0}</p>
        <p>${axPhrase('Rework rate')}: ${fmtRatio(q.reworkRate)}</p>
        <p>${axPhrase('Reopen rate')}: ${fmtRatio(q.reopenRate)}</p>
        <p>${axPhrase('First-pass yield')}: ${fmtRatio(q.firstPassYield)}</p>
        <p>${axPhrase('On-time completion')}: ${fmtRatio(q.onTimeCompletion)}</p>
        <p>${axPhrase('Estimate accuracy')}: ${fmtPct(q.estimateAccuracy)}</p>
        <p>${axPhrase('Rework by category')}: ${Object.keys(cats).length ? Object.entries(cats).map(([k, v]) => `${axPhrase(k)}=${v}`).join(', ') : '—'}</p>`;
}

function renderAnalyticsDq(dq) {
    const el = document.getElementById('analytics-dq');
    const c = dq.coverage || {};
    el.innerHTML = `<p>${axPhrase('Snapshot coverage')}: ${c.daysWithSnapshot ?? 0}/${c.expectedDays ?? 0} (missing ${c.missingDays ?? 0})</p>
        <p>${axPhrase('No-estimate active')}: ${dq.tasksWithNoEstimate ?? 0}</p>
        <p>Stale open intervals (&gt;${dq.openIntervalsOlderThanDays ?? 14}d): ${dq.staleOpenIntervals ?? 0}</p>
        <p>${axPhrase('Backfilled days')}: ${dq.includesBackfilledDays ?? 0} · ${axPhrase('interval count')}: ${dq.backfilledIntervalCount ?? 0}</p>
        <p>${axPhrase('Completed without IN_PROGRESS')}: ${dq.completedWithoutInProgress ?? 0}</p>
        <p>${axPhrase('Unestimated completion rate')}: ${fmtRatio(dq.unestimatedCompletionRate)}</p>
        <p>${axPhrase('Suppression threshold')} n≥${dq.suppressionThreshold ?? 5}</p>`;
}

function renderAnalyticsOrg(org) {
    const el = document.getElementById('analytics-org');
    const rows = org.departments || [];
    if (!rows.length) {
        el.innerHTML = `<p class="empty-state">${axPhrase('No departments.')}</p>`;
        return;
    }
    el.innerHTML = `<table class="data-table"><thead><tr>
        <th>${axPhrase('Dept')}</th><th>${axPhrase('Throughput')}</th><th>${axPhrase('Arrivals')}</th>
        <th>${axPhrase('Backlog')}</th><th>${axPhrase('Utilisation')}</th><th>${axPhrase('Unestimated')}</th><th>${axPhrase('SLA')}</th>
    </tr></thead><tbody>${rows.map(r => {
        const m = r.metrics || {};
        return `<tr>
            <td>${r.departmentName ? escapeHtml(axDeptName(r.departmentName)) : r.departmentId}</td>
            <td>${m.throughput ?? 0}</td>
            <td>${m.arrivals ?? 0}</td>
            <td>${r.backlog ?? 0}</td>
            <td>${r.utilisationPercent ?? '—'}</td>
            <td>${r.unestimatedTaskCount ?? 0}</td>
            <td>${fmtRatio(r.slaAttainment)}</td>
        </tr>`;
    }).join('')}</tbody></table>`;
}

function renderAnalyticsCompare(cmp) {
    const el = document.getElementById('analytics-compare');
    const d = cmp.delta || {};
    el.innerHTML = `<p>${axPhrase('Mode')}: ${escapeHtml(axPhrase(cmp.mode || '') || cmp.mode || '')}</p>
        <pre style="white-space:pre-wrap;font-size:12px;">${escapeHtml(JSON.stringify({ left: cmp.left, right: cmp.right, delta: d }, null, 2))}</pre>`;
}

async function rebuildAnalytics() {
    if (!isAdmin()) {
        showRestricted();
        return;
    }
    const { departmentId, from, to } = analyticsQuery();
    try {
        const res = await fetchApi('/analytics/rebuild', {
            method: 'POST',
            body: JSON.stringify({ from, to, departmentId: Number(departmentId) })
        });
        document.getElementById('analytics-status').textContent =
            ax('ui.rebuild_queued', { id: res.jobId }) !== 'ui.rebuild_queued'
                ? ax('ui.rebuild_queued', { id: res.jobId })
                : `Rebuild queued (job ${res.jobId}). Re-load after the worker processes it.`;
    } catch (e) {
        showError(e.message || axPhrase('Rebuild failed'));
    }
}

async function exportAnalytics() {
    const status = document.getElementById('analytics-status');
    const btn = document.querySelector('button[onclick="exportAnalytics()"]');
    const { departmentId, from, to, basis, granularity, overhead } = analyticsQuery();

    if (!departmentId || !from || !to) {
        showError(axPhrase('Choose a department and date range before exporting'));
        return;
    }

    const report = analyticsTab === 'flow' ? 'flow'
        : analyticsTab === 'organisation' ? 'compare'
        : analyticsTab === 'department' ? 'throughput'
        : 'throughput';

    const setStatus = (msg) => { if (status) status.textContent = msg; };
    if (btn) btn.disabled = true;

    try {
        setStatus(`${axPhrase('Queuing')} ${axPhrase(report)} CSV…`);
        const queued = await fetchApi('/analytics/exports', {
            method: 'POST',
            body: JSON.stringify({
                report,
                departmentId: Number(departmentId),
                from,
                to,
                basis,
                granularity,
                overheadPercent: Number(overhead),
                mode: analyticsTab === 'organisation' ? 'department' : 'period',
                otherDepartmentId: Number(document.getElementById('analytics-other-dept')?.value || departmentId)
            })
        }, {}, true);

        const exportId = queued?.exportId ?? queued?.id;
        if (!exportId) throw new Error(axPhrase('Export did not return an id'));

        setStatus(`${axPhrase('Export')} #${exportId} — ${axPhrase('Loading…')}`);
        const ready = await pollAnalyticsExport(exportId, setStatus);
        setStatus(`${axPhrase('Export')} #${exportId} — ${axPhrase('Download')}`);
        await downloadAnalyticsExport(exportId, `analytics-${report}-${from}-to-${to}.csv`);
        setStatus(`${axPhrase('Export CSV')} (${ready.rowCount ?? '—'})`);
        return true;
    } catch (e) {
        setStatus(e.message || axPhrase('Export failed'));
        showError(e.message || axPhrase('Export failed'));
        throw e;
    } finally {
        if (btn) btn.disabled = false;
    }
}

async function pollAnalyticsExport(exportId, setStatus) {
    const deadline = Date.now() + 90000;
    let attempt = 0;
    while (Date.now() < deadline) {
        attempt += 1;
        const row = await fetchApi(`/analytics/exports/${exportId}`, {}, {}, true);
        const state = (row?.state || row?.State || '').toUpperCase();
        if (state === 'SUCCEEDED' || row?.downloadReady === true) return row;
        if (state === 'FAILED') {
            throw new Error(row?.error || row?.Error || axPhrase('Export job failed'));
        }
        setStatus(`${axPhrase('Export')} #${exportId} ${state.toLowerCase() || 'pending'}… (${attempt})`);
        await new Promise(r => setTimeout(r, 1000));
    }
    throw new Error(axPhrase('Export timed out — check that the worker is running, then try again'));
}

async function downloadAnalyticsExport(exportId, fallbackName) {
    const token = localStorage.getItem('token');
    const res = await fetch(`/v1/analytics/exports/${exportId}/download`, {
        headers: token ? { Authorization: `Bearer ${token}` } : {}
    });
    if (res.status === 401) {
        window.location.href = '/login.html';
        throw new Error(axPhrase('Session expired'));
    }
    if (!res.ok) {
        let message = axPhrase('Download failed');
        try {
            const body = await res.json();
            message = body?.message || body?.error || message;
        } catch { /* binary or empty */ }
        throw new Error(message);
    }

    const blob = await res.blob();
    let filename = fallbackName;
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
}
