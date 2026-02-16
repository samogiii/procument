<template>
  <v-dialog v-model="model" fullscreen transition="dialog-bottom-transition">
    <v-card class="d-flex flex-column" style="background: #1a1a2e;">
      <!-- ── Toolbar ── -->
      <v-toolbar color="rgba(30,30,60,0.95)" density="compact">
        <v-btn icon="mdi-close" @click="model = false" />
        <v-toolbar-title class="text-body-1 font-weight-bold">
          Quotation PDF — {{ quote.quoteNumber }}
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
          <!-- Template Selector -->
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

          <!-- Logo Upload -->
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

          <!-- Custom Header Text -->
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

          <!-- Custom Footer Text -->
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
  quote: any
}>()

const model = defineModel<boolean>({ default: false })

const selectedTemplate = ref<'classic' | 'modern' | 'minimal'>('classic')
const headerText = ref('Your Company Name')
const footerText = ref('Terms and conditions apply. Prices valid for 30 days.')
const logoDataUrl = ref('')
const generating = ref(false)

const templateOptions = [
  { label: '📄 Classic', value: 'classic' },
  { label: '🎨 Modern', value: 'modern' },
  { label: '✨ Minimal', value: 'minimal' },
]

// ── Logo upload handler ──
function onLogoUpload(files: File[] | File | null) {
  const file = Array.isArray(files) ? files[0] : files
  if (!file) { logoDataUrl.value = ''; return }
  const reader = new FileReader()
  reader.onload = (e) => { logoDataUrl.value = (e.target?.result as string) || '' }
  reader.readAsDataURL(file)
}

// ── Computed HTML for preview ──
const renderedHtml = computed(() => {
  const q = props.quote
  const items: any[] = q.items || []
  const logo = logoDataUrl.value
  const header = headerText.value
  const footer = footerText.value
  const tpl = selectedTemplate.value

  const logoImg = logo
    ? `<img src="${logo}" style="max-height:60px; max-width:180px; object-fit:contain;" />`
    : `<div style="width:60px;height:60px;border-radius:8px;background:#e0e0e0;display:flex;align-items:center;justify-content:center;font-size:10px;color:#999;">LOGO</div>`

  const quoteDate = q.createdAt ? new Date(q.createdAt).toLocaleDateString() : '—'
  const validUntil = q.validUntil ? new Date(q.validUntil).toLocaleDateString() : '—'
  const total = q.totalAmount?.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }) || '0.00'

  // ── Table rows ──
  const rows = items.map((it: any, i: number) => `
    <tr>
      <td style="padding:10px 12px; border-bottom:1px solid #eee; text-align:center;">${i + 1}</td>
      <td style="padding:10px 12px; border-bottom:1px solid #eee;">${it.partNumberName || '—'}</td>
      <td style="padding:10px 12px; border-bottom:1px solid #eee;">${it.condition || '—'}</td>
      <td style="padding:10px 12px; border-bottom:1px solid #eee; text-align:center;">${it.qty}</td>
      <td style="padding:10px 12px; border-bottom:1px solid #eee; text-align:right;">$${Number(it.unitPrice).toFixed(2)}</td>
      <td style="padding:10px 12px; border-bottom:1px solid #eee; text-align:right; font-weight:600;">$${Number(it.totalPrice).toFixed(2)}</td>
    </tr>
  `).join('')

  const tableHtml = `
    <table style="width:100%; border-collapse:collapse; font-size:13px; margin-top:16px;">
      <thead>
        <tr style="background:#f5f7fa;">
          <th style="padding:10px 12px; text-align:center; font-weight:600; border-bottom:2px solid #ddd; width:50px;">#</th>
          <th style="padding:10px 12px; text-align:left; font-weight:600; border-bottom:2px solid #ddd;">Part Number</th>
          <th style="padding:10px 12px; text-align:left; font-weight:600; border-bottom:2px solid #ddd;">Condition</th>
          <th style="padding:10px 12px; text-align:center; font-weight:600; border-bottom:2px solid #ddd;">Qty</th>
          <th style="padding:10px 12px; text-align:right; font-weight:600; border-bottom:2px solid #ddd;">Unit Price</th>
          <th style="padding:10px 12px; text-align:right; font-weight:600; border-bottom:2px solid #ddd;">Total</th>
        </tr>
      </thead>
      <tbody>${rows}</tbody>
    </table>
  `

  // ── Info block ──
  const infoBlock = `
    <div style="display:flex; justify-content:space-between; margin:20px 0; font-size:13px;">
      <div>
        <p style="margin:4px 0;"><strong>Quote #:</strong> ${q.quoteNumber || '—'}</p>
        <p style="margin:4px 0;"><strong>Customer:</strong> ${q.customerName || '—'}</p>
        <p style="margin:4px 0;"><strong>Prepared by:</strong> ${q.userName || '—'}</p>
      </div>
      <div style="text-align:right;">
        <p style="margin:4px 0;"><strong>Date:</strong> ${quoteDate}</p>
        <p style="margin:4px 0;"><strong>Valid Until:</strong> ${validUntil}</p>
        <p style="margin:4px 0;"><strong>Status:</strong> ${q.status || '—'}</p>
      </div>
    </div>
  `

  // ── Total ──
  const totalBlock = `
    <div style="display:flex; justify-content:flex-end; margin-top:16px; padding-top:12px; border-top:2px solid #333;">
      <div style="text-align:right;">
        <span style="font-size:14px; color:#666; margin-right:20px;">Grand Total</span>
        <span style="font-size:22px; font-weight:700;">$${total}</span>
      </div>
    </div>
  `

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
              <p style="margin:2px 0 0; font-size:12px; color:#888;">Professional Quotation</p>
            </div>
          </div>
          <div style="text-align:right;">
            <h2 style="margin:0; font-size:28px; color:#1565c0; letter-spacing:2px;">QUOTATION</h2>
          </div>
        </div>
        <!-- Content -->
        <div style="flex:1;">
          ${infoBlock}
          ${tableHtml}
          ${totalBlock}
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
        <div style="background:linear-gradient(135deg, #1a237e 0%, #4a148c 100%); color:#fff; padding:30px 40px; display:flex; justify-content:space-between; align-items:center; border-radius:0 0 12px 12px;">
          <div style="display:flex; align-items:center; gap:16px;">
            ${logo ? `<img src="${logo}" style="max-height:50px; max-width:150px; object-fit:contain; border-radius:6px;" />` : ''}
            <div>
              <h1 style="margin:0; font-size:20px; font-weight:700;">${header}</h1>
              <p style="margin:2px 0 0; font-size:12px; opacity:0.8;">Quotation Document</p>
            </div>
          </div>
          <div style="text-align:right;">
            <p style="margin:0; font-size:32px; font-weight:800; letter-spacing:3px; opacity:0.9;">QUOTE</p>
            <p style="margin:0; font-size:14px; opacity:0.7;">${q.quoteNumber || ''}</p>
          </div>
        </div>
        <!-- Content -->
        <div style="padding:30px 40px; flex:1;">
          ${infoBlock}
          ${tableHtml}
          ${totalBlock}
        </div>
        <!-- Footer -->
        <div style="background:linear-gradient(135deg, #4a148c 0%, #1a237e 100%); color:#fff; padding:16px 40px; display:flex; justify-content:space-between; align-items:center; border-radius:12px 12px 0 0;">
          <span style="font-size:11px; opacity:0.8;">${footer}</span>
          <span style="font-size:11px; opacity:0.6;">Page 1 of 1</span>
        </div>
      </div>
    `
  }

  // Minimal
  return `
    <div style="font-family:'Segoe UI',Roboto,sans-serif; color:#333; padding:40px; box-sizing:border-box; display:flex; flex-direction:column; min-height:100%;">
      <!-- Header -->
      <div style="display:flex; justify-content:space-between; align-items:flex-end; padding-bottom:12px; border-bottom:1px solid #e0e0e0;">
        <div style="display:flex; align-items:center; gap:12px;">
          ${logoImg}
          <span style="font-size:16px; font-weight:600; color:#555;">${header}</span>
        </div>
        <span style="font-size:13px; color:#999;">${q.quoteNumber || ''}</span>
      </div>
      <!-- Content -->
      <div style="flex:1;">
        ${infoBlock}
        ${tableHtml}
        ${totalBlock}
      </div>
      <!-- Footer -->
      <div style="padding-top:12px; border-top:1px solid #e0e0e0; text-align:center; font-size:12px; color:#aaa;">
        <p style="margin:0;">${footer}</p>
        <p style="margin:6px 0 0; font-style:italic;">Thank you for your business</p>
      </div>
    </div>
  `
})

// ── PDF Download ──
const pdfContent = ref<HTMLElement | null>(null)

async function downloadPdf() {
  if (!pdfContent.value) return
  generating.value = true
  try {
    const html2pdf = (await import('html2pdf.js')).default
    const opt = {
      margin: 0,
      filename: `${props.quote.quoteNumber || 'Quote'}.pdf`,
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
