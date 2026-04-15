const $ = (id) => document.getElementById(id);

function apiBase() {
  return (window.location?.origin || "").replace(/\/$/, "");
}

function getAuth() {
  const token = localStorage.getItem("maintenanceAccessToken");
  const emp = (() => {
    try {
      return JSON.parse(localStorage.getItem("maintenanceEmployee") || "null");
    } catch {
      return null;
    }
  })();
  return { token, emp };
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

async function fetchJson(path, options = {}) {
  const headers = { ...(options.headers || {}) };
  if (options.body != null && !headers["Content-Type"])
    headers["Content-Type"] = "application/json";

  const { token } = getAuth();
  if (token) headers["Authorization"] = `Bearer ${token}`;

  const method = (options.method || "GET").toUpperCase();
  const res = await fetch(`${apiBase()}${path}`, {
    cache: method === "GET" ? "no-store" : undefined,
    ...options,
    headers,
  });
  const text = await res.text();
  if (res.status === 401) {
    localStorage.removeItem("maintenanceAccessToken");
    localStorage.removeItem("maintenanceEmployee");
    window.location.href = "/maintenance-login.html";
    return null;
  }
  if (!res.ok) throw new Error((text || "").trim() || res.statusText);
  return text ? JSON.parse(text) : null;
}

function badgeUrgency(u) {
  if (!u) return '<span class="badge text-bg-secondary">—</span>';
  const map = { Emergency: "danger", Urgent: "warning", Routine: "success" };
  const c = map[u] || "secondary";
  return `<span class="badge text-bg-${c}">${u}</span>`;
}

function escapeHtml(s) {
  const d = document.createElement("div");
  d.textContent = s ?? "";
  return d.innerHTML;
}

function fmtWhen(iso) {
  if (!iso) return "—";
  const d = new Date(iso);
  return d.toLocaleString(undefined, { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" });
}

function layoutKey(employeeId) {
  // Versioned to avoid carrying forward bad layouts from earlier iterations.
  return `gridLayout:v2:${employeeId}`;
}

const DEFAULT_LAYOUT = [
  { id: "myOpen", x: 0, y: 0, w: 6, h: 7 },
  { id: "unassigned", x: 6, y: 0, w: 6, h: 7 },
  { id: "needsReview", x: 0, y: 7, w: 4, h: 6 },
  { id: "urgentAll", x: 4, y: 7, w: 4, h: 6 },
  { id: "triageQueue", x: 8, y: 7, w: 4, h: 6 },
  { id: "myClosed", x: 0, y: 13, w: 6, h: 6 },
  { id: "recentEvents", x: 6, y: 13, w: 6, h: 6 },
  { id: "summary", x: 0, y: 19, w: 12, h: 4 },
];

function loadLayout(employeeId) {
  const raw = localStorage.getItem(layoutKey(employeeId));
  if (!raw) return DEFAULT_LAYOUT;
  try {
    const parsed = JSON.parse(raw);
    if (Array.isArray(parsed) && parsed.length) return parsed;
  } catch {}
  return DEFAULT_LAYOUT;
}

function saveLayout(employeeId, items) {
  localStorage.setItem(layoutKey(employeeId), JSON.stringify(items));
}

function resetLayout(employeeId) {
  localStorage.removeItem(layoutKey(employeeId));
}

function widgetSpec(employeeId) {
  return {
    myOpen: {
      title: "My open tickets",
      subtitle: "Assigned to you · Status = Open",
      fetch: () => fetchJson(`/api/tickets?status=Open&assignedEmployeeId=${employeeId}`),
    },
    unassigned: {
      title: "Unassigned open tickets",
      subtitle: "Status = Open · Assignee = Unassigned",
      fetch: () => fetchJson(`/api/tickets?status=Open&assignedEmployeeId=0`),
    },
    needsReview: {
      title: "Needs review",
      subtitle: "Confidence below threshold",
      fetch: () => fetchJson(`/api/tickets?needsHumanOnly=true`),
    },
    myClosed: {
      title: "My closed tickets",
      subtitle: "Assigned to you · Status = Closed",
      fetch: () => fetchJson(`/api/tickets?status=Closed&assignedEmployeeId=${employeeId}`),
    },
    urgentAll: {
      title: "Urgent + Emergency",
      subtitle: "All tickets · Highest urgency first",
      fetch: async () => {
        const [urgent, emergency] = await Promise.all([
          fetchJson(`/api/tickets?urgency=Urgent`),
          fetchJson(`/api/tickets?urgency=Emergency`),
        ]);
        const merged = [...(emergency || []), ...(urgent || [])];
        const byNum = new Map();
        for (const t of merged) byNum.set(t.requestNumber, t);
        return [...byNum.values()];
      },
    },
    triageQueue: {
      title: "Triage queue",
      subtitle: "Open tickets still generating / not triaged",
      fetch: async () => {
        const rows = await fetchJson(`/api/tickets?status=Open`);
        return (rows || []).filter(isGeneratingTicket);
      },
    },
    recentEvents: {
      title: "Recent events",
      subtitle: "Latest notes/assign/close/reopen",
      fetch: () => fetchJson(`/api/tickets/recent-events?take=50`),
      render: (data, bodyEl) => renderRecentEvents(data, bodyEl),
    },
    summary: {
      title: "Summary",
      subtitle: "Live counts",
      fetch: () => fetchJson(`/api/tickets/summary`),
      render: (data, bodyEl) => {
        bodyEl.innerHTML = `
          <div class="row g-2 small">
            <div class="col-6">Total: <strong>${data.total}</strong></div>
            <div class="col-6">Triaged: <strong>${data.triaged}</strong></div>
            <div class="col-6">Needs review: <strong>${data.needsHumanReview}</strong></div>
            <div class="col-6">Urgent+Emergency: <strong>${(data.byPredictedUrgency?.Urgent || 0) + (data.byPredictedUrgency?.Emergency || 0)}</strong></div>
          </div>
          <hr />
          <div class="small text-muted">By urgency</div>
          <div class="small">${Object.entries(data.byPredictedUrgency || {}).map(([k,v]) => `${escapeHtml(k)}: <strong>${v}</strong>`).join("<br/>") || "—"}</div>
        `;
      },
    },
  };
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

function renderTicketsList(rows, bodyEl) {
  if (!rows || !rows.length) {
    bodyEl.innerHTML = `
      <div class="text-muted small">
        No tickets right now.
      </div>
    `;
    return;
  }
  const html = rows.slice(0, 50).map((t) => {
    const title = escapeHtml(t.requestText || "");
    return `
      <div class="ticket-line" role="button" tabindex="0" data-open-ticket="${t.requestNumber}">
        <div class="text-muted small">#${t.requestNumber}</div>
        <div class="ticket-title">
          <div class="text-truncate small" title="${title}">${title}</div>
          <div class="text-muted small">${escapeHtml(t.propertyId)} ${escapeHtml(t.unitNumber)} · ${fmtWhen(t.requestTimestamp)}</div>
        </div>
        <div class="small text-nowrap">${badgeUrgency(t.predictedUrgency)}</div>
      </div>
    `;
  }).join("");
  bodyEl.innerHTML = html;

  bodyEl.querySelectorAll("[data-open-ticket]").forEach((el) => {
    const num = el.getAttribute("data-open-ticket");
    el.addEventListener("click", () => openTicket(num).catch((e) => showAlert(e.message)));
    el.addEventListener("keydown", (e) => {
      if (e.key === "Enter" || e.key === " ") {
        e.preventDefault();
        openTicket(num).catch((err) => showAlert(err.message));
      }
    });
  });
}

function renderEvent(ev) {
  const wrap = document.createElement("div");
  wrap.className = "border rounded-3 p-2 bg-body";
  const who = ev.author ? escapeHtml(ev.author) : "—";
  wrap.innerHTML = `
    <div class="d-flex align-items-center">
      <div class="small"><strong>${escapeHtml(ev.eventType || "event")}</strong></div>
      <div class="ms-auto small text-muted">${fmtWhen(ev.createdAt)}</div>
    </div>
    <div class="small text-muted">by ${who} · #${ev.requestNumber}</div>
    ${ev.note ? `<div class="mt-1">${escapeHtml(ev.note)}</div>` : ""}
  `;
  wrap.style.cursor = "pointer";
  wrap.addEventListener("click", () => openTicket(ev.requestNumber).catch((e) => showAlert(e.message)));
  return wrap;
}

function renderRecentEvents(list, bodyEl) {
  bodyEl.replaceChildren();
  if (!list || !list.length) {
    bodyEl.innerHTML = `<div class="text-muted small">No recent events.</div>`;
    return;
  }
  for (const ev of list.slice(0, 50)) bodyEl.appendChild(renderEvent(ev));
}

async function refreshWidget(widgetId, spec, el) {
  const body = el.querySelector("[data-body]");
  body.innerHTML = `<div class="text-muted small">Loading…</div>`;
  try {
    const data = await spec.fetch();
    if (spec.render) spec.render(data, body);
    else renderTicketsList(data, body);
  } catch (e) {
    body.innerHTML = `<div class="text-muted small">Failed to load.</div>`;
    toast(`Widget failed: ${e.message}`, "warning");
    throw e;
  }
}

function buildWidgetElement(widgetId) {
  const tpl = $("widgetTpl");
  const node = tpl.content.firstElementChild.cloneNode(true);
  node.dataset.widgetId = widgetId;
  return node;
}

function buildGridItem(widgetId, spec) {
  const item = document.createElement("div");
  item.className = "grid-stack-item";

  const content = document.createElement("div");
  content.className = "grid-stack-item-content";

  const card = buildWidgetElement(widgetId);
  card.querySelector("[data-title]").textContent = spec.title;
  card.querySelector("[data-subtitle]").textContent = spec.subtitle || "";
  card.querySelector("[data-refresh]").addEventListener("click", () => {
    refreshWidget(widgetId, spec, content).catch((e) => showAlert(e.message));
  });

  content.appendChild(card);
  item.appendChild(content);

  return { item, content };
}

let __employees = null;
let __activeTicket = null;
let __ticketModal = null;
let __activeTicketHasImages = false;

async function loadEmployees({ force = false } = {}) {
  if (!force && __employees) return __employees;
  __employees = await fetchJson("/api/employees");
  return __employees;
}

function escapeHtmlSafe(s) {
  const d = document.createElement("div");
  d.textContent = s ?? "";
  return d.innerHTML;
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

async function openTicket(num) {
  const n = Number(num);
  if (!Number.isFinite(n)) return;
  hideAlert();
  __activeTicket = n;
  __activeTicketHasImages = false;
  if (!__ticketModal) __ticketModal = new bootstrap.Modal($("ticketModal"));

  $("mReqNum").textContent = `#${n}`;
  const t = await fetchJson(`/api/tickets/${n}`);
  let employees = [];
  try {
    employees = await loadEmployees({ force: true });
  } catch (err) {
    showAlert(`Employees load failed: ${err.message}`);
    employees = [];
  }

  $("mWhen").textContent = fmtWhen(t.requestTimestamp);
  $("mProp").textContent = `${t.propertyId} ${t.unitNumber} · ${t.buildingType || "—"}`;
  $("mText").innerHTML = escapeHtmlSafe(t.requestText);
  const risk = (t.riskNotes || "").trim();
  if (risk) {
    $("mRisk").innerHTML = escapeHtmlSafe(risk);
    $("mRiskWrap")?.classList.remove("d-none");
  } else {
    $("mRisk").innerHTML = "";
    $("mRiskWrap")?.classList.add("d-none");
  }

  $("mStatus").innerHTML = badgeStatus(t.status);
  const isGen = isGeneratingTicket(t);
  $("mPred").innerHTML = isGen ? badgeGenerating() : `${t.predictedCategory || "—"} ${badgeUrgency(t.predictedUrgency)}`;
  $("mConf").textContent = isGen ? "—" : (t.confidenceScore != null ? (Number(t.confidenceScore) * 100).toFixed(0) + "%" : "—");

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
  const hasEmployees = employees.length > 0;
  sel.disabled = !hasEmployees;
  $("mAssignBtn").disabled = !hasEmployees;

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
    const { token } = getAuth();
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
    if (!res.ok) throw new Error((text || "").trim() || res.statusText);
    input.value = "";
    toast("Uploaded.", "success");
    await refreshImages();
  } catch (e) {
    showAlert(`Upload failed: ${e.message}`);
  } finally {
    if (btn) btn.disabled = false;
  }
}

async function assignActiveTicket() {
  if (!__activeTicket) return;
  const employeeId = $("mAssignee").value;
  if ($("mAssignee").disabled) {
    showAlert("Employees list not loaded yet. Close and reopen the modal.");
    return;
  }
  await fetchJson(`/api/tickets/${__activeTicket}/assign`, {
    method: "POST",
    body: JSON.stringify({ employeeId: employeeId ? Number(employeeId) : 0, source: "manual" }),
  });
  await openTicket(__activeTicket);
}

async function closeActiveTicket() {
  if (!__activeTicket) return;
  const resolutionNotes = prompt("Resolution notes (optional):", "");
  await fetchJson(`/api/tickets/${__activeTicket}/close`, {
    method: "POST",
    body: JSON.stringify({ closedBy: null, resolutionNotes }),
  });
  await openTicket(__activeTicket);
}

async function reopenActiveTicket() {
  if (!__activeTicket) return;
  await fetchJson(`/api/tickets/${__activeTicket}/reopen`, { method: "POST" });
  await openTicket(__activeTicket);
}

async function triageActiveTicket() {
  if (!__activeTicket) return;
  await fetchJson(`/api/tickets/${__activeTicket}/triage`, { method: "POST" });
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

async function main() {
  hideAlert();
  const { token, emp } = getAuth();
  if (!token || !emp?.id) {
    window.location.href = "/maintenance-login.html";
    return;
  }
  $("whoami").textContent = `${emp.fullName} (${emp.role})`;
  $("whoami").classList.remove("d-none");

  const employeeId = emp.id;
  const specs = widgetSpec(employeeId);
  const layout = loadLayout(employeeId);

  const grid = GridStack.init({
    float: true,
    margin: 8,
    disableOneColumnMode: false,
    cellHeight: 80,
    draggable: { handle: ".card-header" },
    resizable: { handles: "e, se, s, sw, w" },
    disableDrag: false,
    disableResize: false,
  }, "#grid");

  // Auto-save layout continuously.
  let saveTimer = null;
  grid.on("change", () => {
    window.clearTimeout(saveTimer);
    saveTimer = window.setTimeout(() => {
      const items = grid.save(false).map((i) => ({
        id: i.id,
        x: i.x, y: i.y, w: i.w, h: i.h,
      }));
      saveLayout(employeeId, items);
      const hint = $("saveHint");
      if (hint) {
        hint.classList.remove("d-none");
        hint.textContent = "Auto-saved";
      }
    }, 250);
  });

  for (const item of layout) {
    const spec = specs[item.id];
    if (!spec) continue;

    const built = buildGridItem(item.id, spec);
    // Setting gs-* attributes is the most reliable across GridStack builds.
    built.item.setAttribute("gs-id", String(item.id));
    built.item.setAttribute("gs-x", String(item.x));
    built.item.setAttribute("gs-y", String(item.y));
    built.item.setAttribute("gs-w", String(item.w));
    built.item.setAttribute("gs-h", String(item.h));
    grid.addWidget(built.item);
    refreshWidget(item.id, spec, built.content).catch((e) => showAlert(e.message));
  }

  const btnReset = $("btnReset");

  btnReset.addEventListener("click", () => {
    resetLayout(employeeId);
    window.location.reload();
  });

  $("mAssignBtn").addEventListener("click", () => assignActiveTicket().catch((err) => showAlert(err.message)));
  $("mCloseBtn").addEventListener("click", () => closeActiveTicket().catch((err) => showAlert(err.message)));
  $("mReopenBtn").addEventListener("click", () => reopenActiveTicket().catch((err) => showAlert(err.message)));
  $("mTriageBtn").addEventListener("click", () => triageActiveTicket().catch((err) => showAlert(err.message)));
  $("mUploadImageBtn")?.addEventListener("click", () => uploadActiveTicketImage().catch((err) => showAlert(err.message)));
  $("mNoteForm").addEventListener("submit", (e) => addNoteActiveTicket(e).catch((err) => showAlert(err.message)));

  $("btnLogout").addEventListener("click", () => {
    localStorage.removeItem("maintenanceAccessToken");
    localStorage.removeItem("maintenanceEmployee");
    window.location.href = "/maintenance-login.html";
  });
}

main().catch((e) => showAlert(e.message));

