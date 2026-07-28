import * as XLSX from 'xlsx'

function sortQuoteItems(items: any[]) {
  return [...(items || [])].sort((a: any, b: any) => {
    const aRef = typeof a.rfqReference === 'string' ? a.rfqReference : (typeof a.rfqItemId === 'number' ? a.rfqItemId.toString() : '999')
    const bRef = typeof b.rfqReference === 'string' ? b.rfqReference : (typeof b.rfqItemId === 'number' ? b.rfqItemId.toString() : '999')
    if (aRef !== bRef) return aRef.localeCompare(bRef, undefined, { numeric: true, sensitivity: 'base' })
    const aProcSo = typeof a.procumentRecordSortOrder === 'number' ? a.procumentRecordSortOrder : Number.MAX_SAFE_INTEGER
    const bProcSo = typeof b.procumentRecordSortOrder === 'number' ? b.procumentRecordSortOrder : Number.MAX_SAFE_INTEGER
    return aProcSo - bProcSo
  })
}

function buildQuoteWorksheetData(quote: any, companyName: string): any[][] {
  const sortedItems = sortQuoteItems(quote?.items || [])

  const data: any[][] = [
    [companyName],
    ['QUOTATION'],
    [],
    ['Quote Number:', quote?.quoteNumber || '—', '', 'Date:', quote?.createdAt ? new Date(quote.createdAt).toLocaleDateString() : '—'],
    ['Customer:', quote?.customerName || '—', '', 'RFQ:', quote?.rfqName || '—'],
    [],
    ['#', 'Ref', 'Part Number', 'Alt Part Number', 'Qty', 'Cond', 'Lead Time', 'Unit Price ($)', 'Total Price ($)'],
  ]

  sortedItems.forEach((it: any, idx: number) => {
    data.push([
      idx + 1,
      it.rfqReference || '—',
      it.partNumberName || '—',
      it.alt || '',
      it.qty,
      it.condition || '—',
      it.leadTime || '—',
      Number(it.unitPrice || 0),
      Number(it.totalPrice || 0),
    ])
  })

  data.push([])
  data.push(['', '', '', '', '', '', '', '', 'Subtotal:', Number(quote?.totalAmount || 0)])
  data.push(['', '', '', '', '', '', '', '', 'Grand Total:', Number(quote?.totalAmount || 0)])

  const terms = quote?.customerTermsAndConditions?.trim() || null
  if (terms) {
    data.push([], ['Terms & Conditions:'], [terms])
  }

  return data
}

function buildQuoteWorkbook(quote: any, preset: any) {
  const companyName = preset?.name || 'JETRUX'
  const data = buildQuoteWorksheetData(quote, companyName)
  const ws = XLSX.utils.aoa_to_sheet(data)
  ws['!cols'] = [
    { wch: 5 },
    { wch: 10 },
    { wch: 22 },
    { wch: 22 },
    { wch: 22 },
    { wch: 8 },
    { wch: 8 },
    { wch: 15 },
    { wch: 14 },
    { wch: 14 },
  ]
  const wb = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(wb, ws, 'Quotation')
  return wb
}

export function buildQuoteExcelBlob(quote: any, preset: any): Blob {
  const wb = buildQuoteWorkbook(quote, preset)
  const arrayBuffer = XLSX.write(wb, { bookType: 'xlsx', type: 'array' })
  return new Blob([arrayBuffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' })
}

export function buildQuoteExcelPreviewHtml(quote: any, preset: any): string {
  const wb = buildQuoteWorkbook(quote, preset)
  const sheet = wb.Sheets[wb.SheetNames[0]]
  return XLSX.utils.sheet_to_html(sheet, { editable: false })
}

export function buildQuoteExcelFileName(quote: any) {
  const safeName = (s: string) => (s || '').replace(/[/\\?%*:|"<>]/g, '_').trim()
  return `${safeName(quote?.quoteNumber || 'QT')} - ${safeName(quote?.customerName || '')} - ${safeName(quote?.rfqName || '')}.xlsx`
}
