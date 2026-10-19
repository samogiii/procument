<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-4 mb-md-6">
      <h1 class="text-h5 font-weight-bold">RFQ Items</h1>
      <v-spacer />
      <v-btn
        color="success"
        variant="tonal"
        prepend-icon="mdi-microsoft-excel"
        :disabled="loading || allItems.length === 0"
        @click="exportFilteredItems"
      >
        Export Excel
      </v-btn>
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
            v-model="searchInput"
            label="Search..."
            single-line
            hide-details
            clearable
            class="flex-grow-1 mx-2"
            style="min-width: 180px;"
            @keyup.enter="runSearch"
            @click:clear="runSearch"
          >
            <template #append-inner>
              <v-btn
                color="primary"
                variant="tonal"
                size="small"
                icon="mdi-magnify"
                @click="runSearch"
              />
            </template>
          </v-text-field>
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
          class="fixed-header-table"
          :headers="headers"
          :items="allItems"
          :items-length="totalItems"
          :loading="loading"
          v-model:page="currentPage"
          v-model:items-per-page="currentItemsPerPage"
          :items-per-page-options="pageOptions"
          fixed-header
          height="calc(100vh - 210px)"
          :sort-by="sort.sortByModel.value"
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
            <div class="proc-th-inner" :class="{ 'proc-th-inner--filtered': colRfqId.length }">
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
            <div class="proc-th-inner" :class="{ 'proc-th-inner--filtered': colRfqName.length }">
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
            <div class="proc-th-inner" :class="{ 'proc-th-inner--filtered': colPn.length }">
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
            <div class="proc-th-inner" :class="{ 'proc-th-inner--filtered': colCond.length }">
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
            <div class="proc-th-inner" :class="{ 'proc-th-inner--filtered': customerFilter.length }">
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
            <div class="proc-th-inner" :class="{ 'proc-th-inner--filtered': statusFilter.length }">
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
            <div class="proc-th-inner" :class="{ 'proc-th-inner--filtered': userFilter.length }">
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
                    <div class="d-flex align-center gap-2">
                      <v-btn
                        size="x-small"
                        color="blue-grey"
                        variant="tonal"
                        prepend-icon="mdi-history"
                        title="Who worked on this part, and every supplier / price ever recorded for it"
                        @click.stop="openPartHistory(item.partNumberId)"
                      >
                        History
                      </v-btn>
                      <v-btn
                        size="x-small"
                        color="primary"
                        variant="flat"
                        prepend-icon="mdi-plus"
                        @click.stop="openNewQuote(item)"
                      >
                        Add Supplier
                      </v-btn>
                    </div>
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
                        v-for="g in getRecentSuppliers(item.rfqItemId)"
                        :key="'recent-' + g.supplierId"
                        size="small"
                        color="amber"
                        variant="tonal"
                        prepend-icon="mdi-flash"
                        class="cursor-pointer"
                        :title="g.quotes.length > 1 ? 'Adds all ' + g.quotes.length + ' conditions' : ''"
                        @click.stop="applySuggestionGroup(item, g)"
                      >
                        {{ g.supplierName }}
                        <template v-for="(q, qi) in g.quotes" :key="qi">
                          <span v-if="qi > 0" class="text-caption ml-1" style="opacity:0.5;">|</span>
                          <span class="text-caption ml-1 text-medium-emphasis">{{ q.condition || 'NE' }}</span>
                          <span v-if="q.priceHidden" class="text-caption ml-1" style="color:#ef5350;" title="Price expired (older than 14 days)">Expired</span>
                          <span v-else class="text-caption ml-1 text-medium-emphasis">${{ formatPrice(q.price) }}</span>
                        </template>
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
                    <!-- Read-only grid: every field is edited in SupplierQuoteEditDialog (pencil or double-click) -->
                    <table class="quote-grid" style="width: 100%; border-collapse: collapse; min-width: 1100px;">
                      <thead>
                        <tr>
                          <th style="min-width: 200px;">Supplier</th>
                          <th>Cond</th>
                          <th>Alt P/N</th>
                          <th>Qty</th>
                          <th v-if="allQuoteColumns">Unit</th>
                          <th>Cost Price ($)</th>
                          <th>Cert Type</th>
                          <th v-if="allQuoteColumns">Tag Date</th>
                          <th v-if="allQuoteColumns">Shipping Cost</th>
                          <th v-if="allQuoteColumns">Shipping Point</th>
                          <th>Tagged By</th>
                          <th>Buy LeadTime</th>
                          <th v-if="allQuoteColumns">Sell LeadTime</th>
                          <template v-if="allQuoteColumns"><th v-for="f in OTHER_QUOTE_DETAIL_FIELDS" :key="f.key" style="min-width: 100px;">{{ f.label }}</th></template>
                          <th v-if="allQuoteColumns" style="min-width: 130px;">Our Company</th>
                          <th v-if="isSuperAdmin" style="min-width: 110px;">Expert</th>
                          <th v-if="allQuoteColumns" style="min-width: 150px;">Note</th>
                          <th v-if="allQuoteColumns" style="min-width: 150px;">My Notes</th>
                          <th v-if="allQuoteColumns" style="width: 40px" class="text-center">Cert</th>
                          <th v-if="allQuoteColumns" style="min-width: 180px">Cert Reference</th>
                          <th style="width: 120px;" class="text-right"><v-btn size="x-small" variant="text" density="comfortable" :icon="allQuoteColumns ? 'mdi-table-column-remove' : 'mdi-table-column-plus-after'" :title="allQuoteColumns ? 'Show the main columns only' : 'Show all columns'" @click.stop="allQuoteColumns = !allQuoteColumns" /></th>
                        </tr>
                      </thead>
                      <tbody>
                        <template v-for="(quote, qIdx) in getEditableQuotes(item.rfqItemId)" :key="qIdx">
                        <tr class="quote-row quote-row--readonly" title="Double-click to edit" @click.stop @dblclick.stop="openEditQuote(item, quote)">
                          <td>
                            <div class="quote-cell quote-cell--supplier">
                              <span class="font-weight-medium">{{ quote.supplierName || '—' }}</span>
                              <v-chip v-if="!quote.id" size="x-small" color="warning" variant="tonal" class="ml-1" title="Not saved yet — edit or save it">Unsaved</v-chip>
                            </div>
                            <div v-if="quote.supplierEmail" class="quote-cell-email" :title="quote.supplierEmail">
                              <v-icon icon="mdi-email-check-outline" size="11" class="mr-1" />{{ quote.supplierEmail }}
                            </div>
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
                          <td><span class="quote-cell">{{ cellText(quote.condition) }}</span></td>
                          <td><span class="quote-cell">{{ quote.alt || 'Same P/N' }}</span></td>
                          <td><span class="quote-cell text-center">{{ cellText(quote.qty) }}</span></td>
                          <td v-if="allQuoteColumns"><span class="quote-cell">{{ cellText(quote.unit) }}</span></td>
                          <td>
                            <span class="quote-cell price-text">
                              <template v-if="quote.price">{{ '$' + formatPrice(quote.price) }}</template>
                              <span v-else-if="quote.priceHidden" style="color:#ef5350; font-size:10px;" title="Price expired (older than 14 days)">Expired</span>
                              <template v-else>—</template>
                            </span>
                          </td>
                          <td><span class="quote-cell">{{ cellText(quote.certName) }}</span></td>
                          <td v-if="allQuoteColumns"><span class="quote-cell">{{ cellText(quote.tagDate) }}</span></td>
                          <td v-if="allQuoteColumns"><span class="quote-cell price-text">{{ quote.shippingCost ? '$' + formatPrice(quote.shippingCost) : '—' }}</span></td>
                          <td v-if="allQuoteColumns"><span class="quote-cell">{{ cellText(quote.shippingPoint) }}</span></td>
                          <td><span class="quote-cell">{{ cellText(quote.taggedBy) }}</span></td>
                          <td><span class="quote-cell">{{ cellText(quote.leadTime) }}</span></td>
                          <td v-if="allQuoteColumns"><span class="quote-cell">{{ cellText(quote.sellLeadTime) }}</span></td>
                          <template v-if="allQuoteColumns"><td v-for="f in OTHER_QUOTE_DETAIL_FIELDS" :key="f.key"><span class="quote-cell">{{ cellText(quote[f.key]) }}</span></td></template>
                          <td v-if="allQuoteColumns"><span class="quote-cell">{{ cellText(presetName(quote)) }}</span></td>
                          <td v-if="isSuperAdmin">
                            <span class="expert-cell" :title="quote.expertName ? `Imported by ${quote.expertName}` : 'Set when the quote is saved'">
                              <v-icon icon="mdi-account-tie-outline" size="13" class="mr-1" />{{ quote.expertName || '—' }}
                            </span>
                          </td>
                          <td v-if="allQuoteColumns"><div class="quote-cell quote-cell--text">{{ cellText(quote.note) }}</div></td>
                          <td v-if="allQuoteColumns"><div class="quote-cell quote-cell--text">{{ cellText(quote.myNotes) }}</div></td>
                          <td v-if="allQuoteColumns" class="text-center">
                            <v-icon
                              v-if="quote.supplierDependency === 'Certificated'"
                              :icon="quote.isCertificated ? 'mdi-checkbox-marked' : 'mdi-checkbox-blank-outline'"
                              :color="quote.isCertificated ? 'success' : 'grey'"
                              size="18"
                            />
                          </td>
                          <td v-if="allQuoteColumns" class="cert-ref-cell">
                            <div class="d-flex flex-wrap align-center gap-1">
                              <v-chip
                                v-for="reference in quote.certReferences || []"
                                :key="'pdf-' + reference"
                                size="x-small"
                                color="error"
                                variant="tonal"
                                prepend-icon="mdi-file-pdf-box"
                                closable
                                class="chip-close-left"
                                title="From an uploaded cert PDF — X deletes the PDF"
                                @click:close="askDeleteCertificate(item, quote, reference)"
                              >REF#{{ reference }}</v-chip>
                              <v-chip
                                v-for="(reference, rIdx) in quote.customCertReferences || []"
                                :key="'txt-' + rIdx"
                                size="x-small"
                                color="primary"
                                variant="tonal"
                              >{{ reference }}</v-chip>
                              <span v-if="!(quote.certReferences || []).length && !(quote.customCertReferences || []).length" class="quote-cell">—</span>
                            </div>
                          </td>
                          <td class="text-center" style="white-space: nowrap;">
                            <v-btn
                              icon="mdi-pencil"
                              size="x-small"
                              variant="text"
                              color="primary"
                              title="Edit"
                              @click.stop="openEditQuote(item, quote)"
                            />
                            <v-btn
                              icon="mdi-file-pdf-box"
                              size="x-small"
                              variant="text"
                              :color="quote.certificateCount || quote.certReferences?.length ? 'error' : 'grey'"
                              :title="quote.id ? 'Manage certificate PDFs' : 'Save this supplier quote before adding certificates'"
                              @click.stop="openCertificateDialog(item, quote)"
                            />
                            <span v-if="quote.certificateCount || quote.certReferences?.length" class="text-caption">
                              {{ quote.certificateCount || quote.certReferences?.length }}
                            </span>
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
                              v-if="!quote.id"
                              icon="mdi-content-save"
                              size="x-small"
                              variant="text"
                              color="success"
                              :loading="quote._saving"
                              @click.stop="saveQuote(item, quote)"
                              title="Save as it is"
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
                          <td :colspan="quoteGridColspan" class="pa-0">
                            <div class="shop-panel">
                              <div class="d-flex align-center justify-space-between mb-2">
                                <div class="d-flex align-center gap-2">
                                  <v-btn
                                    size="x-small"
                                    color="warning"
                                    variant="flat"
                                    prepend-icon="mdi-plus"
                                    :disabled="!quote.id"
                                    :title="quote.id ? 'Add a repair shop' : 'Save this AR quote first'"
                                    @click.stop="openNewShop(item, quote)"
                                  >
                                    Add Shop
                                  </v-btn>
                                  <span class="text-caption text-uppercase font-weight-bold" style="color: #ff9800;">
                                    <v-icon icon="mdi-wrench" size="14" class="mr-1" />
                                    Shop Records ({{ (quote.shopRecords || []).length }})
                                  </span>
                                </div>
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
                                </div>
                              </div>
                              <table class="quote-grid" v-if="(quote.shopRecords || []).length > 0" style="width: 100%; border-collapse: collapse;">
                                <thead>
                                  <tr>
                                    <th>Supplier</th>
                                    <th>Alt P/N</th>
                                    <th>Condition</th>
                                    <th>Qty</th>
                                    <th v-if="allQuoteColumns">Unit</th>
                                    <th>Cost Price ($)</th>
                                    <th style="color: #ff9800;">Repair Cost ($)</th>
                                    <th>Cert Type</th>
                                    <th v-if="allQuoteColumns">Tag Date</th>
                                    <th v-if="allQuoteColumns">Shipping Cost</th>
                                    <th v-if="allQuoteColumns">Shipping Point</th>
                                    <th>Tagged By</th>
                                    <th>Buy LeadTime</th>
                                    <th v-if="allQuoteColumns">Sell LeadTime</th>
                                    <template v-if="allQuoteColumns"><th v-for="f in OTHER_QUOTE_DETAIL_FIELDS" :key="f.key">{{ f.label }}</th></template>
                                    <th v-if="allQuoteColumns">Note</th>
                                    <th v-if="allQuoteColumns">My Notes</th>
                                    <th v-if="allQuoteColumns" style="width: 40px" class="text-center">Cert</th>
                                    <th v-if="allQuoteColumns" style="min-width: 180px">Cert Reference</th>
                                    <th style="width: 110px;" class="text-right"><v-btn size="x-small" variant="text" density="comfortable" :icon="allQuoteColumns ? 'mdi-table-column-remove' : 'mdi-table-column-plus-after'" :title="allQuoteColumns ? 'Show the main columns only' : 'Show all columns'" @click.stop="allQuoteColumns = !allQuoteColumns" /></th>
                                  </tr>
                                </thead>
                                <tbody>
                                  <tr v-for="(shop, sIdx) in quote.shopRecords" :key="'shop-' + sIdx" class="shop-row quote-row--readonly" title="Double-click to edit" @click.stop @dblclick.stop="openEditShop(item, quote, shop)">
                                    <td>
                                      <div class="quote-cell quote-cell--supplier">
                                        <span class="font-weight-medium">{{ shop.supplierName || '—' }}</span>
                                        <v-chip v-if="!shop.id" size="x-small" color="warning" variant="tonal" class="ml-1" title="Not saved yet — edit or save it">Unsaved</v-chip>
                                      </div>
                                      <div v-if="shop.supplierEmail" class="quote-cell-email" :title="shop.supplierEmail">
                                        <v-icon icon="mdi-email-check-outline" size="11" class="mr-1" />{{ shop.supplierEmail }}
                                      </div>
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
                                    <td><span class="quote-cell">{{ shop.alt || 'Same P/N' }}</span></td>
                                    <td><span class="quote-cell">{{ cellText(shop.condition) }}</span></td>
                                    <td><span class="quote-cell text-center">{{ cellText(shop.qty) }}</span></td>
                                    <td v-if="allQuoteColumns"><span class="quote-cell">{{ cellText(shop.unit) }}</span></td>
                                    <td><span class="quote-cell price-text">{{ shop.price ? '$' + formatPrice(shop.price) : '—' }}</span></td>
                                    <td><span class="quote-cell price-text" style="color: #ff9800;">{{ shop.fixPrice ? '$' + formatPrice(shop.fixPrice) : '—' }}</span></td>
                                    <td><span class="quote-cell">{{ cellText(shop.certName) }}</span></td>
                                    <td v-if="allQuoteColumns"><span class="quote-cell">{{ cellText(shop.tagDate) }}</span></td>
                                    <td v-if="allQuoteColumns"><span class="quote-cell price-text">{{ shop.shippingCost ? '$' + formatPrice(shop.shippingCost) : '—' }}</span></td>
                                    <td v-if="allQuoteColumns"><span class="quote-cell">{{ cellText(shop.shippingPoint) }}</span></td>
                                    <td><span class="quote-cell">{{ cellText(shop.taggedBy) }}</span></td>
                                    <td><span class="quote-cell">{{ cellText(shop.leadTime) }}</span></td>
                                    <td v-if="allQuoteColumns"><span class="quote-cell">{{ cellText(shop.sellLeadTime) }}</span></td>
                                    <template v-if="allQuoteColumns"><td v-for="f in OTHER_QUOTE_DETAIL_FIELDS" :key="f.key"><span class="quote-cell">{{ cellText(shop[f.key]) }}</span></td></template>
                                    <td v-if="allQuoteColumns"><div class="quote-cell quote-cell--text">{{ cellText(shop.note) }}</div></td>
                                    <td v-if="allQuoteColumns"><div class="quote-cell quote-cell--text">{{ cellText(shop.myNotes) }}</div></td>
                                    <td v-if="allQuoteColumns" class="text-center">
                                      <v-icon
                                        :icon="shop.isCertificated ? 'mdi-checkbox-marked' : 'mdi-checkbox-blank-outline'"
                                        :color="shop.isCertificated ? 'success' : 'grey'"
                                        size="18"
                                      />
                                    </td>
                                    <td v-if="allQuoteColumns" class="cert-ref-cell">
                                      <div class="d-flex flex-wrap align-center gap-1">
                                        <v-chip
                                          v-for="reference in shop.certReferences || []"
                                          :key="'pdf-' + reference"
                                          size="x-small"
                                          color="error"
                                          variant="tonal"
                                          prepend-icon="mdi-file-pdf-box"
                                          closable
                                          class="chip-close-left"
                                          title="From an uploaded cert PDF — X deletes the PDF"
                                          @click:close="askDeleteCertificate(item, shop, reference)"
                                        >REF#{{ reference }}</v-chip>
                                        <v-chip
                                          v-for="(reference, rIdx) in shop.customCertReferences || []"
                                          :key="'txt-' + rIdx"
                                          size="x-small"
                                          color="primary"
                                          variant="tonal"
                                        >{{ reference }}</v-chip>
                                        <span v-if="!(shop.certReferences || []).length && !(shop.customCertReferences || []).length" class="quote-cell">—</span>
                                      </div>
                                    </td>
                                    <td class="text-center" style="white-space: nowrap;">
                                      <v-btn icon="mdi-pencil" size="x-small" variant="text" color="primary" title="Edit" @click.stop="openEditShop(item, quote, shop)" />
                                      <v-btn
                                        icon="mdi-file-pdf-box"
                                        size="x-small"
                                        variant="text"
                                        :color="shop.certificateCount || shop.certReferences?.length ? 'error' : 'grey'"
                                        :title="shop.id ? 'Manage certificate PDFs' : 'Save this shop record before adding certificates'"
                                        @click.stop="openCertificateDialog(item, shop)"
                                      />
                                      <span v-if="shop.certificateCount || shop.certReferences?.length" class="text-caption">
                                        {{ shop.certificateCount || shop.certReferences?.length }}
                                      </span>
                                      <v-btn
                                        v-if="!shop.id"
                                        icon="mdi-content-save"
                                        size="x-small"
                                        variant="text"
                                        color="success"
                                        :loading="shop._saving"
                                        @click.stop="saveShopQuote(item, quote, shop)"
                                        title="Save as it is"
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

    <!-- Add / edit one supplier quote or shop record — the grids above are read-only -->
    <SupplierQuoteEditDialog
      v-model="quoteEditor.open"
      :quote="quoteEditor.quote"
      :shop="quoteEditor.shop"
      :part-number="quoteEditor.item?.partNumberName"
      :customer-base="quoteEditor.item?.customerBase"
      :company-preset-options="companyPresetOptions"
      :save="saveQuoteFromEditor"
    />

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

    <!-- Certificate PDFs for one saved supplier-part quote -->
    <v-dialog v-model="showCertificateDialog" max-width="700">
      <v-card>
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-certificate-outline" color="primary" class="mr-2" />
          Certificate PDFs
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="showCertificateDialog = false" />
        </v-card-title>
        <v-card-text class="pa-4">
          <p class="text-body-2 mb-1">
            {{ certificateTarget?.quote.supplierName || 'Supplier quote' }}
          </p>
          <p v-if="certificateTarget?.item.partNumberName" class="text-caption text-medium-emphasis mb-4">
            Part number: {{ certificateTarget.item.partNumberName }}
          </p>
          <FileDropZone
            accept="application/pdf,.pdf"
            label="Drop certificate PDFs here or click to add"
            :on-upload="uploadCertificates"
          />

          <v-divider class="my-5" />
          <div class="d-flex align-center mb-2">
            <span class="text-subtitle-2">Saved certificates</span>
            <v-spacer />
            <v-progress-circular v-if="certificatesLoading" indeterminate size="18" width="2" />
          </div>
          <v-list v-if="certificates.length" density="compact" class="border rounded">
            <v-list-item v-for="certificate in certificates" :key="certificate.id">
              <template #prepend><v-icon icon="mdi-file-pdf-box" color="error" /></template>
              <v-list-item-title>{{ certificate.originalFileName }}</v-list-item-title>
              <v-list-item-subtitle>
                <strong>REF#{{ certificate.reference }}</strong> ·
                {{ formatFileSize(certificate.fileSizeBytes) }} · {{ new Date(certificate.uploadedAt).toLocaleDateString() }}
                <span v-if="certificate.uploadedByName"> · {{ certificate.uploadedByName }}</span>
              </v-list-item-subtitle>
              <template #append>
                <v-btn icon="mdi-download-outline" size="small" variant="text" @click="downloadCertificate(certificate)" />
                <v-btn icon="mdi-delete-outline" size="small" variant="text" color="error" title="Delete this PDF" @click="certificateTarget && askDeleteCertificate(certificateTarget.item, certificateTarget.quote, certificate.reference, certificate)" />
              </template>
            </v-list-item>
          </v-list>
          <p v-else-if="!certificatesLoading" class="text-caption text-medium-emphasis text-center py-4 mb-0">
            No certificate PDFs have been uploaded for this quote.
          </p>
        </v-card-text>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="snackbarTimeout" location="bottom right">
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

    <ConfirmDialog
      v-model="showConfirmCertificate"
      title="Delete Certificate PDF?"
      :message="`REF#${certificateDeleteTarget?.reference} and its PDF file will be permanently deleted.`"
      confirm-text="Delete"
      confirm-color="error"
      @confirm="doDeleteCertificate"
    />

    <PartHistoryDialog v-model="showPartHistory" :part-number-id="historyPartNumberId" />
  </div>
</template>

<script setup lang="ts">
import { downloadExcel } from '~/utils/exportExcel'

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
  // Remember the last column sort (incl. Deadline/days) across refreshes
  sortKey: '',
  sortDesc: false,
})
const search = pf.search
// `search` is the COMMITTED term that drives the query. `searchInput` is the raw
// text-field value — it only commits on button click / Enter so the heavy backend
// search does not fire on every keystroke.
const searchInput = ref(search.value)
function runSearch() {
  search.value = (searchInput.value ?? '').trim()
}
watch(search, (v) => {
  if ((v ?? '') !== (searchInput.value ?? '')) searchInput.value = v
})
const currentPage = pf.page           // top-level ref so Vue auto-unwraps in template
const currentItemsPerPage = pf.itemsPerPage
const loading = ref(false)
const allItems = ref<any[]>([])
const totalItems = ref(0)
const sort = useServerSort({ sortKey: pf.sortKey, sortDesc: pf.sortDesc })

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
const customerOptions = ref<{ title: string; value: string }[]>([])
const allCustomerOptions = ref<{ title: string; value: string }[]>([])

// Users: from filter-options (full DB)
// allUserOptions is already populated — format: { id, name }
const userOptions = ref<{ id: number; name: string }[]>([])
const allUserOptions = ref<{ id: number; name: string }[]>([])

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

// "Show all" is temporary. A change in another column returns every menu to
// available-only mode so an impossible second choice is not shown by default.
watch(() => colPnOpts.value.join('\u001f'), () => { showAllPnOpts.value = false })
watch(() => condOptsPage.value.join('\u001f'), () => { showAllCondOpts.value = false })
watch(() => statusColOptsPage.value.join('\u001f'), () => { showAllStatusColOpts.value = false })
watch(() => custColOptsPage.value.map(o => o.value).join('\u001f'), () => { showAllCustColOpts.value = false })
watch(() => userColOptsPage.value.map(u => u.id).join('\u001f'), () => { showAllUserColOpts.value = false })

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
const itemSuggestions = ref<Record<number, { knownSuppliers: any[]; recentQuotes: any[]; loading: boolean }>>({})
const partAvailability = ref<Record<number, any>>({})

type Certificate = {
  id: number
  supplierQuoteId: number
  reference: string
  originalFileName: string
  fileSizeBytes: number
  uploadedAt: string
  uploadedByName?: string
}

const showCertificateDialog = ref(false)
const certificateTarget = ref<{ item: any; quote: any } | null>(null)
const certificates = ref<Certificate[]>([])
const certificatesLoading = ref(false)

async function openCertificateDialog(item: any, quote: any) {
  if (!quote.id) {
    showSnack('Save this supplier quote before attaching certificate PDFs', 'warning')
    return
  }
  certificateTarget.value = { item, quote }
  certificates.value = []
  showCertificateDialog.value = true
  await loadCertificates()
}

async function loadCertificates() {
  const target = certificateTarget.value
  if (!target?.quote.id) return
  certificatesLoading.value = true
  try {
    certificates.value = await api.get<Certificate[]>(
      `/rfqs/${target.item.rfqId}/supplier-quotes/${target.quote.id}/certificates`
    )
    syncCertificateSummary(target.quote)
  } catch {
    showSnack('Could not load certificate PDFs', 'error')
  } finally {
    certificatesLoading.value = false
  }
}

async function uploadCertificates(files: File[], onProgress: (done: number) => void) {
  const target = certificateTarget.value
  if (!target?.quote.id) throw new Error('A saved supplier quote is required')
  const data = new FormData()
  files.forEach(file => data.append('files', file))
  try {
    const uploaded = await api.post<Certificate[]>(
      `/rfqs/${target.item.rfqId}/supplier-quotes/${target.quote.id}/certificates`,
      data
    )
    onProgress(files.length)
    certificates.value = [...uploaded, ...certificates.value]
    syncCertificateSummary(target.quote)
    showSnack(`${uploaded.length} certificate PDF${uploaded.length === 1 ? '' : 's'} uploaded`, 'success')
  } catch (error: any) {
    showSnack(error?.data?.message || error?.data || 'Certificate upload failed', 'error')
    throw error
  }
}

function syncCertificateSummary(quote: any, list: Certificate[] = certificates.value) {
  quote.certificateCount = list.length
  // Server orders references oldest-first; the dialog list is newest-first.
  quote.certReferences = [...list]
    .sort((a, b) => new Date(a.uploadedAt).getTime() - new Date(b.uploadedAt).getTime())
    .map(certificate => certificate.reference)
}

const showConfirmCertificate = ref(false)
const certificateDeleteTarget = ref<{ item: any; quote: any; reference: string; certificate?: Certificate } | null>(null)

function askDeleteCertificate(item: any, quote: any, reference: string, certificate?: Certificate) {
  if (!quote?.id) return
  certificateDeleteTarget.value = { item, quote, reference, certificate }
  showConfirmCertificate.value = true
}

async function doDeleteCertificate() {
  const target = certificateDeleteTarget.value
  showConfirmCertificate.value = false
  if (!target) return
  const base = `/rfqs/${target.item.rfqId}/supplier-quotes/${target.quote.id}/certificates`
  try {
    // Chips only know the reference — look up the certificate id when not opened from the dialog.
    const list = await api.get<Certificate[]>(base)
    const certificate = target.certificate ?? list.find(c => c.reference === target.reference)
    if (!certificate) {
      showSnack('Certificate not found — it may already be deleted', 'warning')
      syncCertificateSummary(target.quote, list)
      return
    }
    await api.del(`${base}/${certificate.id}`)
    const remaining = list.filter(c => c.id !== certificate.id)
    syncCertificateSummary(target.quote, remaining)
    if (certificateTarget.value?.quote === target.quote) certificates.value = remaining
    showSnack(`REF#${certificate.reference} deleted`, 'success')
  } catch (error: any) {
    showSnack(error?.data?.message || error?.data || 'Could not delete certificate PDF', 'error')
  } finally {
    certificateDeleteTarget.value = null
  }
}

async function downloadCertificate(certificate: Certificate) {
  const target = certificateTarget.value
  if (!target?.quote.id) return
  try {
    const blob = await api.get<Blob>(
      `/rfqs/${target.item.rfqId}/supplier-quotes/${target.quote.id}/certificates/${certificate.id}/download`,
      { responseType: 'blob' }
    )
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = certificate.originalFileName
    link.click()
    URL.revokeObjectURL(url)
  } catch {
    showSnack('Certificate download failed', 'error')
  }
}

function formatFileSize(bytes: number) {
  return bytes < 1024 * 1024
    ? `${Math.max(1, Math.round(bytes / 1024))} KB`
    : `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

// Part history modal — read-only view of every expert, supplier and price ever recorded for a part
const showPartHistory = ref(false)
const historyPartNumberId = ref<number | null>(null)
function openPartHistory(partNumberId: number) {
  historyPartNumberId.value = partNumberId
  showPartHistory.value = true
}

const snackbar = ref(false)
const snackbarText = ref('')
const snackbarColor = ref('success')
const snackbarTimeout = ref(2000)
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
  // Errors (e.g. "add the supplier in the Catalog first") need longer to read.
  snackbarTimeout.value = color === 'error' ? 6000 : 2000
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

function buildProcumentParams(page: number, pageSize: number) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  sort.appendTo(params)
  if (search.value?.trim()) params.set('search', search.value.trim())
  if (pnSearch.value?.trim()) params.set('pnSearch', pnSearch.value.trim())
  if (statusFilter.value?.length) (statusFilter.value as string[]).forEach(status => params.append('status', status))
  if (userFilter.value?.length) (userFilter.value as number[]).forEach(userId => params.append('userIds', String(userId)))
  if (customerFilter.value?.length) (customerFilter.value as string[]).forEach(customer => params.append('customerSearch', customer))
  if (colPn.value?.length) (colPn.value as string[]).forEach(partNumber => params.append('colPartNames', partNumber))
  if (colCond.value?.length) (colCond.value as string[]).forEach(condition => params.append('conditions', condition))
  if (colRfqId.value?.length) (colRfqId.value as string[]).forEach(rfqId => params.append('rfqIds', rfqId))
  if (colRfqName.value?.length) (colRfqName.value as string[]).forEach(rfqName => params.append('rfqNames', rfqName))
  if (showPendingOnly.value) params.set('pendingOnly', 'true')
  if (showNoQuote.value) params.set('includeNoQuote', 'true')
  return params
}

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
    const params = buildProcumentParams(page, itemsPerPage)
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

function exportFilteredItems() {
  if (loading.value || allItems.value.length === 0) return
  try {
    downloadExcel(allItems.value.map(item => ({
      'RFQ #': item.rfqId,
      'RFQ Name': item.rfqName || '',
      'Part Number': item.partNumberName || '',
      'Description': item.description || '',
      'Qty': item.qty,
      'Condition': item.condition || '',
      'Customer': item.customerCode || '',
      'Status': item.rfqStatus || 'Open',
      'Suppliers': item.supplierQuotes?.length ?? 0,
      'Assigned Users': (item.assignedUsers || []).map((user: any) => user.name).join(', '),
      'Deadline': formatExportDate(item.leadTime),
      'Received Date': formatExportDate(item.createdAt),
    })), `RFQ-Items-${new Date().toISOString().slice(0, 10)}`, 'RFQ Items')

    showSnack(`Exported ${allItems.value.length} RFQ items from this page.`, 'success')
  } catch (error) {
    console.error('Failed to export RFQ items:', error)
    showSnack('Failed to export RFQ items.', 'error')
  }
}

function formatExportDate(value?: string | null) {
  if (!value) return ''
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '' : date.toLocaleDateString()
}

async function loadData() {
  await loadServerPage()
}

let procumentDebounce: any = null
function debouncedProcumentLoad() {
  clearTimeout(procumentDebounce)
  procumentDebounce = setTimeout(() => loadServerPage({ ...lastProcumentOpts.value, page: 1 }), 350)
}
// `search` only changes on explicit commit (button / Enter), so load immediately.
watch(search, () => loadServerPage({ ...lastProcumentOpts.value, page: 1 }))
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
  loadCompanyPresetOptions()
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

// Group recent quotes by supplier so the same supplier with multiple conditions
// shows as ONE chip listing every condition/price (instead of duplicate name chips).
function getRecentSuppliers(rfqItemId: number) {
  const groups = new Map<number, { supplierId: number; supplierName: string; quotes: any[] }>()
  for (const s of getSuggestions(rfqItemId).recentQuotes) {
    let g = groups.get(s.supplierId)
    if (!g) {
      g = { supplierId: s.supplierId, supplierName: s.supplierName, quotes: [] }
      groups.set(s.supplierId, g)
    }
    g.quotes.push(s)
  }
  return Array.from(groups.values())
}

// Clicking a supplier chip applies every condition that supplier has.
function applySuggestionGroup(item: any, group: { quotes: any[] }) {
  for (const q of group.quotes) {
    applySuggestion(item, q)
  }
}

function applySuggestion(item: any, suggestion: any) {
  const key = item.rfqItemId
  if (!editableQuotes.value[key]) {
    editableQuotes.value[key] = []
  }
  // Check if this supplier+condition is already added
  const suggestionCondition = (suggestion.condition || 'NE').toUpperCase()
  const alreadyExists = editableQuotes.value[key].some(
    (q: any) =>
      q.supplierName?.toLowerCase() === suggestion.supplierName?.toLowerCase() &&
      (q.condition || 'NE').toUpperCase() === suggestionCondition
  )
  if (alreadyExists) {
    showSnack(`${suggestion.supplierName} (${suggestionCondition}) is already added`, 'warning')
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
    sellLeadTime: toSellLeadTime(suggestion.leadTime, item.customerBase) || suggestion.sellLeadTime || '',
    note: suggestion.note || '',
    myNotes: suggestion.myNotes || '',
    ...quoteDetailsOf(suggestion, ''),
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
    const sCondition = (s.condition || 'NE').toUpperCase()
    const alreadyExists = (editableQuotes.value[key] || []).some(
      (q: any) =>
        q.supplierName?.toLowerCase() === s.supplierName?.toLowerCase() &&
        (q.condition || 'NE').toUpperCase() === sCondition
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
    sellLeadTime: toSellLeadTime(rec.leadTime, item.customerBase),
    note: '',
    myNotes: '',
    ...emptyQuoteDetails(),
    _saving: false,
    shopRecords: [],
  })
  showSnack(`${rec.label} added`, 'success')
}

function getEditableQuotes(rfqItemId: number) {
  return editableQuotes.value[rfqItemId] || []
}

// ── Supplier Quote Management ──
async function saveQuote(item: any, quote: any) {
  if (!quote.supplierName?.trim()) {
    showSnack('Supplier name is required', 'error')
    return
  }

  quote._saving = true
  try {
    const payload = toSupplierQuotePayload({ ...quote, rfqItemId: item.rfqItemId })

    const result = await api.post(`/rfqs/${item.rfqId}/supplier-quotes`, payload)
    // Update the quote with the returned ID
    if (result && (result as any).id) {
      quote.id = (result as any).id
      quote.supplierId = (result as any).supplierId
      quote.supplierStatus = (result as any).supplierStatus
      quote.supplierDependency = (result as any).supplierDependency
      quote.companyPresetName = (result as any).companyPresetName
      if (isSuperAdmin.value) quote.expertName = (result as any).expertName
    }
    // If RFQ was Rejected, backend resets it to In Progress - update local state
    if (item.rfqStatus === 'Rejected') {
      item.rfqStatus = 'In Progress'
    }
    showSnack('Supplier quote saved', 'success')
  } catch (e: any) {
    // Suppliers can't be created from here — the backend rejects a name that isn't
    // in the Catalog. Surface its message so the user knows to add it in the Catalog.
    showSnack(e?.data?.message || 'Failed to save quote', 'error')
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

// ── Supplier quote add / edit modal (the grids are read-only) ──
const quoteEditor = ref({
  open: false,
  quote: null as any,
  item: null as any,
  /** The grid row being edited; null when adding. */
  row: null as any,
  /** Parent AR quote of a shop record. */
  parent: null as any,
  shop: false,
})

function presetName(quote: any) {
  if (!quote.companyPresetId) return ''
  return companyPresetOptions.value.find(p => p.id === quote.companyPresetId)?.name || quote.companyPresetName || ''
}

function blankQuote(item: any) {
  return {
    id: null,
    rfqItemId: item.rfqItemId,
    supplierName: '',
    supplierId: null,
    supplierEmail: null,
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
    sellLeadTime: '',
    note: '',
    myNotes: '',
    companyPresetId: null,
    customCertReferences: [],
    ...emptyQuoteDetails(),
  }
}

function openNewQuote(item: any) {
  quoteEditor.value = { open: true, quote: { ...blankQuote(item), shopRecords: [] }, item, row: null, parent: null, shop: false }
}

function openEditQuote(item: any, quote: any) {
  quoteEditor.value = { open: true, quote, item, row: quote, parent: null, shop: false }
}

function openNewShop(item: any, parent: any) {
  const shop = {
    ...blankQuote(item),
    qty: parent.qty || item.qty || 1,
    price: parent.price || 0,
    fixPrice: null,
    condition: 'IN',
    alt: parent.alt || '',
    type: 'Shop',
    parentProcumentId: parent.id,
  }
  quoteEditor.value = { open: true, quote: shop, item, row: null, parent, shop: true }
}

function openEditShop(item: any, parent: any, shop: any) {
  quoteEditor.value = { open: true, quote: shop, item, row: shop, parent, shop: true }
}

/** Saves the modal's copy straight away, then patches (or adds) the grid row. Throws keep the modal open. */
async function saveQuoteFromEditor(form: any) {
  const { item, row, parent, shop } = quoteEditor.value
  if (shop && !parent?.id) throw new Error('Save the parent AR quote first')
  const saved = await api.post<any>(
    `/rfqs/${item.rfqId}/supplier-quotes`,
    toSupplierQuotePayload({ ...form, rfqItemId: item.rfqItemId }, { shop, parentProcumentId: parent?.id }),
  )
  const merged = {
    ...form,
    ...saved,
    customCertReferences: saved?.customCertReferences ?? form.customCertReferences ?? [],
    shopRecords: shop ? [] : (row?.shopRecords ?? []),
    _saving: false,
  }
  if (row) {
    Object.assign(row, merged)
  } else if (shop) {
    ;(parent.shopRecords ||= []).unshift(merged)
    const key = `${parent.id}-${item.rfqItemId}`
    expandedShops.value.add(key)
    expandedShops.value = new Set(expandedShops.value)
  } else {
    ;(editableQuotes.value[item.rfqItemId] ||= []).unshift(merged)
    if (!expandedArray.value.includes(item.rfqItemId)) expandedArray.value.push(item.rfqItemId)
  }
  // The backend re-opens a Rejected RFQ when a quote is saved.
  if (item.rfqStatus === 'Rejected') item.rfqStatus = 'In Progress'
  showSnack(shop ? 'Shop record saved' : 'Supplier quote saved', 'success')
}

// ── Our Company (company presets) + Expert column ──
const isSuperAdmin = computed(() => authStore.isSuperAdmin)
const companyPresetOptions = ref<{ id: number; name: string }[]>([])
async function loadCompanyPresetOptions() {
  try {
    companyPresetOptions.value = await api.get<{ id: number; name: string }[]>('/companypresets/options') || []
  } catch {
    companyPresetOptions.value = []
  }
}
// Supplier grid columns: 25, plus the SuperAdmin-only Expert column.
// Main columns only unless "all columns" is on (see composables/useQuoteGridColumns).
const { allQuoteColumns, OTHER_QUOTE_DETAIL_FIELDS } = useQuoteGridColumns()
const quoteGridColspan = computed(() => allQuoteColumns.value ? (isSuperAdmin.value ? 27 : 26) : (isSuperAdmin.value ? 10 : 9))

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
    const payload = toSupplierQuotePayload({ ...shop, rfqItemId: item.rfqItemId }, { shop: true, parentProcumentId: parentQuote.id })
    const result = await api.post(`/rfqs/${item.rfqId}/supplier-quotes`, payload)
    if (result && (result as any).id) shop.id = (result as any).id
    showSnack('Shop record saved', 'success')
  } catch (e: any) {
    showSnack(e?.data?.message || 'Failed to save shop record', 'error')
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
/* Put the chip's close (X) button on the left side */
.v-chip.chip-close-left :deep(.v-chip__close) {
  order: -1;
  margin-inline-start: -4px;
  margin-inline-end: 4px;
}
.cert-ref-input {
  flex: 1 1 90px;
  min-width: 90px;
}
:deep(.fixed-header-table .v-table__wrapper) {
  max-height: none !important;
  overflow-y: auto !important;
}

:deep(.fixed-header-table .v-table__wrapper > table) {
  border-collapse: separate !important;
  border-spacing: 0;
  overflow: visible !important;
}

:deep(.fixed-header-table .v-table__wrapper > table > thead) {
  position: static !important;
}

:deep(.fixed-header-table thead th) {
  position: sticky !important;
  top: 0;
  z-index: 11 !important;
  background: rgb(var(--v-theme-surface)) !important;
  box-shadow: 0 1px 0 rgba(var(--v-border-color), 0.7);
}

.proc-th-inner { display: flex; align-items: center; gap: 2px; white-space: nowrap; }
.proc-th-inner--filtered { color: rgb(var(--v-theme-primary)); font-weight: 700; }
.proc-filter-btn { opacity: 0.5; flex-shrink: 0; }
.proc-filter-btn:hover, .proc-filter-btn.v-btn--active { opacity: 1; }

:deep(th:has(.proc-th-inner--filtered)) {
  background: linear-gradient(rgba(var(--v-theme-primary), 0.14), rgba(var(--v-theme-primary), 0.14)), rgb(var(--v-theme-surface)) !important;
  box-shadow: inset 0 -2px 0 rgb(var(--v-theme-primary));
}

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
.expert-cell {
  display: inline-flex;
  align-items: center;
  font-size: 12px;
  white-space: nowrap;
  opacity: 0.8;
}

/* Read-only supplier quote cells (edited in SupplierQuoteEditDialog) */
.quote-row--readonly {
  cursor: default;
}
.quote-cell {
  display: block;
  min-height: 28px;
  line-height: 28px;
  padding: 0 6px;
  font-size: 12px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 220px;
}
.quote-cell--text {
  line-height: 1.4;
  padding: 4px 6px;
  white-space: pre-wrap;
  overflow-wrap: anywhere;
  max-height: 4.4em;
  overflow-y: auto;
  min-width: 140px;
}
.quote-cell--supplier {
  display: flex;
  align-items: center;
  max-width: none;
}
.quote-cell-email {
  font-size: 10px;
  color: #22c55e;
  padding: 0 6px 2px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 220px;
}
.price-text {
  color: #4ade80;
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
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
