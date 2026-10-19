/**
 * PUT /pending-supplier-quotes/{id} body for an email quote line (pending or accepted).
 * Every editable field is sent, so a partial save (e.g. only the supplier) never clears the rest.
 */
export function toEmailQuoteUpdatePayload(r: any, supplierId: number | null) {
  const num = (v: any) => (v === '' || v == null ? null : Number(v))
  return {
    supplierId,
    partNumber: r.partNumber,
    alt: r.alt || null,
    tagDate: r.tagDate || null,
    price: num(r.price),
    qty: num(r.qty),
    condition: r.condition || null,
    certName: r.certName || null,
    leadTime: r.leadTime || null,
    remark: r.remark || null,
    shippingCost: num(r.shippingCost),
    shippingPoint: r.shippingPoint || null,
    sellLeadTime: r.sellLeadTime || null,
    myNotes: r.myNotes || null,
    customCertReferences: r.customCertReferences ?? [],
    ...quoteDetailsOf(r, null), // Serial Number … Warranty
  }
}
