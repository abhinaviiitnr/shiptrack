import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "react-router-dom";
import { api } from "../api";
import { clearCart, readCart, type Cart } from "../cart";

export function CheckoutPage() {
  const [cart] = useState<Cart>(readCart);
  const [shippingMethod, setShippingMethod] = useState(1);
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const products = useQuery({ queryKey: ["checkout-products", Object.keys(cart).sort().join(",")], queryFn: () => api.productsByIds(Object.keys(cart)), enabled: Object.keys(cart).length > 0 });
  const order = useMutation({
    mutationFn: () => api.createOrder(Object.entries(cart).map(([productId, quantity]) => ({ productId, quantity })), shippingMethod),
    onSuccess: (created) => { clearCart(); queryClient.invalidateQueries({ queryKey: ["orders"] }); navigate(`/orders/${created.id}`); },
  });
  const total = useMemo(() => (products.data ?? []).reduce((sum, product) => sum + product.price * (cart[product.id] ?? 0), 0), [cart, products.data]);
  if (!Object.keys(cart).length) return <div className="empty"><h2>Your cart is empty</h2><Link className="primary" to="/">Browse catalogue</Link></div>;
  return <section>
    <Link className="muted back-link" to="/">← Back to catalogue</Link>
    <div className="page-heading"><div><div className="eyebrow">CHECKOUT</div><h1>Review your order</h1></div></div>
    {order.error && <p className="error">{order.error.message}</p>}
    {products.isLoading && <p className="muted">Loading order summary...</p>}
    <div className="checkout-grid"><div className="panel"><h2>Items</h2>{products.data?.map((product) => <div className="admin-order" key={product.id}><div><strong>{product.name}</strong><span>× {cart[product.id]}</span></div><strong>${(product.price * cart[product.id]).toFixed(2)}</strong></div>)}</div>
      <div className="panel"><h2>Delivery</h2><label>Shipping method<select value={shippingMethod} onChange={(event) => setShippingMethod(Number(event.target.value))}><option value="1">Standard</option><option value="2">Express</option></select></label><div className="summary-row total"><span>Estimated total</span><strong>${total.toFixed(2)}</strong></div><button className="primary full" disabled={order.isPending || products.isLoading} onClick={() => order.mutate()}>{order.isPending ? "Placing order..." : "Place order"}</button></div></div>
  </section>;
}
