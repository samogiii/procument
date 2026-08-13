<template>
  <v-container fluid class="pa-4">
    <!-- Header -->
    <div class="d-flex align-start gap-3 mb-6 flex-wrap">
      <v-btn icon="mdi-arrow-left" variant="text" @click="navigateTo('/payment-control')" />
      <div class="flex-1-1">
        <div class="d-flex align-center gap-2">
          <span class="text-h5 font-weight-bold">{{ detail ? boxLabel(detail) : '' }}</span>
          <v-chip v-if="detail" size="small" color="primary" variant="tonal">{{ detail.currency }}</v-chip>
        </div>
        <div class="text-caption text-medium-emphasis mb-2">Transaction ledger</div>

        <!-- Stat chips -->
        <div v-if="detail" class="d-flex gap-2 flex-wrap">
          <v-chip color="success" variant="tonal" prepend-icon="mdi-arrow-down-circle">
            +{{ formatPrice(detail.totalDeposit) }}
          </v-chip>
          <v-chip color="error" variant="tonal" prepend-icon="mdi-arrow-up-circle">
            -{{ formatPrice(detail.totalWithdraw) }}
          </v-chip>
        </div>
      </div>

      <div class="d-flex align-center gap-2">
        <v-btn
          v-if="hasAnyFilter"
          variant="text"
          color="error"
          prepend-icon="mdi-filter-off-outline"
          @click="clearFilters"
        >
          Clear Filters
        </v-btn>
        <v-btn
          color="success"
          variant="tonal"
          prepend-icon="mdi-microsoft-excel"
          @click="exportDialog = true"
        >
          Export
        </v-btn>
        <v-btn
          v-if="authStore.isSuperAdmin"
          color="primary"
          prepend-icon="mdi-plus"
          @click="openAddTx"
        >
          Add Transaction
        </v-btn>
      </div>

      <!-- Remaining balance — the headline figure, kept far right and oversized -->
      <v-card v-if="detail" class="glass-card wallet-balance-panel px-6 py-4" rounded="lg">
        <div class="text-caption text-medium-emphasis text-right text-uppercase">Remaining Balance</div>
        <div
          class="wallet-balance-amount font-weight-bold text-right"
          :class="detail.balance >= 0 ? 'text-success' : 'text-error'"
        >
          {{ currencySymbol(detail.currency) }}{{ formatPrice(detail.balance) }}
        </div>
      </v-card>
    </div>

    <!-- Table -->
    <v-card class="glass-card" rounded="lg">
      <v-data-table
        :headers="headers"
        :items="displayedTransactions"
        :loading="loading"
        density="comfortable"
        :items-per-page="50"
      >
        <!-- ── Excel-style filter + sort headers ── -->
        <template #header.deposit="{ column, toggleSort, isSorted, sortBy }">
          <ColRangeFilterMenu
            col-key="deposit"
            :label="column.title"
            :min="rangeFilter.get('deposit').min"
            :max="rangeFilter.get('deposit').max"
            :bounds="depositBounds"
            :prefix="currencySymbol(detail?.currency ?? '')"
            :is-sorted="isSorted(column)"
            :sort-desc="sortBy.find((s: any) => s.key === column.key)?.order === 'desc'"
            @update:min="(v) => rangeFilter.setMin('deposit', v)"
            @update:max="(v) => rangeFilter.setMax('deposit', v)"
            @select-all="() => rangeFilter.setBounds('deposit', depositBounds?.lo ?? null, depositBounds?.hi ?? null)"
            @clear-all="() => rangeFilter.clear('deposit')"
            @sort-click="toggleSort(column)"
          />
        </template>

        <template #header.withdraw="{ column, toggleSort, isSorted, sortBy }">
          <ColRangeFilterMenu
            col-key="withdraw"
            :label="column.title"
            :min="rangeFilter.get('withdraw').min"
            :max="rangeFilter.get('withdraw').max"
            :bounds="withdrawBounds"
            :prefix="currencySymbol(detail?.currency ?? '')"
            :is-sorted="isSorted(column)"
            :sort-desc="sortBy.find((s: any) => s.key === column.key)?.order === 'desc'"
            @update:min="(v) => rangeFilter.setMin('withdraw', v)"
            @update:max="(v) => rangeFilter.setMax('withdraw', v)"
            @select-all="() => rangeFilter.setBounds('withdraw', withdrawBounds?.lo ?? null, withdrawBounds?.hi ?? null)"
            @clear-all="() => rangeFilter.clear('withdraw')"
            @sort-click="toggleSort(column)"
          />
        </template>

        <template #header.balance="{ column, toggleSort, isSorted, sortBy }">
          <ColRangeFilterMenu
            col-key="balance"
            :label="column.title"
            :min="rangeFilter.get('balance').min"
            :max="rangeFilter.get('balance').max"
            :bounds="balanceBounds"
            :prefix="currencySymbol(detail?.currency ?? '')"
            :is-sorted="isSorted(column)"
            :sort-desc="sortBy.find((s: any) => s.key === column.key)?.order === 'desc'"
            @update:min="(v) => rangeFilter.setMin('balance', v)"
            @update:max="(v) => rangeFilter.setMax('balance', v)"
            @select-all="() => rangeFilter.setBounds('balance', balanceBounds?.lo ?? null, balanceBounds?.hi ?? null)"
            @clear-all="() => rangeFilter.clear('balance')"
            @sort-click="toggleSort(column)"
          />
        </template>

        <template #header.fromName="{ column, toggleSort, isSorted, sortBy }">
          <ColFilterMenu
            col-key="fromName"
            :label="column.title"
            :options="cfOptions.fromName"
            :selected="colFilter.selected['fromName'] || new Set()"
            :search="colFilter.search['fromName'] || ''"
            :is-sorted="isSorted(column)"
            :sort-desc="sortBy.find((s: any) => s.key === column.key)?.order === 'desc'"
            @toggle="(v) => colFilter.toggle('fromName', v)"
            @select-all="() => colFilter.selectAll('fromName', cfOptions.fromName)"
            @clear-all="() => colFilter.clearAll('fromName')"
            @update:search="(v) => colFilter.search['fromName'] = v"
            @sort-click="toggleSort(column)"
          />
        </template>

        <template #header.toName="{ column, toggleSort, isSorted, sortBy }">
          <ColFilterMenu
            col-key="toName"
            :label="column.title"
            :options="cfOptions.toName"
            :selected="colFilter.selected['toName'] || new Set()"
            :search="colFilter.search['toName'] || ''"
            :is-sorted="isSorted(column)"
            :sort-desc="sortBy.find((s: any) => s.key === column.key)?.order === 'desc'"
            @toggle="(v) => colFilter.toggle('toName', v)"
            @select-all="() => colFilter.selectAll('toName', cfOptions.toName)"
            @clear-all="() => colFilter.clearAll('toName')"
            @update:search="(v) => colFilter.search['toName'] = v"
            @sort-click="toggleSort(column)"
          />
        </template>

        <template #header.piNumber="{ column, toggleSort, isSorted, sortBy }">
          <ColFilterMenu
            col-key="piNumber"
            :label="column.title"
            :options="cfOptions.piNumber"
            :selected="colFilter.selected['piNumber'] || new Set()"
            :search="colFilter.search['piNumber'] || ''"
            :is-sorted="isSorted(column)"
            :sort-desc="sortBy.find((s: any) => s.key === column.key)?.order === 'desc'"
            @toggle="(v) => colFilter.toggle('piNumber', v)"
            @select-all="() => colFilter.selectAll('piNumber', cfOptions.piNumber)"
            @clear-all="() => colFilter.clearAll('piNumber')"
            @update:search="(v) => colFilter.search['piNumber'] = v"
            @sort-click="toggleSort(column)"
          />
        </template>

        <template #header.prNumber="{ column, toggleSort, isSorted, sortBy }">
          <ColFilterMenu
            col-key="prNumber"
            :label="column.title"
            :options="cfOptions.prNumber"
            :selected="colFilter.selected['prNumber'] || new Set()"
            :search="colFilter.search['prNumber'] || ''"
            :is-sorted="isSorted(column)"
            :sort-desc="sortBy.find((s: any) => s.key === column.key)?.order === 'desc'"
            @toggle="(v) => colFilter.toggle('prNumber', v)"
            @select-all="() => colFilter.selectAll('prNumber', cfOptions.prNumber)"
            @clear-all="() => colFilter.clearAll('prNumber')"
            @update:search="(v) => colFilter.search['prNumber'] = v"
            @sort-click="toggleSort(column)"
          />
        </template>

        <template #header.base="{ column, toggleSort, isSorted, sortBy }">
          <ColFilterMenu
            col-key="base"
            :label="column.title"
            :options="cfOptions.base"
            :selected="colFilter.selected['base'] || new Set()"
            :search="colFilter.search['base'] || ''"
            :all-options="allBaseOptions"
            :is-sorted="isSorted(column)"
            :sort-desc="sortBy.find((s: any) => s.key === column.key)?.order === 'desc'"
            @toggle="(v) => colFilter.toggle('base', v)"
            @select-all="() => colFilter.selectAll('base', cfOptions.base)"
            @clear-all="() => colFilter.clearAll('base')"
            @update:search="(v) => colFilter.search['base'] = v"
            @sort-click="toggleSort(column)"
          />
        </template>

        <template #header.exchangeRate="{ column, toggleSort, isSorted, sortBy }">
          <ColFilterMenu
            col-key="exchangeRate"
            :label="column.title"
            :options="cfOptions.exchangeRate"
            :selected="colFilter.selected['exchangeRate'] || new Set()"
            :search="colFilter.search['exchangeRate'] || ''"
            :is-sorted="isSorted(column)"
            :sort-desc="sortBy.find((s: any) => s.key === column.key)?.order === 'desc'"
            @toggle="(v) => colFilter.toggle('exchangeRate', v)"
            @select-all="() => colFilter.selectAll('exchangeRate', cfOptions.exchangeRate)"
            @clear-all="() => colFilter.clearAll('exchangeRate')"
            @update:search="(v) => colFilter.search['exchangeRate'] = v"
            @sort-click="toggleSort(column)"
          />
        </template>

        <template #header.notes="{ column, toggleSort, isSorted, sortBy }">
          <ColFilterMenu
            col-key="notes"
            :label="column.title"
            :options="cfOptions.notes"
            :selected="colFilter.selected['notes'] || new Set()"
            :search="colFilter.search['notes'] || ''"
            :is-sorted="isSorted(column)"
            :sort-desc="sortBy.find((s: any) => s.key === column.key)?.order === 'desc'"
            @toggle="(v) => colFilter.toggle('notes', v)"
            @select-all="() => colFilter.selectAll('notes', cfOptions.notes)"
            @clear-all="() => colFilter.clearAll('notes')"
            @update:search="(v) => colFilter.search['notes'] = v"
            @sort-click="toggleSort(column)"
          />
        </template>

        <template #header.isAuto="{ column, toggleSort, isSorted, sortBy }">
          <ColFilterMenu
            col-key="isAuto"
            :label="column.title"
            :options="cfOptions.isAuto"
            :selected="colFilter.selected['isAuto'] || new Set()"
            :search="colFilter.search['isAuto'] || ''"
            :is-sorted="isSorted(column)"
            :sort-desc="sortBy.find((s: any) => s.key === column.key)?.order === 'desc'"
            @toggle="(v) => colFilter.toggle('isAuto', v)"
            @select-all="() => colFilter.selectAll('isAuto', cfOptions.isAuto)"
            @clear-all="() => colFilter.clearAll('isAuto')"
            @update:search="(v) => colFilter.search['isAuto'] = v"
            @sort-click="toggleSort(column)"
          />
        </template>

        <template #header.createdAt="{ column, toggleSort, isSorted, sortBy }">
          <ColFilterMenu
            col-key="createdAt"
            :label="column.title"
            :options="cfOptions.createdAt"
            :selected="colFilter.selected['createdAt'] || new Set()"
            :search="colFilter.search['createdAt'] || ''"
            :is-sorted="isSorted(column)"
            :sort-desc="sortBy.find((s: any) => s.key === column.key)?.order === 'desc'"
            @toggle="(v) => colFilter.toggle('createdAt', v)"
            @select-all="() => colFilter.selectAll('createdAt', cfOptions.createdAt)"
            @clear-all="() => colFilter.clearAll('createdAt')"
            @update:search="(v) => colFilter.search['createdAt'] = v"
            @sort-click="toggleSort(column)"
          />
        </template>

        <!-- Deposit -->
        <template #item.deposit="{ item }">
          <span v-if="item.deposit != null" class="text-success font-weight-medium text-no-wrap">
            +{{ currencySymbol(item.txCurrency || detail?.currency || '') }}{{ formatPrice(item.deposit) }}
          </span>
          <span v-else class="text-medium-emphasis">—</span>
        </template>

        <!-- Withdraw -->
        <template #item.withdraw="{ item }">
          <span v-if="item.withdraw != null" class="text-error font-weight-medium text-no-wrap">
            -{{ currencySymbol(item.txCurrency || detail?.currency || '') }}{{ formatPrice(item.withdraw) }}
          </span>
          <span v-else class="text-medium-emphasis">—</span>
        </template>

        <!-- From (only shown for Deposit rows) -->
        <template #item.fromName="{ item }">
          <template v-if="item.type === 'Deposit'">
            <div class="d-flex align-center gap-1">
              <v-icon
                :icon="item.fromType === 'Wallet' ? 'mdi-bank-transfer' : item.fromType === 'MotherWallet' ? 'mdi-bank-outline' : 'mdi-account-outline'"
                size="14"
                class="text-medium-emphasis"
              />
              <span>{{ item.fromName ?? 'Mother Wallet' }}</span>
            </div>
          </template>
          <span v-else class="text-medium-emphasis">—</span>
        </template>

        <!-- To (only shown for Withdraw rows) -->
        <template #item.toName="{ item }">
          <template v-if="item.type === 'Withdraw'">
            <div class="d-flex align-center gap-1">
              <v-icon
                :icon="item.toType === 'Wallet' ? 'mdi-bank-transfer' : item.toType === 'MotherWallet' ? 'mdi-bank-outline' : 'mdi-truck-outline'"
                size="14"
                class="text-medium-emphasis"
              />
              <span>{{ item.toName ?? 'Mother Wallet' }}</span>
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

        <!-- Base -->
        <template #item.base="{ item }">
          <v-chip v-if="item.base" size="x-small" color="deep-purple" variant="tonal">{{ item.base }}</v-chip>
          <span v-else class="text-medium-emphasis">—</span>
        </template>

        <!-- Exchange Rate -->
        <template #item.exchangeRate="{ item }">
          <div v-if="item.exchangeRate != null" class="text-caption">
            <v-chip size="x-small" color="surface-variant" variant="tonal" class="mr-1">
              {{ item.txCurrency ?? detail?.currency }}
            </v-chip>
            <span>×{{ item.exchangeRate }}</span>
          </div>
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

        <!-- Source -->
        <template #item.isAuto="{ item }">
          <v-chip
            :color="item.isAuto ? 'teal' : 'default'"
            :prepend-icon="item.isAuto ? 'mdi-robot-outline' : 'mdi-account-outline'"
            size="x-small"
            variant="tonal"
          >
            {{ item.isAuto ? 'Auto' : 'Manual' }}
          </v-chip>
        </template>

        <!-- Date -->
        <template #item.createdAt="{ item }">
          <span class="text-caption text-medium-emphasis">
            {{ new Date(item.createdAt).toLocaleDateString() }}
          </span>
        </template>

        <!-- Actions -->
        <template #item.actions="{ item }">
          <div v-if="authStore.isSuperAdmin" class="d-flex align-center">
            <v-btn
              icon="mdi-pencil-outline"
              size="x-small"
              variant="text"
              color="primary"
              @click="openEditTx(item)"
            />
            <v-btn
              icon="mdi-delete-outline"
              size="x-small"
              variant="text"
              color="error"
              @click="confirmDeleteTx(item)"
            />
          </div>
        </template>

        <!-- Totals footer — sums the rows currently shown (filters applied) -->
        <template #body.append="{ columns }">
          <tr class="tx-total-row">
            <td v-for="col in columns" :key="String(col.key ?? col.title)" :class="totalCellClass(col.key)">
              <template v-if="col.key === 'deposit'">
                <div v-if="depositTotals.length === 0" class="text-medium-emphasis">—</div>
                <div v-for="t in depositTotals" :key="t.currency" class="text-success font-weight-bold text-no-wrap">
                  +{{ currencySymbol(t.currency) }}{{ formatPrice(t.total) }}
                </div>
              </template>
              <template v-else-if="col.key === 'withdraw'">
                <div v-if="withdrawTotals.length === 0" class="text-medium-emphasis">—</div>
                <div v-for="t in withdrawTotals" :key="t.currency" class="text-error font-weight-bold text-no-wrap">
                  -{{ currencySymbol(t.currency) }}{{ formatPrice(t.total) }}
                </div>
              </template>
              <template v-else-if="col.key === 'fromName'">
                <span class="text-caption text-medium-emphasis">
                  Totals · {{ displayedTransactions.length }} row{{ displayedTransactions.length === 1 ? '' : 's' }}
                </span>
              </template>
            </td>
          </tr>
        </template>
      </v-data-table>
    </v-card>

    <!-- Export Dialog -->
    <v-dialog v-model="exportDialog" max-width="460">
      <v-card rounded="lg">
        <v-card-title class="pa-4 text-h6">Export Transactions</v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <div class="text-caption text-medium-emphasis mb-2">Transaction Type</div>
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

    <!-- Add/Edit Transaction Dialog -->
    <v-dialog v-model="txDialog" max-width="520">
      <v-card rounded="lg">
        <v-card-title class="pa-4 text-h6">
          {{ txForm.id ? 'Edit Transaction' : 'Add Transaction' }}
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <!-- Type -->
          <div class="mb-4">
            <div class="text-caption text-medium-emphasis mb-2">Transaction Type</div>
            <v-btn-toggle v-model="txForm.type" mandatory color="primary" variant="outlined" divided :disabled="!!txForm.id">
              <v-btn value="Deposit" prepend-icon="mdi-arrow-down-circle">Deposit</v-btn>
              <v-btn value="Withdraw" prepend-icon="mdi-arrow-up-circle">Withdraw</v-btn>
              <v-btn v-if="!txForm.id" value="Transfer" prepend-icon="mdi-bank-transfer">Transfer</v-btn>
            </v-btn-toggle>
          </div>

          <!-- Transfer UI -->
          <template v-if="txForm.type === 'Transfer'">
            <v-alert type="info" variant="tonal" density="compact" icon="mdi-shield-check-outline" class="mb-3">
              Transfer requests require acceptance and POP upload before execution. They will appear in <strong>Payment Withdraw</strong>.
            </v-alert>
            <v-autocomplete
              v-model="txForm.toBoxId"
              :items="allBoxes"
              :item-title="(b) => `${boxLabel(b)} (${b.currency})`"
              item-value="id"
              label="Transfer To Wallet"
              variant="outlined"
              density="comfortable"
              class="mb-3"
              clearable
              @update:model-value="txForm.transferExchangeRate = null"
            />
            <v-text-field
              v-model.number="txForm.amount"
              label="Withdraw Amount"
              type="number"
              variant="outlined"
              density="comfortable"
              class="mb-3"
              :prefix="currencySymbol(detail?.currency ?? '')"
            />
            <v-text-field
              v-if="showTransferRate"
              v-model.number="txForm.transferExchangeRate"
              :label="`Exchange Rate (1 ${detail?.currency} = ? ${toBoxCurrency})`"
              type="number"
              variant="outlined"
              density="comfortable"
              class="mb-3"
            />
            <!-- Deposited amount is derived: Exchange Rate × Withdraw Amount -->
            <v-text-field
              v-if="showTransferRate && txForm.transferExchangeRate"
              :model-value="transferRealAmount"
              label="Real Amount"
              variant="outlined"
              density="comfortable"
              class="mb-3"
              readonly
              :prefix="currencySymbol(toBoxCurrency)"
              hint="Exchange Rate × Withdraw Amount — the amount deposited into the target wallet"
              persistent-hint
            />
            <v-text-field
              v-model="txForm.notes"
              label="Notes (optional)"
              variant="outlined"
              density="comfortable"
              class="mb-3"
            />
          </template>

          <!-- Deposit / Withdraw UI -->
          <template v-else>
          <!-- Amount + Currency -->
          <v-row dense class="mb-3">
            <v-col cols="7">
              <v-text-field
                v-model.number="txForm.amount"
                label="Amount"
                type="number"
                variant="outlined"
                density="comfortable"
                :prefix="currencySymbol(txForm.currency || detail?.currency || '')"
              />
            </v-col>
            <v-col cols="5">
              <v-select
                v-model="txForm.currency"
                :items="currencies"
                label="Currency"
                variant="outlined"
                density="comfortable"
                @update:model-value="txForm.exchangeRate = null"
              />
            </v-col>
          </v-row>

          <!-- Base tag -->
          <v-select
            v-model="txForm.base"
            :items="bases"
            label="Base (optional)"
            variant="outlined"
            density="comfortable"
            class="mb-3"
            clearable
            prepend-inner-icon="mdi-tag-outline"
          />

          <!-- Exchange Rate (only when currency differs from box currency) -->
          <v-text-field
            v-if="showExchangeRate"
            v-model.number="txForm.exchangeRate"
            :label="`Exchange Rate (1 ${txForm.currency} = ? ${detail?.currency})`"
            type="number"
            variant="outlined"
            density="comfortable"
            class="mb-3"
            hint="Used to compute balance in wallet's base currency"
            persistent-hint
          />

          <!-- From (Deposit) -->
          <template v-if="txForm.type === 'Deposit'">
            <v-select
              v-model="txForm.fromType"
              :items="[{ title: 'Mother Wallet', value: 'MotherWallet' }, { title: 'Customer', value: 'Customer' }]"
              label="From"
              variant="outlined"
              density="comfortable"
              class="mb-3"
              @update:model-value="onFromTypeChange"
            />
            <template v-if="txForm.fromType === 'Customer'">
              <v-autocomplete
                v-model="txForm.fromCustomerId"
                v-model:search="customerSearch"
                :items="customerItems"
                :item-title="customerTitle"
                item-value="id"
                label="Customer"
                variant="outlined"
                density="comfortable"
                class="mb-3"
                clearable
                no-filter
                :loading="customersLoading"
                no-data-text="Type to search customers"
                @update:search="onCustomerSearch"
                @update:model-value="onCustomerPicked"
              />
              <v-autocomplete
                v-if="txForm.fromCustomerId"
                v-model="txForm.invoiceId"
                :items="invoices"
                item-title="invoiceNumber"
                item-value="id"
                label="PI# (optional)"
                variant="outlined"
                density="comfortable"
                clearable
                :loading="invoicesLoading"
                no-data-text="No invoices for this customer"
                class="mb-3"
              />
            </template>
          </template>

          <!-- To (Withdraw) -->
          <template v-if="txForm.type === 'Withdraw'">
            <v-select
              v-model="txForm.toType"
              :items="[{ title: 'Mother Wallet', value: 'MotherWallet' }, { title: 'Supplier', value: 'Supplier' }]"
              label="To"
              variant="outlined"
              density="comfortable"
              class="mb-3"
              @update:model-value="onToTypeChange"
            />
            <template v-if="txForm.toType === 'Supplier'">
              <v-autocomplete
                v-model="txForm.toSupplierId"
                v-model:search="supplierSearch"
                :items="supplierItems"
                item-title="name"
                item-value="id"
                label="Supplier"
                variant="outlined"
                density="comfortable"
                class="mb-3"
                clearable
                no-filter
                :loading="suppliersLoading"
                no-data-text="Type to search suppliers"
                @update:search="onSupplierSearch"
                @update:model-value="onSupplierPicked"
              />
              <v-autocomplete
                v-if="txForm.toSupplierId"
                v-model="txForm.paymentRequestId"
                :items="filteredPaymentRequests"
                :item-title="(pr) => `PR-${pr.prNumber}`"
                item-value="id"
                label="PR# (optional)"
                variant="outlined"
                density="comfortable"
                clearable
                no-data-text="No payment requests for this supplier"
                class="mb-3"
              />
            </template>
          </template>

          <v-text-field
            v-if="txForm.id"
            v-model="txForm.createdAt"
            label="Transaction Date/Time"
            type="datetime-local"
            variant="outlined"
            density="comfortable"
            class="mb-3"
          />

          <!-- Notes -->
          <v-textarea
            v-model="txForm.notes"
            label="Notes (optional)"
            variant="outlined"
            density="comfortable"
            rows="2"
            auto-grow
          />
          </template>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="txDialog = false">Cancel</v-btn>
          <v-btn color="primary" :loading="txSaving" @click="saveTx">
        {{ txForm.id ? 'Save Changes' : txForm.type === 'Transfer' ? 'Send for Approval' : 'Add' }}
      </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Negative Balance Warning -->
    <v-dialog v-model="negativeWarnDialog" max-width="420">
      <v-card rounded="lg">
        <v-card-title class="pa-4 text-h6 d-flex align-center gap-2">
          <v-icon icon="mdi-alert-outline" color="warning" />
          Balance Will Go Negative
        </v-card-title>
        <v-card-text class="pa-4">
          This transaction will make the wallet balance negative
          (<span class="font-weight-bold text-error">{{ currencySymbol(detail?.currency ?? '') }}{{ formatPrice(projectedBalance) }}</span>).
          Do you want to proceed?
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="negativeWarnDialog = false">Cancel</v-btn>
          <v-btn color="error" @click="proceedDespiteNegative">Proceed Anyway</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="4000" location="bottom end">
      {{ snackbarText }}
    </v-snackbar>

    <!-- Delete Transaction Confirm -->
    <v-dialog v-model="deleteTxDialog" max-width="400">
      <v-card rounded="lg">
        <v-card-title class="pa-4 text-h6">Delete Transaction?</v-card-title>
        <v-card-text class="pa-4">This action cannot be undone.</v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="deleteTxDialog = false">Cancel</v-btn>
          <v-btn color="error" :loading="txDeleting" @click="deleteTx">Delete</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-container>
</template>

<script setup lang="ts">
import { formatPrice } from '~/utils/formatPrice'
import { downloadExcel } from '~/utils/exportExcel'

interface TransactionRow {
  id: number
  type: string
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
  base: string | null
}

interface BoxDetail {
  id: number
  name: string | null
  companyPresetName: string
  currency: string
  totalDeposit: number
  totalWithdraw: number
  balance: number
  transactions: TransactionRow[]
}

interface Customer { id: number; name: string; customerCode: string | null }
interface Supplier { id: number; name: string }
interface Invoice { id: number; invoiceNumber: string; customerId: number }
interface PR { id: number; prNumber: number | null; supplierId: number | null; supplierName: string | null }

const route = useRoute()
const authStore = useAuthStore()
const api = useApi()

const id = computed(() => Number(route.params.id))
const detail = ref<BoxDetail | null>(null)
const loading = ref(true)

// Show the wallet name instead of the raw id in the breadcrumb trail
const { setBreadcrumbLabel } = useBreadcrumb()
watchEffect(() => setBreadcrumbLabel(detail.value ? boxLabel(detail.value) : null))

const exportDialog = ref(false)
const exportForm = ref({
  type: 'All' as 'All' | 'Deposit' | 'Withdraw',
  fromDate: '',
  toDate: '',
})

const negativeWarnDialog = ref(false)
const projectedBalance = ref(0)
let pendingSubmit: (() => Promise<void>) | null = null

const txDialog = ref(false)
const txSaving = ref(false)
const deleteTxDialog = ref(false)
const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')
const txDeleting = ref(false)
const deleteTxTarget = ref<TransactionRow | null>(null)

const customers = ref<Customer[]>([])
const suppliers = ref<Supplier[]>([])
const invoices = ref<Invoice[]>([])
const paymentRequests = ref<PR[]>([])
const allBoxes = ref<{ id: number; name: string | null; companyPresetName: string; currency: string }[]>([])

const txForm = ref({
  id: null as number | null,
  type: 'Deposit' as 'Deposit' | 'Withdraw' | 'Transfer',
  amount: 0,
  currency: '' as string,           // set on open to box currency
  exchangeRate: null as number | null,
  fromType: 'MotherWallet',
  fromCustomerId: null as number | null,
  toType: 'MotherWallet',
  toSupplierId: null as number | null,
  invoiceId: null as number | null,
  paymentRequestId: null as number | null,
  // Wallet-to-wallet
  toBoxId: null as number | null,
  transferExchangeRate: null as number | null,
  base: null as string | null,
  notes: '',
  createdAt: '',
})

// ── Customer / supplier pickers ───────────────────────────────────────────────
// Both catalogs run to thousands of rows, well past any single page, so the
// pickers query the API as the user types instead of filtering a preloaded slice.
const LOOKUP_PAGE_SIZE = 50

const customerSearch = ref('')
const supplierSearch = ref('')
const customersLoading = ref(false)
const suppliersLoading = ref(false)
const invoicesLoading = ref(false)
/** The picked row is kept aside so the field keeps its label once it drops out of the current results. */
const selectedCustomer = ref<Customer | null>(null)
const selectedSupplier = ref<Supplier | null>(null)

function customerTitle(c: Customer) {
  return c.customerCode ? `${c.customerCode} — ${c.name}` : c.name
}

const customerItems = computed(() => {
  const sel = selectedCustomer.value
  return sel && !customers.value.some(c => c.id === sel.id) ? [sel, ...customers.value] : customers.value
})

const supplierItems = computed(() => {
  const sel = selectedSupplier.value
  return sel && !suppliers.value.some(s => s.id === sel.id) ? [sel, ...suppliers.value] : suppliers.value
})

async function loadCustomers(search = '') {
  customersLoading.value = true
  try {
    const s = search.trim()
    const res = await api.get<{ items: Customer[] }>(
      `/customers?page=1&pageSize=${LOOKUP_PAGE_SIZE}${s ? `&search=${encodeURIComponent(s)}` : ''}`
    )
    customers.value = (res as any).items ?? res
  } catch (e) {
    console.error(e)
  } finally {
    customersLoading.value = false
  }
}

async function loadSuppliers(search = '') {
  suppliersLoading.value = true
  try {
    const s = search.trim()
    const res = await api.get<{ items: Supplier[] }>(
      `/suppliers?page=1&pageSize=${LOOKUP_PAGE_SIZE}${s ? `&search=${encodeURIComponent(s)}` : ''}`
    )
    suppliers.value = (res as any).items ?? res
  } catch (e) {
    console.error(e)
  } finally {
    suppliersLoading.value = false
  }
}

let customerSearchTimer: any = null
function onCustomerSearch(val: string) {
  clearTimeout(customerSearchTimer)
  // Vuetify echoes the picked item's title back through :search — that is not a new query.
  if (selectedCustomer.value && val === customerTitle(selectedCustomer.value)) return
  customersLoading.value = true
  customerSearchTimer = setTimeout(() => loadCustomers(val ?? ''), 300)
}

let supplierSearchTimer: any = null
function onSupplierSearch(val: string) {
  clearTimeout(supplierSearchTimer)
  if (selectedSupplier.value && val === selectedSupplier.value.name) return
  suppliersLoading.value = true
  supplierSearchTimer = setTimeout(() => loadSuppliers(val ?? ''), 300)
}

function onCustomerPicked(id: number | null) {
  txForm.value.invoiceId = null
  selectedCustomer.value = customerItems.value.find(c => c.id === id) ?? null
  loadCustomerInvoices()
}

function onFromTypeChange() {
  txForm.value.fromCustomerId = null
  txForm.value.invoiceId = null
  selectedCustomer.value = null
  customerSearch.value = ''
  invoices.value = []
}

function onToTypeChange() {
  txForm.value.toSupplierId = null
  txForm.value.paymentRequestId = null
  selectedSupplier.value = null
  supplierSearch.value = ''
}

function onSupplierPicked(id: number | null) {
  txForm.value.paymentRequestId = null
  selectedSupplier.value = supplierItems.value.find(s => s.id === id) ?? null
}

/** PI# options are scoped to the picked customer server-side — the invoice table is far too large to preload. */
async function loadCustomerInvoices() {
  const c = selectedCustomer.value
  if (!c) { invoices.value = []; return }
  invoicesLoading.value = true
  try {
    const scope = c.customerCode
      ? `customerCodes=${encodeURIComponent(c.customerCode)}`
      : `customer=${encodeURIComponent(c.name)}`
    const res = await api.get<{ items: Invoice[] }>(`/invoices?page=1&pageSize=500&${scope}`)
    invoices.value = ((res as any).items ?? res).filter((i: Invoice) => i.customerId === c.id)
  } catch (e) {
    console.error(e)
    invoices.value = []
  } finally {
    invoicesLoading.value = false
  }
}

/**
 * Restores a picker's selection when editing an existing transaction: the row is only known
 * by id + display label, so look it up by label and keep the id-matched hit.
 */
async function seedSelectedCustomer(id: number, label: string | null) {
  selectedCustomer.value = { id, name: label ?? `Customer ${id}`, customerCode: null }
  if (label) {
    await loadCustomers(label)
    const match = customers.value.find(c => c.id === id)
    if (match) selectedCustomer.value = match
  }
  await loadCustomerInvoices()
}

async function seedSelectedSupplier(id: number, label: string | null) {
  selectedSupplier.value = { id, name: label ?? `Supplier ${id}` }
  if (!label) return
  await loadSuppliers(label)
  const match = suppliers.value.find(s => s.id === id)
  if (match) selectedSupplier.value = match
}

const filteredPaymentRequests = computed(() =>
  txForm.value.toSupplierId
    ? paymentRequests.value.filter(pr => pr.supplierId === txForm.value.toSupplierId)
    : []
)

const currencies = ['USD', 'EUR', 'CNY', 'GBP', 'AED', 'RUB']

/** Wallet-side base tag options. */
const bases = ['B1', 'B2', 'B3', 'B4', 'B5', 'B6', 'B7']
/** Full list for the filter dropdown's "Show all", so unused bases stay visible. */
const allBaseOptions = [...bases, '—']

function currencySymbol(c: string) {
  return ({ USD: '$', EUR: '€', GBP: '£', CNY: '¥', AED: 'د.إ', RUB: '₽' } as Record<string, string>)[c] ?? c
}

const headers = [
  { title: 'Deposit', key: 'deposit', sortable: true, width: '130px' },
  { title: 'Withdraw', key: 'withdraw', sortable: true, width: '130px' },
  { title: 'From', key: 'fromName', sortable: true },
  { title: 'To', key: 'toName', sortable: true },
  { title: 'PI#', key: 'piNumber', sortable: true, width: '100px' },
  { title: 'PR#', key: 'prNumber', sortable: true, width: '100px' },
  { title: 'Base', key: 'base', sortable: true, width: '90px' },
  { title: 'Rate', key: 'exchangeRate', sortable: true, width: '90px' },
  { title: 'Notes', key: 'notes', sortable: true },
  { title: 'Source', key: 'isAuto', sortable: true, width: '90px' },
  { title: 'Date', key: 'createdAt', sortable: true, width: '100px' },
  { title: 'Balance', key: 'balance', sortable: true, width: '120px' },
  { title: '', key: 'actions', sortable: false, width: '70px' },
]

// ── Excel-style column filters ────────────────────────────────────────────────
const colFilter = useColFilter()
const rangeFilter = useRangeFilter()

/** Column keys carrying a value-list filter, paired with the label shown in the cell. */
const LIST_COLS = {
  fromName: (t: TransactionRow) => (t.type === 'Deposit' ? (t.fromName ?? 'Mother Wallet') : '—'),
  toName: (t: TransactionRow) => (t.type === 'Withdraw' ? (t.toName ?? 'Mother Wallet') : '—'),
  piNumber: (t: TransactionRow) => t.piNumber ?? '—',
  prNumber: (t: TransactionRow) => t.prNumber ?? '—',
  base: (t: TransactionRow) => t.base ?? '—',
  exchangeRate: (t: TransactionRow) =>
    t.exchangeRate != null ? `${t.txCurrency ?? detail.value?.currency ?? ''} ×${t.exchangeRate}` : '—',
  notes: (t: TransactionRow) => t.notes?.trim() || '—',
  isAuto: (t: TransactionRow) => (t.isAuto ? 'Auto' : 'Manual'),
  createdAt: (t: TransactionRow) => new Date(t.createdAt).toLocaleDateString(),
} satisfies Record<string, (t: TransactionRow) => string>

type ListColKey = keyof typeof LIST_COLS
const LIST_KEYS = Object.keys(LIST_COLS) as ListColKey[]
const RANGE_KEYS = ['deposit', 'withdraw', 'balance'] as const

function uniq(vals: string[]) {
  return [...new Set(vals)].sort((a, b) => a.localeCompare(b, undefined, { numeric: true }))
}

const allTx = computed(() => detail.value?.transactions ?? [])

const cfOptions = computed(() => {
  const out = {} as Record<ListColKey, string[]>
  for (const key of LIST_KEYS) out[key] = uniq(allTx.value.map(LIST_COLS[key]))
  return out
})

function bounds(values: number[]) {
  if (values.length === 0) return null
  return { lo: Math.min(...values), hi: Math.max(...values) }
}

const depositBounds = computed(() => bounds(allTx.value.filter(t => t.deposit != null).map(t => t.deposit!)))
const withdrawBounds = computed(() => bounds(allTx.value.filter(t => t.withdraw != null).map(t => t.withdraw!)))
const balanceBounds = computed(() => bounds(allTx.value.map(t => t.balance)))

const displayedTransactions = computed(() =>
  allTx.value.filter(t => {
    for (const key of LIST_KEYS) {
      const sel = colFilter.selected[key]
      if (sel?.size && !sel.has(LIST_COLS[key](t))) return false
    }
    if (!rangeFilter.matches('deposit', t.deposit)) return false
    if (!rangeFilter.matches('withdraw', t.withdraw)) return false
    if (!rangeFilter.matches('balance', t.balance)) return false
    return true
  })
)

const hasAnyFilter = computed(() =>
  LIST_KEYS.some(k => colFilter.isActive(k)) || RANGE_KEYS.some(k => rangeFilter.isActive(k))
)

function clearFilters() {
  for (const k of LIST_KEYS) colFilter.clearAll(k)
  for (const k of RANGE_KEYS) rangeFilter.clear(k)
}

// ── Footer totals ─────────────────────────────────────────────────────────────
/**
 * Deposits and withdrawals are recorded in the transaction's own currency, so
 * they are summed per currency rather than added into one misleading figure.
 */
function sumByCurrency(rows: TransactionRow[], field: 'deposit' | 'withdraw') {
  const map = new Map<string, number>()
  for (const r of rows) {
    const v = r[field]
    if (v == null) continue
    const c = r.txCurrency || detail.value?.currency || ''
    map.set(c, (map.get(c) ?? 0) + v)
  }
  return [...map].map(([currency, total]) => ({ currency, total }))
}

const depositTotals = computed(() => sumByCurrency(displayedTransactions.value, 'deposit'))
const withdrawTotals = computed(() => sumByCurrency(displayedTransactions.value, 'withdraw'))

function totalCellClass(key: unknown) {
  return key === 'deposit' || key === 'withdraw' ? 'tx-total-cell' : ''
}

async function loadDetail() {
  loading.value = true
  try {
    detail.value = await api.get<BoxDetail>(`/payment-boxes/${id.value}`)
  } catch (e) {
    console.error(e)
  } finally {
    loading.value = false
  }
}

async function loadLookups() {
  // Customers and suppliers only seed their first page here; typing in the picker re-queries the API.
  const [, , pr, boxes] = await Promise.allSettled([
    loadCustomers(),
    loadSuppliers(),
    api.get<PR[]>('/paymentrequests'),
    api.get<any[]>('/payment-boxes'),
  ])
  if (pr.status === 'fulfilled') paymentRequests.value = pr.value
  if (boxes.status === 'fulfilled') allBoxes.value = (boxes.value as any[]).filter(b => b.id !== id.value)
}

function openAddTx() {
  resetForm()
  txDialog.value = true
}

function openEditTx(tx: TransactionRow) {
  txForm.value = {
    id: tx.id,
    type: tx.type as any,
    amount: tx.deposit || tx.withdraw || 0,
    currency: tx.txCurrency || detail.value?.currency || 'USD',
    exchangeRate: tx.exchangeRate,
    fromType: tx.fromType || 'MotherWallet',
    fromCustomerId: tx.fromCustomerId,
    toType: tx.toType || 'MotherWallet',
    toSupplierId: tx.toSupplierId,
    invoiceId: tx.piId,
    paymentRequestId: tx.prId,
    toBoxId: null,
    transferExchangeRate: null,
    base: tx.base,
    notes: tx.notes || '',
    createdAt: tx.createdAt ? new Date(tx.createdAt).toISOString().slice(0, 16) : '',
  }
  // The picked customer/supplier may sit outside the seeded page — pull it back in by label.
  selectedCustomer.value = null
  selectedSupplier.value = null
  invoices.value = []
  if (tx.fromType === 'Customer' && tx.fromCustomerId) seedSelectedCustomer(tx.fromCustomerId, tx.fromName)
  if (tx.toType === 'Supplier' && tx.toSupplierId) seedSelectedSupplier(tx.toSupplierId, tx.toName)
  txDialog.value = true
}

function buildSubmitFn(): (() => Promise<void>) | null {
  const form = txForm.value
  if (form.type === 'Transfer' && !form.id) {
    if (!form.toBoxId || !form.amount || form.amount <= 0) return null
    const payload = {
      fromBoxId: id.value,
      toBoxId: form.toBoxId,
      withdrawAmount: form.amount,
      // Deposited amount is always derived from the rate — 1:1 when no exchange is involved.
      depositAmount: transferRealAmount.value,
      exchangeRate: form.transferExchangeRate || null,
      notes: form.notes || null,
    }
    return () => api.post('/wallet-transfers', payload)
  } else {
    if (!form.amount || form.amount <= 0) return null
    const boxCurrency = detail.value?.currency ?? 'USD'
    const isSameCurrency = !form.currency || form.currency === boxCurrency
    const body = {
      type: form.type,
      amount: form.amount,
      fromType: form.type === 'Deposit' ? form.fromType : 'MotherWallet',
      fromCustomerId: form.type === 'Deposit' && form.fromType === 'Customer' ? form.fromCustomerId : null,
      toType: form.type === 'Withdraw' ? form.toType : 'MotherWallet',
      toSupplierId: form.type === 'Withdraw' && form.toType === 'Supplier' ? form.toSupplierId : null,
      invoiceId: form.type === 'Deposit' ? form.invoiceId : null,
      paymentRequestId: form.type === 'Withdraw' ? form.paymentRequestId : null,
      notes: form.notes || null,
      base: form.base || null,
      currency: isSameCurrency ? null : form.currency,
      exchangeRate: isSameCurrency ? null : form.exchangeRate,
      toPaymentBoxId: null,
      createdAt: form.id ? new Date(form.createdAt).toISOString() : new Date().toISOString(),
    }
    if (form.id) {
      return () => api.patch(`/payment-boxes/${id.value}/transactions/${form.id}`, body)
    } else {
      return () => api.post(`/payment-boxes/${id.value}/transactions`, body)
    }
  }
}

async function saveTx() {
  const submitFn = buildSubmitFn()
  if (!submitFn) return

  // Transfers go to pending approval — skip negative balance check and immediate deduction
  if (txForm.value.type === 'Transfer') {
    await executeSubmit(submitFn)
    return
  }

  // Check if withdraw would make balance negative
  const form = txForm.value
  if (form.type === 'Withdraw' && detail.value) {
    const deductAmount = form.amount * (form.exchangeRate ?? 1)
    const newBalance = detail.value.balance - deductAmount
    if (newBalance < 0) {
      projectedBalance.value = newBalance
      pendingSubmit = submitFn
      negativeWarnDialog.value = true
      return
    }
  }

  await executeSubmit(submitFn)
}

async function proceedDespiteNegative() {
  negativeWarnDialog.value = false
  if (pendingSubmit) {
    await executeSubmit(pendingSubmit)
    pendingSubmit = null
  }
}

async function executeSubmit(submitFn: () => Promise<void>) {
  txSaving.value = true
  const isTransfer = txForm.value.type === 'Transfer'
  try {
    await submitFn()
    txDialog.value = false
    resetForm()
    if (isTransfer) {
      snackbarText.value = 'Transfer request submitted — pending acceptance in Payment Withdraw'
      snackbarColor.value = 'deep-purple'
      snackbar.value = true
    } else {
      await loadDetail()
    }
  } catch (e) {
    console.error(e)
    snackbarText.value = 'Failed to save transaction'
    snackbarColor.value = 'error'
    snackbar.value = true
  } finally {
    txSaving.value = false
  }
}

function resetForm() {
  selectedCustomer.value = null
  selectedSupplier.value = null
  customerSearch.value = ''
  supplierSearch.value = ''
  invoices.value = []
  txForm.value = {
    id: null,
    type: 'Deposit',
    amount: 0,
    currency: detail.value?.currency ?? 'USD',
    exchangeRate: null,
    fromType: 'MotherWallet',
    fromCustomerId: null,
    toType: 'MotherWallet',
    toSupplierId: null,
    invoiceId: null,
    paymentRequestId: null,
    toBoxId: null,
    transferExchangeRate: null,
    base: null,
    notes: '',
    createdAt: '',
  }
}

const toBoxCurrency = computed(() =>
  allBoxes.value.find(b => b.id === txForm.value.toBoxId)?.currency ?? ''
)

const showExchangeRate = computed(() =>
  txForm.value.currency && txForm.value.currency !== (detail.value?.currency ?? 'USD')
)

const showTransferRate = computed(() =>
  txForm.value.toBoxId &&
  toBoxCurrency.value &&
  toBoxCurrency.value !== (detail.value?.currency ?? 'USD')
)

// Amount that actually lands in the target wallet: rate × withdraw (1:1 when no exchange).
const transferRealAmount = computed(() =>
  Math.round(txForm.value.amount * (txForm.value.transferExchangeRate || 1) * 100) / 100
)

/** Wallets are identified by their own name; fall back to the company preset when unnamed. */
function boxLabel(box: { id?: number; name?: string | null; companyPresetName?: string | null }) {
  return box.name?.trim() || box.companyPresetName || `Wallet ${box.id ?? ''}`.trim()
}

function confirmDeleteTx(tx: TransactionRow) {
  deleteTxTarget.value = tx
  deleteTxDialog.value = true
}

async function deleteTx() {
  if (!deleteTxTarget.value) return
  txDeleting.value = true
  try {
    await api.del(`/payment-boxes/${id.value}/transactions/${deleteTxTarget.value.id}`)
    await loadDetail()
    deleteTxDialog.value = false
  } catch (e) {
    console.error(e)
  } finally {
    txDeleting.value = false
  }
}

function doExport() {
  const { type, fromDate, toDate } = exportForm.value
  const from = fromDate ? new Date(fromDate) : null
  const to = toDate ? new Date(toDate + 'T23:59:59') : null
  const walletName = detail.value ? boxLabel(detail.value) : 'wallet'

  // Exports what the table currently shows — the column filters carry over.
  const rows = displayedTransactions.value.filter(t => {
    if (type === 'Deposit' && t.deposit == null) return false
    if (type === 'Withdraw' && t.withdraw == null) return false
    const d = new Date(t.createdAt)
    if (from && d < from) return false
    if (to && d > to) return false
    return true
  }).map(t => ({
    Deposit: t.deposit ?? '',
    Withdraw: t.withdraw ?? '',
    From: t.fromName ?? '',
    To: t.toName ?? '',
    'PI#': t.piNumber ?? '',
    'PR#': t.prNumber ?? '',
    Base: t.base ?? '',
    Currency: t.txCurrency ?? detail.value?.currency ?? '',
    'Exchange Rate': t.exchangeRate ?? '',
    Notes: t.notes ?? '',
    Source: t.isAuto ? 'Auto' : 'Manual',
    Date: new Date(t.createdAt).toLocaleDateString(),
    Balance: t.balance,
  }))

  downloadExcel(rows, `${walletName}-transactions`)
  exportDialog.value = false
}

watch(txDialog, (open) => {
  if (open && authStore.isSuperAdmin) {
    txForm.value.currency = detail.value?.currency ?? 'USD'
    if (customers.value.length === 0) loadLookups()
  }
})

onMounted(async () => {
  await loadDetail()
  const editId = Number(route.query.edit)
  if (editId && detail.value) {
    const tx = detail.value.transactions.find(t => t.id === editId)
    if (tx) {
      await loadLookups()
      openEditTx(tx)
    }
  }
})
</script>

<style scoped>
/* Remaining balance: the one number the user scans for, so it outsizes everything else. */
.wallet-balance-panel {
  min-width: 260px;
  margin-left: auto;
}
.wallet-balance-amount {
  font-size: 2.4rem;
  line-height: 1.15;
  letter-spacing: -0.5px;
}
@media (max-width: 960px) {
  .wallet-balance-panel { min-width: 100%; }
  .wallet-balance-amount { font-size: 1.85rem; }
}

.tx-total-row {
  background: rgba(var(--v-theme-primary), 0.06);
}
.tx-total-row td {
  border-top: 2px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.tx-total-cell {
  white-space: nowrap;
}
</style>
