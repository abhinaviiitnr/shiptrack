import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { api, type Product } from "../api";
import { readCart, writeCart, type Cart } from "../cart";

export function ShopPage() {
  const [search, setSearch] = useState(""); const [cart, setCart] = useState<Cart>(readCart); const [message, setMessage] = useState("");
  const navigate = useNavigate(); const products = useQuery({ queryKey: ["products", search], queryFn: () => api.products(1, search) });
  const items = products.data?.items ?? []; const cartCount = Object.values(cart).reduce((sum, qty) => sum + qty, 0);
  function addToCart(productId: string) {
    const next = { ...cart, [productId]: (cart[productId] ?? 0) + 1 };
    setCart(next); writeCart(next); setMessage("");
  }
  return <><section className="hero"><div><div className="eyebrow">SIMPLE SHIPPING</div><h1>Everything you need,<br /><em>delivered.</em></h1><p>Browse our catalogue and get your order moving in minutes.</p></div><div className="hero-mark">✦</div></section>
    <div className="toolbar"><input placeholder="Search products..." value={search} onChange={(e) => setSearch(e.target.value)} /><span className="muted">{products.data?.total ?? 0} products</span></div>
    {message && <p className="success">{message}</p>}{products.isLoading && <p className="muted">Loading products...</p>}{products.error && <p className="error">{products.error.message}</p>}
    <div className="product-grid">{items.map((product) => <ProductCard key={product.id} product={product} quantity={cart[product.id] ?? 0} onAdd={() => addToCart(product.id)} />)}</div>
    {cartCount > 0 && <aside className="cart"><strong>{cartCount} item{cartCount > 1 ? "s" : ""} in cart</strong><span>Ready to review</span><button className="primary" onClick={() => navigate("/checkout")}>Checkout</button></aside>}
  </>;
}
function ProductCard({ product, quantity, onAdd }: { product: Product; quantity: number; onAdd: () => void }) {
  return <article className="product-card"><div className="product-icon">▦</div><div className="product-info"><span className="sku">{product.sku}</span><h3>{product.name}</h3><p>{product.description || "Reliable shipping essential."}</p><div className="product-footer"><strong>${product.price.toFixed(2)}</strong><button className="add-button" onClick={onAdd}>{quantity ? `+ Add (${quantity})` : "+ Add"}</button></div></div></article>;
}
