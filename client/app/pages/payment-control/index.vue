<template>
  <v-container fluid class="pa-4">
    <div class="d-flex align-center justify-space-between mb-4">
      <div>
        <div class="text-h5 font-weight-bold">Wallets</div>
        <div class="text-caption text-medium-emphasis">Company wallet accounts and ledger</div>
      </div>      <v-btn
        v-if="authStore.isSuperAdmin && tab === 'wallets'"
        color="primary"
        prepend-icon="mdi-plus"
        @click="addDialog = true"
      >
        Add Wallet
      </v-btn>
    </div>

    <!-- Stat Bar -->
    <v-row class="mb-4" align="stretch">
      <!-- Total Wallets -->
      <v-col cols="12" sm="6" md="2" class="d-flex">
        <v-card class="glass-card pa-4 w-100" rounded="lg">
          <div class="d-flex align-center gap-3 h-100">
            <v-icon icon="mdi-wallet-outline" color="primary" size="32" />
            <div>
              <div class="text-caption text-medium-emphasis">Total Wallets</div>
              <div class="text-h5 font-weight-bold">{{ boxes.length }}</div>
            </div>
          </div>
        </v-card>
      </v-col>

      <!-- Net Balance -->
      <v-col cols="12" sm="6" md="10" class="d-flex">
        <v-card class="glass-card pa-4 w-100" rounded="lg">
          <div class="d-flex align-start gap-3 h-100">
            <v-icon icon="mdi-scale-balance" color="primary" size="32" class="mt-1" />
            <div class="flex-1-1">
              <div class="text-caption text-medium-emphasis mb-2">Net Balance</div>
              <div v-if="totalBalanceByCurrency.length === 0" class="text-body-2 text-medium-emphasis">—</div>
              <div
                v-for="item in totalBalanceByCurrency"
                :key="item.currency"
                class="d-flex align-center gap-2 mb-1"
              >
                <v-chip size="x-small" :color="item.balance >= 0 ? 'success' : 'error'" variant="tonal">{{ item.symbol }} {{ item.currency }}</v-chip>
                <span class="text-body-1 font-weight-bold" :class="item.balance >= 0 ? 'text-success' : 'text-error'">
                  {{ item.symbol }}{{ formatPrice(item.balance) }}
                </span>
              </div>
            </div>
          </div>
        </v-card>
      </v-col>

      <!-- Total Deposits -->
      <!-- <v-col cols="12" sm="6" md="3" class="d-flex">
        <v-card class="glass-card pa-4 w-100" rounded="lg">
          <div class="d-flex align-start gap-3 h-100">
            <v-icon icon="mdi-arrow-down-circle" color="success" size="32" class="mt-1" />
            <div class="flex-1-1">
              <div class="text-caption text-medium-emphasis mb-1">Total Deposits</div>
              <div v-if="grandDepositByCurrency.length === 0" class="text-body-2 text-medium-emphasis">—</div>
              <div
                v-for="item in grandDepositByCurrency"
                :key="item.currency"
                class="d-flex align-center gap-2 mb-1"
              >
                <v-chip size="x-small" color="success" variant="tonal">{{ item.symbol }} {{ item.currency }}</v-chip>
                <span class="text-body-1 font-weight-bold text-success">{{ item.symbol }}{{ formatPrice(item.total) }}</span>
              </div>
            </div>
          </div>
        </v-card>
      </v-col> -->

      <!-- Total Withdraws -->
      <!-- <v-col cols="12" sm="6" md="3" class="d-flex">
        <v-card class="glass-card pa-4 w-100" rounded="lg">
          <div class="d-flex align-start gap-3 h-100">
            <v-icon icon="mdi-arrow-up-circle" color="error" size="32" class="mt-1" />
            <div class="flex-1-1">
              <div class="text-caption text-medium-emphasis mb-1">Total Withdraws</div>
              <div v-if="grandWithdrawByCurrency.length === 0" class="text-body-2 text-medium-emphasis">—</div>
              <div
                v-for="item in grandWithdrawByCurrency"
                :key="item.currency"
                class="d-flex align-center gap-2 mb-1"
              >
                <v-chip size="x-small" color="error" variant="tonal">{{ item.symbol }} {{ item.currency }}</v-chip>
                <span class="text-body-1 font-weight-bold text-error">{{ item.symbol }}{{ formatPrice(item.total) }}</span>
              </div>
            </div>
          </div>
        </v-card>
      </v-col> -->
    </v-row>

    <!-- Tabs -->
    <v-tabs v-model="tab" class="mb-4" color="primary">
      <v-tab value="wallets" prepend-icon="mdi-wallet-outline">Wallets</v-tab>
      <v-tab value="ledger" prepend-icon="mdi-format-list-bulleted">All Transactions</v-tab>
    </v-tabs>

    <!-- ── Wallets Tab ── -->
    <v-tabs-window v-model="tab">
      <v-tabs-window-item value="wallets">
        <v-row v-if="!loading">
          <v-col
            v-for="box in boxes"
            :key="box.id"
            cols="12"
            md="6"
            lg="4"
          >
            <v-card
              class="glass-card cursor-pointer"
              rounded="lg"
              hover
              @click="navigateTo('/payment-control/' + box.id)"
            >
              <v-card-text class="pa-5">
                <div class="d-flex align-center justify-space-between mb-4">
                  <div class="d-flex align-center gap-2">
                    <v-icon icon="mdi-bank-outline" color="primary" size="24" />
                    <div>
                      <div class="text-subtitle-1 font-weight-bold">{{ box.name || box.companyPresetName }}</div>
                      <div v-if="box.name" class="text-caption text-medium-emphasis">{{ box.companyPresetName }}</div>
                    </div>
                  </div>
                  <div class="d-flex align-center gap-1">
                    <v-chip size="x-small" color="primary" variant="tonal">{{ currencySymbol(box.currency) }} {{ box.currency }}</v-chip>
                    <v-btn
                      v-if="authStore.isAdmin"
                      icon="mdi-pencil-outline"
                      size="x-small"
                      variant="text"
                      color="primary"
                      @click.stop="openRename(box)"
                      title="Rename wallet"
                    />
                    <v-btn
                      v-if="authStore.isAdmin"
                      icon="mdi-bank-outline"
                      size="x-small"
                      variant="text"
                      color="secondary"
                      @click.stop="openBankDetails(box)"
                      title="Edit bank details"
                    />
                    <v-btn
                      v-if="authStore.isSuperAdmin"
                      icon="mdi-delete-outline"
                      size="x-small"
                      variant="text"
                      color="error"
                      @click.stop="confirmDelete(box)"
                    />
                  </div>
                </div>

                <div class="text-center my-4">
                  <div class="text-caption text-medium-emphasis mb-1">Current Balance</div>
                  <div
                    class="text-h4 font-weight-bold"
                    :class="box.balance >= 0 ? 'text-success' : 'text-error'"
                  >
                    {{ currencySymbol(box.currency) }}{{ formatPrice(box.balance) }}
                  </div>
                </div>

                <v-divider class="mb-3" />

                <!-- Per-currency breakdown -->
                <template v-if="box.currencyBreakdowns?.length">
                  <div
                    v-for="bd in box.currencyBreakdowns"
                    :key="bd.currency"
                    class="d-flex align-center justify-space-between mb-1"
                  >
                    
                  </div>
                  <!-- <v-divider class="my-2" /> -->
                </template>

                <div class="d-flex justify-space-between">
                  <div class="text-center">
                    <div class="text-caption text-medium-emphasis">Deposits</div>
                    <div class="text-body-2 font-weight-medium text-success">
                      +{{ currencySymbol(box.currency) }}{{ formatPrice(box.totalDeposit) }}
                    </div>
                  </div>
                  <div class="text-center">
                    <div class="text-caption text-medium-emphasis">Withdraws</div>
                    <div class="text-body-2 font-weight-medium text-error">
                      -{{ currencySymbol(box.currency) }}{{ formatPrice(box.totalWithdraw) }}
                    </div>
                  </div>
                </div>
              </v-card-text>
            </v-card>
          </v-col>

          <v-col v-if="boxes.length === 0" cols="12">
            <v-card class="glass-card pa-8 text-center" rounded="lg">
              <v-icon icon="mdi-wallet-outline" size="64" color="medium-emphasis" class="mb-4" />
              <div class="text-h6 text-medium-emphasis">No wallets yet</div>
              <div class="text-caption text-medium-emphasis mt-1">Add a Payment Box to start tracking.</div>
            </v-card>
          </v-col>
        </v-row>

        <v-row v-else>
          <v-col v-for="n in 3" :key="n" cols="12" md="6" lg="4">
            <v-skeleton-loader type="card" rounded="lg" />
          </v-col>
        </v-row>
      </v-tabs-window-item>

      <!-- ── All Transactions Tab ── -->
      <v-tabs-window-item value="ledger">
        <div class="d-flex justify-end mb-3">
          <v-btn
            color="success"
            variant="tonal"
            prepend-icon="mdi-microsoft-excel"
            @click="exportDialog = true"
          >
            Export Excel
          </v-btn>
        </div>
        <v-card class="glass-card" rounded="lg">
          <v-data-table
            :headers="ledgerHeaders"
            :items="allTransactions"
            :loading="ledgerLoading"
            density="comfortable"
            :items-per-page="100"
          >
            <!-- Account -->
            <template #item.accountName="{ item }">
              <v-chip
                size="x-small"
                color="primary"
                variant="tonal"
                class="cursor-pointer"
                @click="navigateTo('/payment-control/' + getBoxId(item.accountName))"
              >
                {{ item.accountName }}
              </v-chip>
            </template>

            <!-- Deposit -->
            <template #item.deposit="{ item }">
              <span v-if="item.deposit != null" class="text-success font-weight-medium text-no-wrap">
                +{{ currencySymbol(item.txCurrency || item.currency) }}{{ formatPrice(item.deposit) }}
              </span>
              <span v-else class="text-medium-emphasis">—</span>
            </template>

            <!-- Withdraw -->
            <template #item.withdraw="{ item }">
              <span v-if="item.withdraw != null" class="text-error font-weight-medium text-no-wrap">
                -{{ currencySymbol(item.txCurrency || item.currency) }}{{ formatPrice(item.withdraw) }}
              </span>
              <span v-else class="text-medium-emphasis">—</span>
            </template>

            <!-- From (Deposit only) -->
            <template #item.fromName="{ item }">
              <template v-if="item.deposit != null && item.fromName">
                <div class="d-flex align-center gap-1">
                  <v-icon
                    :icon="item.fromType === 'MotherWallet' ? 'mdi-bank-outline' : 'mdi-account-outline'"
                    size="14"
                    class="text-medium-emphasis"
                  />
                  <span>{{ item.fromName }}</span>
                </div>
              </template>
              <span v-else class="text-medium-emphasis">—</span>
            </template>

            <!-- To (Withdraw only) -->
            <template #item.toName="{ item }">
              <template v-if="item.withdraw != null && item.toName">
                <div class="d-flex align-center gap-1">
                  <v-icon
                    :icon="item.toType === 'MotherWallet' ? 'mdi-bank-outline' : 'mdi-truck-outline'"
                    size="14"
                    class="text-medium-emphasis"
                  />
                  <span>{{ item.toName }}</span>
                </div>
              </template>
              <span v-else class="text-medium-emphasis">—</span>
            </template>

            <!-- PI# -->
            <template #item.piNumber="{ item }">
              <v-chip
                v-if="item.piNumber"
                size="x-small"
                color="primary"
                variant="tonal"
                class="cursor-pointer"
                @click="navigateTo('/invoices/' + item.piId)"
              >
                {{ item.piNumber }}
              </v-chip>
              <span v-else class="text-medium-emphasis">—</span>
            </template>

            <!-- PR# -->
            <template #item.prNumber="{ item }">
              <v-chip
                v-if="item.prNumber"
                size="x-small"
                color="secondary"
                variant="tonal"
                class="cursor-pointer"
                @click="navigateTo('/purchase-orders/' + item.poId)"
              >
                {{ item.prNumber }}
              </v-chip>
              <span v-else class="text-medium-emphasis">—</span>
            </template>

            <!-- Balance -->
            <template #item.balance="{ item }">
              <span
                class="font-weight-medium"
                :class="item.balance >= 0 ? 'text-success' : 'text-error'"
              >
                {{ formatPrice(item.balance) }}
              </span>
            </template>

            <!-- Actions -->
            <template #item.actions="{ item }">
              <v-btn
                v-if="authStore.isSuperAdmin"
                icon="mdi-pencil-outline"
                size="x-small"
                variant="text"
                color="primary"
                @click="navigateTo(`/payment-control/${item.boxId}?edit=${item.id}`)"
              />
            </template>
          </v-data-table>
        </v-card>
      </v-tabs-window-item>
    </v-tabs-window>

    <!-- Add Wallet Dialog -->
    <v-dialog v-model="addDialog" max-width="500">
      <v-card rounded="lg">
        <v-card-title class="pa-4 text-h6">Add Payment Wallet</v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-text-field
            v-model="form.name"
            label="Wallet Name *"
            placeholder="e.g. Main Account, Reserve USD"
            variant="outlined"
            density="comfortable"
            class="mb-3"
            prepend-inner-icon="mdi-wallet-outline"
          />
          <v-autocomplete
            v-model="form.companyPresetId"
            :items="presets"
            item-title="name"
            item-value="id"
            label="Company Preset"
            variant="outlined"
            density="comfortable"
            class="mb-3"
          />
          <v-select
            v-model="form.currency"
            :items="currencies"
            label="Currency"
            variant="outlined"
            density="comfortable"
            class="mb-3"
          />
          <v-divider class="mb-3" />
          <div class="text-caption text-medium-emphasis mb-2">Bank Details (optional)</div>
          <v-text-field
            v-model="form.beneficiaryName"
            label="Beneficiary Name"
            variant="outlined"
            density="comfortable"
            class="mb-3"
          />
          <v-text-field
            v-model="form.bankName"
            label="Bank Name"
            variant="outlined"
            density="comfortable"
            class="mb-3"
          />
          <v-text-field
            v-model="form.bankAddress"
            label="Bank Address"
            variant="outlined"
            density="comfortable"
            class="mb-3"
          />
          <v-text-field
            v-model="form.accountNumber"
            label="Account Number"
            variant="outlined"
            density="comfortable"
            class="mb-3"
          />
          <v-text-field
            v-model="form.swiftCode"
            label="SWIFT / BIC Code"
            variant="outlined"
            density="comfortable"
          />
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="addDialog = false">Cancel</v-btn>
          <v-btn color="primary" :loading="saving" @click="saveBox">Create</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Edit Bank Details Dialog -->
    <v-dialog v-model="bankDialog" max-width="480">
      <v-card rounded="lg">
        <v-card-title class="pa-4 text-h6">Edit Bank Details</v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-text-field
            v-model="bankForm.beneficiaryName"
            label="Beneficiary Name"
            variant="outlined"
            density="comfortable"
            class="mb-3"
          />
          <v-text-field
            v-model="bankForm.bankName"
            label="Bank Name"
            variant="outlined"
            density="comfortable"
            class="mb-3"
          />
          <v-text-field
            v-model="bankForm.bankAddress"
            label="Bank Address"
            variant="outlined"
            density="comfortable"
            class="mb-3"
          />
          <v-text-field
            v-model="bankForm.accountNumber"
            label="Account Number"
            variant="outlined"
            density="comfortable"
            class="mb-3"
          />
          <v-text-field
            v-model="bankForm.swiftCode"
            label="SWIFT / BIC Code"
            variant="outlined"
            density="comfortable"
          />
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="bankDialog = false">Cancel</v-btn>
          <v-btn color="primary" :loading="bankSaving" @click="saveBankDetails">Save</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Rename Wallet Dialog -->
    <v-dialog v-model="renameDialog" max-width="420">
      <v-card rounded="lg">
        <v-card-title class="pa-4 text-h6">Rename Wallet</v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-text-field
            v-model="renameValue"
            label="New Wallet Name"
            variant="outlined"
            density="comfortable"
            autofocus
            prepend-inner-icon="mdi-pencil-outline"
            @keyup.enter="doRename"
          />
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="renameDialog = false">Cancel</v-btn>
          <v-btn color="primary" :loading="renaming" :disabled="!renameValue.trim()" @click="doRename">Rename</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Export Dialog -->
    <v-dialog v-model="exportDialog" max-width="520">
      <v-card rounded="lg">
        <v-card-title class="pa-4 text-h6">Export Transactions</v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-autocomplete
            v-model="exportForm.boxIds"
            :items="boxes"
            :item-title="(b) => `${b.companyPresetName} (${b.currency})`"
            item-value="id"
            label="Wallets (leave empty for all)"
            variant="outlined"
            density="comfortable"
            multiple
            chips
            closable-chips
            clearable
            class="mb-3"
          />
          <v-btn-toggle
            v-model="exportForm.type"
            mandatory
            color="primary"
            variant="outlined"
            divided
            class="mb-4"
          >
            <v-btn value="All">All</v-btn>
            <v-btn value="Deposit">Deposits</v-btn>
            <v-btn value="Withdraw">Withdrawals</v-btn>
          </v-btn-toggle>
          <v-row dense>
            <v-col cols="6">
              <v-text-field
                v-model="exportForm.fromDate"
                label="From Date"
                type="date"
                variant="outlined"
                density="comfortable"
                clearable
              />
            </v-col>
            <v-col cols="6">
              <v-text-field
                v-model="exportForm.toDate"
                label="To Date"
                type="date"
                variant="outlined"
                density="comfortable"
                clearable
              />
            </v-col>
          </v-row>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="exportDialog = false">Cancel</v-btn>
          <v-btn color="success" prepend-icon="mdi-microsoft-excel" @click="doExport">Export</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Delete Confirm Dialog -->
    <v-dialog v-model="deleteDialog" max-width="400">
      <v-card rounded="lg">
        <v-card-title class="pa-4 text-h6">Delete Wallet?</v-card-title>
        <v-card-text class="pa-4">
          Are you sure you want to delete the <strong>{{ deleteTarget?.companyPresetName }}</strong> wallet and all its transactions?
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="deleteDialog = false">Cancel</v-btn>
          <v-btn color="error" :loading="deleting" @click="deleteBox">Delete</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-container>
</template>

<script setup lang="ts">
import { formatPrice } from '~/utils/formatPrice'
import { downloadExcel } from '~/utils/exportExcel'

interface CurrencyBreakdown {
  currency: string
  symbol: string
  totalDeposit: number
  totalWithdraw: number
}

interface PaymentBox {
  id: number
  companyPresetId: number
  companyPresetName: string
  name: string
  currency: string
  totalDeposit: number
  totalWithdraw: number
  balance: number
  currencyBreakdowns: CurrencyBreakdown[]
  bankName?: string | null
  bankAddress?: string | null
  accountNumber?: string | null
  beneficiaryName?: string | null
  swiftCode?: string | null
}

interface AllTransactionRow {
  id: number
  boxId: number
  accountName: string
  currency: string
  deposit: number | null
  withdraw: number | null
  fromType: string
  fromName: string | null
  fromCustomerId: number | null
  toType: string
  toName: string | null
  toSupplierId: number | null
  piNumber: string | null
  piId: number | null
  prNumber: string | null
  prId: number | null
  poId: number | null
  notes: string | null
  isAuto: boolean
  createdAt: string
  balance: number
  txCurrency: string | null
  exchangeRate: number | null
}

interface CompanyPreset {
  id: number
  name: string
}

const authStore = useAuthStore()
const api = useApi()

const tab = ref<'wallets' | 'ledger'>('wallets')
const boxes = ref<PaymentBox[]>([])
const allTransactions = ref<AllTransactionRow[]>([])
const presets = ref<CompanyPreset[]>([])
const loading = ref(true)
const ledgerLoading = ref(false)
const saving = ref(false)
const deleting = ref(false)
const addDialog = ref(false)
const deleteDialog = ref(false)
const deleteTarget = ref<PaymentBox | null>(null)
const exportDialog = ref(false)
const exportForm = ref({
  boxIds: [] as number[],
  type: 'All' as 'All' | 'Deposit' | 'Withdraw',
  fromDate: '',
  toDate: '',
})

const currencies = ['USD', 'EUR', 'CNY', 'GBP', 'AED', 'RUB']

function currencySymbol(c: string) {
  return ({ USD: '$', EUR: '€', GBP: '£', CNY: '¥', AED: 'د.إ', RUB: '₽' } as Record<string, string>)[c] ?? c
}
const form = ref({
  companyPresetId: null as number | null,
  currency: 'USD',
  name: '',
  bankName: '',
  bankAddress: '',
  accountNumber: '',
  beneficiaryName: '',
  swiftCode: '',
})

// Rename wallet state
const renameDialog = ref(false)
const renameTarget = ref<PaymentBox | null>(null)
const renameValue = ref('')
const renaming = ref(false)

// Edit bank details state
const bankDialog = ref(false)
const bankTarget = ref<PaymentBox | null>(null)
const bankSaving = ref(false)
const bankForm = ref({ bankName: '', bankAddress: '', accountNumber: '', beneficiaryName: '', swiftCode: '' })

function openBankDetails(box: PaymentBox) {
  bankTarget.value = box
  bankForm.value = {
    bankName: box.bankName || '',
    bankAddress: box.bankAddress || '',
    accountNumber: box.accountNumber || '',
    beneficiaryName: box.beneficiaryName || '',
    swiftCode: box.swiftCode || '',
  }
  bankDialog.value = true
}

async function saveBankDetails() {
  if (!bankTarget.value) return
  bankSaving.value = true
  try {
    const updated = await api.patch<PaymentBox>(`/payment-boxes/${bankTarget.value.id}/bank-details`, {
      bankName: bankForm.value.bankName || null,
      bankAddress: bankForm.value.bankAddress || null,
      accountNumber: bankForm.value.accountNumber || null,
      beneficiaryName: bankForm.value.beneficiaryName || null,
      swiftCode: bankForm.value.swiftCode || null,
    })
    const idx = boxes.value.findIndex(b => b.id === bankTarget.value!.id)
    if (idx >= 0) boxes.value[idx] = updated
    bankDialog.value = false
  } catch (e) {
    console.error(e)
  } finally {
    bankSaving.value = false
  }
}

function openRename(box: PaymentBox) {
  renameTarget.value = box
  renameValue.value = box.name || box.companyPresetName
  renameDialog.value = true
}

async function doRename() {
  if (!renameTarget.value || !renameValue.value.trim()) return
  renaming.value = true
  try {
    const updated = await api.patch<PaymentBox>(`/payment-boxes/${renameTarget.value.id}/rename`, { name: renameValue.value.trim() })
    const idx = boxes.value.findIndex(b => b.id === renameTarget.value!.id)
    if (idx >= 0) boxes.value[idx] = updated
    renameDialog.value = false
  } catch (e) {
    console.error(e)
  } finally {
    renaming.value = false
  }
}

function aggregateByCurrency(key: 'totalDeposit' | 'totalWithdraw') {
  const map: Record<string, { symbol: string; total: number }> = {}
  for (const box of boxes.value) {
    for (const bd of box.currencyBreakdowns ?? []) {
      if (!map[bd.currency]) map[bd.currency] = { symbol: bd.symbol, total: 0 }
      map[bd.currency].total += bd[key]
    }
  }
  return Object.entries(map)
    .filter(([, v]) => v.total > 0)
    .map(([currency, v]) => ({ currency, symbol: v.symbol, total: v.total }))
}

const grandDepositByCurrency = computed(() => aggregateByCurrency('totalDeposit'))
const grandWithdrawByCurrency = computed(() => aggregateByCurrency('totalWithdraw'))

const totalBalanceByCurrency = computed(() => {
  const map: Record<string, { symbol: string; balance: number }> = {}
  for (const box of boxes.value) {
    for (const bd of box.currencyBreakdowns ?? []) {
      if (!map[bd.currency]) map[bd.currency] = { symbol: bd.symbol, balance: 0 }
      map[bd.currency].balance += bd.totalDeposit - bd.totalWithdraw
    }
  }
  return Object.entries(map)
    .filter(([, v]) => v.balance !== 0)
    .map(([currency, v]) => ({ currency, symbol: v.symbol, balance: v.balance }))
})

const ledgerHeaders = [
  { title: 'Account', key: 'accountName', sortable: true },
  { title: 'Deposit', key: 'deposit', sortable: false, width: '120px' },
  { title: 'Withdraw', key: 'withdraw', sortable: false, width: '120px' },
  { title: 'From', key: 'fromName', sortable: false },
  { title: 'To', key: 'toName', sortable: false },
  { title: 'PI#', key: 'piNumber', sortable: false, width: '100px' },
  { title: 'PR#', key: 'prNumber', sortable: false, width: '100px' },
  { title: 'Balance', key: 'balance', sortable: false, width: '120px' },
  { title: '', key: 'actions', sortable: false, width: '50px' },
]

function getBoxId(accountName: string) {
  return boxes.value.find(b => b.companyPresetName === accountName)?.id
}

async function loadBoxes() {
  loading.value = true
  try {
    boxes.value = await api.get<PaymentBox[]>('/payment-boxes')
  } catch (e) {
    console.error(e)
  } finally {
    loading.value = false
  }
}

async function loadAllTransactions() {
  ledgerLoading.value = true
  try {
    allTransactions.value = await api.get<AllTransactionRow[]>('/payment-boxes/all-transactions')
  } catch (e) {
    console.error(e)
  } finally {
    ledgerLoading.value = false
  }
}

async function loadPresets() {
  try {
    presets.value = await api.get<CompanyPreset[]>('/companypresets')
  } catch (e) {
    console.error(e)
  }
}

watch(tab, (val) => {
  if (val === 'ledger' && allTransactions.value.length === 0) {
    loadAllTransactions()
  }
})

async function saveBox() {
  if (!form.value.companyPresetId) return
  saving.value = true
  try {
    const created = await api.post<PaymentBox>('/payment-boxes', {
      companyPresetId: form.value.companyPresetId,
      currency: form.value.currency,
      name: form.value.name,
      bankName: form.value.bankName || null,
      bankAddress: form.value.bankAddress || null,
      accountNumber: form.value.accountNumber || null,
      beneficiaryName: form.value.beneficiaryName || null,
      swiftCode: form.value.swiftCode || null,
    })
    boxes.value.push(created)
    addDialog.value = false
    form.value = { companyPresetId: null, currency: 'USD', name: '', bankName: '', bankAddress: '', accountNumber: '', beneficiaryName: '', swiftCode: '' }
  } catch (e) {
    console.error(e)
  } finally {
    saving.value = false
  }
}

function confirmDelete(box: PaymentBox) {
  deleteTarget.value = box
  deleteDialog.value = true
}

async function deleteBox() {
  if (!deleteTarget.value) return
  deleting.value = true
  try {
    await api.del(`/payment-boxes/${deleteTarget.value.id}`)
    boxes.value = boxes.value.filter(b => b.id !== deleteTarget.value!.id)
    deleteDialog.value = false
  } catch (e) {
    console.error(e)
  } finally {
    deleting.value = false
  }
}

async function doExport() {
  if (allTransactions.value.length === 0) await loadAllTransactions()
  const { boxIds, type, fromDate, toDate } = exportForm.value
  const from = fromDate ? new Date(fromDate) : null
  const to = toDate ? new Date(toDate + 'T23:59:59') : null

  const rows = allTransactions.value.filter(t => {
    if (boxIds.length > 0 && !boxIds.includes(t.boxId)) return false
    if (type === 'Deposit' && t.deposit == null) return false
    if (type === 'Withdraw' && t.withdraw == null) return false
    const d = new Date(t.createdAt)
    if (from && d < from) return false
    if (to && d > to) return false
    return true
  }).map(t => ({
    Account: t.accountName,
    Currency: t.currency,
    Deposit: t.deposit ?? '',
    Withdraw: t.withdraw ?? '',
    From: t.fromName ?? '',
    To: t.toName ?? '',
    'PI#': t.piNumber ?? '',
    'PR#': t.prNumber ?? '',
    Notes: t.notes ?? '',
    Source: t.isAuto ? 'Auto' : 'Manual',
    Date: new Date(t.createdAt).toLocaleDateString(),
    Balance: t.balance,
  }))

  downloadExcel(rows, 'payment-transactions')
  exportDialog.value = false
}

onMounted(() => {
  loadBoxes()
  if (authStore.isSuperAdmin) loadPresets()
})
</script>
