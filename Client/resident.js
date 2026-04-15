const $ = (id) => document.getElementById(id);

function apiBase() {
  // Always same-origin when served from LocalAPI/TestingAPI.
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

function setStatus(t) {
  $("statusText").textContent = t || "";
}

function renderPreviews(files) {
  const box = $("previews");
  box.replaceChildren();
  for (const f of files) {
    const url = URL.createObjectURL(f);
    const img = document.createElement("img");
    img.src = url;
    img.alt = f.name;
    img.style.width = "120px";
    img.style.height = "90px";
    img.style.objectFit = "cover";
    img.className = "rounded border";
    box.appendChild(img);
  }
}

async function submitResident(e) {
  e.preventDefault();
  hideAlert();
  setStatus("Submitting…");
  $("btnSubmit").disabled = true;

  try {
    const form = $("residentForm");
    const fd = new FormData(form);
    const res = await fetch(`${apiBase()}/api/resident/tickets`, { method: "POST", body: fd });
    const text = await res.text();
    if (!res.ok) throw new Error(text || res.statusText);
    const json = text ? JSON.parse(text) : {};
    showAlert(`Submitted. Ticket #${json.requestNumber}.`, "success");
    setStatus("");
    form.reset();
    renderPreviews([]);
  } catch (err) {
    showAlert(err.message || String(err));
    setStatus("");
  } finally {
    $("btnSubmit").disabled = false;
  }
}

$("residentForm").addEventListener("submit", submitResident);
$("images").addEventListener("change", (e) => renderPreviews(e.target.files || []));

