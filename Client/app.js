const $ = (id) => document.getElementById(id);
let __employees = null;
let __activeTicket = null; // requestNumber
let __ticketModal = null;
let __activeTicketHasImages = false;
let __modalOpen = false;
let __autoInterval = null;
let __refreshInFlight = false;
let __assignDebounceTimers = new Map(); // requestNumber -> timeoutId
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

function toast(message, kind = "secondary") {
  const host = $("toastHost");
  if (!host || typeof bootstrap === "undefined") return;
  const el = document.createElement("div");
  el.className = `toast align-items-center text-bg-${kind} border-0`;
  el.setAttribute("role", "status");
  el.setAttribute("aria-live", "polite");
  el.setAttribute("aria-atomic", "true");
  el.innerHTML = `
    <div class="d-flex">
      <div class="toast-body">${escapeHtml(message)}</div>
      <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
    </div>
  `;
  host.appendChild(el);
  const t = new bootstrap.Toast(el, { delay: 2600 });
  el.addEventListener("hidden.bs.toast", () => el.remove());
  t.show();
}

function hideAlert() {
  $("alert").classList.add("d-none");
}

function isStaffPage() {
  const p = window.location?.pathname || "";
  return p.endsWith("/maintenance.html") || p.endsWith("/employee-dashboard.html");
}

function normalizeErrorText(text) {
  const raw = (text || "").trim();
  if (!raw) return "";
  // Common case: API returns JSON { message: "..." } or { error: "..." }
  if (raw.startsWith("{") || raw.startsWith("[")) {
    try {
      const j = JSON.parse(raw);
      if (typeof j === "string") return j;
      if (j?.message) return String(j.message);
      if (j?.error) return String(j.error);
      if (j?.title) return String(j.title);
    } catch {}
  }
  // Strip HTML error pages.
  if (raw.startsWith("<!DOCTYPE") || raw.startsWith("<html")) return "Request failed (server returned HTML).";
  return raw;
}

async function fetchJson(path, options = {}) {
  const headers = { ...(options.headers || {}) };
  if (options.body != null && !headers["Content-Type"])
    headers["Content-Type"] = "application/json";
  const token = localStorage.getItem("maintenanceAccessToken");
  if (token && !headers["Authorization"]) headers["Authorization"] = `Bearer ${token}`;
  const method = (options.method || "GET").toUpperCase();
  const res = await fetch(`${apiBase()}${path}`, {
    cache: method === "GET" ? "no-store" : undefined,
    ...options,
    headers,
  });
  const text = await res.text();
  if (res.status === 401) {
    // Force maintenance users to login if token expired/missing.
    if (isStaffPage()) {
      localStorage.removeItem("maintenanceAccessToken");
      localStorage.removeItem("maintenanceEmployee");
      window.location.href = "/maintenance-login.html";
      return null;
    }
  }
  if (!res.ok) throw new Error(normalizeErrorText(text) || res.statusText);
  return text ? JSON.parse(text) : null;
}

// Surface any unexpected runtime errors in the UI.
window.addEventListener("error", (e) => {
  try {
    if (typeof e?.message === "string" && e.message.includes("ResizeObserver loop")) return;
    showAlert(`UI error: ${e.message}`);
  } catch { }
});
window.addEventListener("unhandledrejection", (e) => {
  try {
    const msg = e?.reason?.message || e?.reason || "";
    if (typeof msg === "string" && msg.includes("ResizeObserver loop")) return;
    showAlert(`UI error: ${msg || "Unhandled promise rejection"}`);
  } catch { }
});

async function loadSummary() {
  try {
    const s = await fetchJson("/api/tickets/summary");
    $("sumTotal").textContent = s.total;
    $("sumTriaged").textContent = s.triaged;
    $("sumHuman").textContent = s.needsHumanReview;
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
  const review = isGenerating
    ? '<span class="text-muted">—</span>'
    : (t.needsHumanReview ? '<span class="badge text-bg-danger">Needs review</span>' : '<span class="badge text-bg-secondary">OK</span>');
  const status = isGenerating ? '<span class="text-muted">—</span>' : badgeStatus(t.status);
  const assignee = isGenerating
    ? '<span class="text-muted">—</span>'
    : (t.assignedEmployeeName ? escapeHtml(t.assignedEmployeeName) : '<span class="text-muted">Unassigned</span>');

  const canAssignInline = !isGenerating && Array.isArray(__employees) && __employees.length > 0;
  const assigneeControl = (() => {
    if (!canAssignInline) return '<span class="text-muted">—</span>';
    const opts = [
      `<option value="">Unassigned</option>`,
      ...__employees.map((e) => `<option value="${e.id}">${escapeHtml(e.fullName)}</option>`)
    ].join("");
    return `
      <div class="mre-assignee-inline">
        <select class="form-select form-select-sm assignee-select" data-assign-select="${t.requestNumber}">${opts}</select>
      </div>
    `;
  })();
  tr.innerHTML = `
    <td>${t.requestNumber}</td>
    <td class="text-nowrap small">${fmtWhen(t.requestTimestamp)}</td>
    <td class="small"><strong>${t.propertyId}</strong> ${t.unitNumber}<br/><span class="text-muted">${t.buildingType}</span></td>
    <td class="small mre-col-request cell-truncate" title="${escapeHtml(t.requestText)}">${escapeHtml(t.requestText)}</td>
    <td class="small">${pred}</td>
    <td class="small">${conf}</td>
    <td class="small">${review}</td>
    <td class="small">${status}</td>
    <td class="small mre-assignee-cell">${assigneeControl}</td>
    <td class="text-end mre-col-actions">
      <div class="btn-group btn-group-sm" role="group">
        <button type="button" class="btn btn-outline-primary" data-view="${t.requestNumber}">View</button>
        <button type="button" class="btn btn-outline-secondary" data-triage="${t.requestNumber}">Triage</button>
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
  if (__refreshInFlight) return;
  __refreshInFlight = true;
  const loading = $("tableLoading");
  if (loading) loading.classList.remove("d-none");
  const btnLoad = $("btnLoad");
  const btnAll = $("btnTriageAll");
  if (btnLoad) btnLoad.disabled = true;
  if (btnAll) btnAll.disabled = true;
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
    if (!rows || rows.length === 0) {
      const tr = document.createElement("tr");
      tr.innerHTML = `<td colspan="10" class="py-4 text-center text-muted">No tickets match your filters.</td>`;
      tbody.appendChild(tr);
    } else {
      for (const t of rows) tbody.appendChild(renderRow(t));
    }
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
  } finally {
    if (loading) loading.classList.add("d-none");
    if (btnLoad) btnLoad.disabled = false;
    if (btnAll) btnAll.disabled = false;
    __refreshInFlight = false;
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

  // Default to "my tickets" on the maintenance dashboard (first load only).
  if (window.location?.pathname.endsWith("/maintenance.html")) {
    if (!sessionStorage.getItem("assigneeFilterInitialized")) {
      sessionStorage.setItem("assigneeFilterInitialized", "1");
      try {
        const emp = JSON.parse(localStorage.getItem("maintenanceEmployee") || "null");
        const myId = emp?.id != null ? String(emp.id) : "";
        if (myId && [...sel.options].some((o) => o.value === myId)) {
          sel.value = myId;
        }
      } catch {}
    }
  }
}

async function assignTicketInline(requestNumber) {
  const sel = document.querySelector(`[data-assign-select="${requestNumber}"]`);
  if (!sel || sel.disabled) return;
  const employeeId = sel.value;
  sel.disabled = true;
  sel.classList.add("opacity-75");
  try {
    await fetchJson(`/api/tickets/${requestNumber}/assign`, {
      method: "POST",
      body: JSON.stringify({ employeeId: employeeId ? Number(employeeId) : 0, source: "manual" }),
    });
    toast("Assignee saved.", "success");
    await loadTickets();
  } finally {
    try {
      sel.disabled = false;
      sel.classList.remove("opacity-75");
    } catch {}
  }
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
  __activeTicketHasImages = false;

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
  const riskWrap = $("mRiskWrap");
  const risk = (t.riskNotes || "").trim();
  if (risk) {
    $("mRisk").innerHTML = escapeHtml(risk);
    riskWrap?.classList.remove("d-none");
  } else {
    $("mRisk").innerHTML = "";
    riskWrap?.classList.add("d-none");
  }
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
  // Show only the action that makes sense.
  if ((t.status || "Open") === "Closed") {
    $("mCloseBtn")?.classList.add("d-none");
    $("mReopenBtn")?.classList.remove("d-none");
  } else {
    $("mCloseBtn")?.classList.remove("d-none");
    $("mReopenBtn")?.classList.add("d-none");
  }

  await refreshEvents();
  await refreshImages().catch(() => {});
  __ticketModal.show();
}

function renderImages(list) {
  const box = $("mImages");
  if (!box) return;
  box.replaceChildren();
  if (!list || !list.length) {
    const empty = document.createElement("div");
    empty.className = "small text-muted";
    empty.textContent = "No photos uploaded.";
    box.appendChild(empty);
    return;
  }
  for (const img of list.slice(0, 50)) {
    const a = document.createElement("a");
    a.href = img.url;
    a.target = "_blank";
    a.rel = "noopener";
    const el = document.createElement("img");
    el.src = img.url;
    el.alt = img.fileName || "ticket image";
    el.loading = "lazy";
    el.className = "rounded border";
    el.style.width = "120px";
    el.style.height = "90px";
    el.style.objectFit = "cover";
    a.appendChild(el);
    box.appendChild(a);
  }
}

async function refreshImages() {
  if (!__activeTicket) return;
  const wrap = $("mImagesWrap");
  if (!wrap) return;
  try {
    const list = await fetchJson(`/api/tickets/${__activeTicket}/images`);
    __activeTicketHasImages = Array.isArray(list) && list.length > 0;
    renderImages(list);
  } catch (e) {
    // Images are optional; show a light hint but don't break the modal.
    renderImages([]);
    toast(`Images unavailable: ${e.message}`, "warning");
  }
}

async function uploadActiveTicketImage() {
  if (!__activeTicket) return;
  const input = $("mImageFile");
  if (!input || !input.files || input.files.length === 0) {
    toast("Choose a file first.", "secondary");
    return;
  }
  const file = input.files[0];
  const btn = $("mUploadImageBtn");
  if (btn) btn.disabled = true;
  try {
    const token = localStorage.getItem("maintenanceAccessToken");
    const headers = {};
    if (token) headers["Authorization"] = `Bearer ${token}`;
    const fd = new FormData();
    fd.append("file", file);
    const res = await fetch(`${apiBase()}/api/tickets/${__activeTicket}/images`, {
      method: "POST",
      headers,
      body: fd,
    });
    const text = await res.text();
    if (!res.ok) throw new Error(normalizeErrorText(text) || res.statusText);
    input.value = "";
    toast("Uploaded.", "success");
    await refreshImages();
  } catch (e) {
    showAlert(`Upload failed: ${e.message}`);
  } finally {
    if (btn) btn.disabled = false;
  }
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

$("btnLoad")?.addEventListener("click", loadTickets);
$("btnTriageAll")?.addEventListener("click", triageAll);
$("filterUrgency")?.addEventListener("change", loadTickets);
$("filterHuman")?.addEventListener("change", loadTickets);
$("filterStatus")?.addEventListener("change", loadTickets);
$("filterAssignee")?.addEventListener("change", loadTickets);
$("tbody")?.addEventListener("click", (e) => {
  const btn = e.target.closest("[data-triage]");
  if (!btn) return;
  triageOne(btn.getAttribute("data-triage"));
});
$("tbody")?.addEventListener("click", (e) => {
  const btn = e.target.closest("[data-view]");
  if (!btn) return;
  openTicket(btn.getAttribute("data-view")).catch((err) => showAlert(err.message));
});
// Auto-save assignee on dropdown change (full dashboard table view)
$("tbody")?.addEventListener("change", (e) => {
  const sel = e.target.closest?.("[data-assign-select]");
  if (!sel) return;
  const requestNumber = sel.getAttribute("data-assign-select");
  if (!requestNumber) return;

  // Debounce changes so fast scroll/select doesn't spam the API.
  const existing = __assignDebounceTimers.get(requestNumber);
  if (existing) window.clearTimeout(existing);
  const t = window.setTimeout(() => {
    __assignDebounceTimers.delete(requestNumber);
    assignTicketInline(requestNumber).catch((err) => {
      toast(`Assign failed: ${err.message}`, "danger");
      showAlert(err.message);
      try {
        sel.disabled = false;
        sel.classList.remove("opacity-75");
      } catch {}
    });
  }, 350);
  __assignDebounceTimers.set(requestNumber, t);
});
$("submitForm")?.addEventListener("submit", submitTicket);
$("mAssignBtn")?.addEventListener("click", () => assignActiveTicket().catch((err) => showAlert(err.message)));
$("mCloseBtn")?.addEventListener("click", () => closeActiveTicket().catch((err) => showAlert(err.message)));
$("mReopenBtn")?.addEventListener("click", () => reopenActiveTicket().catch((err) => showAlert(err.message)));
$("mTriageBtn")?.addEventListener("click", () => triageActiveTicket().catch((err) => showAlert(err.message)));
$("mUploadImageBtn")?.addEventListener("click", () => uploadActiveTicketImage().catch((err) => showAlert(err.message)));
$("mNoteForm")?.addEventListener("submit", (e) => addNoteActiveTicket(e).catch((err) => showAlert(err.message)));

hydrateAssigneeFilter().finally(() => loadTickets());

// Background auto-refresh (AJAX). No full page reload.
function startAutoRefresh() {
  if (__autoInterval) return;
  __autoInterval = window.setInterval(() => {
    // Only auto-refresh on the table view.
    if (!(window.location?.pathname || "").endsWith("/maintenance.html")) return;
    if (document.hidden) return;
    if (__modalOpen) return;
    // Avoid stomping on an active dropdown interaction.
    if (document.activeElement && document.activeElement.matches?.("select, input, textarea")) return;
    loadTickets().catch(() => {});
  }, 8000);
}

startAutoRefresh();

// Pause refresh while modal is open (prevents UI "jumping" during edits).
try {
  const modalEl = $("ticketModal");
  if (modalEl) {
    modalEl.addEventListener("show.bs.modal", () => { __modalOpen = true; });
    modalEl.addEventListener("hidden.bs.modal", () => { __modalOpen = false; });
  }
} catch {}

// Maintenance navbar auth UX
if (window.location?.pathname.endsWith("/maintenance.html")) {
  const token = localStorage.getItem("maintenanceAccessToken");
  const logout = $("btnLogout");
  const loginLink = $("btnLoginLink");
  const empDash = $("btnEmployeeDash");
  if (token) {
    logout?.classList.remove("d-none");
    loginLink?.classList.add("d-none");
    empDash?.classList.remove("d-none");
  } else {
    empDash?.classList.add("d-none");
  }
  logout?.addEventListener("click", () => {
    localStorage.removeItem("maintenanceAccessToken");
    localStorage.removeItem("maintenanceEmployee");
    window.location.href = "/maintenance-login.html";
  });
}
