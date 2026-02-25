<template>
  <v-dialog v-model="model" fullscreen transition="dialog-bottom-transition">
    <v-card class="d-flex flex-column" style="background: #1a1a2e;">
      <!-- ── Toolbar ── -->
      <v-toolbar color="rgba(30,30,60,0.95)" density="compact">
        <v-btn icon="mdi-close" @click="model = false" />
        <v-toolbar-title class="text-body-1 font-weight-bold">
          RFQ PDF — {{ rfq.name || `#${rfq.id}` }}
        </v-toolbar-title>
        <v-spacer />
        <v-btn
          variant="tonal"
          color="primary"
          prepend-icon="mdi-download"
          :loading="generating"
          @click="downloadPdf"
        >
          Download PDF
        </v-btn>
      </v-toolbar>

      <!-- ── Controls Panel ── -->
      <v-container fluid class="flex-shrink-0 py-4">
        <v-row dense align="center">
          <v-col cols="12" md="3">
            <v-select
              v-model="selectedTemplate"
              :items="templateOptions"
              item-title="label"
              item-value="value"
              label="Template Style"
              variant="outlined"
              density="compact"
              hide-details
            />
          </v-col>
          <v-col cols="12" md="3">
            <v-file-input
              label="Company Logo"
              variant="outlined"
              density="compact"
              hide-details
              accept="image/*"
              prepend-icon="mdi-image"
              @update:model-value="onLogoUpload"
            />
          </v-col>
          <v-col cols="12" md="3">
            <v-text-field
              v-model="headerText"
              label="Header Text"
              variant="outlined"
              density="compact"
              hide-details
              placeholder="Your Company Name"
            />
          </v-col>
          <v-col cols="12" md="3">
            <v-text-field
              v-model="footerText"
              label="Footer Text"
              variant="outlined"
              density="compact"
              hide-details
              placeholder="Terms & conditions apply"
            />
          </v-col>
        </v-row>
      </v-container>

      <v-divider />

      <!-- ── Live Preview ── -->
      <div class="flex-grow-1 overflow-y-auto d-flex justify-center pa-6" style="background: #12121f;">
        <div
          ref="pdfContent"
          class="pdf-page"
          v-html="renderedHtml"
        />
      </div>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
const props = defineProps<{
  rfq: any
}>()

const model = defineModel<boolean>({ default: false })

const selectedTemplate = ref<'classic' | 'modern' | 'minimal'>('classic')
const headerText = ref('Your Company Name')
const footerText = ref('Terms and conditions apply.')
const logoDataUrl = ref('')
const generating = ref(false)
const pdfContent = ref<HTMLElement | null>(null)

const templateOptions = [
  { label: 'Classic', value: 'classic' },
  { label: 'Modern', value: 'modern' },
  { label: 'Minimal', value: 'minimal' },
]

function onLogoUpload(files: File[] | File | null) {
  const file = Array.isArray(files) ? files[0] : files
  if (!file) { logoDataUrl.value = ''; return }
  const reader = new FileReader()
  reader.onload = (e) => { logoDataUrl.value = (e.target?.result as string) || '' }
  reader.readAsDataURL(file)
}

// ── Computed HTML for preview ──
const renderedHtml = computed(() => {
  const r = props.rfq
  const items: any[] = r.items || []
  const logo = logoDataUrl.value
  const header = headerText.value
  const footer = footerText.value
  const tpl = selectedTemplate.value

  const logoImg = logo
    ? `<img src="${logo}" style="max-height:50px; max-width:150px; object-fit:contain; border-radius:6px;" />`
    : `<div style="width:50px;height:50px;border-radius:8px;background:#e8eaf6;display:flex;align-items:center;justify-content:center;font-size:9px;color:#7986cb;font-weight:600;">LOGO</div>`

  const rfqDate = r.createdAt ? new Date(r.createdAt).toLocaleDateString() : '—'
  const leadTime = r.leadTime ? new Date(r.leadTime).toLocaleDateString() : '—'

  // ── Info block ──
  const infoBlock = `
    <div style="display:flex; justify-content:space-between; margin:24px 0 16px; font-size:13px; gap:20px;">
      <div style="flex:1;">
        <div style="background:#f8fafc; border-radius:8px; padding:14px 16px; border:1px solid #e2e8f0;">
          <p style="margin:0 0 6px; font-size:11px; text-transform:uppercase; letter-spacing:0.5px; color:#94a3b8; font-weight:600;">RFQ Details</p>
          <p style="margin:3px 0; color:#334155;"><strong>Name:</strong> ${r.name || '—'}</p>

        </div>
      </div>
      <div style="flex:1;">
        <div style="background:#f8fafc; border-radius:8px; padding:14px 16px; border:1px solid #e2e8f0;">
          <p style="margin:0 0 6px; font-size:11px; text-transform:uppercase; letter-spacing:0.5px; color:#94a3b8; font-weight:600;">Dates</p>
          <p style="margin:3px 0; color:#334155;"><strong>Created:</strong> ${rfqDate}</p>
          <p style="margin:3px 0; color:#334155;"><strong>Items:</strong> ${items.length} part${items.length !== 1 ? 's' : ''}</p>
        </div>
      </div>
    </div>
  `

  // ── Table rows ──
  const rows = items.map((it: any, i: number) => {
    const alts = it.alternatives && it.alternatives.length > 0
      ? it.alternatives.map((a: any) => `<span style="display:inline-block;background:#fff8e1;color:#f57f17;padding:1px 6px;border-radius:3px;font-size:11px;margin:1px 2px;">${a.name}</span>`).join(' ')
      : '<span style="color:#ccc;">—</span>'

    const bgColor = i % 2 === 0 ? '#ffffff' : '#f8fafc'
    // const priorityColor = (it.priority || '').toLowerCase() === 'aog' ? '#ef4444'
    //   : (it.priority || '').toLowerCase() === 'urgent' ? '#f59e0b' : '#64748b'

    return `
    <tr style="background:${bgColor};">
      <td style="padding:10px 12px; border-bottom:1px solid #f1f5f9; text-align:center; color:#94a3b8; font-weight:600;">${i + 1}</td>
      <td style="padding:10px 12px; border-bottom:1px solid #f1f5f9; font-weight:600; color:#1e40af; font-family:'Courier New',monospace; font-size:12px;">${it.partNumberName || '—'}</td>
      <td style="padding:10px 12px; border-bottom:1px solid #f1f5f9; color:#475569;">${it.description || '—'}</td>
      <td style="padding:10px 12px; border-bottom:1px solid #f1f5f9; text-align:center; font-weight:600;">${it.qty || 1}</td>
      <td style="padding:10px 12px; border-bottom:1px solid #f1f5f9; text-align:center;"><span style="background:#e0f2fe;color:#0369a1;padding:2px 8px;border-radius:4px;font-size:11px;font-weight:600;">${it.condition || '—'}</span></td>
    </tr>
    <tr style="background:${bgColor};">
      <td colspan="6" style="padding:2px 12px 10px 48px; border-bottom:1px solid #e2e8f0; font-size:11px; color:#64748b; line-height:1.8;">
        <strong style="color:#475569;">Remark:</strong> ${it.remark || '—'} &nbsp;&nbsp;&bull;&nbsp;&nbsp;
        <strong style="color:#475569;">Alternatives:</strong> ${alts}
      </td>
    </tr>`
  }).join('')

  const tableHtml = `
    <table style="width:100%; border-collapse:collapse; font-size:13px; margin-top:8px; border-radius:8px; overflow:hidden;">
      <thead>
        <tr style="background:#1e293b;">
          <th style="padding:11px 12px; text-align:center; font-weight:600; color:#e2e8f0; font-size:11px; text-transform:uppercase; letter-spacing:0.5px; width:45px;">#</th>
          <th style="padding:11px 12px; text-align:left; font-weight:600; color:#e2e8f0; font-size:11px; text-transform:uppercase; letter-spacing:0.5px;">Part Number</th>
          <th style="padding:11px 12px; text-align:left; font-weight:600; color:#e2e8f0; font-size:11px; text-transform:uppercase; letter-spacing:0.5px;">Description</th>
          <th style="padding:11px 12px; text-align:center; font-weight:600; color:#e2e8f0; font-size:11px; text-transform:uppercase; letter-spacing:0.5px; width:60px;">Qty</th>
          <th style="padding:11px 12px; text-align:center; font-weight:600; color:#e2e8f0; font-size:11px; text-transform:uppercase; letter-spacing:0.5px; width:80px;">Cond.</th>
        </tr>
      </thead>
      <tbody>${rows}</tbody>
    </table>
  `

  // ── Notes block ──
  const notesBlock = r.notes ? `
    <div style="margin-top:24px; padding:14px 16px; background:#fffbeb; border-left:4px solid #f59e0b; border-radius:0 8px 8px 0; font-size:12px; color:#92400e;">
      <strong style="display:block; margin-bottom:4px; color:#78350f;">Notes</strong>
      ${r.notes}
    </div>
  ` : ''

  // ── Templates ──
  if (tpl === 'classic') {
    return `
      <div style="font-family:'Segoe UI',Roboto,sans-serif; color:#222; padding:40px; box-sizing:border-box; display:flex; flex-direction:column; min-height:297mm;">
        <!-- Header -->
        <div style="display:flex; justify-content:space-between; align-items:center; padding-bottom:16px; border-bottom:3px solid #1565c0;">
          <div style="display:flex; align-items:center; gap:16px;">
            ${logoImg}
            <div>
              <h1 style="margin:0; font-size:22px; color:#1565c0;">${header}</h1>
              <p style="margin:2px 0 0; font-size:12px; color:#888;">Aviation Procurement</p>
              <p style="margin:2px 0 0; font-size:10px; color:#aaa;">Unit 1203, 12/F, Tower 1, Lippo Centre, 89 Queensway, Admiralty, Hong Kong</p>
              <p style="margin:1px 0 0; font-size:10px; color:#aaa;">Tel: +852 1234 5678 &nbsp;|&nbsp; Email: info@company.com</p>
            </div>
          </div>
          <div style="text-align:right;">
            <h2 style="margin:0; font-size:28px; color:#1565c0; letter-spacing:2px;">RFQ</h2>
            <p style="margin:2px 0 0; font-size:12px; color:#666;">#${r.id || '—'}</p>
          </div>
        </div>
        <!-- Content -->
        <div style="flex:1;">
          ${infoBlock}
          ${tableHtml}
          ${notesBlock}
        </div>
        <!-- Footer -->
        <div style="padding-top:16px; border-top:1px solid #ddd; text-align:center; font-size:11px; color:#999;">
          <p style="margin:0;">${footer}</p>
          <p style="margin:4px 0 0;">Page 1 of 1</p>
        </div>
      </div>
    `
  }

  if (tpl === 'modern') {
    return `
      <div style="font-family:'Segoe UI',Roboto,sans-serif; color:#222; box-sizing:border-box; display:flex; flex-direction:column; min-height:297mm;">
        <!-- Header Banner -->
        <div style="background:linear-gradient(135deg, #0f172a 0%, #1e3a5f 50%, #0c4a6e 100%); color:#fff; padding:30px 40px; display:flex; justify-content:space-between; align-items:center;">
          <div style="display:flex; align-items:center; gap:16px;">
            ${logo ? `<img src="${logo}" style="max-height:50px; max-width:150px; object-fit:contain; border-radius:6px;" />` : ''}
            <div>
              <h1 style="margin:0; font-size:20px; font-weight:700;">${header}</h1>
              <p style="margin:2px 0 0; font-size:12px; opacity:0.7;">Aviation Procurement</p>
              <p style="margin:2px 0 0; font-size:10px; opacity:0.5;">Unit 1203, 12/F, Tower 1, Lippo Centre, 89 Queensway, Admiralty, Hong Kong</p>
              <p style="margin:1px 0 0; font-size:10px; opacity:0.5;">Tel: +852 1234 5678 &nbsp;|&nbsp; Email: info@company.com</p>
            </div>
          </div>
          <div style="text-align:right;">
            <p style="margin:0; font-size:32px; font-weight:800; letter-spacing:3px; opacity:0.9;">RFQ</p>
            <p style="margin:0; font-size:14px; opacity:0.6;">#${r.id || '—'}</p>
          </div>
        </div>
        <!-- Content -->
        <div style="padding:30px 40px; flex:1;">
          ${infoBlock}
          ${tableHtml}
          ${notesBlock}
        </div>
        <!-- Footer -->
        <div style="background:linear-gradient(135deg, #0c4a6e 0%, #0f172a 100%); color:#fff; padding:16px 40px; display:flex; justify-content:space-between; align-items:center;">
          <span style="font-size:11px; opacity:0.8;">${footer}</span>
          <span style="font-size:11px; opacity:0.6;">Page 1 of 1</span>
        </div>
      </div>
    `
  }

  // Minimal
  return `
    <div style="font-family:'Segoe UI',Roboto,sans-serif; color:#333; padding:40px; box-sizing:border-box; display:flex; flex-direction:column; min-height:297mm;">
      <!-- Header -->
      <div style="display:flex; justify-content:space-between; align-items:flex-end; padding-bottom:12px; border-bottom:1px solid #e0e0e0;">
        <div style="display:flex; align-items:center; gap:12px;">
          ${logoImg}
          <div>
            <span style="font-size:16px; font-weight:600; color:#555;">${header}</span>
            <p style="margin:2px 0 0; font-size:10px; color:#aaa;">Unit 1203, 12/F, Tower 1, Lippo Centre, 89 Queensway, Admiralty, Hong Kong</p>
            <p style="margin:1px 0 0; font-size:10px; color:#aaa;">Tel: +852 1234 5678 &nbsp;|&nbsp; Email: info@company.com</p>
          </div>
        </div>
        <div style="text-align:right;">
          <span style="font-size:18px; font-weight:700; color:#333;">RFQ</span>
          <span style="font-size:13px; color:#999; margin-left:6px;">#${r.id || '—'}</span>
        </div>
      </div>
      <!-- Content -->
      <div style="flex:1;">
        ${infoBlock}
        ${tableHtml}
        ${notesBlock}
      </div>
      <!-- Footer -->
      <div style="padding-top:12px; border-top:1px solid #e0e0e0; text-align:center; font-size:12px; color:#aaa;">
        <p style="margin:0;">${footer}</p>
      </div>
    </div>
  `
})

// ── PDF Download ──
async function downloadPdf() {
  if (!pdfContent.value) return
  generating.value = true
  try {
    const html2pdf = (await import('html2pdf.js')).default
    const opt = {
      margin: 0,
      filename: `RFQ_${props.rfq.name || props.rfq.id}.pdf`,
      image: { type: 'jpeg' as const, quality: 0.98 },
      html2canvas: { scale: 2, useCORS: true, logging: false },
      jsPDF: { unit: 'mm' as const, format: 'a4', orientation: 'portrait' as const },
    }
    await html2pdf().set(opt).from(pdfContent.value).save()
  } catch (err) {
    console.error('PDF generation failed:', err)
  } finally {
    generating.value = false
  }
}
</script>

<style scoped>
.pdf-page {
  width: 210mm;
  min-height: 297mm;
  background: #fff;
  box-shadow: 0 4px 40px rgba(0,0,0,0.4);
  border-radius: 4px;
  overflow: hidden;
}
</style>
