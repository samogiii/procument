/**
 * Live-preview renderers for the alternative PDF templates.
 *
 * The backend (src/Procument.API/Pdf/Templates) renders three looks from one shared document
 * model: Modern (the original), Classic (plain black-and-white agency form) and Standard
 * (halfway — fully ruled grid with restrained brand colour). Each PDF dialog keeps its own
 * hand-written HTML for Modern; these two functions mirror the other two so the on-screen
 * preview matches what the API produces.
 *
 * Keep this file in step with ClassicTemplateRenderer.cs / StandardTemplateRenderer.cs.
 */

export type PdfTemplateKey = 'modern' | 'classic' | 'standard'

export const PDF_TEMPLATE_OPTIONS: { title: string; value: PdfTemplateKey }[] = [
  { title: 'Modern (default)', value: 'modern' },
  { title: 'Classic (plain form)', value: 'classic' },
  { title: 'Standard (ruled)', value: 'standard' },
]

export interface PdfPreviewField { label: string; value?: string | null }

export interface PdfPreviewAddress {
  title: string
  name?: string | null
  address?: string | null
  fields?: PdfPreviewField[]
  /** Secondary block inside the same box, under a blank line (FFW under Ship To). */
  appended?: PdfPreviewAddress | null
}

export interface PdfPreviewColumn {
  header: string
  /** Fixed width in PDF points; converted to px for the preview. */
  width?: number
  relative?: number
  align?: 'left' | 'center' | 'right'
}

export interface PdfPreviewCell {
  text?: string | null
  /** Smaller second line inside the cell — "(Alt to: …)". */
  subText?: string | null
  highlight?: boolean
  negative?: boolean
  bold?: boolean
}

export interface PdfPreviewRow { cells: PdfPreviewCell[]; note?: string | null }

export interface PdfPreviewInfoBlock { title: string; fields: PdfPreviewField[] }

/** `amount` is already formatted, currency symbol included. */
export interface PdfPreviewTotal { label: string; amount: string; isGrand?: boolean; isNegative?: boolean }

export interface PdfPreviewModel {
  docTitle: string
  docNumber?: string | null
  /** Bold notice line under the header — usually omitted. */
  notice?: string | null
  logoDataUrl?: string | null
  companyName?: string | null
  companyLocation?: string | null
  companyPhone?: string | null
  companyWebsite?: string | null
  companyEmail?: string | null
  primary: string
  accent: string
  meta?: PdfPreviewField[]
  addresses?: PdfPreviewAddress[]
  itemsTitle?: string
  columns: PdfPreviewColumn[]
  rows: PdfPreviewRow[]
  infoBlocks?: PdfPreviewInfoBlock[]
  totals?: PdfPreviewTotal[]
  comments?: string | null
  terms?: string | null
  footerText?: string | null
  /** Classic only — the form-style signature grid. */
  showSignature?: boolean
}

// ── helpers ───────────────────────────────────────────────
const esc = (v: unknown): string => String(v ?? '')
  .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;')

/** PDF points → preview pixels. */
const px = (pt: number) => Math.round(pt * 1.333)

const filled = (fields?: PdfPreviewField[]) => (fields || []).filter(f => !!f.value && String(f.value).trim() !== '')

const hasContent = (b: PdfPreviewInfoBlock) => filled(b.fields).length > 0

/** A form reads better with empty boxes than with em-dashes. */
const plain = (v?: string | null) => (!v || v === '—' ? '' : esc(v))

const logoTag = (src?: string | null, maxHeight = 48) =>
  src ? `<img src="${src}" style="max-height:${maxHeight}px;max-width:170px;object-fit:contain;" />` : ''

const colStyle = (c: PdfPreviewColumn) =>
  c.width ? `width:${px(c.width)}px;` : ''

// ══════════════════════════════════════════════════════════
// CLASSIC — black and white, boxed, agency-form look
// ══════════════════════════════════════════════════════════
export function renderClassicPreview(m: PdfPreviewModel): string {
  const R = '1px solid #000'
  const BAR = '#e0e0e0'

  const contactLines = [
    m.companyLocation,
    m.companyPhone ? `Tel: ${m.companyPhone}` : '',
    m.companyWebsite ? `Web: ${m.companyWebsite}` : '',
    m.companyEmail ? `Email: ${m.companyEmail}` : '',
  ].filter(Boolean).map(l => `<div>${esc(l)}</div>`).join('')

  const header = `
    <table style="width:100%;border-collapse:collapse;border:${R};table-layout:fixed;">
      <tr>
        <td style="width:30%;border-right:${R};padding:8px;vertical-align:middle;">
          ${logoTag(m.logoDataUrl, 42)}
          ${m.companyName ? `<div style="font-size:14px;font-weight:700;margin-top:4px;">${esc(m.companyName)}</div>` : ''}
        </td>
        <td style="width:40%;border-right:${R};padding:8px;text-align:center;vertical-align:middle;">
          <div style="font-size:20px;font-weight:700;letter-spacing:1px;">${esc(m.docTitle).toUpperCase()}</div>
          ${m.docNumber ? `<div style="font-size:12px;margin-top:2px;">No. ${esc(m.docNumber)}</div>` : ''}
        </td>
        <td style="width:30%;padding:8px;font-size:10px;line-height:1.55;vertical-align:middle;">${contactLines}</td>
      </tr>
    </table>`

  const meta = (m.meta || []).length
    ? `<table style="width:100%;border-collapse:collapse;border-left:${R};border-right:${R};border-bottom:${R};table-layout:fixed;">
         <tr>${(m.meta || []).map((f, i, arr) => `
           <td style="padding:5px 6px;${i < arr.length - 1 ? `border-right:${R};` : ''}">
             <div style="font-size:8.5px;font-weight:700;letter-spacing:.5px;">${esc(f.label).toUpperCase()}</div>
             <div style="font-size:11px;margin-top:1px;">${plain(f.value)}</div>
           </td>`).join('')}</tr>
       </table>`
    : ''

  const notice = m.notice
    ? `<div style="border-left:${R};border-right:${R};border-bottom:${R};padding:5px;text-align:center;font-size:12px;font-weight:700;">${esc(m.notice).toUpperCase()}</div>`
    : ''

  const addressBody = (a: PdfPreviewAddress, topPad: number) => `
    <div style="${topPad ? `margin-top:${topPad}px;` : ''}font-size:10px;">
      <div style="font-size:12px;font-weight:700;">${plain(a.name)}</div>
      ${a.address ? `<div style="margin-top:1px;white-space:pre-wrap;">${esc(a.address)}</div>` : ''}
      ${filled(a.fields).map(f => `<div><strong>${esc(f.label)}:</strong> ${esc(f.value)}</div>`).join('')}
    </div>`

  const addresses = (m.addresses || []).length
    ? `<table style="width:100%;border-collapse:collapse;border:${R};margin-top:9px;table-layout:fixed;">
         <tr>${(m.addresses || []).map((a, i, arr) => `
           <td style="vertical-align:top;padding:0;${i < arr.length - 1 ? `border-right:${R};` : ''}">
             <div style="background:${BAR};border-bottom:${R};padding:4px 6px;font-size:9.5px;font-weight:700;letter-spacing:.5px;">${esc(a.title).toUpperCase()}</div>
             <div style="padding:7px;">
               ${addressBody(a, 0)}
               ${a.appended ? `<div style="margin-top:12px;font-size:9.5px;font-weight:700;letter-spacing:.5px;">${esc(a.appended.title).toUpperCase()}</div>${addressBody(a.appended, 1)}` : ''}
             </div>
           </td>`).join('')}</tr>
       </table>`
    : ''

  const headerCells = m.columns.map((c, i, arr) => `
    <th style="padding:5px 4px;font-size:9.5px;font-weight:700;text-align:${c.align || 'center'};${colStyle(c)}${i < arr.length - 1 ? `border-right:${R};` : ''}">${esc(c.header)}</th>`).join('')

  const bodyRows = m.rows.map((row, r) => {
    const isLast = r === m.rows.length - 1
    const bb = isLast || row.note ? '' : `border-bottom:${R};`
    const cells = m.columns.map((c, i, arr) => {
      const v = row.cells[i] || {}
      const br = i < arr.length - 1 ? `border-right:${R};` : ''
      const inner = v.subText
        ? `<div style="font-weight:700;">${plain(v.text)}</div><div style="font-size:8.5px;">${esc(v.subText)}</div>`
        : `<span style="${v.bold || v.highlight ? 'font-weight:700;' : ''}">${plain(v.text)}</span>`
      return `<td style="padding:4px;font-size:10px;vertical-align:middle;text-align:${c.align || 'center'};${colStyle(c)}${br}${bb}">${inner}</td>`
    }).join('')
    const noteRow = row.note
      ? `<tr><td colspan="${m.columns.length}" style="padding:0 5px 4px 5px;font-size:9.5px;font-style:italic;${isLast ? '' : `border-bottom:${R};`}">${esc(row.note)}</td></tr>`
      : ''
    return `<tr>${cells}</tr>${noteRow}`
  }).join('')

  const table = `
    ${sectionBar(m.itemsTitle || 'Description of Materials or Services', R, BAR, 9)}
    <table style="width:100%;border-collapse:collapse;border-left:${R};border-right:${R};border-bottom:${R};table-layout:fixed;">
      <thead><tr style="background:${BAR};border-bottom:${R};">${headerCells}</tr></thead>
      <tbody>${bodyRows}</tbody>
    </table>`

  const infoHtml = (m.infoBlocks || []).filter(hasContent).map(b => `
    <div style="border:${R};margin-bottom:8px;">
      <div style="background:${BAR};border-bottom:${R};padding:4px 6px;font-size:9.5px;font-weight:700;letter-spacing:.5px;">${esc(b.title).toUpperCase()}</div>
      <div style="padding:7px;font-size:10px;">${filled(b.fields).map(f => `<div><strong>${esc(f.label)}:</strong> ${esc(f.value)}</div>`).join('')}</div>
    </div>`).join('')

  const totalsHtml = (m.totals || []).length
    ? `<table style="width:305px;border-collapse:collapse;border:${R};">
         ${(m.totals || []).map((t, i, arr) => `
           <tr style="${t.isGrand ? `background:${BAR};` : ''}">
             <td style="padding:5px 6px;font-size:11px;${t.isGrand ? 'font-weight:700;' : ''}${i < arr.length - 1 ? `border-bottom:${R};` : ''}">${esc(t.label)}</td>
             <td style="padding:5px 6px;font-size:${t.isGrand ? '13px' : '11px'};text-align:right;${t.isGrand ? 'font-weight:700;' : ''}${i < arr.length - 1 ? `border-bottom:${R};` : ''}">${t.isNegative ? '-' : ''}${esc(t.amount)}</td>
           </tr>`).join('')}
       </table>`
    : ''

  const summary = (infoHtml || totalsHtml)
    ? `<div style="display:flex;justify-content:space-between;align-items:flex-start;gap:12px;margin-top:9px;">
         <div style="flex:1;">${infoHtml}</div>
         <div>${totalsHtml}</div>
       </div>`
    : ''

  const textBlock = (title: string, body?: string | null) => body
    ? `${sectionBar(title, R, BAR, 9)}<div style="border-left:${R};border-right:${R};border-bottom:${R};padding:8px;font-size:10px;white-space:pre-wrap;line-height:1.5;">${esc(body)}</div>`
    : ''

  const signature = m.showSignature
    ? `${sectionBar('This section to be completed by the issuer', R, BAR, 9)}
       <table style="width:100%;border-collapse:collapse;border-left:${R};border-right:${R};border-bottom:${R};table-layout:fixed;">
         <tr>
           ${sigFilled('Company Name', m.companyName, R, true)}
           ${sigFilled('Address', m.companyLocation, R, true)}
           ${sigFilled('Phone', m.companyPhone, R, true)}
           ${sigFilled('Email', m.companyEmail, R, false)}
         </tr>
       </table>
       <table style="width:100%;border-collapse:collapse;border-left:${R};border-right:${R};border-bottom:${R};table-layout:fixed;">
         <tr>
           ${sigBlank('Signature', R, true)}
           ${sigBlank('Typed Name and Title', R, true)}
           ${sigBlank('Date', R, false)}
         </tr>
       </table>`
    : ''

  return `
    <div style="font-family:Arial,Helvetica,sans-serif;color:#000;padding:20px;display:flex;flex-direction:column;min-height:297mm;">
      ${header}${meta}${notice}${addresses}
      <div style="margin-top:9px;">${table}</div>
      ${summary}
      <div style="margin-top:9px;">${textBlock('Comments', m.comments)}</div>
      <div style="margin-top:9px;">${textBlock('Terms and Conditions', m.terms)}</div>
      <div style="margin-top:9px;">${signature}</div>
      <div style="margin-top:auto;padding-top:6px;border-top:${R};display:flex;justify-content:space-between;font-size:9.5px;">
        <span>${esc(m.footerText || '')}</span>
        <span>Page 1 of 1</span>
        <span>${esc(m.companyEmail || '')}</span>
      </div>
    </div>`
}

function sectionBar(text: string, rule: string, bar: string, fontSize: number): string {
  return `<div style="border:${rule};background:${bar};padding:5px 6px;font-size:${fontSize + 1}px;font-weight:700;letter-spacing:.5px;">${esc(text).toUpperCase()}</div>`
}

function sigFilled(label: string, value: string | null | undefined, rule: string, border: boolean): string {
  return `<td style="padding:5px 6px;vertical-align:top;${border ? `border-right:${rule};` : ''}">
    <div style="font-size:8.5px;">${esc(label)}</div>
    <div style="font-size:10.5px;margin-top:1px;">${plain(value)}</div>
  </td>`
}

function sigBlank(label: string, rule: string, border: boolean): string {
  return `<td style="padding:5px 6px;vertical-align:bottom;${border ? `border-right:${rule};` : ''}">
    <div style="height:26px;"></div>
    <div style="border-top:${rule};padding-top:2px;font-size:8.5px;">${esc(label)}</div>
  </td>`
}

// ══════════════════════════════════════════════════════════
// STANDARD — ruled grid, brand colour used sparingly
// ══════════════════════════════════════════════════════════
export function renderStandardPreview(m: PdfPreviewModel): string {
  const p = m.primary
  const a = m.accent
  const R = '1px solid #bdbdbd'
  const BAND = '#eeeeee'
  const MUTED = '#757575'

  const contactLines = [
    m.companyLocation,
    m.companyPhone ? `Tel: ${m.companyPhone}` : '',
    m.companyWebsite ? `Web: ${m.companyWebsite}` : '',
    m.companyEmail ? `Email: ${m.companyEmail}` : '',
  ].filter(Boolean).map(l => `<div>${esc(l)}</div>`).join('')

  const header = `
    <div style="display:flex;justify-content:space-between;align-items:flex-end;gap:20px;">
      <div>
        ${logoTag(m.logoDataUrl, 52)}
        <div style="font-size:17px;font-weight:700;color:${p};margin-top:4px;">${esc(m.companyName || '')}</div>
        <div style="font-size:10px;color:${MUTED};line-height:1.6;">${contactLines}</div>
      </div>
      <div style="text-align:right;">
        <div style="font-size:25px;font-weight:700;color:${p};letter-spacing:.5px;">${esc(m.docTitle).toUpperCase()}</div>
        ${m.docNumber ? `<div style="font-size:13px;color:${MUTED};margin-top:2px;">${esc(m.docNumber)}</div>` : ''}
      </div>
    </div>
    <div style="height:2px;background:${p};margin-top:8px;"></div>`

  const meta = (m.meta || []).length
    ? `<table style="width:100%;border-collapse:collapse;border:${R};margin-top:11px;table-layout:fixed;">
         <tr>${(m.meta || []).map((f, i, arr) => `
           <td style="padding:8px;${i < arr.length - 1 ? `border-right:${R};` : ''}">
             <div style="font-size:8.5px;font-weight:700;letter-spacing:.5px;color:${p};">${esc(f.label).toUpperCase()}</div>
             <div style="font-size:12px;color:${MUTED};margin-top:2px;">${esc(f.value || '—')}</div>
           </td>`).join('')}</tr>
       </table>`
    : ''

  const notice = m.notice
    ? `<div style="border:1px solid ${p};padding:6px;text-align:center;font-size:12px;font-weight:700;color:${p};margin-top:11px;">${esc(m.notice).toUpperCase()}</div>`
    : ''

  const addressBody = (b: PdfPreviewAddress, topPad: number) => `
    <div style="${topPad ? `margin-top:${topPad}px;` : ''}">
      <div style="font-size:13px;font-weight:700;color:${p};">${esc(b.name || '—')}</div>
      ${b.address ? `<div style="font-size:10.5px;color:${MUTED};margin-top:2px;white-space:pre-wrap;">${esc(b.address)}</div>` : ''}
      ${filled(b.fields).map(f => `<div style="font-size:10.5px;"><strong style="color:${p};">${esc(f.label)}:</strong> <span style="color:${MUTED};">${esc(f.value)}</span></div>`).join('')}
    </div>`

  const addresses = (m.addresses || []).length
    ? `<div style="display:flex;gap:11px;margin-top:13px;">
         ${(m.addresses || []).map(b => `
           <div style="flex:1;border:${R};">
             <div style="background:${BAND};border-bottom:${R};padding:5px 8px;font-size:9.5px;font-weight:700;letter-spacing:.5px;color:${p};">${esc(b.title).toUpperCase()}</div>
             <div style="padding:10px;">
               ${addressBody(b, 0)}
               ${b.appended ? `<div style="margin-top:13px;font-size:9.5px;font-weight:700;letter-spacing:.5px;color:${p};">${esc(b.appended.title).toUpperCase()}</div>${addressBody(b.appended, 2)}` : ''}
             </div>
           </div>`).join('')}
       </div>`
    : ''

  const headerCells = m.columns.map((c, i, arr) => `
    <th style="padding:7px 5px;font-size:10px;font-weight:700;color:${p};text-align:${c.align || 'center'};${colStyle(c)}${i < arr.length - 1 ? `border-right:${R};` : ''}">${esc(c.header)}</th>`).join('')

  const bodyRows = m.rows.map((row, r) => {
    const isLast = r === m.rows.length - 1
    const bb = isLast || row.note ? '' : `border-bottom:${R};`
    const cells = m.columns.map((c, i, arr) => {
      const v = row.cells[i] || {}
      const color = v.negative ? '#e53935' : v.highlight ? a : v.bold ? p : MUTED
      const br = i < arr.length - 1 ? `border-right:${R};` : ''
      const inner = v.subText
        ? `<div style="font-weight:700;color:${color};">${esc(v.text || '—')}</div><div style="font-size:8.5px;color:#9e9e9e;">${esc(v.subText)}</div>`
        : `<span style="color:${color};${v.bold || v.highlight ? 'font-weight:700;' : ''}">${esc(v.text || '—')}</span>`
      return `<td style="padding:7px 5px;font-size:10.5px;vertical-align:middle;text-align:${c.align || 'center'};${colStyle(c)}${br}${bb}">${inner}</td>`
    }).join('')
    const noteRow = row.note
      ? `<tr><td colspan="${m.columns.length}" style="padding:0 6px 5px 6px;font-size:10px;color:${MUTED};${isLast ? '' : `border-bottom:${R};`}">${esc(row.note)}</td></tr>`
      : ''
    return `<tr>${cells}</tr>${noteRow}`
  }).join('')

  const table = `
    <div style="border-left:3px solid ${p};padding-left:8px;margin-top:15px;font-size:10.5px;font-weight:700;letter-spacing:.5px;color:${p};">${esc(m.itemsTitle || 'Description of Materials or Services').toUpperCase()}</div>
    <table style="width:100%;border-collapse:collapse;border:${R};margin-top:5px;table-layout:fixed;">
      <thead><tr style="background:${BAND};border-bottom:1px solid ${p};">${headerCells}</tr></thead>
      <tbody>${bodyRows}</tbody>
    </table>`

  const infoHtml = (m.infoBlocks || []).filter(hasContent).map(b => `
    <div style="border:${R};margin-bottom:10px;">
      <div style="background:${BAND};border-bottom:${R};padding:5px 8px;font-size:9.5px;font-weight:700;letter-spacing:.5px;color:${p};">${esc(b.title).toUpperCase()}</div>
      <div style="padding:10px;font-size:10.5px;">${filled(b.fields).map(f => `<div><strong style="color:${p};">${esc(f.label)}:</strong> <span style="color:${MUTED};">${esc(f.value)}</span></div>`).join('')}</div>
    </div>`).join('')

  const totalsHtml = (m.totals || []).length
    ? `<table style="width:312px;border-collapse:collapse;border:${R};">
         ${(m.totals || []).map((t, i, arr) => `
           <tr style="${t.isGrand ? `background:${BAND};` : ''}">
             <td style="padding:8px;font-size:11px;color:${t.isGrand ? p : MUTED};${t.isGrand ? `font-weight:700;border-top:2px solid ${p};` : ''}${!t.isGrand && i < arr.length - 1 ? `border-bottom:${R};` : ''}">${esc(t.label)}</td>
             <td style="padding:8px;font-size:${t.isGrand ? '15px' : '11px'};text-align:right;color:${t.isNegative ? '#e53935' : p};${t.isGrand ? `font-weight:700;border-top:2px solid ${p};` : ''}${!t.isGrand && i < arr.length - 1 ? `border-bottom:${R};` : ''}">${t.isNegative ? '-' : ''}${esc(t.amount)}</td>
           </tr>`).join('')}
       </table>`
    : ''

  const summary = (infoHtml || totalsHtml)
    ? `<div style="display:flex;justify-content:space-between;align-items:flex-start;gap:16px;margin-top:15px;">
         <div style="flex:1;">${infoHtml}</div>
         <div>${totalsHtml}</div>
       </div>`
    : ''

  const textBlock = (title: string, body?: string | null) => body
    ? `<div style="border:${R};margin-top:13px;">
         <div style="background:${BAND};border-bottom:${R};padding:5px 8px;font-size:9.5px;font-weight:700;letter-spacing:.5px;color:${p};">${esc(title).toUpperCase()}</div>
         <div style="padding:10px;font-size:10.5px;color:${MUTED};white-space:pre-wrap;line-height:1.55;">${esc(body)}</div>
       </div>`
    : ''

  return `
    <div style="font-family:'Segoe UI','Helvetica Neue',Arial,sans-serif;padding:24px;display:flex;flex-direction:column;min-height:297mm;">
      ${header}${meta}${notice}${addresses}${table}${summary}
      ${textBlock('Comments', m.comments)}
      ${textBlock('Terms & Conditions', m.terms)}
      <div style="margin-top:auto;padding-top:8px;border-top:1px solid ${p};display:flex;justify-content:space-between;font-size:10px;color:#9e9e9e;">
        <span>${esc(m.footerText || '')}</span>
        <span>Page 1 / 1</span>
        <span style="font-weight:700;color:${p};">${esc(m.companyEmail || '')}</span>
      </div>
    </div>`
}
