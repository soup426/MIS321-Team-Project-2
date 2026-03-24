const $ = (id) => document.getElementById(id);

function apiBase() {
  return $("apiBase").value.replace(/\/$/, "");
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
  const res = await fetch(`${apiBase()}${path}`, { ...options, headers });
  const text = await res.text();
  if (!res.ok) throw new Error(text || res.statusText);
  return text ? JSON.parse(text) : null;
}

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

function renderRow(t) {
  const tr = document.createElement("tr");
  if (t.needsHumanReview) tr.classList.add("table-warning");
  const pred =
    t.predictedCategory || t.predictedUrgency
      ? `${t.predictedCategory || "—"} ${badgeUrgency(t.predictedUrgency)}`
      : '<span class="text-muted">Not triaged</span>';
  const conf =
    t.confidenceScore != null ? (Number(t.confidenceScore) * 100).toFixed(0) + "%" : "—";
  const vs =
    t.categoryMatchesSample == null
      ? "—"
      : `${t.categoryMatchesSample ? "✓" : "✗"} cat / ${t.urgencyMatchesSample ? "✓" : "✗"} urg`;
  tr.innerHTML = `
    <td>${t.requestNumber}</td>
    <td class="text-nowrap small">${fmtWhen(t.requestTimestamp)}</td>
    <td class="small"><strong>${t.propertyId}</strong> ${t.unitNumber}<br/><span class="text-muted">${t.buildingType}</span></td>
    <td class="small">${escapeHtml(t.requestText)}</td>
    <td class="small">${pred}</td>
    <td class="small">${conf}</td>
    <td>${t.needsHumanReview ? '<span class="text-danger">Yes</span>' : "No"}</td>
    <td class="small">${vs}</td>
    <td><button type="button" class="btn btn-outline-secondary btn-sm" data-triage="${t.requestNumber}">Triage</button></td>
  `;
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
  const params = new URLSearchParams();
  if (urgency) params.set("urgency", urgency);
  if (human) params.set("needsHumanOnly", "true");
  const q = params.toString();
  const path = `/api/tickets${q ? `?${q}` : ""}`;
  try {
    const rows = await fetchJson(path);
    const tbody = $("tbody");
    tbody.replaceChildren();
    for (const t of rows) tbody.appendChild(renderRow(t));
    await loadSummary();
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

$("btnLoad").addEventListener("click", loadTickets);
$("btnTriageAll").addEventListener("click", triageAll);
$("filterUrgency").addEventListener("change", loadTickets);
$("filterHuman").addEventListener("change", loadTickets);
$("tbody").addEventListener("click", (e) => {
  const btn = e.target.closest("[data-triage]");
  if (!btn) return;
  triageOne(btn.getAttribute("data-triage"));
});

loadTickets();
