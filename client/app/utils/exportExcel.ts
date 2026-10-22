import * as XLSX from 'xlsx'

export function downloadExcel(rows: Record<string, unknown>[], filename: string, sheetName = 'Transactions') {
  const ws = XLSX.utils.json_to_sheet(rows)
  const wb = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(wb, ws, sheetName)
  XLSX.writeFile(wb, `${filename}.xlsx`)
}

/** Same rows as downloadExcel, as a UTF-8 CSV (BOM so Excel reads non-ASCII text right). */
export function downloadCsv(rows: Record<string, unknown>[], filename: string) {
  const headers = Object.keys(rows[0] ?? {})
  const esc = (v: unknown) => {
    if (v == null) return ''
    const s = String(v).replace(/"/g, '""')
    return /[",\r\n]/.test(s) ? `"${s}"` : s
  }
  const lines = [headers.map(esc).join(','), ...rows.map(r => headers.map(h => esc(r[h])).join(','))]
  const url = URL.createObjectURL(new Blob(['﻿' + lines.join('\r\n')], { type: 'text/csv;charset=utf-8;' }))
  const a = document.createElement('a')
  a.href = url
  a.download = `${filename}.csv`
  a.click()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}
