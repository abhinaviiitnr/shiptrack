export type Cart = Record<string, number>;

const CART_KEY = "shiptrack.cart";

export function readCart(): Cart {
  try {
    const value = JSON.parse(localStorage.getItem(CART_KEY) ?? "{}") as Cart;
    return Object.fromEntries(Object.entries(value).filter(([, quantity]) => Number.isInteger(quantity) && quantity > 0));
  } catch {
    return {};
  }
}

export function writeCart(cart: Cart) {
  localStorage.setItem(CART_KEY, JSON.stringify(cart));
}

export function clearCart() {
  localStorage.removeItem(CART_KEY);
}
