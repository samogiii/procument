<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4 mb-md-6">
      <h1 class="text-h5 font-weight-bold">RFQ Items</h1>
      <v-spacer />
    </div>

    <!-- Rejected Supplier Banner (user-facing) -->
    <v-alert
      v-if="!isAdmin && rejectedSupplierQuotes.length > 0"
      type="error"
      variant="tonal"
      density="compact"
      class="mb-4"
      prepend-icon="mdi-account-alert-outline"
    >
      <strong>{{ rejectedSupplierQuotes.length }} supplier(s) were rejected.</strong>
      Please correct and resubmit — click the red indicator next to the supplier name.
    </v-alert>

    <!-- Pending Supplier Banner (user-facing) -->
    <v-alert
      v-if="!isAdmin && pendingSupplierQuotes.length > 0"
      type="warning"
      variant="tonal"
      density="compact"
      class="mb-4"
      prepend-icon="mdi-clock-outline"
    >
      <strong>{{ pendingSupplierQuotes.length }} supplier(s) pending admin approval.</strong>
      They will appear in quotes once approved.
    </v-alert>

    <v-card class="glass-card">
      <v-card-text>
        <div class="d-flex flex-wrap gap-3 mb-4">
          <v-text-field
            v-model="search"
            prepend-inner-icon="mdi-magnify"
            label="Search..."
            single-line
            hide-details
            class="flex-grow-1 mx-2"
            style="min-width: 180px;"
          />
          <v-select
            v-model="statusFilter"
            :items="statusSelectItems"
            item-title="label"
            item-value="value"
            label="Status"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            class="mr-2"
            style="min-width: 120px; max-width: 260px;"
          >
            <template #item="{ props: iProps, item }">
              <v-list-item v-bind="iProps" :class="{ 'text-disabled': !item.raw.available }" density="compact">
                <template #prepend="{ isSelected }">
                  <v-checkbox-btn :model-value="isSelected" density="compact" class="mr-1" />
                </template>
                <template #append>
                  <v-icon v-if="!item.raw.available" icon="mdi-eye-off-outline" size="14" class="text-disabled ml-1" />
                </template>
              </v-list-item>
            </template>
            <template #append-item>
              <v-divider class="mt-1 mb-1" />
              <v-list-item
                :title="showAllStatuses ? 'Show available only' : 'Show all statuses'"
                :prepend-icon="showAllStatuses ? 'mdi-filter' : 'mdi-filter-off'"
                density="compact"
                class="text-caption text-medium-emphasis"
                @click.stop="showAllStatuses = !showAllStatuses"
              />
            </template>
          </v-select>
           <v-text-field
            v-model="pnSearch"
            label="Search by P/N"
            prepend-inner-icon="mdi-cog-outline"
            hide-details
            clearable
            density="compact"
            variant="outlined"
            class="mx-2"
            style="min-width: 160px; max-width: 260px;"
          />
          <v-autocomplete
            v-model="userFilter"
            :items="userSelectItems"
            item-title="name"
            item-value="id"
            label="User"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            density="compact"
            variant="outlined"
            class="mx-2"
            style="min-width: 140px; max-width: 240px;"
          >
            <template #append-item>
              <v-divider class="mt-1 mb-1" />
              <v-list-item
                :title="showAllUsers ? 'Show available only' : 'Show all users'"
                :prepend-icon="showAllUsers ? 'mdi-filter' : 'mdi-filter-off'"
                density="compact"
                class="text-caption text-medium-emphasis"
                @click.stop="showAllUsers = !showAllUsers"
              />
            </template>
          </v-autocomplete>
          <v-autocomplete
            v-model="customerFilter"
            :items="customerSelectItems"
            item-title="title"
            item-value="value"
            label="Customer"
            hide-details
            multiple
            chips
            closable-chips
            clearable
            density="compact"
            variant="outlined"
            style="min-width: 140px; max-width: 260px;"
          >
            <template #append-item>
              <v-divider class="mt-1 mb-1" />
              <v-list-item
                :title="showAllCustomers ? 'Show available only' : 'Show all customers'"
                :prepend-icon="showAllCustomers ? 'mdi-filter' : 'mdi-filter-off'"
                density="compact"
                class="text-caption text-medium-emphasis"
                @click.stop="showAllCustomers = !showAllCustomers"
              />
            </template>
          </v-autocomplete>
          <v-btn
            :variant="showPendingOnly ? 'flat' : 'tonal'"
            :color="showPendingOnly ? 'warning' : 'grey'"
            size="small"
            prepend-icon="mdi-clock-outline"
            class="align-self-center"
            @click="showPendingOnly = !showPendingOnly"
          >
            Pending Suppliers
          </v-btn>
          <v-btn
            :color="showNoQuote ? 'warning' : 'default'"
            :variant="showNoQuote ? 'tonal' : 'outlined'"
            size="small"
            :prepend-icon="showNoQuote ? 'mdi-eye' : 'mdi-eye-off-outline'"
            class="align-self-center"
            @click="showNoQuote = !showNoQuote"
          >
            No Quote
          </v-btn>
          <v-btn
            v-if="hasActiveFilters"
            variant="tonal"
            color="error"
            size="small"
            prepend-icon="mdi-filter-off"
            class="align-self-center"
            @click="clearFilters"
          >
            Clear
          </v-btn>
        </div>

        <!-- Column filter status bar -->
        <div v-if="procActiveFilterCount > 0" class="d-flex align-center gap-2 mb-2">
          <v-chip size="small" color="primary" variant="tonal" closable prepend-icon="mdi-filter" @click:close="clearAllProcFilters">
            {{ procActiveFilterCount }} column filter{{ procActiveFilterCount !== 1 ? 's' : '' }} active
          </v-chip>
        </div>

        <v-data-table-server
          :headers="headers"
          :items="allItems"
          :items-length="totalItems"
          :loading="loading"
          v-model:page="currentPage"
          v-model:items-per-page="currentItemsPerPage"
          :items-per-page-options="pageOptions"
          hover
          density="comfortable"
          item-value="rfqItemId"
          v-model:expanded="expandedArray"
          show-expand
          :row-props="getRowProps"
          @update:options="loadServerPage"
          @click:row="(_: any, { item }: any) => toggleExpand(item)"
        >
          <!-- Column filter: RFQ # -->
          <template #header.rfqId="{ column, toggleSort, isSorted, sortBy }">
            <div class="proc-th-inner">
              <span class="cursor-pointer" @click="toggleSort(column)">{{ column.title }}
                <v-icon v-if="isSorted(column)" :icon="sortBy.find((s: any) => s.key === column.key)?.order === 'asc' ? 'mdi-arrow-up' : 'mdi-arrow-down'" size="12" />
              </span>
              <v-menu :close-on-content-click="false" max-width="260">
                <template #activator="{ props: mp }">
                  <v-btn v-bind="mp" :icon="colRfqId.length ? 'mdi-filter' : 'mdi-filter-outline'" size="x-small" variant="text" :color="colRfqId.length ? 'primary' : undefined" class="proc-filter-btn" @click.stop />
                </template>
                <v-card class="pa-2" min-width="220">
                  <v-text-field v-model="colSearch.rfqId" placeholder="Search…" density="compact" variant="outlined" hide-details clearable class="mb-2" />
                  <div style="max-height:220px;overflow-y:auto;">
                    <v-checkbox v-for="val in filteredColRfqIdOpts" :key="val" :label="val" :model-value="colRfqId.includes(val)" density="compact" hide-details @update:model-value="toggleCol(colRfqId, val)" />
                  </div>
                  <v-divider class="my-1" />
                  <div class="d-flex gap-1">
                    <v-btn size="x-small" variant="text" color="primary" @click="selectAllCol(colRfqId, filteredColRfqIdOpts)">All</v-btn>
                    <v-btn size="x-small" variant="text" color="error" :disabled="!colRfqId.length" @click="colRfqId.splice(0)">None</v-btn>
                  </div>
                </v-card>
              </v-menu>
            </div>
          </template>

          <!-- Column filter: RFQ Name -->
          <template #header.rfqName="{ column, toggleSort, isSorted, sortBy }">
            <div class="proc-th-inner">
              <span class="cursor-pointer" @click="toggleSort(column)">{{ column.title }}
                <v-icon v-if="isSorted(column)" :icon="sortBy.find((s: any) => s.key === column.key)?.order === 'asc' ? 'mdi-arrow-up' : 'mdi-arrow-down'" size="12" />
              </span>
              <v-menu :close-on-content-click="false" max-width="260">
                <template #activator="{ props: mp }">
                  <v-btn v-bind="mp" :icon="colRfqName.length ? 'mdi-filter' : 'mdi-filter-outline'" size="x-small" variant="text" :color="colRfqName.length ? 'primary' : undefined" class="proc-filter-btn" @click.stop />
                </template>
                <v-card class="pa-2" min-width="220">
                  <v-text-field v-model="colSearch.rfqName" placeholder="Search…" density="compact" variant="outlined" hide-details clearable class="mb-2" />
                  <div style="max-height:220px;overflow-y:auto;">
                    <v-checkbox v-for="val in filteredColRfqNameOpts" :key="val" :label="val" :model-value="colRfqName.includes(val)" density="compact" hide-details @update:model-value="toggleCol(colRfqName, val)" />
                  </div>
                  <v-divider class="my-1" />
                  <div class="d-flex gap-1">
                    <v-btn size="x-small" variant="text" color="primary" @click="selectAllCol(colRfqName, filteredColRfqNameOpts)">All</v-btn>
                    <v-btn size="x-small" variant="text" color="error" :disabled="!colRfqName.length" @click="colRfqName.splice(0)">None</v-btn>
                  </div>
                </v-card>
              </v-menu>
            </div>
          </template>

          <!-- Column header filter menus (server-side, persisted via usePageFilters) -->
          <template #header.partNumberName="{ column, toggleSort, isSorted, sortBy }">
            <div class="proc-th-inner">
              <span class="cursor-pointer" @click="toggleSort(column)">{{ column.title }}
                <v-icon v-if="isSorted(column)" :icon="sortBy.find((s: any) => s.key === column.key)?.order === 'asc' ? 'mdi-arrow-up' : 'mdi-arrow-down'" size="12" />
              </span>
              <v-menu :close-on-content-click="false" max-width="280">
                <template #activator="{ props: mp }">
                  <v-btn v-bind="mp" :icon="colPn.length ? 'mdi-filter' : 'mdi-filter-outline'" size="x-small" variant="text" :color="colPn.length ? 'primary' : undefined" class="proc-filter-btn" @click.stop />
                </template>
                <v-card class="pa-2" min-width="240">
                  <v-text-field v-model="colSearch.pn" placeholder="Search…" density="compact" variant="outlined" hide-details clearable class="mb-2" />
                  <div style="max-height:220px;overflow-y:auto;">
                    <v-checkbox
                      v-for="val in filteredColPnOpts"
                      :key="val"
                      :label="val"
                      :model-value="colPn.includes(val)"
                      density="compact"
                      hide-details
                      :class="{ 'opacity-40': showAllPnOpts && !colPnOpts.includes(val) }"
                      @update:model-value="toggleCol(colPn, val)"
                    />
                  </div>
                  <v-divider class="my-1" />
                  <div class="d-flex gap-1">
                    <v-btn size="x-small" variant="text" color="primary" @click="selectAllCol(colPn, filteredColPnOpts)">All</v-btn>
                    <v-btn size="x-small" variant="text" color="error" :disabled="!colPn.length" @click="colPn.splice(0)">None</v-btn>
                  </div>
                  <v-divider class="mt-1 mb-1" />
                  <v-list-item
                    :title="showAllPnOpts ? 'Show available only' : 'Show all part numbers'"
                    :prepend-icon="showAllPnOpts ? 'mdi-filter' : 'mdi-filter-off'"
                    density="compact"
                    class="text-caption text-medium-emphasis"
                    @click.stop="showAllPnOpts = !showAllPnOpts"
                  />
                </v-card>
              </v-menu>
            </div>
          </template>
          <template #header.condition="{ column, toggleSort, isSorted, sortBy }">
            <div class="proc-th-inner">
              <span class="cursor-pointer" @click="toggleSort(column)">{{ column.title }}
                <v-icon v-if="isSorted(column)" :icon="sortBy.find((s: any) => s.key === column.key)?.order === 'asc' ? 'mdi-arrow-up' : 'mdi-arrow-down'" size="12" />
              </span>
              <v-menu :close-on-content-click="false" max-width="260">
                <template #activator="{ props: mp }">
                  <v-btn v-bind="mp" :icon="colCond.length ? 'mdi-filter' : 'mdi-filter-outline'" size="x-small" variant="text" :color="colCond.length ? 'primary' : undefined" class="proc-filter-btn" @click.stop />
                </template>
                <v-card class="pa-2" min-width="220">
                  <v-text-field v-model="colSearch.cond" placeholder="Search…" density="compact" variant="outlined" hide-details clearable class="mb-2" />
                  <div style="max-height:220px;overflow-y:auto;">
                    <v-checkbox v-for="val in filteredColCondOpts" :key="val" :label="val" :model-value="colCond.includes(val)" density="compact" hide-details :class="{ 'opacity-40': showAllCondOpts && !condOptsPage.includes(val) }" @update:model-value="toggleCol(colCond, val)" />
                  </div>
                  <v-divider class="my-1" />
                  <div class="d-flex gap-1">
                    <v-btn size="x-small" variant="text" color="primary" @click="selectAllCol(colCond, filteredColCondOpts)">All</v-btn>
                    <v-btn size="x-small" variant="text" color="error" :disabled="!colCond.length" @click="colCond.splice(0)">None</v-btn>
                  </div>
                  <v-divider class="mt-1 mb-1" />
                  <v-list-item
                    :title="showAllCondOpts ? 'Show available only' : 'Show all'"
                    :prepend-icon="showAllCondOpts ? 'mdi-filter' : 'mdi-filter-off'"
                    density="compact"
                    class="text-caption text-medium-emphasis"
                    @click.stop="showAllCondOpts = !showAllCondOpts"
                  />
                </v-card>
              </v-menu>
            </div>
          </template>
          <template #header.customerName="{ column, toggleSort, isSorted, sortBy }">
            <div class="proc-th-inner">
              <span class="cursor-pointer" @click="toggleSort(column)">{{ column.title }}
                <v-icon v-if="isSorted(column)" :icon="sortBy.find((s: any) => s.key === column.key)?.order === 'asc' ? 'mdi-arrow-up' : 'mdi-arrow-down'" size="12" />
              </span>
              <v-menu :close-on-content-click="false" max-width="260">
                <template #activator="{ props: mp }">
                  <v-btn v-bind="mp" :icon="customerFilter.length ? 'mdi-filter' : 'mdi-filter-outline'" size="x-small" variant="text" :color="customerFilter.length ? 'primary' : undefined" class="proc-filter-btn" @click.stop />
                </template>
                <v-card class="pa-2" min-width="220">
                  <v-text-field v-model="colSearch.cust" placeholder="Search…" density="compact" variant="outlined" hide-details clearable class="mb-2" />
                  <div style="max-height:220px;overflow-y:auto;">
                    <!-- opt.title = customer code (display), opt.value = customer name (stored in customerFilter) -->
                    <v-checkbox v-for="opt in filteredColCustOpts" :key="opt.value" :label="opt.title" :model-value="customerFilter.includes(opt.value)" density="compact" hide-details :class="{ 'opacity-40': showAllCustColOpts && !custColOptsPage.some(p => p.value === opt.value) }" @update:model-value="toggleCol(customerFilter, opt.value)" />
                  </div>
                  <v-divider class="my-1" />
                  <div class="d-flex gap-1">
                    <v-btn size="x-small" variant="text" color="primary" @click="selectAllCust(customerFilter, filteredColCustOpts)">All</v-btn>
                    <v-btn size="x-small" variant="text" color="error" :disabled="!customerFilter.length" @click="customerFilter.splice(0)">None</v-btn>
                  </div>
                  <v-divider class="mt-1 mb-1" />
                  <v-list-item
                    :title="showAllCustColOpts ? 'Show available only' : 'Show all'"
                    :prepend-icon="showAllCustColOpts ? 'mdi-filter' : 'mdi-filter-off'"
                    density="compact"
                    class="text-caption text-medium-emphasis"
                    @click.stop="showAllCustColOpts = !showAllCustColOpts"
                  />
                </v-card>
              </v-menu>
            </div>
          </template>
          <template #header.status="{ column, toggleSort, isSorted, sortBy }">
            <div class="proc-th-inner">
              <span class="cursor-pointer" @click="toggleSort(column)">{{ column.title }}
                <v-icon v-if="isSorted(column)" :icon="sortBy.find((s: any) => s.key === column.key)?.order === 'asc' ? 'mdi-arrow-up' : 'mdi-arrow-down'" size="12" />
              </span>
              <v-menu :close-on-content-click="false" max-width="260">
                <template #activator="{ props: mp }">
                  <v-btn v-bind="mp" :icon="statusFilter.length ? 'mdi-filter' : 'mdi-filter-outline'" size="x-small" variant="text" :color="statusFilter.length ? 'primary' : undefined" class="proc-filter-btn" @click.stop />
                </template>
                <v-card class="pa-2" min-width="220">
                  <v-text-field v-model="colSearch.status" placeholder="Search…" density="compact" variant="outlined" hide-details clearable class="mb-2" />
                  <div style="max-height:220px;overflow-y:auto;">
                    <v-checkbox v-for="val in filteredColStatusOpts" :key="val" :label="val" :model-value="statusFilter.includes(val)" density="compact" hide-details :class="{ 'opacity-40': showAllStatusColOpts && !statusColOptsPage.includes(val) }" @update:model-value="toggleCol(statusFilter, val)" />
                  </div>
                  <v-divider class="my-1" />
                  <div class="d-flex gap-1">
                    <v-btn size="x-small" variant="text" color="primary" @click="selectAllCol(statusFilter, filteredColStatusOpts)">All</v-btn>
                    <v-btn size="x-small" variant="text" color="error" :disabled="!statusFilter.length" @click="statusFilter.splice(0)">None</v-btn>
                  </div>
                  <v-divider class="mt-1 mb-1" />
                  <v-list-item
                    :title="showAllStatusColOpts ? 'Show available only' : 'Show all'"
                    :prepend-icon="showAllStatusColOpts ? 'mdi-filter' : 'mdi-filter-off'"
                    density="compact"
                    class="text-caption text-medium-emphasis"
                    @click.stop="showAllStatusColOpts = !showAllStatusColOpts"
                  />
                </v-card>
              </v-menu>
            </div>
          </template>
          <template #header.assignedUsers="{ column }">
            <div class="proc-th-inner">
              <span>{{ column.title }}</span>
              <v-menu :close-on-content-click="false" max-width="260">
                <template #activator="{ props: mp }">
                  <v-btn v-bind="mp" :icon="userFilter.length ? 'mdi-filter' : 'mdi-filter-outline'" size="x-small" variant="text" :color="userFilter.length ? 'primary' : undefined" class="proc-filter-btn" @click.stop />
                </template>
                <v-card class="pa-2" min-width="220">
                  <v-text-field v-model="colSearch.users" placeholder="Search…" density="compact" variant="outlined" hide-details clearable class="mb-2" />
                  <div style="max-height:220px;overflow-y:auto;">
                    <v-checkbox v-for="u in filteredColUserOpts" :key="u.id" :label="u.name" :model-value="userFilter.includes(u.id)" density="compact" hide-details :class="{ 'opacity-40': showAllUserColOpts && !userColOptsPage.some(p => p.id === u.id) }" @update:model-value="toggleColId(userFilter, u.id)" />
                  </div>
                  <v-divider class="my-1" />
                  <div class="d-flex gap-1">
                    <v-btn size="x-small" variant="text" color="primary" @click="selectAllUsers(userFilter, filteredColUserOpts)">All</v-btn>
                    <v-btn size="x-small" variant="text" color="error" :disabled="!userFilter.length" @click="userFilter.splice(0)">None</v-btn>
                  </div>
                  <v-divider class="mt-1 mb-1" />
                  <v-list-item
                    :title="showAllUserColOpts ? 'Show available only' : 'Show all'"
                    :prepend-icon="showAllUserColOpts ? 'mdi-filter' : 'mdi-filter-off'"
                    density="compact"
                    class="text-caption text-medium-emphasis"
                    @click.stop="showAllUserColOpts = !showAllUserColOpts"
                  />
                </v-card>
              </v-menu>
            </div>
          </template>

          <template #item.rfqId="{ item }">
            <div class="d-flex align-center gap-1">
              <v-icon
                :icon="expandedArray.includes(item.rfqItemId) ? 'mdi-chevron-down' : 'mdi-chevron-right'"
                size="18"
                :color="expandedArray.includes(item.rfqItemId) ? 'primary' : 'grey'"
              />
              <NuxtLink
                :to="`/rfqs/${item.rfqId}?itemId=${item.rfqItemId}`"
                class="text-primary font-weight-medium text-decoration-none"
                @click.stop
              >
                {{ item.rfqId }}
              </NuxtLink>
            </div>
          </template>
          <template #item.status="{ item }">
            <v-tooltip v-if="item.rejectionNote || (['No Quote', 'Waiting For Admin'].includes(item.rfqStatus) && item.noQuoteReason)" location="bottom">
              <template #activator="{ props: tp }">
                <v-chip
                  v-bind="tp"
                  size="small"
                  :color="rfqStatusColor(item.rfqStatus || 'Open')"
                  variant="tonal"
                >
                  {{ item.rfqStatus }}
                  <v-icon icon="mdi-information-outline" size="14" class="ml-1" />
                </v-chip>
              </template>
              <span>{{ item.noQuoteReason }}{{ item.rejectionNote ? ` (Admin: ${item.rejectionNote})` : '' }}</span>
            </v-tooltip>
            <v-chip
              v-else
              size="small"
              :color="rfqStatusColor(item.rfqStatus || 'Open')"
              variant="tonal"
            >
              {{ item.rfqStatus || 'Open' }}
            </v-chip>
          </template>
          <template #item.supplierCount="{ item }">
            <div class="d-flex flex-wrap gap-1 align-center">
              <v-chip size="small" :color="item.supplierQuotes.length > 0 ? 'success' : 'grey'" variant="tonal">
                {{ item.supplierQuotes.length }} supplier{{ item.supplierQuotes.length !== 1 ? 's' : '' }}
              </v-chip>
              <v-chip
                v-if="item.supplierQuotes.filter((q: any) => q.supplierStatus === 'Pending').length > 0"
                size="x-small"
                color="warning"
                variant="tonal"
                prepend-icon="mdi-clock-outline"
              >
                {{ item.supplierQuotes.filter((q: any) => q.supplierStatus === 'Pending').length }} pending
              </v-chip>
              <v-chip
                v-if="item.supplierQuotes.filter((q: any) => q.supplierStatus === 'Rejected').length > 0"
                size="x-small"
                color="error"
                variant="tonal"
                prepend-icon="mdi-alert-circle-outline"
              >
                {{ item.supplierQuotes.filter((q: any) => q.supplierStatus === 'Rejected').length }} rejected
              </v-chip>
            </div>
          </template>
          <template #item.assignedUsers="{ item }">
            <div class="d-flex flex-wrap gap-1">
              <v-chip
                v-for="user in item.assignedUsers"
                :key="user.id"
                size="x-small"
                color="primary"
                variant="tonal"
                prepend-icon="mdi-account"
              >
                {{ user.name }}
              </v-chip>
              <span v-if="!item.assignedUsers?.length" class="text-medium-emphasis">—</span>
            </div>
          </template>
          <template #item.leadTime="{ item }">
            <div class="d-flex flex-column align-start gap-1">
              <span :class="{ 'text-error font-weight-bold': ['Open', 'In Progress', 'Waiting For Admin'].includes(item.rfqStatus) && isLeadTimeExpired(item.leadTime) }" :style="isLeadTimeUrgent(item.leadTime) ? 'font-weight: 600;' : ''">
                {{ new Date(item.leadTime).toLocaleDateString() }}
              </span>
              <v-chip
                v-if="['Open', 'In Progress', 'Waiting For Admin'].includes(item.rfqStatus)"
                :color="remainingDaysColor(item.leadTime)"
                size="x-small"
                variant="tonal"
              >
                {{ remainingDaysLabel(item.leadTime) }}
              </v-chip>
            </div>
          </template>
          <template #item.customerName="{ item }">
            <template v-if="isAdmin">{{ item.customerCode }}</template>
            <template v-else>{{ item.customerCode || '—' }}</template>
          </template>
          <template #item.createdAt="{ item }">
            {{ new Date(item.createdAt).toLocaleDateString() }}
          </template>

          <!-- Expanded row -->
          <template #expanded-row="{ item, columns }">
            <tr>
              <td :colspan="columns.length" class="pa-0">
                <div class="supplier-panel pa-4" style="background: rgba(var(--v-theme-surface-variant), 0.3);">
                  <div class="d-flex align-center justify-space-between mb-3">
                    <span class="text-caption text-uppercase font-weight-bold letter-spacing-wide" style="color: #60a5fa;">
                      Supplier Quotes for {{ item.partNumberName }}
                    </span>
                    <v-btn
                      size="x-small"
                      color="primary"
                      variant="flat"
                      prepend-icon="mdi-plus"
                      @click.stop="addQuoteRow(item)"
                    >
                      Add Supplier
                    </v-btn>
                  </div>

                  <!-- Supplier Suggestions -->
                  <div
                    v-if="getSuggestions(item.rfqItemId).recentQuotes.length > 0 || getSuggestions(item.rfqItemId).knownSuppliers.length > 0 || partAvailability[item.partNumberId]"
                    class="suggestions-bar mb-3"
                  >
                    <!-- Recent quotes (auto-fill capable) -->
                    <div v-if="getSuggestions(item.rfqItemId).recentQuotes.length > 0" class="d-flex flex-wrap align-center gap-2 mb-2">
                      <span class="text-caption text-medium-emphasis" style="white-space: nowrap;">
                        <v-icon icon="mdi-lightbulb-on-outline" size="14" color="amber" class="mr-1" />
                        Recent suppliers:
                      </span>
                      <v-chip
                        v-for="s in getSuggestions(item.rfqItemId).recentQuotes"
                        :key="'recent-' + s.supplierId"
                        size="small"
                        color="amber"
                        variant="tonal"
                        prepend-icon="mdi-flash"
                        class="cursor-pointer"
                        @click.stop="applySuggestion(item, s)"
                      >
                        {{ s.supplierName }}
                        <span v-if="s.priceHidden" class="text-caption ml-1" style="color:#ef5350;" title="Price expired (older than 14 days)">Expired</span>
                        <span v-else class="text-caption ml-1 text-medium-emphasis">${{ formatPrice(s.price) }}</span>
                      </v-chip>
                      <v-btn
                        v-if="getSuggestions(item.rfqItemId).recentQuotes.length > 1"
                        size="x-small"
                        variant="outlined"
                        color="amber"
                        prepend-icon="mdi-plus-box-multiple"
                        @click.stop="applyAllSuggestions(item)"
                      >
                        Add All
                      </v-btn>
                    </div>
                    <!-- Known suppliers (name only) -->
                    <div
                      v-if="getSuggestions(item.rfqItemId).knownSuppliers.filter((k: any) => !getSuggestions(item.rfqItemId).recentQuotes.some((r: any) => r.supplierId === k.supplierId)).length > 0"
                      class="d-flex flex-wrap align-center gap-2 mb-2"
                    >
                      <span class="text-caption text-medium-emphasis" style="white-space: nowrap;">
                        <v-icon icon="mdi-account-group-outline" size="14" color="grey" class="mr-1" />
                        Known suppliers:
                      </span>
                      <v-chip
                        v-for="k in getSuggestions(item.rfqItemId).knownSuppliers.filter((k: any) => !getSuggestions(item.rfqItemId).recentQuotes.some((r: any) => r.supplierId === k.supplierId))"
                        :key="'known-' + k.supplierId"
                        size="x-small"
                        color="grey"
                        variant="tonal"
                        prepend-icon="mdi-account"
                      >
                        {{ k.supplierName }}
                      </v-chip>
                    </div>
                    <!-- Availability chips (Inventory=green, CapList=blue, ILS=orange, FastImport=yellow, KnownSup=gray) -->
                    <template v-if="partAvailability[item.partNumberId]">
                      <div class="d-flex flex-wrap align-center gap-2 mt-1">
                        <span class="text-caption text-medium-emphasis" style="white-space: nowrap;">
                          <v-icon icon="mdi-database-search-outline" size="14" color="primary" class="mr-1" />
                          In stock:
                        </span>
                        <v-chip
                          v-for="rec in partAvailability[item.partNumberId].inventoryRecords"
                          :key="'inv-' + rec.label"
                          size="small"
                          class="avail-chip avail-chip--inventory cursor-pointer"
                          prepend-icon="mdi-archive-outline"
                          :title="`Inventory · ${rec.label}${rec.condition ? ' · ' + rec.condition : ''}`"
                          @click.stop="applyAvailability(item, rec)"
                        >{{ rec.label }}</v-chip>
                        <v-chip
                          v-for="rec in partAvailability[item.partNumberId].capListRecords"
                          :key="'cap-' + rec.label"
                          size="small"
                          class="avail-chip avail-chip--caplist cursor-pointer"
                          prepend-icon="mdi-format-list-checks"
                          :title="`Cap List · ${rec.label}`"
                          @click.stop="applyAvailability(item, rec)"
                        >{{ rec.label }}</v-chip>
                        <v-chip
                          v-for="rec in partAvailability[item.partNumberId].ilsRecords"
                          :key="'ils-' + rec.condition"
                          size="small"
                          class="avail-chip avail-chip--ils cursor-pointer"
                          prepend-icon="mdi-warehouse"
                          :title="`ILS${rec.condition ? ' · ' + rec.condition : ''}${rec.certName ? ' · ' + rec.certName : ''}`"
                          @click.stop="applyAvailability(item, rec)"
                        >ILS</v-chip>
                        <v-chip
                          v-for="rec in partAvailability[item.partNumberId].fastImportRecords"
                          :key="'fast-' + rec.label"
                          size="small"
                          class="avail-chip avail-chip--fast cursor-pointer"
                          prepend-icon="mdi-flash"
                          :title="`Past record · ${rec.label}${rec.condition ? ' · ' + rec.condition : ''}`"
                          @click.stop="applyAvailability(item, rec, true)"
                        >{{ rec.label }}</v-chip>
                        <v-chip
                          v-for="rec in partAvailability[item.partNumberId].knownSupplierRecords.filter((k: any) => !partAvailability[item.partNumberId].fastImportRecords.some((f: any) => f.label === k.label))"
                          :key="'sup-' + rec.label"
                          size="small"
                          class="avail-chip avail-chip--supplier cursor-pointer"
                          prepend-icon="mdi-account-outline"
                          :title="`Known supplier · ${rec.label}`"
                          @click.stop="applyAvailability(item, rec)"
                        >{{ rec.label }}</v-chip>
                      </div>
                    </template>
                  </div>
                  <div v-else-if="getSuggestions(item.rfqItemId).loading" class="mb-3">
                    <v-progress-linear indeterminate height="2" color="primary" />
                  </div>

                  <div v-if="getEditableQuotes(item.rfqItemId).length > 0" style="overflow-x: auto;">
                    <table class="quote-grid" style="width: 100%; border-collapse: collapse; min-width: 1100px;">
                      <thead>
                        <tr>
                          <th>Supplier</th>
                          <th>Cond</th>
                          <th>Alt P/N</th>
                          <th>Qty</th>
                          <th>Unit</th>
                          <th>Cost Price ($)</th>
                          <th>Cert Type</th>
                          <th>Tag Date</th>
                          <th>Shipping Cost</th>
                          <th>Shipping Point</th>
                          <th>LeadTime</th>
                          <th>Note</th>
                          <th>My Notes</th>
                          <th style="width: 40px" class="text-center">Cert</th>
                          <th style="width: 90px;"></th>
                        </tr>
                      </thead>
                      <tbody>
                        <template v-for="(quote, qIdx) in getEditableQuotes(item.rfqItemId)" :key="qIdx">
                        <tr class="quote-row">
                          <td>
                            <input
                              type="text"
                              class="quote-input"
                              placeholder="Supplier name..."
                              v-model="quote.supplierName"
                              @input="searchSupplier(quote.supplierName)"
                              list="supplier-suggestions"
                            />
                            <div
                              v-if="quote.supplierStatus === 'Pending'"
                              style="font-size:10px; color:#f59e0b; padding: 1px 4px; line-height:1.4; pointer-events:none;"
                            >
                              ⏳ Pending approval
                            </div>
                            <div
                              v-else-if="quote.supplierStatus === 'Rejected'"
                              style="font-size:10px; color:#ef4444; padding: 1px 4px; line-height:1.4; cursor:pointer; text-decoration:underline;"
                              @click.stop="openResubmit(quote)"
                            >
                              ❌ Rejected — click to correct &amp; resubmit
                            </div>
                          </td>
                          <td>
                            <select class="quote-input quote-select" v-model="quote.condition">
                              <option value="">—</option>
                              <option value="NE">NE</option>
                              <option value="OH">OH</option>
                              <option value="SV">SV</option>
                              <option value="AR">AR</option>
                              <option value="RP">RP</option>
                              <option value="NS">NS</option>
                              <option value="FN">FN</option>
                              <option value="IN">IN</option>
                            </select>
                          </td>
                          <td>
                            <input type="text" class="quote-input" placeholder="Same P/N" v-model="quote.alt" />
                          </td>
                          <td>
                            <input type="number" class="quote-input text-center" v-model.number="quote.qty" min="1" />
                          </td>
                          <td>
                            <select class="quote-input quote-select" v-model="quote.unit">
                              <option value="">—</option>
                              <option value="EA">EA</option>
                              <option value="Meter">METER</option>
                              <option value="Kg">KG</option>
                            </select>
                          </td>
                          <td>
                            <input
                              v-if="focusedField === `price-${qIdx}-${item.rfqItemId}`"
                              :data-focus-key="`price-${qIdx}-${item.rfqItemId}`"
                              type="number"
                              class="quote-input price-input"
                              placeholder="0.00"
                              v-model.number="quote.price"
                              step="0.01"
                              min="0"
                              @blur="focusedField = ''"
                            />
                            <span
                              v-else
                              class="quote-input price-display"
                              tabindex="0"
                              @click.stop="focusField(`price-${qIdx}-${item.rfqItemId}`)"
                              @focus="focusField(`price-${qIdx}-${item.rfqItemId}`)"
                            >
                              {{ quote.price ? '$' + formatPrice(quote.price) : '' }}
                            </span>
                          </td>
                          <td>
                            <select class="quote-input quote-select" v-model="quote.certName">
                              <option value="">—</option>
                              <option value="FAA">FAA</option>
                              <option value="EASA">EASA</option>
                              <option value="CAAC">CAAC</option>
                              <option value="Dual">Dual</option>
                              <option value="MFG COC">MFG COC</option>
                              <option value="Vendor COC">Vendor COC</option>
                              <option value="No Cert">No Cert</option>
                            </select>
                          </td>
                          <td>
                            <input type="date" class="quote-input" v-model="quote.tagDate" :max="today" />
                          </td>
                          <td>
                            <input
                              v-if="focusedField === `ship-${qIdx}-${item.rfqItemId}`"
                              :data-focus-key="`ship-${qIdx}-${item.rfqItemId}`"
                              type="number"
                              class="quote-input price-input"
                              placeholder="0.00"
                              v-model.number="quote.shippingCost"
                              step="0.01"
                              min="0"
                              @blur="focusedField = ''"
                            />
                            <span
                              v-else
                              class="quote-input price-display"
                              tabindex="0"
                              @click.stop="focusField(`ship-${qIdx}-${item.rfqItemId}`)"
                              @focus="focusField(`ship-${qIdx}-${item.rfqItemId}`)"
                            >
                              {{ quote.shippingCost ? '$' + formatPrice(quote.shippingCost) : '' }}
                            </span>
                          </td>
                          <td>
                            <input type="text" class="quote-input" placeholder="City / Hub" v-model="quote.shippingPoint" />
                          </td>
                          <td>
                            <input type="text" class="quote-input" placeholder="e.g. 5 days" v-model="quote.leadTime" />
                          </td>
                          <td>
                            <VTextarea
                              type="text"
                              rows="2"
                              placeholder="Note..."
                              v-model="quote.note"
                              hide-details
                              density="compact"
                              variant="plain"
                            />
                          </td>
                          <td>
                            <VTextarea
                              type="text"
                              rows="2"
                              placeholder="My Notes..."
                              v-model="quote.myNotes"
                              hide-details
                              density="compact"
                              variant="plain"
                            />
                          </td>
                          <td class="text-center">
                            <v-checkbox
                              v-if="quote.supplierDependency === 'Certificated'"
                              v-model="quote.isCertificated"
                              density="compact"
                              hide-details
                              class="ma-0 pa-0 d-inline-block"
                              @change="saveQuote(item, quote)"
                            />
                          </td>
                          <td class="text-center" style="white-space: nowrap;">
                            <v-btn
                              v-if="quote.condition === 'AR'"
                              icon="mdi-wrench"
                              size="x-small"
                              variant="text"
                              :color="isShopExpanded(quote.id, item.rfqItemId) ? 'warning' : 'grey'"
                              @click.stop="toggleShop(quote.id, item.rfqItemId)"
                              :title="'Shops (' + (quote.shopRecords || []).length + ')'"
                            />
                            <v-btn
                              icon="mdi-content-save"
                              size="x-small"
                              variant="text"
                              color="success"
                              :loading="quote._saving"
                              @click.stop="saveQuote(item, quote)"
                              title="Save"
                            />
                            <v-btn
                              icon="mdi-close"
                              size="x-small"
                              variant="text"
                              color="error"
                              @click.stop="confirmRemoveQuote(item, qIdx)"
                              title="Delete"
                            />
                          </td>
                        </tr>
                        <!-- Shop Records sub-table (collapsible, for AR condition) -->
                        <tr v-if="quote.condition === 'AR' && isShopExpanded(quote.id, item.rfqItemId)">
                          <td :colspan="14" class="pa-0">
                            <div class="shop-panel">
                              <div class="d-flex align-center justify-space-between mb-2">
                                <span class="text-caption text-uppercase font-weight-bold" style="color: #ff9800;">
                                  <v-icon icon="mdi-wrench" size="14" class="mr-1" />
                                  Shop Records ({{ (quote.shopRecords || []).length }})
                                </span>
                                <div class="d-flex align-center gap-2">
                                  <v-btn
                                    v-for="otherQ in getOtherARQuotesWithShops(item, quote)"
                                    :key="otherQ.id"
                                    size="x-small"
                                    color="info"
                                    variant="tonal"
                                    prepend-icon="mdi-content-copy"
                                    @click.stop="copyShopsFrom(item, quote, otherQ)"
                                    :title="`Copy all shops from ${otherQ.supplierName}`"
                                  >
                                    Copy from {{ otherQ.supplierName }}
                                  </v-btn>
                                  <v-btn
                                    size="x-small"
                                    color="warning"
                                    variant="flat"
                                    prepend-icon="mdi-plus"
                                    @click.stop="addShopRow(item, quote)"
                                  >
                                    Add Shop
                                  </v-btn>
                                </div>
                              </div>
                              <table class="quote-grid" v-if="(quote.shopRecords || []).length > 0" style="width: 100%; border-collapse: collapse;">
                                <thead>
                                  <tr>
                                    <th>Supplier</th>
                                    <th>Alt P/N</th>
                                    <th>Condition</th>
                                    <th>Qty</th>
                                    <th>Unit</th>
                                    <th>Cost Price ($)</th>
                                    <th style="color: #ff9800;">Repair Cost ($)</th>
                                    <th>Cert Type</th>
                                    <th>Tag Date</th>
                                    <th>Shipping Cost</th>
                                    <th>Shipping Point</th>
                                    <th>LeadTime</th>
                                    <th>Note</th>
                                    <th>My Notes</th>
                                    <th style="width: 40px" class="text-center">Cert</th>
                                    <th style="width: 70px;"></th>
                                  </tr>
                                </thead>
                                <tbody>
                                  <tr v-for="(shop, sIdx) in quote.shopRecords" :key="'shop-' + sIdx" class="shop-row">
                                    <td>
                                      <input
                                        type="text"
                                        class="quote-input"
                                        placeholder="Shop name..."
                                        v-model="shop.supplierName"
                                        @input="searchSupplier(shop.supplierName)"
                                        list="supplier-suggestions"
                                      />
                                      <div
                                        v-if="shop.supplierStatus === 'Pending'"
                                        style="font-size:10px; color:#f59e0b; padding: 1px 4px; line-height:1.4; pointer-events:none;"
                                      >
                                        ⏳ Pending approval
                                      </div>
                                      <div
                                        v-else-if="shop.supplierStatus === 'Rejected'"
                                        style="font-size:10px; color:#ef4444; padding: 1px 4px; line-height:1.4; cursor:pointer; text-decoration:underline;"
                                        @click.stop="openResubmit(shop)"
                                      >
                                        ❌ Rejected — click to correct &amp; resubmit
                                      </div>
                                    </td>
                                    <td>
                                      <input type="text" class="quote-input" placeholder="Same P/N" v-model="shop.alt" />
                                    </td>
                                    <td>
                                      <select class="quote-input quote-select" v-model="shop.condition">
                                        <option value="">—</option>
                                        <option value="IN">IN</option>
                                        <option value="RP">RP</option>
                                        <option value="OH">OH</option>
                                      </select>
                                    </td>
                                    <td>
                                      <input type="number" class="quote-input text-center" v-model.number="shop.qty" min="1" />
                                    </td>
                                    <td>
                                      <select class="quote-input quote-select" v-model="shop.unit">
                                        <option value="">—</option>
                                        <option value="EA">EA</option>
                                        <option value="Meter">METER</option>
                                        <option value="Kg">KG</option>
                                      </select>
                                    </td>
                                    <td>
                                      <input
                                        v-if="focusedField === `shop-price-${sIdx}-${quote.id}`"
                                        :data-focus-key="`shop-price-${sIdx}-${quote.id}`"
                                        type="number"
                                        class="quote-input price-input"
                                        placeholder="0.00"
                                        v-model.number="shop.price"
                                        step="0.01"
                                        min="0"
                                        @blur="focusedField = ''"
                                      />
                                      <span
                                        v-else
                                        class="quote-input price-display"
                                        tabindex="0"
                                        @click.stop="focusField(`shop-price-${sIdx}-${quote.id}`)"
                                        @focus="focusField(`shop-price-${sIdx}-${quote.id}`)"
                                      >
                                        {{ shop.price ? '$' + formatPrice(shop.price) : '' }}
                                      </span>
                                    </td>
                                    <td>
                                      <input
                                        v-if="focusedField === `shop-fix-${sIdx}-${quote.id}`"
                                        :data-focus-key="`shop-fix-${sIdx}-${quote.id}`"
                                        type="number"
                                        class="quote-input price-input"
                                        style="color: #ff9800;"
                                        placeholder="0.00"
                                        v-model.number="shop.fixPrice"
                                        step="0.01"
                                        min="0"
                                        @blur="focusedField = ''"
                                      />
                                      <span
                                        v-else
                                        class="quote-input price-display"
                                        style="color: #ff9800;"
                                        tabindex="0"
                                        @click.stop="focusField(`shop-fix-${sIdx}-${quote.id}`)"
                                        @focus="focusField(`shop-fix-${sIdx}-${quote.id}`)"
                                      >
                                        {{ shop.fixPrice ? '$' + formatPrice(shop.fixPrice) : '' }}
                                      </span>
                                    </td>
                                    <td>
                                      <select class="quote-input quote-select" v-model="shop.certName">
                                        <option value="">—</option>
                                        <option value="FAA">FAA</option>
                                        <option value="EASA">EASA</option>
                                        <option value="CAAC">CAAC</option>
                                        <option value="Dual">Dual</option>
                                        <option value="MFG COC">MFG COC</option>
                                        <option value="Vendor COC">Vendor COC</option>
                                        <option value="No Cert">No Cert</option>
                                      </select>
                                    </td>
                                    <td>
                                      <input type="date" class="quote-input" v-model="shop.tagDate" :max="today" />
                                    </td>
                                    <td>
                                      <input
                                        v-if="focusedField === `shop-ship-${sIdx}-${quote.id}`"
                                        :data-focus-key="`shop-ship-${sIdx}-${quote.id}`"
                                        type="number"
                                        class="quote-input price-input"
                                        placeholder="0.00"
                                        v-model.number="shop.shippingCost"
                                        step="0.01"
                                        min="0"
                                        @blur="focusedField = ''"
                                      />
                                      <span
                                        v-else
                                        class="quote-input price-display"
                                        tabindex="0"
                                        @click.stop="focusField(`shop-ship-${sIdx}-${quote.id}`)"
                                        @focus="focusField(`shop-ship-${sIdx}-${quote.id}`)"
                                      >
                                        {{ shop.shippingCost ? '$' + formatPrice(shop.shippingCost) : '' }}
                                      </span>
                                    </td>
                                    <td>
                                      <input type="text" class="quote-input" placeholder="City / Hub" v-model="shop.shippingPoint" />
                                    </td>
                                    <td>
                                      <input type="text" class="quote-input" placeholder="e.g. 5 days" v-model="shop.leadTime" />
                                    </td>
                                    <td>
                                      <VTextarea rows="2" placeholder="Note..." v-model="shop.note" hide-details density="compact" variant="plain" />
                                    </td>
                                    <td>
                                      <VTextarea rows="2" placeholder="My Notes..." v-model="shop.myNotes" hide-details density="compact" variant="plain" />
                                    </td>
                                    <td class="text-center" style="white-space: nowrap;">
                                      <v-btn
                                        icon="mdi-content-save"
                                        size="x-small"
                                        variant="text"
                                        color="success"
                                        :loading="shop._saving"
                                        @click.stop="saveShopQuote(item, quote, shop)"
                                        title="Save Shop"
                                      />
                                      <v-btn
                                        icon="mdi-close"
                                        size="x-small"
                                        variant="text"
                                        color="error"
                                        @click.stop="confirmRemoveShop(item, quote, sIdx)"
                                        title="Delete"
                                      />
                                    </td>
                                  </tr>
                                </tbody>
                              </table>
                              <div v-else class="text-center pa-3">
                                <p class="text-caption text-medium-emphasis">No shop records. Click "Add Shop" to start.</p>
                              </div>
                            </div>
                          </td>
                        </tr>
                        </template>
                      </tbody>
                    </table>
                  </div>

                  <div v-else class="text-center pa-4">
                    <v-icon icon="mdi-package-variant" size="32" color="grey-darken-1" class="mb-2" />
                    <p class="text-caption text-medium-emphasis">No supplier quotes yet. Click "Add Supplier" to start.</p>
                  </div>
                </div>
              </td>
            </tr>
          </template>
        </v-data-table-server>
      </v-card-text>
    </v-card>

    <!-- Shared datalist for supplier name autocomplete -->
    <datalist id="supplier-suggestions">
      <option v-for="s in supplierSuggestions" :key="s.id" :value="s.name">
        {{ s.status === 'Pending' ? '(Pending Approval)' : '' }}
      </option>
    </datalist>

    <!-- Resubmit Supplier Dialog -->
    <v-dialog v-model="resubmitDialog" max-width="440">
      <v-card class="glass-card">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-account-sync-outline" color="error" class="mr-2" />
          Correct Supplier Name
        </v-card-title>
        <v-card-text>
          <v-alert type="error" variant="tonal" density="compact" class="mb-4">
            This supplier was rejected. Enter the correct supplier name and resubmit for admin approval.
          </v-alert>
          <v-text-field
            v-model="resubmitName"
            label="Correct Supplier Name"
            variant="outlined"
            density="compact"
            autofocus
          />
        </v-card-text>
        <v-card-actions class="justify-end pa-4">
          <v-btn variant="text" @click="resubmitDialog = false">Cancel</v-btn>
          <v-btn color="primary" variant="tonal" :loading="resubmitSaving" :disabled="!resubmitName.trim()" @click="doResubmit">
            Resubmit
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="2000" location="bottom right">
      {{ snackbarText }}
    </v-snackbar>

    <ConfirmDialog
      v-model="showConfirmQuote"
      title="Delete Supplier Quote?"
      message="Are you sure you want to remove this quote?"
      @confirm="doRemoveQuote"
    />

    <ConfirmDialog
      v-model="showConfirmShop"
      title="Delete Shop Record?"
      message="Are you sure you want to remove this shop record?"
      @confirm="doRemoveShop"
    />
  </div>
</template>

<script setup lang="ts">
const api = useApi()
const authStore = useAuthStore()
const { statusColor: rfqStatusColor } = useStatusColor()

const pageOptions = [
  { value: 50, title: '50' },
  { value: 75, title: '75' },
  { value: 100, title: '100' },
  { value: 150, title: '150' },
  { value: 200, title: '200' },
  { value: 300, title: '300' },
  { value: 500, title: '500' },
  { value: 1000, title: '1000' },
  { value: -1, title: 'All' },
]

const today = new Date().toISOString().split('T')[0]

const { filters: pf, clearFilters, hasActiveFilters } = usePageFilters('procument', {
  search: '',
  status: [] as string[],
  user: [] as number[],
  customer: [] as string[],
  pnSearch: '',
  // Column filter selections — persisted across refreshes via localStorage
  colPn:   [] as string[],   // part number exact matches
  colCond: [] as string[],   // condition exact matches
  colRfqId: [] as string[],
  colRfqName: [] as string[],
  page: 1,
  itemsPerPage: 50,
})
const search = pf.search
const currentPage = pf.page           // top-level ref so Vue auto-unwraps in template
const currentItemsPerPage = pf.itemsPerPage
const loading = ref(false)
const allItems = ref<any[]>([])
const totalItems = ref(0)
const sort = useServerSort()

// ── Excel-style column filters (server-side, persisted) ──
// colPn / colCond live in usePageFilters → auto-persisted to localStorage.
// Customer, Status, User col filters reuse the toolbar filter refs directly
// so both controls stay in sync and share persistence for free.
const colPn   = pf.colPn    // Ref<string[]> — part number exact matches
const colCond = pf.colCond  // Ref<string[]> — condition exact matches
const colRfqId = pf.colRfqId
const colRfqName = pf.colRfqName

// Per-column in-dropdown search text (not persisted — ephemeral UI state)
const colSearch = reactive<Record<string, string>>({
  pn: '', cond: '', cust: '', status: '', users: '', rfqId: '', rfqName: ''
})

// ── Option sources ──
// Conditions: finite hardcoded set
const ALL_CONDITIONS = ['AR', 'FN', 'IN', 'NE', 'NS', 'OH', 'RP', 'SV']

// Part numbers: accumulates across ALL fetches so the list never shrinks when a filter is active.
// colPnOpts = current page results (the "available" subset)
// allPnOptions = every part number ever seen (grows, never shrinks)
const allPnOptions = ref<string[]>([])
const showAllPnOpts = ref(false)

const colPnOpts = computed(() =>
  [...new Set(allItems.value.map((r: any) => r.partNumberName).filter(Boolean))].sort() as string[]
)

// Customers: from filter-options (full DB, cascades with toolbar filters)
// allCustomerOptions is already populated by loadFilterOptions()
// format: { title: customerCode, value: customerName }

// Users: from filter-options (full DB)
// allUserOptions is already populated — format: { id, name }

// ── Per-column show-all toggles (false = available only, true = show all DB values) ──
const showAllCondOpts = ref(false)
const showAllStatusColOpts = ref(false)
const showAllCustColOpts = ref(false)
const showAllUserColOpts = ref(false)

// "Available" sets — values present in the current 50-row page
const condOptsPage = computed(() =>
  [...new Set(allItems.value.map((r: any) => r.condition).filter(Boolean))].sort() as string[]
)
const statusColOptsPage = computed(() =>
  [...new Set(allItems.value.map((r: any) => (r.rfqStatus || r.status)).filter(Boolean))].sort() as string[]
)
const custColOptsPage = computed(() => {
  const names = new Set(allItems.value.map((r: any) => r.customerName).filter(Boolean))
  return allCustomerOptions.value.filter(o => names.has(o.value))
})
const userColOptsPage = computed(() => {
  const ids = new Set<number>()
  for (const item of allItems.value) {
    for (const u of (item.assignedUsers || [])) ids.add(u.id)
  }
  return allUserOptions.value.filter(u => ids.has(u.id))
})

// Filtered option lists (applying in-dropdown search + show-all toggle)
const filteredColPnOpts = computed(() => applySearch(showAllPnOpts.value ? allPnOptions.value : colPnOpts.value, colSearch.pn))
const filteredColCondOpts = computed(() =>
  applySearch(showAllCondOpts.value ? ALL_CONDITIONS : condOptsPage.value, colSearch.cond)
)
const filteredColCustOpts = computed(() => {
  const source = showAllCustColOpts.value ? allCustomerOptions.value : custColOptsPage.value
  const s = colSearch.cust.toLowerCase()
  return s ? source.filter(o => o.title.toLowerCase().includes(s) || o.value.toLowerCase().includes(s)) : source
})
const filteredColStatusOpts = computed(() =>
  applySearch(showAllStatusColOpts.value ? ALL_PROC_STATUSES : statusColOptsPage.value, colSearch.status)
)
const filteredColUserOpts = computed(() => {
  const source = showAllUserColOpts.value ? allUserOptions.value : userColOptsPage.value
  const s = colSearch.users.toLowerCase()
  return s ? source.filter(u => u.name.toLowerCase().includes(s)) : source
})

const colRfqIdOpts = computed(() =>
  [...new Set(allItems.value.map((r: any) => String(r.rfqId)).filter(Boolean))].sort((a, b) => Number(b) - Number(a)) as string[]
)
const colRfqNameOpts = computed(() =>
  [...new Set(allItems.value.map((r: any) => r.rfqName).filter(Boolean))].sort() as string[]
)
const filteredColRfqIdOpts = computed(() => applySearch(colRfqIdOpts.value, colSearch.rfqId))
const filteredColRfqNameOpts = computed(() => applySearch(colRfqNameOpts.value, colSearch.rfqName))

function applySearch(opts: string[], s: string): string[] {
  const q = s.toLowerCase()
  return q ? opts.filter(v => v.toLowerCase().includes(q)) : opts
}

// ── Toggle helpers ──
/** Toggle a value in a reactive string[] (template-safe, passes array by ref). */
function toggleCol(arr: string[], val: string) {
  const idx = arr.indexOf(val)
  if (idx >= 0) arr.splice(idx, 1)
  else arr.push(val)
}
/** Toggle a numeric ID in a reactive number[]. */
function toggleColId(arr: number[], id: number) {
  const idx = arr.indexOf(id)
  if (idx >= 0) arr.splice(idx, 1)
  else arr.push(id)
}
/** Select all provided values into a string[]. */
function selectAllCol(arr: string[], opts: string[]) {
  arr.splice(0, arr.length, ...opts)
}
/** Select all user IDs from a filtered user list. */
function selectAllUsers(arr: number[], opts: { id: number; name: string }[]) {
  arr.splice(0, arr.length, ...opts.map(u => u.id))
}
/** Select all customer names from a filtered customer option list. */
function selectAllCust(arr: string[], opts: { title: string; value: string }[]) {
  arr.splice(0, arr.length, ...opts.map(o => o.value))
}

// ── Active count & clear ──
const procActiveFilterCount = computed(() => [
  colPn.value?.length,
  colCond.value?.length,
  colRfqId.value?.length,
  colRfqName.value?.length,
  // Customer, Status, User share toolbar refs — counted by hasActiveFilters already
].filter(Boolean).length)

function clearAllProcFilters() {
  colPn.value.splice(0)
  colCond.value.splice(0)
  colRfqId.value.splice(0)
  colRfqName.value.splice(0)
  // Also clear toolbar filters that double as col filters:
  ;(statusFilter.value as string[]).splice(0)
  ;(customerFilter.value as string[]).splice(0)
  ;(userFilter.value as number[]).splice(0)
}
const editableQuotes = ref<Record<number, any[]>>({})
const expandedArray = ref<any[]>([])
const focusedField = ref('')
const supplierSuggestions = ref<{ id: number; name: string; status: string }[]>([])
const itemSuggestions = ref<Record<number, { knownSuppliers: any[]; recentQuotes: any[]; loading: boolean }>>({})
const partAvailability = ref<Record<number, any>>({})

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')
const showPendingOnly = ref(false)
const showNoQuote = ref(false)

// Delete Confirmations
const showConfirmQuote = ref(false)
const showConfirmShop = ref(false)
const deleteTargetQuote = ref<{ item: any; idx: number } | null>(null)
const deleteTargetShop = ref<{ item: any; parentQuote: any; idx: number } | null>(null)

function showSnack(text: string, color = 'success') {
  snackbarText.value = text
  snackbarColor.value = color
  snackbar.value = true
}

// ──── Supplier Resubmit ────

const resubmitDialog = ref(false)
const resubmitSaving = ref(false)
const resubmitName = ref('')
const resubmitTarget = ref<any>(null)

const allEditableQuotesList = computed(() => {
  const all: any[] = []
  for (const quotes of Object.values(editableQuotes.value)) {
    for (const q of quotes) {
      all.push(q)
      for (const shop of (q.shopRecords || [])) all.push(shop)
    }
  }
  return all
})

const pendingSupplierQuotes = computed(() =>
  allEditableQuotesList.value.filter(q => q.supplierStatus === 'Pending')
)
const rejectedSupplierQuotes = computed(() =>
  allEditableQuotesList.value.filter(q => q.supplierStatus === 'Rejected')
)

function openResubmit(quote: any) {
  resubmitTarget.value = quote
  resubmitName.value = quote.supplierName || ''
  resubmitDialog.value = true
}

async function doResubmit() {
  if (!resubmitTarget.value?.supplierId || !resubmitName.value.trim()) return
  resubmitSaving.value = true
  try {
    await api.post(`/suppliers/${resubmitTarget.value.supplierId}/resubmit`, { name: resubmitName.value.trim() })
    showSnack('Resubmitted for approval', 'success')
    resubmitDialog.value = false
    await loadData()
  } catch (e: any) {
    showSnack(e?.data?.message || 'Failed to resubmit', 'error')
  } finally {
    resubmitSaving.value = false
  }
}

// ── Filters ──
const statusFilter = pf.status
const userFilter = pf.user
const customerFilter = pf.customer
const pnSearch = pf.pnSearch
const isAdmin = computed(() => authStore.isAdmin)

const ALL_PROC_STATUSES = ['Open', 'In Progress', 'Waiting For Admin', 'Ready To Quote', 'Sent', 'Accepted', 'Rejected', 'No Quote']
const statusOptions = ref<string[]>([...ALL_PROC_STATUSES])
const showAllStatuses = ref(false)
const statusSelectItems = computed(() => {
  const available = new Set(statusOptions.value)
  if (showAllStatuses.value) {
    return ALL_PROC_STATUSES.map(s => ({ label: s, value: s, available: available.has(s) }))
  }
  return ALL_PROC_STATUSES
    .filter(s => available.has(s) || (statusFilter.value as string[]).includes(s))
    .map(s => ({ label: s, value: s, available: true }))
})

const headers = [
  { title: 'RFQ #', key: 'rfqId', width: '80px' },
  { title: 'RFQ Name', key: 'rfqName' },
  { title: 'Part Number', key: 'partNumberName' },
  { title: 'Description', key: 'description' },
  { title: 'Qty', key: 'qty', width: '70px' },
  { title: 'Condition', key: 'condition', width: '90px' },
  { title: 'Customer', key: 'customerName' },
  { title: 'Status', key: 'status', width: '110px' },
  { title: 'Suppliers', key: 'supplierCount', sortable: false, width: '120px' },
  { title: 'Assigned Users', key: 'assignedUsers', sortable: false },
  { title: 'Deadline', key: 'leadTime' },
  { title: 'Received Date', key: 'createdAt' },
]

const userOptions = ref<{ id: number; name: string }[]>([])
const customerOptions = ref<{ title: string; value: string }[]>([])
const allUserOptions = ref<{ id: number; name: string }[]>([])
const allCustomerOptions = ref<{ title: string; value: string }[]>([])
const showAllUsers = ref(false)
const showAllCustomers = ref(false)

const userSelectItems = computed(() => showAllUsers.value ? allUserOptions.value : userOptions.value)
const customerSelectItems = computed(() => showAllCustomers.value ? allCustomerOptions.value : customerOptions.value)

// ── Filter-options loading (cascading) ──
let filterOptsDebounce: any = null

async function loadFilterOptions() {
  try {
    const params = new URLSearchParams()
    if (statusFilter.value?.length) (statusFilter.value as string[]).forEach(s => params.append('statuses', s))
    if (userFilter.value?.length) (userFilter.value as number[]).forEach(id => params.append('userIds', String(id)))
    if (customerFilter.value?.length) (customerFilter.value as string[]).forEach(c => params.append('customerSearch', c))
    const res = await api.get<any>(`/procument-page/filter-options?${params}`)

    statusOptions.value = res.statuses?.length
      ? [...new Set([...ALL_PROC_STATUSES, ...res.statuses])]
      : [...ALL_PROC_STATUSES]

    userOptions.value = (res.users || [])
      .map((u: any) => ({ id: u.id, name: u.name }))
      .sort((a: any, b: any) => a.name.localeCompare(b.name))

    customerOptions.value = (res.customers || [])
      .map((c: any) => ({ title: c.code || '-', value: c.name }))
      .sort((a: any, b: any) => a.title.localeCompare(b.title))

  } catch {}
}

/** One-time unconstrained fetch to populate the full "all options" lists for column filters. */
async function loadAllFilterOptions() {
  try {
    const res = await api.get<any>('/procument-page/filter-options')
    allUserOptions.value = (res.users || [])
      .map((u: any) => ({ id: u.id, name: u.name }))
      .sort((a: any, b: any) => a.name.localeCompare(b.name))
    allCustomerOptions.value = (res.customers || [])
      .map((c: any) => ({ title: c.code || '-', value: c.name }))
      .sort((a: any, b: any) => a.title.localeCompare(b.title))
  } catch {}
}

function debouncedFilterOptions() {
  clearTimeout(filterOptsDebounce)
  filterOptsDebounce = setTimeout(loadFilterOptions, 200)
}

watch(statusFilter, debouncedFilterOptions, { deep: true })
watch(userFilter, debouncedFilterOptions, { deep: true })
watch(customerFilter, debouncedFilterOptions, { deep: true })

function isLeadTimeUrgent(dateStr: string) {
  if (!dateStr) return false
  const diff = new Date(dateStr).getTime() - Date.now()
  const daysLeft = diff / (1000 * 60 * 60 * 24)
  return daysLeft >= 0 && daysLeft <= 3
}

function isLeadTimeExpired(dateStr: string) {
  if (!dateStr) return false
  return new Date(dateStr).getTime() < Date.now()
}

function getRemainingDays(dateStr: string): number {
  const today = new Date(); today.setHours(0, 0, 0, 0)
  const d = new Date(dateStr); d.setHours(0, 0, 0, 0)
  return Math.round((d.getTime() - today.getTime()) / 86400000)
}

function remainingDaysLabel(dateStr: string): string {
  const days = getRemainingDays(dateStr)
  if (days === 0) return 'Today'
  if (days < 0) return `${Math.abs(days)}d overdue`
  if (days === 1) return '1 day left'
  return `${days}d left`
}

function remainingDaysColor(dateStr: string): string {
  const days = getRemainingDays(dateStr)
  if (days < 0) return 'error'
  if (days <= 3) return 'warning'
  if (days <= 7) return 'orange'
  return 'info'
}

function getRowProps({ item }: { item: any }) {
  const classes: string[] = []
  if (['Open', 'In Progress', 'Waiting For Admin'].includes(item.rfqStatus) && isLeadTimeUrgent(item.leadTime)) classes.push('lead-time-urgent-row')
  if (['Open', 'In Progress', 'Waiting For Admin'].includes(item.rfqStatus) && isLeadTimeExpired(item.leadTime)) classes.push('lead-time-expired-row')
  if (item.isHighlighted) classes.push('highlighted-row')
  if (expandedArray.value.includes(item.rfqItemId)) classes.push('expanded-row')
  return classes.length ? { class: classes.join(' ') } : {}
}

// ── Data Loading ──
const lastProcumentOpts = ref<any>({ page: pf.page.value, itemsPerPage: pf.itemsPerPage.value })

async function loadServerPage(opts?: any) {
  if (opts) {
    sort.capture(opts)
    lastProcumentOpts.value = { page: opts.page ?? lastProcumentOpts.value.page, itemsPerPage: opts.itemsPerPage ?? lastProcumentOpts.value.itemsPerPage }
    pf.page.value = lastProcumentOpts.value.page
    currentItemsPerPage.value = lastProcumentOpts.value.itemsPerPage
  }
  const { page, itemsPerPage } = lastProcumentOpts.value
  loading.value = true
  try {
    const params = new URLSearchParams({ page: String(page), pageSize: String(itemsPerPage) })
    sort.appendTo(params)
    if (search.value?.trim()) params.set('search', search.value.trim())
    if (pnSearch.value?.trim()) params.set('pnSearch', pnSearch.value.trim())
    if (statusFilter.value?.length) (statusFilter.value as string[]).forEach((s: string) => params.append('status', s))
    if (userFilter.value?.length) (userFilter.value as number[]).forEach((id: number) => params.append('userIds', String(id)))
    if (customerFilter.value?.length) (customerFilter.value as string[]).forEach((c: string) => params.append('customerSearch', c))
    if (colPn.value?.length) (colPn.value as string[]).forEach((v: string) => params.append('colPartNames', v))
    if (colCond.value?.length) (colCond.value as string[]).forEach((v: string) => params.append('conditions', v))
    if (colRfqId.value?.length) (colRfqId.value as string[]).forEach((v: string) => params.append('rfqIds', v))
    if (colRfqName.value?.length) (colRfqName.value as string[]).forEach((v: string) => params.append('rfqNames', v))
    if (showPendingOnly.value) params.set('pendingOnly', 'true')
    if (showNoQuote.value) params.set('includeNoQuote', 'true')
    const res = await api.get<any>(`/procument-page?${params.toString()}`)
    const batch: any[] = Array.isArray(res) ? res : (res.items ?? res.Items ?? [])
    totalItems.value = (!Array.isArray(res) && res != null) ? (res.totalCount ?? res.TotalCount ?? batch.length) : batch.length
    allItems.value = batch.map((item: any) => ({
      ...item,
      altPartNumbers: [
        ...(item.alternatives || []).map((a: any) => a.name),
        ...(item.supplierQuotes || []).map((q: any) => q.alt),
      ].filter(Boolean).join(', '),
    }))

    // Accumulate part numbers — list grows but never shrinks, so the column filter
    // dropdown always shows all previously seen values even when a filter is active.
    const newPns = batch.map((item: any) => item.partNumberName).filter(Boolean) as string[]
    if (newPns.length) {
      allPnOptions.value = [...new Set([...allPnOptions.value, ...newPns])].sort()
    }

    // Initialize editable quotes keyed by rfqItemId — order doesn't matter
    const quotesMap: Record<number, any[]> = {}
    for (const item of allItems.value) {
      quotesMap[item.rfqItemId] = (item.supplierQuotes || []).map((q: any) => ({
        ...q,
        _saving: false,
        shopRecords: (q.shopRecords || []).map((s: any) => ({ ...s, _saving: false })),
      }))
    }
    editableQuotes.value = quotesMap

    // Load part availability in background
    const allPartIds = [...new Set(allItems.value.map((i: any) => i.partNumberId).filter(Boolean))]
    if (allPartIds.length > 0) {
      try {
        const avail = await api.post<any[]>('/availability/parts', { partNumberIds: allPartIds })
        const map: Record<number, any> = {}
        for (const a of avail) map[a.partNumberId] = a
        partAvailability.value = map
      } catch {}
    }
  } catch (e) {
    console.error('Failed to load procument data:', e)
  } finally {
    loading.value = false
  }
}

async function loadData() {
  await loadServerPage()
}

let procumentDebounce: any = null
function debouncedProcumentLoad() {
  clearTimeout(procumentDebounce)
  procumentDebounce = setTimeout(() => loadServerPage({ ...lastProcumentOpts.value, page: 1 }), 350)
}
watch(search, debouncedProcumentLoad)
watch(pnSearch, debouncedProcumentLoad)
watch(statusFilter, () => loadServerPage({ ...lastProcumentOpts.value, page: 1 }), { deep: true })
watch(userFilter, () => loadServerPage({ ...lastProcumentOpts.value, page: 1 }), { deep: true })
watch(customerFilter, () => loadServerPage({ ...lastProcumentOpts.value, page: 1 }), { deep: true })
watch(colPn, () => loadServerPage({ ...lastProcumentOpts.value, page: 1 }), { deep: true })
watch(colCond, () => loadServerPage({ ...lastProcumentOpts.value, page: 1 }), { deep: true })
watch(colRfqId, () => loadServerPage({ ...lastProcumentOpts.value, page: 1 }), { deep: true })
watch(colRfqName, () => loadServerPage({ ...lastProcumentOpts.value, page: 1 }), { deep: true })
watch(showPendingOnly, () => loadServerPage({ ...lastProcumentOpts.value, page: 1 }))
watch(showNoQuote, () => loadServerPage({ ...lastProcumentOpts.value, page: 1 }))

onMounted(() => {
  loadData()
  loadAllFilterOptions()   // always fetch full DB options for col filter dropdowns
  loadFilterOptions()      // cascading options for toolbar selects
})

// ── Expand / Collapse ──
function toggleExpand(item: any) {
  const id = item.rfqItemId
  const idx = expandedArray.value.indexOf(id)
  if (idx >= 0) {
    expandedArray.value.splice(idx, 1)
  } else {
    expandedArray.value.push(id)
    loadSuggestions(item)
  }
}

async function loadSuggestions(item: any) {
  const key = item.rfqItemId
  if (itemSuggestions.value[key] && !itemSuggestions.value[key].loading) return // already loaded
  itemSuggestions.value[key] = { knownSuppliers: [], recentQuotes: [], loading: true }
  try {
    const data = await api.get<any>(`/procument-page/suggestions?partNumberId=${item.partNumberId}&rfqId=${item.rfqId}`)
    itemSuggestions.value[key] = {
      knownSuppliers: data.knownSuppliers || [],
      recentQuotes: data.recentQuotes || [],
      loading: false,
    }
  } catch {
    itemSuggestions.value[key] = { knownSuppliers: [], recentQuotes: [], loading: false }
  }
}

function getSuggestions(rfqItemId: number) {
  return itemSuggestions.value[rfqItemId] || { knownSuppliers: [], recentQuotes: [], loading: false }
}

function applySuggestion(item: any, suggestion: any) {
  const key = item.rfqItemId
  if (!editableQuotes.value[key]) {
    editableQuotes.value[key] = []
  }
  // Check if this supplier is already added
  const alreadyExists = editableQuotes.value[key].some(
    (q: any) => q.supplierName?.toLowerCase() === suggestion.supplierName?.toLowerCase()
  )
  if (alreadyExists) {
    showSnack(`${suggestion.supplierName} is already added`, 'warning')
    return
  }
  editableQuotes.value[key].unshift({
    id: null,
    rfqItemId: item.rfqItemId,
    supplierName: suggestion.supplierName,
    qty: suggestion.qty || item.qty || 1,
    price: suggestion.priceHidden ? 0 : (suggestion.price || 0),
    condition: suggestion.condition || 'NE',
    alt: suggestion.alt || '',
    certName: suggestion.certName || '',
    tagDate: suggestion.tagDate || '',
    shippingCost: suggestion.shippingCost ?? null,
    shippingPoint: suggestion.shippingPoint || '',
    unit: suggestion.unit || 'EA',
    leadTime: suggestion.leadTime || '',
    note: suggestion.note || '',
    myNotes: suggestion.myNotes || '',
    _saving: false,
  })
  if (!expandedArray.value.includes(item.rfqItemId)) {
    expandedArray.value.push(item.rfqItemId)
  }
  showSnack(`${suggestion.supplierName} added from history`, 'success')
}

function applyAllSuggestions(item: any) {
  const suggestions = getSuggestions(item.rfqItemId)
  let added = 0
  // Reverse to maintain order when using unshift (first suggestion ends up at top)
  for (const s of [...suggestions.recentQuotes].reverse()) {
    const key = item.rfqItemId
    const alreadyExists = (editableQuotes.value[key] || []).some(
      (q: any) => q.supplierName?.toLowerCase() === s.supplierName?.toLowerCase()
    )
    if (!alreadyExists) {
      applySuggestion(item, s)
      added++
    }
  }
  if (added === 0) {
    showSnack('All suggested suppliers are already added', 'info')
  }
}

function applyAvailability(item: any, rec: any, skipPrice = false) {
  const key = item.rfqItemId
  if (!editableQuotes.value[key]) editableQuotes.value[key] = []
  if (!expandedArray.value.includes(key)) expandedArray.value.push(key)
  editableQuotes.value[key].unshift({
    id: null,
    rfqItemId: item.rfqItemId,
    supplierName: rec.label || '',
    qty: rec.qty || item.qty || 1,
    price: skipPrice ? 0 : (rec.price || 0),
    condition: rec.condition || item.condition || 'NE',
    alt: rec.altPartNumber || '',
    certName: rec.certName || '',
    tagDate: rec.tagDate || '',
    shippingCost: null,
    shippingPoint: '',
    unit: 'EA',
    leadTime: rec.leadTime || '',
    note: '',
    myNotes: '',
    _saving: false,
    shopRecords: [],
  })
  showSnack(`${rec.label} added`, 'success')
}

function getEditableQuotes(rfqItemId: number) {
  return editableQuotes.value[rfqItemId] || []
}

// ── Supplier Quote Management ──
function addQuoteRow(item: any) {
  const key = item.rfqItemId
  if (!editableQuotes.value[key]) {
    editableQuotes.value[key] = []
  }
  editableQuotes.value[key]!.unshift({
    id: null,
    rfqItemId: item.rfqItemId,
    supplierName: '',
    qty: item.qty || 1,
    price: 0,
    condition: 'NE',
    alt: '',
    certName: '',
    tagDate: '',
    shippingCost: null,
    shippingPoint: '',
    unit: 'EA',
    leadTime: '',
    note: '',
    myNotes: '',
    _saving: false,
  })
  // Ensure expanded
  if (!expandedArray.value.includes(item.rfqItemId)) {
    expandedArray.value.push(item.rfqItemId)
  }
}

async function saveQuote(item: any, quote: any) {
  if (!quote.supplierName?.trim()) {
    showSnack('Supplier name is required', 'error')
    return
  }

  quote._saving = true
  try {
    const payload = {
      id: quote.id || null,
      rfqItemId: item.rfqItemId,
      supplierName: quote.supplierName,
      qty: quote.qty,
      price: quote.price,
      condition: quote.condition,
      alt: quote.alt,
      certName: quote.certName || null,
      tagDate: quote.tagDate || null,
      shippingCost: quote.shippingCost ?? null,
      shippingPoint: quote.shippingPoint || null,
      unit: quote.unit || null,
      leadTime: quote.leadTime || null,
      note: quote.note || null,
      myNotes: quote.myNotes || null,
      isCertificated: quote.isCertificated || false,
      type: quote.type || 'Procument',
    }

    const result = await api.post(`/rfqs/${item.rfqId}/supplier-quotes`, payload)
    // Update the quote with the returned ID
    if (result && (result as any).id) {
      quote.id = (result as any).id
    }
    // If RFQ was Rejected, backend resets it to In Progress - update local state
    if (item.rfqStatus === 'Rejected') {
      item.rfqStatus = 'In Progress'
    }
    showSnack('Supplier quote saved', 'success')
  } catch (e) {
    showSnack('Failed to save quote', 'error')
  } finally {
    quote._saving = false
  }
}

function confirmRemoveQuote(item: any, qIdx: number) {
  deleteTargetQuote.value = { item, idx: qIdx }
  showConfirmQuote.value = true
}

async function doRemoveQuote() {
  if (!deleteTargetQuote.value) return
  const { item, idx } = deleteTargetQuote.value
  await removeQuote(item, idx)
  deleteTargetQuote.value = null
  showConfirmQuote.value = false
}

async function removeQuote(item: any, qIdx: number) {
  const quotes = editableQuotes.value[item.rfqItemId]
  if (!quotes) return
  const quote = quotes[qIdx]

  if (quote.id) {
    try {
      await api.del(`/rfqs/${item.rfqId}/supplier-quotes/${quote.id}`)
      showSnack('Quote removed', 'success')
    } catch {
      showSnack('Failed to remove quote', 'error')
      return
    }
  }

  quotes.splice(qIdx, 1)
}

// ── Supplier Autocomplete ──
let supplierSearchDebounce: any = null
function searchSupplier(val: string) {
  clearTimeout(supplierSearchDebounce)
  if (!val || val.length < 1) {
    supplierSuggestions.value = []
    return
  }
  supplierSearchDebounce = setTimeout(async () => {
    try {
      supplierSuggestions.value = await api.get<{ id: number; name: string; status: string; dependency: string }[]>(`/suppliers/search?q=${encodeURIComponent(val)}`)
    } catch {
      supplierSuggestions.value = []
    }
  }, 300)
}

// ── Focus Field (for formatted price inputs) ──
function focusField(key: string) {
  focusedField.value = key
  nextTick(() => {
    const input = document.querySelector(`[data-focus-key="${key}"]`) as HTMLInputElement
    input?.focus()
  })
}

// ── Shop Records (AR condition) ──
const expandedShops = ref<Set<string>>(new Set())

/** Returns other AR quotes for the same RFQ item that already have shop records */
function getOtherARQuotesWithShops(item: any, currentQuote: any) {
  return (editableQuotes.value[item.rfqItemId] || []).filter(
    (q: any) => q.condition === 'AR' && q.id !== currentQuote.id && (q.shopRecords || []).length > 0
  )
}

/** Copies all shop records from sourceQuote into targetQuote (IDs cleared so they'll be saved as new) */
function copyShopsFrom(item: any, targetQuote: any, sourceQuote: any) {
  if (!targetQuote.shopRecords) targetQuote.shopRecords = []
  const copies = (sourceQuote.shopRecords || []).map((s: any) => ({
    ...s,
    id: null,
    _saving: false,
    parentProcumentId: targetQuote.id,
    rfqItemId: item.rfqItemId,
  }))
  targetQuote.shopRecords.push(...copies)
  // Auto-expand so user sees the pasted rows
  const key = `${targetQuote.id}-${item.rfqItemId}`
  expandedShops.value.add(key)
  expandedShops.value = new Set(expandedShops.value)
  showSnack(`Copied ${copies.length} shop record${copies.length !== 1 ? 's' : ''} from ${sourceQuote.supplierName}`, 'info')
}

function toggleShop(quoteId: number, rfqItemId: number) {
  const key = `${quoteId}-${rfqItemId}`
  if (expandedShops.value.has(key)) {
    expandedShops.value.delete(key)
  } else {
    expandedShops.value.add(key)
  }
  expandedShops.value = new Set(expandedShops.value)
}

function isShopExpanded(quoteId: number, rfqItemId: number) {
  return expandedShops.value.has(`${quoteId}-${rfqItemId}`)
}

function addShopRow(item: any, parentQuote: any) {
  if (!parentQuote.shopRecords) parentQuote.shopRecords = []
  parentQuote.shopRecords.push({
    id: null,
    rfqItemId: item.rfqItemId,
    supplierName: '',
    qty: parentQuote.qty || item.qty || 1,
    price: parentQuote.price || 0,
    fixPrice: null,
    condition: 'IN',
    alt: parentQuote.alt || '',
    certName: '',
    tagDate: '',
    shippingCost: null,
    shippingPoint: '',
    unit: 'EA',
    leadTime: '',
    note: '',
    myNotes: '',
    type: 'Shop',
    parentProcumentId: parentQuote.id,
    _saving: false,
  })
  // Auto-expand shop section
  const key = `${parentQuote.id}-${item.rfqItemId}`
  expandedShops.value.add(key)
  expandedShops.value = new Set(expandedShops.value)
}

async function saveShopQuote(item: any, parentQuote: any, shop: any) {
  if (!shop.supplierName?.trim()) {
    showSnack('Supplier name is required', 'error')
    return
  }
  if (!parentQuote.id) {
    showSnack('Save the parent procurement record first', 'error')
    return
  }

  shop._saving = true
  try {
    const payload = {
      id: shop.id || null,
      rfqItemId: item.rfqItemId,
      supplierName: shop.supplierName,
      qty: shop.qty,
      price: shop.price,
      fixPrice: shop.fixPrice,
      condition: shop.condition || 'IN',
      alt: shop.alt,
      certName: shop.certName || null,
      tagDate: shop.tagDate || null,
      shippingCost: shop.shippingCost ?? null,
      shippingPoint: shop.shippingPoint || null,
      unit: shop.unit || null,
      leadTime: shop.leadTime || null,
      note: shop.note || null,
      myNotes: shop.myNotes || null,
      isCertificated: shop.isCertificated || false,
      type: 'Shop',
      parentProcumentId: parentQuote.id,
    }
    const result = await api.post(`/rfqs/${item.rfqId}/supplier-quotes`, payload)
    if (result && (result as any).id) shop.id = (result as any).id
    showSnack('Shop record saved', 'success')
  } catch {
    showSnack('Failed to save shop record', 'error')
  } finally {
    shop._saving = false
  }
}

function confirmRemoveShop(item: any, parentQuote: any, sIdx: number) {
  deleteTargetShop.value = { item, parentQuote, idx: sIdx }
  showConfirmShop.value = true
}

async function doRemoveShop() {
  if (!deleteTargetShop.value) return
  const { item, parentQuote, idx } = deleteTargetShop.value
  await removeShopQuote(item, parentQuote, idx)
  deleteTargetShop.value = null
  showConfirmShop.value = false
}

async function removeShopQuote(item: any, parentQuote: any, sIdx: number) {
  const shop = parentQuote.shopRecords[sIdx]
  if (shop.id) {
    try {
      await api.del(`/rfqs/${item.rfqId}/supplier-quotes/${shop.id}`)
      showSnack('Shop record removed', 'success')
    } catch {
      showSnack('Failed to remove shop record', 'error')
      return
    }
  }
  parentQuote.shopRecords.splice(sIdx, 1)
}
</script>

<style scoped>
.proc-th-inner { display: flex; align-items: center; gap: 2px; white-space: nowrap; }
.proc-filter-btn { opacity: 0.5; flex-shrink: 0; }
.proc-filter-btn:hover, .proc-filter-btn.v-btn--active { opacity: 1; }

:deep(.lead-time-urgent-row) {
  background-color: rgba(255, 193, 7, 0.08) !important;
}
:deep(.lead-time-expired-row) {
  background-color: rgba(239, 68, 68, 0.12) !important;
  border-left: 3px solid #ef4444;
}
:deep(.lead-time-expired-row) td {
  color: #ef4444 !important;
}
:deep(.lead-time-expired-row:hover) {
  background-color: rgba(239, 68, 68, 0.2) !important;
}
:deep(.highlighted-row) {
  background-color: rgba(251, 191, 36, 0.12) !important;
  border-left: 3px solid #fbbf24;
}
:deep(.expanded-row) {
  background: rgba(var(--v-theme-primary), 0.06) !important;
}

.supplier-panel {
  border-top: 2px solid rgba(var(--v-theme-primary), 0.3);
}

.suggestions-bar {
  background: rgba(var(--v-theme-surface-variant), 0.25);
  border: 1px dashed rgba(var(--v-theme-on-surface), 0.12);
  border-radius: 8px;
  padding: 10px 14px;
}
.cursor-pointer {
  cursor: pointer;
}

.quote-grid th {
  font-size: 11px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  padding: 8px 10px;
  color: rgba(var(--v-theme-on-surface), 0.6);
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.1);
  text-align: left;
  white-space: nowrap;
}

.quote-row td {
  padding: 2px 4px;
  vertical-align: middle;
}

.quote-input {
  width: 100%;
  height: 32px;
  border: 1px solid transparent;
  border-radius: 4px;
  padding: 0 8px;
  font-size: 13px;
  background: transparent;
  color: inherit;
  outline: none;
  font-family: 'Inter', sans-serif;
  transition: all 0.15s;
}
.quote-input:hover {
  border-color: rgba(var(--v-theme-on-surface), 0.2);
}
.quote-input:focus {
  background: rgba(var(--v-theme-surface-variant), 0.5);
  border-color: rgb(var(--v-theme-primary));
  box-shadow: 0 0 0 1px rgba(var(--v-theme-primary), 0.2);
}
.quote-input::placeholder {
  opacity: 0.4;
  font-style: italic;
}
.price-display {
  display: flex;
  align-items: center;
  cursor: text;
  color: #4ade80;
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
  font-size: 13px;
  min-height: 32px;
  padding: 0 8px;
}
.quote-select {
  cursor: pointer;
  appearance: auto;
}
.price-input {
  color: #4ade80;
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
  text-align: right;
}

.shop-panel {
  background: rgba(255, 152, 0, 0.04);
  border-left: 3px solid #ff9800;
  margin-left: 20px;
  padding: 10px 16px;
}
.shop-row td {
  background: rgba(255, 152, 0, 0.02);
}
</style>
