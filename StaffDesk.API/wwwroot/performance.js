// ============================================
// Performance management (section 8.10 endpoints only).
// ============================================
(function () {
  let peTab = 'mine';
  let lastCycleId = localStorage.getItem('peCycleId') || '';
  let lastReviewId = localStorage.getItem('peReviewId') || '';
  let lastGoalId = localStorage.getItem('peGoalId') || '';

  function peRole() {
    return localStorage.getItem('role') || '';
  }
  function isHr() { return peRole() === 'HR_ADMIN'; }
  function isMgr() { return peRole() === 'Manager' || isHr(); }
  function isAdminTech() { return peRole() === 'Admin' || peRole() === 'Auditor'; }

  window.showPerformance = function showPerformance() {
    showView('performance-view', 'nav-performance', '#performance');
    document.getElementById('pe-hr-panel').style.display = isHr() ? '' : 'none';
    document.getElementById('pe-cal-panel').style.display = isMgr() ? '' : 'none';
    document.getElementById('pe-admin-block').style.display = isAdminTech() ? '' : 'none';
    document.getElementById('pe-main').style.display = isAdminTech() ? 'none' : '';
    if (lastCycleId) document.getElementById('pe-cycle-id').value = lastCycleId;
    if (lastReviewId) document.getElementById('pe-review-id').value = lastReviewId;
    if (lastGoalId) document.getElementById('pe-goal-id').value = lastGoalId;
    showPeTab(peTab);
  };

  window.showPeTab = function showPeTab(tab) {
    peTab = tab;
    ['mine', 'assess', 'cycle', 'calibration', 'goals', 'competencies'].forEach(t => {
      const btn = document.getElementById('pe-tab-' + t);
      const panel = document.getElementById('pe-panel-' + t);
      if (btn) btn.classList.toggle('active', t === tab);
      if (panel) panel.style.display = t === tab ? '' : 'none';
    });
  };

  function peStatus(msg, ok) {
    const el = document.getElementById('pe-status');
    if (!el) return;
    el.textContent = msg || '';
    el.style.color = ok === false ? '#c53030' : '#2d3748';
  }

  function peOut(id, data) {
    const el = document.getElementById(id);
    if (el) el.textContent = typeof data === 'string' ? data : JSON.stringify(data, null, 2);
  }

  window.peLoadMine = async function peLoadMine() {
    try {
      const rows = await api('/v1/reviews/mine');
      peOut('pe-mine-out', rows);
      peStatus('Loaded my reviews.', true);
    } catch (e) { peStatus(e.message || String(e), false); }
  };

  window.peLoadReview = async function peLoadReview() {
    const id = document.getElementById('pe-review-id').value.trim();
    if (!id) return peStatus('Enter review id', false);
    try {
      const row = await api('/v1/reviews/' + id);
      lastReviewId = id;
      localStorage.setItem('peReviewId', id);
      peOut('pe-review-out', row);
      peStatus('Loaded review ' + id, true);
    } catch (e) { peStatus(e.message || String(e), false); }
  };

  window.peSaveSelf = async function peSaveSelf(submit) {
    const id = document.getElementById('pe-review-id').value.trim();
    const rating = parseInt(document.getElementById('pe-self-rating').value, 10);
    const justification = document.getElementById('pe-self-just').value;
    try {
      const row = await api('/v1/reviews/' + id + '/self-assessment', {
        method: 'PUT',
        etagKey: 'review:' + id,
        body: JSON.stringify({ overallRating: rating, justification, scores: [], submit: !!submit })
      });
      peOut('pe-review-out', row);
      peStatus(submit ? 'Self-assessment submitted.' : 'Self-assessment saved.', true);
    } catch (e) { peStatus(e.message || String(e), false); }
  };

  window.peSaveManager = async function peSaveManager(submit) {
    const id = document.getElementById('pe-review-id').value.trim();
    const rating = parseInt(document.getElementById('pe-mgr-rating').value, 10);
    const justification = document.getElementById('pe-mgr-just').value;
    try {
      const row = await api('/v1/reviews/' + id + '/manager-assessment', {
        method: 'PUT',
        etagKey: 'review:' + id,
        body: JSON.stringify({ overallRating: rating, justification, scores: [], submit: !!submit })
      });
      peOut('pe-review-out', row);
      peStatus(submit ? 'Manager assessment submitted.' : 'Manager assessment saved.', true);
    } catch (e) { peStatus(e.message || String(e), false); }
  };

  window.peAttachEvidence = async function peAttachEvidence() {
    const id = document.getElementById('pe-review-id').value.trim();
    const note = document.getElementById('pe-evidence-note').value;
    try {
      const row = await api('/v1/reviews/' + id + '/evidence', {
        method: 'POST',
        body: JSON.stringify({ evidenceType: 'NOTE', note })
      });
      peStatus('Evidence attached #' + row.id, true);
    } catch (e) { peStatus(e.message || String(e), false); }
  };

  window.peRespond = async function peRespond() {
    const id = document.getElementById('pe-review-id').value.trim();
    const response = document.getElementById('pe-response').value;
    try {
      await api('/v1/reviews/' + id + '/response', { method: 'POST', body: JSON.stringify({ response }) });
      peStatus('Response recorded.', true);
    } catch (e) { peStatus(e.message || String(e), false); }
  };

  window.peAppeal = async function peAppeal() {
    const id = document.getElementById('pe-review-id').value.trim();
    const ground = document.getElementById('pe-appeal').value;
    try {
      await api('/v1/reviews/' + id + '/appeal', { method: 'POST', body: JSON.stringify({ ground }) });
      peStatus('Appeal raised.', true);
    } catch (e) { peStatus(e.message || String(e), false); }
  };

  window.peNominatePeers = async function peNominatePeers() {
    const id = document.getElementById('pe-review-id').value.trim();
    const ids = document.getElementById('pe-peer-ids').value.split(',').map(s => parseInt(s.trim(), 10)).filter(Boolean);
    try {
      const row = await api('/v1/reviews/' + id + '/peer-invitations', {
        method: 'POST',
        body: JSON.stringify({ nomineeEmployeeIds: ids })
      });
      peOut('pe-review-out', row);
      peStatus('Peer invitations updated.', true);
    } catch (e) { peStatus(e.message || String(e), false); }
  };

  window.peSubmitPeer = async function peSubmitPeer() {
    const token = document.getElementById('pe-peer-token').value.trim();
    const rating = parseInt(document.getElementById('pe-peer-rating').value, 10);
    const justification = document.getElementById('pe-peer-just').value;
    try {
      await api('/v1/peer-feedback/' + encodeURIComponent(token), {
        method: 'POST',
        body: JSON.stringify({ overallRating: rating, justification, scores: [] })
      });
      peStatus('Peer feedback submitted.', true);
    } catch (e) { peStatus(e.message || String(e), false); }
  };

  window.peCreateCycle = async function peCreateCycle() {
    const name = document.getElementById('pe-cycle-name').value.trim();
    const deptIds = document.getElementById('pe-cycle-depts').value.split(',').map(s => parseInt(s.trim(), 10)).filter(Boolean);
    const periodStart = document.getElementById('pe-cycle-from').value;
    const periodEnd = document.getElementById('pe-cycle-to').value;
    const joinCutOff = document.getElementById('pe-cycle-cutoff').value || periodStart;
    try {
      const row = await api('/v1/review-cycles', {
        method: 'POST',
        body: JSON.stringify({
          name, departmentIds: deptIds, periodStart, periodEnd, joinCutOff,
          peerPresentationMode: document.getElementById('pe-peer-mode').value,
          responseWindowDays: 14
        })
      });
      lastCycleId = String(row.id);
      localStorage.setItem('peCycleId', lastCycleId);
      document.getElementById('pe-cycle-id').value = lastCycleId;
      peOut('pe-cycle-out', row);
      peStatus('Cycle created #' + row.id, true);
    } catch (e) { peStatus(e.message || String(e), false); }
  };

  window.peAdvanceStage = async function peAdvanceStage() {
    const id = document.getElementById('pe-cycle-id').value.trim();
    const reason = document.getElementById('pe-stage-reason').value;
    try {
      const row = await api('/v1/review-cycles/' + id + '/stage', {
        method: 'POST',
        body: JSON.stringify({ reason })
      });
      peOut('pe-cycle-out', row);
      peStatus('Advanced to ' + row.stage, true);
    } catch (e) { peStatus(e.message || String(e), false); }
  };

  window.peLoadProgress = async function peLoadProgress() {
    const id = document.getElementById('pe-cycle-id').value.trim();
    try {
      const row = await api('/v1/review-cycles/' + id + '/progress');
      peOut('pe-cycle-out', row);
      peStatus('Progress loaded.', true);
    } catch (e) { peStatus(e.message || String(e), false); }
  };

  // The calibration tab has its own id fields; fall back to whatever is already
  // loaded on the Cycle and Assess tabs so the older flow keeps working.
  function idFrom(primaryId, fallbackId) {
    const primary = document.getElementById(primaryId);
    const value = primary ? primary.value.trim() : '';
    if (value) return value;
    const fallback = document.getElementById(fallbackId);
    return fallback ? fallback.value.trim() : '';
  }

  window.peLoadCalibration = async function peLoadCalibration() {
    const id = idFrom('pe-cal-cycle-id', 'pe-cycle-id');
    if (!id) return peStatus('Enter a cycle id', false);
    try {
      const row = await api('/v1/calibration/' + id);
      peOut('pe-cal-out', row);
      peStatus('Calibration loaded.', true);
    } catch (e) { peStatus(e.message || String(e), false); }
  };

  window.peCalibrate = async function peCalibrate() {
    const id = idFrom('pe-cal-review-id', 'pe-review-id');
    if (!id) return peStatus('Enter a review id', false);
    const overallRating = parseInt(document.getElementById('pe-cal-rating').value, 10);
    const reason = document.getElementById('pe-cal-reason').value;
    try {
      const row = await api('/v1/reviews/' + id + '/calibrated-rating', {
        method: 'PATCH',
        body: JSON.stringify({ overallRating, reason })
      });
      peOut('pe-cal-out', row);
      peStatus('Calibration saved.', true);
    } catch (e) { peStatus(e.message || String(e), false); }
  };

  window.peCreateGoal = async function peCreateGoal() {
    const title = document.getElementById('pe-goal-title').value.trim();
    const ownerEmployeeId = parseInt(document.getElementById('pe-goal-owner').value, 10);
    const periodStart = document.getElementById('pe-goal-from').value;
    const periodEnd = document.getElementById('pe-goal-to').value;
    try {
      const row = await api('/v1/goals', {
        method: 'POST',
        body: JSON.stringify({
          title,
          description: document.getElementById('pe-goal-desc').value,
          ownerEmployeeId,
          periodStart,
          periodEnd,
          measureOfSuccess: document.getElementById('pe-goal-measure').value,
          targetValue: parseFloat(document.getElementById('pe-goal-target').value) || 0,
          currentValue: 0,
          weight: 1
        })
      });
      lastGoalId = String(row.id);
      localStorage.setItem('peGoalId', lastGoalId);
      document.getElementById('pe-goal-id').value = lastGoalId;
      peOut('pe-goal-out', row);
      peStatus('Goal created #' + row.id, true);
    } catch (e) { peStatus(e.message || String(e), false); }
  };

  window.peLoadGoal = async function peLoadGoal() {
    const id = document.getElementById('pe-goal-id').value.trim();
    if (!id) return peStatus('Enter goal id', false);
    try {
      const row = await api('/v1/goals/' + id);
      lastGoalId = id;
      localStorage.setItem('peGoalId', id);
      peOut('pe-goal-out', row);
      peStatus('Loaded goal ' + id, true);
    } catch (e) { peStatus(e.message || String(e), false); }
  };

  window.peAckGoal = async function peAckGoal() {
    const id = document.getElementById('pe-goal-id').value.trim();
    try {
      const row = await api('/v1/goals/' + id + '/acknowledge', { method: 'POST', body: '{}' });
      peOut('pe-goal-out', row);
      peStatus('Goal acknowledged.', true);
    } catch (e) { peStatus(e.message || String(e), false); }
  };

  window.peGoalProgress = async function peGoalProgress() {
    const id = document.getElementById('pe-goal-id').value.trim();
    const currentValue = parseFloat(document.getElementById('pe-goal-current').value) || 0;
    try {
      const row = await api('/v1/goals/' + id + '/progress', {
        method: 'PATCH',
        etagKey: 'goal:' + id,
        body: JSON.stringify({ currentValue, changeReason: 'UI update' })
      });
      peOut('pe-goal-out', row);
      peStatus('Goal progress updated.', true);
    } catch (e) { peStatus(e.message || String(e), false); }
  };

  window.peFeedbackNote = async function peFeedbackNote() {
    const toEmployeeId = parseInt(document.getElementById('pe-fb-to').value, 10);
    const body = document.getElementById('pe-fb-body').value;
    try {
      const row = await api('/v1/feedback-notes', {
        method: 'POST',
        body: JSON.stringify({ toEmployeeId, body })
      });
      peStatus('Feedback note #' + row.id, true);
    } catch (e) { peStatus(e.message || String(e), false); }
  };

  window.peLoadCompetencies = async function peLoadCompetencies() {
    try {
      const row = await api('/v1/competencies');
      peOut('pe-comp-out', row);
      peStatus('Competency library loaded.', true);
    } catch (e) { peStatus(e.message || String(e), false); }
  };
})();
