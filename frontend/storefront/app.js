const API_BASE = "http://localhost:5007/api";
const cart = new Map();

async function loadCatalog() {
  const response = await fetch(`${API_BASE}/products`);
  const products = await response.json();
  const catalog = document.getElementById("catalog");
  catalog.innerHTML = products
    .map(
      (p) => `
      <div class="product">
        <span>${p.name} - $${p.price.toFixed(2)} (${p.stockQuantity} in stock)</span>
        <button data-id="${p.id}" data-price="${p.price}" class="add-to-cart">Add</button>
      </div>`
    )
    .join("");

  catalog.querySelectorAll(".add-to-cart").forEach((button) => {
    button.addEventListener("click", () => {
      const id = button.dataset.id;
      const price = parseFloat(button.dataset.price);
      const existing = cart.get(id) || { quantity: 0, price };
      cart.set(id, { quantity: existing.quantity + 1, price });
      renderCart();
    });
  });
}

function renderCart() {
  const list = document.getElementById("cart-items");
  let total = 0;
  list.innerHTML = [...cart.entries()]
    .map(([id, { quantity, price }]) => {
      total += quantity * price;
      return `<li>${id} x${quantity} - $${(quantity * price).toFixed(2)}</li>`;
    })
    .join("");
  document.getElementById("cart-total").textContent = total.toFixed(2);
}

async function checkout() {
  const lines = [...cart.entries()].map(([productId, { quantity }]) => ({
    productId,
    quantity,
  }));

  const response = await fetch(`${API_BASE}/orders`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ customerId: crypto.randomUUID(), lines }),
  });

  const result = document.getElementById("checkout-result");
  if (response.ok) {
    const order = await response.json();
    result.textContent = `Order ${order.id} created with status ${order.status}.`;
    cart.clear();
    renderCart();
    loadCatalog();
  } else {
    const problem = await response.json();
    result.textContent = `Checkout failed: ${problem.detail}`;
  }
}

document.getElementById("checkout-button").addEventListener("click", checkout);
loadCatalog();