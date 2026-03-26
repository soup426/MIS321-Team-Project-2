const $ = (id) => document.getElementById(id);
let __employees = null;
let __activeTicket = null; // requestNumber
let __ticketModal = null;
const __employeeNameById = () => new Map((__employees || []).map((e) => [String(e.id), e.fullName]));

function apiBase() {
  return (window.location?.origin || "").replace(/\/$/, "");
}

function showAlert(message, kind = "danger") {
  const el = $("alert");
  el.className = `alert alert-${kind}`;
  el.textContent = message;
  el.classList.remove("d-none");
}

function hideAlert() {
  $("alert").classList.add("d-none");
}

async function fetchJson(path, options = {}) {
  const headers = { ...(options.headers || {}) };
  if (options.body != null && !headers["Content-Type"])
    headers["Content-Type"] = "application/json";
  const method = (options.method || "GET").toUpperCase();
  const res = await fetch(`${apiBase()}${path}`, {
    cache: method === "GET" ? "no-store" : undefined,
    ...options,
    headers,
  });
  const text = await res.text();
  if (!res.ok) throw new Error(text || res.statusText);
  return text ? JSON.parse(text) : null;
}

// Surface any unexpected runtime errors in the UI.
window.addEventListener("error", (e) => {
  try {
    showAlert(`UI error: ${e.message}`);
  } catch { }
});
window.addEventListener("unhandledrejection", (e) => {
  try {
    showAlert(`UI error: ${e.reason?.message || e.reason || "Unhandled promise rejection"}`);
  } catch { }
});

async function loadSummary() {
  try {
    const s = await fetchJson("/api/tickets/summary");
    $("sumTotal").textContent = s.total;
    $("sumTriaged").textContent = s.triaged;
    $("sumHuman").textContent = s.needsHumanReview;
    $("sumCat").textContent =
      s.comparedRows > 0 ? `${s.categoryMatches} / ${s.comparedRows}` : "—";
    $("sumUrg").textContent =
      s.comparedRows > 0 ? `${s.urgencyMatches} / ${s.comparedRows}` : "—";
  } catch (e) {
    showAlert(`Summary failed: ${e.message}`);
  }
}

function fmtWhen(iso) {
  if (!iso) return "—";
  const d = new Date(iso);
  return d.toLocaleString(undefined, { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" });
}

function badgeUrgency(u) {
  if (!u) return '<span class="badge text-bg-secondary">—</span>';
  const map = { Emergency: "danger", Urgent: "warning", Routine: "success" };
  const c = map[u] || "secondary";
  return `<span class="badge text-bg-${c}">${u}</span>`;
}

function badgeStatus(s) {
  const status = (s || "Open").toLowerCase();
  if (status === "closed") return '<span class="badge text-bg-secondary">Closed</span>';
  return '<span class="badge text-bg-success">Open</span>';
}

function badgeGenerating() {
  return '<span class="badge text-bg-secondary">Generating…</span>';
}

function isGeneratingTicket(t) {
  return (
    !t.lastTriagedAt ||
    !t.triageSource ||
    (!t.predictedCategory && !t.predictedUrgency) ||
    t.confidenceScore == null ||
    Number(t.confidenceScore) <= 0
  );
}

function renderRow(t) {
  const tr = document.createElement("tr");
  const isGenerating = isGeneratingTicket(t);

  if (!isGenerating && t.needsHumanReview) tr.classList.add("table-warning");
  const pred = isGenerating
    ? badgeGenerating()
    : `${t.predictedCategory || "—"} ${badgeUrgency(t.predictedUrgency)}`;
  const conf =
    isGenerating ? "—" : (t.confidenceScore != null ? (Number(t.confidenceScore) * 100).toFixed(0) + "%" : "—");
  const tag = isGenerating
    ? '<span class="text-muted">—</span>'
    : (t.needsHumanReview ? '<span class="badge text-bg-danger">Needs review</span>' : '<span class="text-muted">—</span>');
  const status = isGenerating ? '<span class="text-muted">—</span>' : badgeStatus(t.status);
  const assignee = isGenerating
    ? '<span class="text-muted">—</span>'
    : (t.assignedEmployeeName ? escapeHtml(t.assignedEmployeeName) : '<span class="text-muted">Unassigned</span>');

  const canAssignInline = !isGenerating && Array.isArray(__employees) && __employees.length > 0;
  const selectHtml = (() => {
    if (!canAssignInline) return '<select class="form-select form-select-sm assignee-select" disabled><option>—</option></select>';
    const opts = [
      `<option value="">Unassigned</option>`,
      ...__employees.map((e) => `<option value="${e.id}">${escapeHtml(e.fullName)}</option>`)
    ].join("");
    const current = t.assignedEmployeeId != null ? String(t.assignedEmployeeId) : "";
    return `<select class="form-select form-select-sm assignee-select" data-assign-select="${t.requestNumber}">${opts}</select>
      <script>/*noop*/</script>`;
  })();
  tr.innerHTML = `
    <td>${t.requestNumber}</td>
    <td class="text-nowrap small">${fmtWhen(t.requestTimestamp)}</td>
    <td class="small"><strong>${t.propertyId}</strong> ${t.unitNumber}<br/><span class="text-muted">${t.buildingType}</span></td>
    <td class="small cell-truncate" title="${escapeHtml(t.requestText)}">${escapeHtml(t.requestText)}</td>
    <td class="small">${pred}</td>
    <td class="small">${conf}</td>
    <td class="small">${tag}</td>
    <td class="small">${status}</td>
    <td class="small">${assignee}</td>
    <td class="text-end">
      <div class="btn-group btn-group-sm" role="group">
        <button type="button" class="btn btn-outline-primary" data-view="${t.requestNumber}">View</button>
        <button type="button" class="btn btn-outline-secondary" data-triage="${t.requestNumber}">Triage</button>
      </div>
      <div class="d-inline-flex align-items-center gap-1 ms-2">
        ${selectHtml}
        <button type="button" class="btn btn-outline-primary btn-sm" data-assign-row="${t.requestNumber}" ${canAssignInline ? "" : "disabled"}>Assign</button>
      </div>
    </td>
  `;

  // Set inline select value after insertion (so it doesn't rely on string-building)
  queueMicrotask(() => {
    const sel = tr.querySelector(`[data-assign-select="${t.requestNumber}"]`);
    if (sel) sel.value = t.assignedEmployeeId != null ? String(t.assignedEmployeeId) : "";
  });
  return tr;
}

function escapeHtml(s) {
  const d = document.createElement("div");
  d.textContent = s;
  return d.innerHTML;
}

async function loadTickets() {
  hideAlert();
  const urgency = $("filterUrgency").value;
  const human = $("filterHuman").checked;
  const status = $("filterStatus")?.value || "";
  const assignedEmployeeId = $("filterAssignee")?.value || "";
  const params = new URLSearchParams();
  if (urgency) params.set("urgency", urgency);
  if (human) params.set("needsHumanOnly", "true");
  if (status) params.set("status", status);
  if (assignedEmployeeId !== "") params.set("assignedEmployeeId", assignedEmployeeId);
  const q = params.toString();
  const path = `/api/tickets${q ? `?${q}` : ""}`;
  try {
    const rows = await fetchJson(path);
    const tbody = $("tbody");
    tbody.replaceChildren();
    for (const t of rows) tbody.appendChild(renderRow(t));
    await loadSummary();

    // If any tickets are still waiting for AI triage, auto-refresh briefly.
    if (rows.some(isGeneratingTicket)) {
      window.clearTimeout(window.__autoRefreshTimer);
      window.__autoRefreshTimer = window.setTimeout(() => loadTickets(), 2500);
    } else {
      window.clearTimeout(window.__autoRefreshTimer);
    }
  } catch (e) {
    showAlert(`Load failed: ${e.message}. Is the API running? CORS is open; use a local static server for this page (not file://).`);
  }
}

async function triageOne(num) {
  hideAlert();
  try {
    await fetchJson(`/api/tickets/${num}/triage`, { method: "POST" });
    await loadTickets();
  } catch (e) {
    showAlert(e.message);
  }
}

async function loadEmployees({ force = false } = {}) {
  if (!force && __employees) return __employees;
  __employees = await fetchJson("/api/employees");
  return __employees;
}

async function hydrateAssigneeFilter() {
  const sel = $("filterAssignee");
  if (!sel) return;
  // Keep existing first two options: All + Unassigned
  const keep = new Set(["", "0"]);
  [...sel.options].forEach((o) => {
    if (!keep.has(o.value)) o.remove();
  });

  let employees = [];
  try {
    employees = await loadEmployees({ force: true });
  } catch {
    return;
  }
  for (const e of employees) {
    const opt = document.createElement("option");
    opt.value = String(e.id);
    opt.textContent = e.fullName;
    sel.appendChild(opt);
  }
}

async function assignTicketInline(requestNumber) {
  const sel = document.querySelector(`[data-assign-select="${requestNumber}"]`);
  if (!sel || sel.disabled) return;
  const employeeId = sel.value;
  await fetchJson(`/api/tickets/${requestNumber}/assign`, {
    method: "POST",
    body: JSON.stringify({ employeeId: employeeId ? Number(employeeId) : 0, source: "manual" }),
  });
  await loadTickets();
}

function renderEvent(ev) {
  const wrap = document.createElement("div");
  wrap.className = "border rounded p-2 bg-body";
  const who = ev.author ? escapeHtml(ev.author) : "—";
  wrap.innerHTML = `
    <div class="d-flex align-items-center">
      <div class="small"><strong>${escapeHtml(ev.eventType || "event")}</strong></div>
      <div class="ms-auto small text-muted">${fmtWhen(ev.createdAt)}</div>
    </div>
    <div class="small text-muted">by ${who}</div>
    ${ev.note ? `<div class="mt-1">${escapeHtml(ev.note)}</div>` : ""}
  `;
  return wrap;
}

async function openTicket(num) {
  hideAlert();
  __activeTicket = Number(num);

  if (!__ticketModal) __ticketModal = new bootstrap.Modal($("ticketModal"));
  $("mReqNum").textContent = `#${num}`;

  const t = await fetchJson(`/api/tickets/${num}`);
  let employees = [];
  try {
    employees = await loadEmployees({ force: true });
  } catch (err) {
    // Don't silently swallow this; assignment UI depends on it.
    showAlert(`Employees load failed: ${err.message}`);
    employees = [];
  }

  $("mWhen").textContent = fmtWhen(t.requestTimestamp);
  $("mProp").textContent = `${t.propertyId} ${t.unitNumber} · ${t.buildingType || "—"}`;
  $("mText").innerHTML = escapeHtml(t.requestText);
  $("mStatus").innerHTML = badgeStatus(t.status);
  const isGen = isGeneratingTicket(t);
  $("mPred").innerHTML = isGen ? badgeGenerating() : `${t.predictedCategory || "—"} ${badgeUrgency(t.predictedUrgency)}`;
  $("mConf").textContent = isGen ? "—" : (t.confidenceScore != null ? (Number(t.confidenceScore) * 100).toFixed(0) + "%" : "—");

  // Populate assignee dropdown
  const sel = $("mAssignee");
  sel.replaceChildren();
  const opt0 = document.createElement("option");
  opt0.value = "";
  opt0.textContent = "Unassigned";
  sel.appendChild(opt0);
  for (const e of employees) {
    const opt = document.createElement("option");
    opt.value = String(e.id);
    opt.textContent = e.fullName;
    sel.appendChild(opt);
  }
  sel.value = t.assignedEmployeeId ? String(t.assignedEmployeeId) : "";
  if (employees.length === 0) {
    sel.disabled = true;
    $("mAssignBtn").disabled = true;
  } else {
    sel.disabled = false;
    $("mAssignBtn").disabled = false;
  }

  $("mCloseBtn").disabled = (t.status || "Open") === "Closed";
  $("mReopenBtn").disabled = (t.status || "Open") !== "Closed";

  await refreshEvents();
  __ticketModal.show();
}

async function refreshEvents() {
  if (!__activeTicket) return;
  const list = await fetchJson(`/api/tickets/${__activeTicket}/events`);
  const box = $("mEvents");
  box.replaceChildren();
  if (!list.length) {
    const empty = document.createElement("div");
    empty.className = "small text-muted";
    empty.textContent = "No events yet.";
    box.appendChild(empty);
    return;
  }
  for (const ev of list) box.appendChild(renderEvent(ev));
}

async function assignActiveTicket() {
  if (!__activeTicket) return;
  const employeeId = $("mAssignee").value;
  // allow blank => unassign
  if ($("mAssignee").disabled) {
    showAlert("Employees list not loaded yet. Hit Refresh or reopen the modal.");
    return;
  }
  await fetchJson(`/api/tickets/${__activeTicket}/assign`, {
    method: "POST",
    body: JSON.stringify({ employeeId: employeeId ? Number(employeeId) : 0, source: "manual" }),
  });
  await loadTickets();
  await openTicket(__activeTicket);
}

async function closeActiveTicket() {
  if (!__activeTicket) return;
  const resolutionNotes = prompt("Resolution notes (optional):", "");
  await fetchJson(`/api/tickets/${__activeTicket}/close`, {
    method: "POST",
    body: JSON.stringify({ closedBy: null, resolutionNotes }),
  });
  await loadTickets();
  await openTicket(__activeTicket);
}

async function reopenActiveTicket() {
  if (!__activeTicket) return;
  await fetchJson(`/api/tickets/${__activeTicket}/reopen`, { method: "POST" });
  await loadTickets();
  await openTicket(__activeTicket);
}

async function triageActiveTicket() {
  if (!__activeTicket) return;
  await triageOne(__activeTicket);
  await openTicket(__activeTicket);
}

async function addNoteActiveTicket(e) {
  e.preventDefault();
  if (!__activeTicket) return;
  const author = $("mNoteAuthor").value;
  const note = $("mNoteText").value;
  await fetchJson(`/api/tickets/${__activeTicket}/notes`, {
    method: "POST",
    body: JSON.stringify({ author, note }),
  });
  $("mNoteText").value = "";
  await refreshEvents();
}

async function triageAll() {
  hideAlert();
  try {
    const r = await fetchJson("/api/tickets/triage-all", { method: "POST" });
    showAlert(`Updated ${r.updated} tickets.`, "success");
    await loadTickets();
  } catch (e) {
    showAlert(e.message);
  }
}

async function submitTicket(e) {
  e.preventDefault();
  hideAlert();
  try {
    const body = {
      propertyId: $("subPropertyId").value,
      unitNumber: $("subUnitNumber").value,
      buildingType: $("subBuildingType").value,
      tenantTenureMonths: Number($("subTenure").value || 0),
      submissionChannel: $("subChannel").value,
      requestText: $("subRequestText").value,
      hasImage: $("subHasImage").checked,
      imageType: $("subImageType").value,
      imageSeverityHint: $("subImageHint").value,
      priorRequestsLast6Mo: Number($("subPrior").value || 0),
    };

    const created = await fetchJson("/api/tickets", {
      method: "POST",
      body: JSON.stringify(body),
    });

    showAlert(`Created ticket #${created.requestNumber}.`, "success");
    $("subRequestText").value = "";
    await loadTickets();
  } catch (err) {
    showAlert(`Submit failed: ${err.message}`);
  }
}

$("btnLoad").addEventListener("click", loadTickets);
$("btnTriageAll").addEventListener("click", triageAll);
$("filterUrgency").addEventListener("change", loadTickets);
$("filterHuman").addEventListener("change", loadTickets);
$("filterStatus").addEventListener("change", loadTickets);
$("filterAssignee").addEventListener("change", loadTickets);
$("tbody").addEventListener("click", (e) => {
  const btn = e.target.closest("[data-triage]");
  if (!btn) return;
  triageOne(btn.getAttribute("data-triage"));
});
$("tbody").addEventListener("click", (e) => {
  const btn = e.target.closest("[data-view]");
  if (!btn) return;
  openTicket(btn.getAttribute("data-view")).catch((err) => showAlert(err.message));
});
$("tbody").addEventListener("click", (e) => {
  const btn = e.target.closest("[data-assign-row]");
  if (!btn) return;
  assignTicketInline(btn.getAttribute("data-assign-row")).catch((err) => showAlert(err.message));
});
$("submitForm").addEventListener("submit", submitTicket);
$("mAssignBtn").addEventListener("click", () => assignActiveTicket().catch((err) => showAlert(err.message)));
$("mCloseBtn").addEventListener("click", () => closeActiveTicket().catch((err) => showAlert(err.message)));
$("mReopenBtn").addEventListener("click", () => reopenActiveTicket().catch((err) => showAlert(err.message)));
$("mTriageBtn").addEventListener("click", () => triageActiveTicket().catch((err) => showAlert(err.message)));
$("mRefreshEvents").addEventListener("click", () => refreshEvents().catch((err) => showAlert(err.message)));
$("mNoteForm").addEventListener("submit", (e) => addNoteActiveTicket(e).catch((err) => showAlert(err.message)));

hydrateAssigneeFilter().finally(() => loadTickets());
