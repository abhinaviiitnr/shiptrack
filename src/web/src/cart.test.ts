import { afterEach, describe, expect, it } from "vitest";
import { clearCart, readCart, writeCart } from "./cart";

afterEach(() => localStorage.clear());

describe("cart persistence", () => {
  it("writes and reads positive quantities", () => {
    writeCart({ "product-1": 2 });
    expect(readCart()).toEqual({ "product-1": 2 });
  });

  it("removes invalid and non-positive quantities", () => {
    localStorage.setItem("shiptrack.cart", JSON.stringify({
      valid: 1,
      zero: 0,
      negative: -1,
      decimal: 1.5,
    }));
    expect(readCart()).toEqual({ valid: 1 });
  });

  it("clears the stored cart", () => {
    writeCart({ "product-1": 1 });
    clearCart();
    expect(readCart()).toEqual({});
  });
});
