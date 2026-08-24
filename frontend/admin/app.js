const API_BASE = "http://localhost:5007/api";
let token = sessionStorage.getItem("adminToken");

function authHeaders() {
  return { Authorization: `Bearer ${token}` };
}

async function login() {
  const username = document.getElementById("username").value;
  const password = document.getElementById("password").value;

  const response = await fetch(`${API_BASE}/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ username, password }),
  });

  if (response.ok) {
    const body = await response.json();
    token = body.token;
    sessionStorage.setItem("adminToken", token);
    showAdminSection();
  } else {
    document.getElementById("login-result").textContent = "Invalid credentials.";
  }
}

function showAdminSection() {
  document.getElementById("login-section").hidden = true;
  document.getElementById("admin-section").hidden = false;
  loadProducts();
  loadOrders();
}

async function loadProducts() {
  const response = await fetch(`${API_BASE}/products`);
  const products = await response.json();
  document.getElementById("products").innerHTML = products
    .map((p) => `<div>${p.name} - $${p.price.toFixed(2)} (${p.stockQuantity} in stock)
      <button data-id="${p.id}" class="delete-product">Delete</button></div>`)
    .join("");

  document.querySelectorAll(".delete-product").forEach((button) => {
    button.addEventListener("click", async () => {
      await fetch(`${API_BASE}/products/${button.dataset.id}`, {
        method: "DELETE",
        headers: authHeaders(),
      });
      loadProducts();
    });
  });
}

async function createProduct() {
  const name = document.getElementById("new-product-name").value;
  const price = parseFloat(document.getElementById("new-product-price").value);
  const stockQuantity = parseInt(document.getElementById("new-product-stock").value, 10);

  await fetch(`${API_BASE}/products`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders() },
    body: JSON.stringify({ name, price, stockQuantity }),
  });
  loadProducts();
}

async function loadOrders() {
  const response = await fetch(`${API_BASE}/orders`, { headers: authHeaders() });
  const orders = await response.json();
  const validNextStatus = { Pending: ["Paid", "Cancelled"], Paid: ["Shipped", "Cancelled"] };

  document.getElementById("orders").innerHTML = orders
    .map((o) => {
      const nextOptions = (validNextStatus[o.status] || [])
        .map((s) => `<button data-id="${o.id}" data-status="${s}" class="advance-status">${s}</button>`)
        .join(" ");
      return `<div>Order ${o.id} - ${o.status} ${nextOptions}</div>`;
    })
    .join("");

  document.querySelectorAll(".advance-status").forEach((button) => {
    button.addEventListener("click", async () => {
      await fetch(`${API_BASE}/orders/${button.dataset.id}/status`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json", ...authHeaders() },
        body: JSON.stringify({ status: button.dataset.status }),
      });
      loadOrders();
      loadProducts();
    });
  });
}

document.getElementById("login-button").addEventListener("click", login);
document.getElementById("create-product-button").addEventListener("click", createProduct);

if (token) {
  showAdminSection();
}
