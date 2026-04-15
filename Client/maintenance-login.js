const $ = (id) => document.getElementById(id);

function apiBase() {
  const origin = (window.location?.origin || "").replace(/\/$/, "");
  try {
    const u = new URL(origin);
    // If running under a static/live-preview server (common in Cursor),
    // the origin won't be the API host. Default back to the dotnet host.
    if ((u.hostname === "127.0.0.1" || u.hostname === "localhost") && u.port !== "5288") {
      return "http://localhost:5288";
    }
  } catch {}
  return origin;
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
      <div class="toast-body">${String(message || "")}</div>
      <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
    </div>
  `;
  host.appendChild(el);
  const t = new bootstrap.Toast(el, { delay: 2400 });
  el.addEventListener("hidden.bs.toast", () => el.remove());
  t.show();
}

function hideAlert() {
  $("alert").classList.add("d-none");
}

async function login(e) {
  e.preventDefault();
  hideAlert();
  $("btnLogin").disabled = true;
  try {
    const body = {
      username: $("username").value,
      password: $("password").value,
    };
    const res = await fetch(`${apiBase()}/api/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    const text = await res.text();
    if (!res.ok) throw new Error(text || res.statusText);
    const json = text ? JSON.parse(text) : {};
    if (!json.accessToken) throw new Error("Missing accessToken in response");
    localStorage.setItem("maintenanceAccessToken", json.accessToken);
    localStorage.setItem("maintenanceEmployee", JSON.stringify(json.employee || null));
    toast("Signed in.", "success");
    window.location.href = "/employee-dashboard.html";
  } catch (err) {
    showAlert(err.message || String(err));
  } finally {
    $("btnLogin").disabled = false;
  }
}

$("loginForm").addEventListener("submit", login);

