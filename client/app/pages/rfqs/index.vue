<template>
  <div>
    <PageHeader title="RFQs">
      <template #actions>
        <v-btn
          v-if="isAdmin"
          prepend-icon="mdi-shield-account"
          variant="tonal"
          @click="showBulkPerms = true"
          size="small"
        >
          Perms
        </v-btn>
        <v-btn v-if="isAmir" variant="tonal" color="secondary" prepend-icon="mdi-table-arrow-down" @click="openBulkImport" size="small">
          Bulk Import
        </v-btn>
        <v-btn v-if="isAdmin || addRFQ" color="primary" prepend-icon="mdi-plus" @click="openCreateModal" size="small">
          New RFQ
        </v-btn>
      </template>
    </PageHeader>

    <!-- RFQ List -->
    <v-card class="glass-card">
      <v-card-text>
        <div class="d-flex flex-wrap gap-3 mb-4">
          <v-text-field
            v-model="search"
            prepend-inner-icon="mdi-magnify"
            label="Search RFQs..."
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
            style="min-width: 160px; max-width: 280px;"
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
        <v-data-table-server
          :headers="headers"
          :items="items"
          :items-length="totalItems"
          :loading="loading"
          v-model:page="currentPage"
          v-model:items-per-page="currentItemsPerPage"
          :items-per-page-options="pageOptions"
          hover
          :row-props="getRowProps"
          @update:options="loadServerPage"
          @click:row="goToRfq"
        >
          <!-- Column filter: ID -->
          <template #header.id="{ column, toggleSort, isSorted, sortBy }">
            <ColFilterMenu
              col-key="id"
              :label="column.title"
              :options="cfIdOptionsPage"
              :all-options="cfIdOptions"
              :selected="colFilter.selected['id'] || new Set()"
              :search="colFilter.search['id'] || ''"
              @toggle="(v) => { colFilter.toggle('id', v); debouncedCfLoad() }"
              @select-all="() => { colFilter.selectAll('id', cfIdOptions); debouncedCfLoad() }"
              @clear-all="() => { colFilter.clearAll('id'); debouncedCfLoad() }"
              @update:search="(v) => colFilter.search['id'] = v"
              @sort-click="toggleSort(column)"
            />
          </template>

          <!-- Column filter: Name -->
          <template #header.name="{ column, toggleSort, isSorted, sortBy }">
            <ColFilterMenu
              col-key="name"
              :label="column.title"
              :options="cfNameOptionsPage"
              :all-options="cfNameOptions"
              :selected="colFilter.selected['name'] || new Set()"
              :search="colFilter.search['name'] || ''"
              @toggle="(v) => { colFilter.toggle('name', v); debouncedCfLoad() }"
              @select-all="() => { colFilter.selectAll('name', cfNameOptions); debouncedCfLoad() }"
              @clear-all="() => { colFilter.clearAll('name'); debouncedCfLoad() }"
              @update:search="(v) => colFilter.search['name'] = v"
              @sort-click="toggleSort(column)"
            />
          </template>

          <!-- Column filter: Customer -->
          <template #header.customerName="{ column, toggleSort, isSorted, sortBy }">
            <ColFilterMenu
              col-key="customerName"
              :label="column.title"
              :options="cfCustomerOptionsPage"
              :all-options="cfCustomerOptions"
              :selected="colFilter.selected['customerName'] || new Set()"
              :search="colFilter.search['customerName'] || ''"
              @toggle="(v) => { colFilter.toggle('customerName', v); debouncedCfLoad() }"
              @select-all="() => { colFilter.selectAll('customerName', cfCustomerOptions); debouncedCfLoad() }"
              @clear-all="() => { colFilter.clearAll('customerName'); debouncedCfLoad() }"
              @update:search="(v) => colFilter.search['customerName'] = v"
              @sort-click="toggleSort(column)"
            />
          </template>

          <!-- Column filter: Status -->
          <template #header.status="{ column, toggleSort, isSorted, sortBy }">
            <ColFilterMenu
              col-key="status"
              :label="column.title"
              :options="cfStatusOptionsPage"
              :all-options="cfStatusOptions"
              :selected="colFilter.selected['status'] || new Set()"
              :search="colFilter.search['status'] || ''"
              @toggle="(v) => { colFilter.toggle('status', v); debouncedCfLoad() }"
              @select-all="() => { colFilter.selectAll('status', cfStatusOptions); debouncedCfLoad() }"
              @clear-all="() => { colFilter.clearAll('status'); debouncedCfLoad() }"
              @update:search="(v) => colFilter.search['status'] = v"
              @sort-click="toggleSort(column)"
            />
          </template>

          <!-- Column filter: Deadline -->
          <template #header.leadTime="{ column, toggleSort, sortBy }">
            <ColFilterMenu
              col-key="leadTime"
              :label="column.title"
              :options="cfDeadlineOptionsPage"
              :all-options="cfDeadlineOptions"
              :selected="colFilter.selected['leadTime'] || new Set()"
              :search="colFilter.search['leadTime'] || ''"
              :is-sorted="sortBy?.some((s: any) => s.key === 'leadTime')"
              :sort-desc="sortBy?.find((s: any) => s.key === 'leadTime')?.order === 'desc'"
              @toggle="(v) => { colFilter.toggle('leadTime', v); debouncedCfLoad() }"
              @select-all="() => { colFilter.selectAll('leadTime', cfDeadlineOptions); debouncedCfLoad() }"
              @clear-all="() => { colFilter.clearAll('leadTime'); debouncedCfLoad() }"
              @update:search="(v) => colFilter.search['leadTime'] = v"
              @sort-click="toggleSort(column)"
            />
          </template>

          <!-- Column filter: Assigned Users -->
          <template #header.assignedUsers="{ column }">
            <ColFilterMenu
              col-key="assignedUsers"
              :label="column.title"
              :options="cfUserOptionsPage"
              :all-options="cfUserOptions"
              :selected="colFilter.selected['assignedUsers'] || new Set()"
              :search="colFilter.search['assignedUsers'] || ''"
              @toggle="(v) => { colFilter.toggle('assignedUsers', v); debouncedCfLoad() }"
              @select-all="() => { colFilter.selectAll('assignedUsers', cfUserOptions); debouncedCfLoad() }"
              @clear-all="() => { colFilter.clearAll('assignedUsers'); debouncedCfLoad() }"
              @update:search="(v) => colFilter.search['assignedUsers'] = v"
              @sort-click="() => {}"
            />
          </template>

          <template #item.id="{ item }">
            <div class="d-flex align-center gap-1">
              <v-icon v-if="item.isUnread" icon="mdi-circle" size="10" color="blue" />
              <v-btn
                v-if="isAdmin"
                variant="text"
                size="small"
                class="pa-0 min-width-0 text-primary font-weight-bold"
                @click.stop="openAssignModal(item)"
              >
                {{ item.id }}
              </v-btn>
              <span v-else>{{ item.id }}</span>
            </div>
          </template>
          <template #item.receivedDate="{ item }">
            {{ item.receivedDate && new Date(item.receivedDate).getFullYear() > 2000 ? new Date(item.receivedDate).toLocaleDateString() : '—' }}
          </template>
          <template #item.leadTime="{ item }">
            <span :class="{ 'text-error font-weight-bold': (item.status == 'Open' || item.status == 'In Progress') && isLeadTimeExpired(item.leadTime) }">
              {{ new Date(item.leadTime).toLocaleDateString() }}
              <v-icon v-if="isLeadTimeUrgent(item.leadTime) && (item.status == 'Open' || item.status == 'In Progress')" icon="mdi-alert" size="16" color="warning" class="ml-1" title="Lead time expires within 3 days" />
              <v-icon v-else-if="isLeadTimeExpired(item.leadTime) && (item.status == 'Open' || item.status == 'In Progress')" icon="mdi-alert-circle" size="16" color="error" class="ml-1" title="Lead time has expired" />
            </span>
          </template>

          <template #header.daysRemaining="{ column, toggleSort, sortBy }">
            <div class="d-flex align-center gap-1" style="white-space: nowrap;">
              <span class="cursor-pointer d-flex align-center gap-1" @click="toggleSort(column)">
                {{ column.title }}
                <v-icon
                  v-if="sortBy?.some((s: any) => s.key === 'daysRemaining' || s.key === 'leadTime')"
                  :icon="sortBy?.find((s: any) => s.key === 'daysRemaining' || s.key === 'leadTime')?.order === 'desc' ? 'mdi-arrow-down' : 'mdi-arrow-up'"
                  size="13" color="primary"
                />
                <v-icon v-else icon="mdi-unfold-more-horizontal" size="13" style="opacity:0.3" />
              </span>
              <v-menu :close-on-content-click="false" max-width="180">
                <template #activator="{ props: mp }">
                  <v-btn
                    v-bind="mp"
                    :icon="daysFilter !== null ? 'mdi-filter' : 'mdi-filter-outline'"
                    size="x-small"
                    variant="text"
                    :color="daysFilter !== null ? 'primary' : undefined"
                    @click.stop
                  />
                </template>
                <v-card class="pa-2" min-width="160">
                  <div class="text-caption text-medium-emphasis mb-2 px-1">Filter by days left</div>
                  <div class="d-flex flex-column gap-1">
                    <v-chip
                      v-for="p in daysFilterPresets"
                      :key="p.value"
                      size="small"
                      :color="daysFilter === p.value ? 'primary' : 'default'"
                      :variant="daysFilter === p.value ? 'flat' : 'outlined'"
                      class="cursor-pointer"
                      @click="daysFilter = daysFilter === p.value ? null : p.value"
                    >
                      {{ p.label }}
                    </v-chip>
                  </div>
                  <v-btn v-if="daysFilter !== null" size="x-small" variant="text" color="error" class="mt-2" @click="daysFilter = null">
                    Clear
                  </v-btn>
                </v-card>
              </v-menu>
            </div>
          </template>

          <template #item.daysRemaining="{ item }">
            <v-chip
              v-if="item.leadTime && ['Open', 'In Progress', 'Waiting For Admin'].includes(item.status)"
              :color="remainingDaysColor(item.leadTime)"
              size="x-small"
              variant="tonal"
            >
              {{ remainingDaysLabel(item.leadTime) }}
            </v-chip>
            <span v-else class="text-medium-emphasis">—</span>
          </template>
          <!-- <template #item.priority="{ item }">
            <v-chip
              size="small"
              :color="item.priority === 'AOG' ? 'error' : item.priority === 'Urgent' ? 'warning' : 'secondary'"
              variant="tonal"
            >
              {{ item.priority || 'Normal' }}
            </v-chip>
          </template> -->
          <template #item.customerName="{ item }">
            <template v-if="isAdmin">{{ item.customerCode }}</template>
            <template v-else>{{ item.customerCode || '—' }}</template>
          </template>
          <template #item.status="{ item }">
            <v-tooltip class="background-color:#f00" v-if="item.rejectionNote || (['No Quote', 'Waiting For Admin'].includes(item.status) && item.noQuoteReason)" location="bottom">
              <template #activator="{ props: tp }">
                <v-chip
                  v-bind="tp"
                  size="small"
                  :color="rfqStatusColor(item.status)"
                  variant="tonal"
                >
                  {{ item.status }}
                  <v-icon icon="mdi-information-outline" size="14" class="ml-1" />
                </v-chip>
              </template>
              <span class="text-red">{{ item.noQuoteReason }}{{ item.rejectionNote ? ` (Admin: ${item.rejectionNote})` : '' }}</span>
            </v-tooltip>
            <v-chip
              v-else
              size="small"
              :color="rfqStatusColor(item.status || 'Open')"
              variant="tonal"
            >
              {{ item.status || 'Open' }}
            </v-chip>
          </template>
          <template #item.assignedUsers="{ item }">
            <div class="d-flex flex-wrap gap-1">
              <v-chip
                v-for="user in [...(item.views || []), ...(item.edits || [])].filter((u, i, arr) => arr.findIndex(x => x.id === u.id) === i)"
                :key="user.id"
                size="x-small"
                color="primary"
                variant="tonal"
                prepend-icon="mdi-account"
              >
                {{ user.name }}
              </v-chip>
              <span v-if="!(item.views?.length || item.edits?.length)" class="text-medium-emphasis">—</span>
            </div>
          </template>
          <template #item.itemCount="{ item }">
            <v-chip size="small" color="secondary">{{ item.itemCount || 0 }} parts</v-chip>
          </template>
          <!-- <template #item.actions="{ item }">
            <v-btn icon="mdi-eye" variant="text" size="small" :to="`/rfqs/${item.id}`" />
          </template> -->
        </v-data-table-server>
      </v-card-text>
    </v-card>

    <!-- ═══════════ Create RFQ Modal ═══════════ -->
    <v-dialog v-model="showCreate" max-width="1200" persistent scrollable>
      <v-card class="glass-card">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-file-document-plus-outline" color="primary" class="mr-2" />
          Create New RFQ
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="showCreate = false" />
        </v-card-title>

        <v-divider />

        <v-card-text class="pa-4" style="max-height: 70vh; overflow-y: auto;">
          <v-form ref="formRef">
            <!-- RFQ Name -->
            <v-text-field
              v-model="form.name"
              label="RFQ Name *"
              prepend-inner-icon="mdi-file-document-outline"
              :rules="[rules.required]"
              class="mb-3"
            />
            <div v-if="lastRfqName" class="text-caption text-primary mb-3 mt-n2 ml-9">
              <v-icon icon="mdi-history" size="14" class="mr-1" />
              Last RFQ: <span class="font-weight-bold">{{ lastRfqName }}</span>
            </div>

            <!-- Customer Name (autocomplete) — Admins see Name (Code); Users see Code only -->
            <v-combobox
              v-model="form.customerName"
              :items="customerDisplayItems"
              item-title="display"
              item-value="name"
              :label="'Customer Code *'"
              prepend-inner-icon="mdi-domain"
              :rules="[rules.required]"
              :loading="customerLoading"
              no-filter
              clearable
              return-object
              class="mb-3"
              @update:search="searchCustomers"
            >
              <template #no-data>
                <v-list-item>
                  <v-list-item-title v-if="customerSearchText.length < 3" class="text-medium-emphasis">
                    Type at least 3 characters...
                  </v-list-item-title>
                  <v-list-item-title v-else class="text-medium-emphasis">
                    "{{ customerSearchText }}" — will be created automatically
                  </v-list-item-title>
                </v-list-item>
              </template>
            </v-combobox>

            <!-- Customer Terms (Add RFQ) -->
            <v-alert
              v-if="customerTerms"
              type="info"
              variant="tonal"
              density="compact"
              icon="mdi-text-box-check-outline"
              class="mb-3 text-caption"
            >
              <div class="font-weight-bold mb-1">Customer Terms & Conditions:</div>
              <div style="white-space: pre-wrap;">{{ customerTerms }}</div>
            </v-alert>
            <!-- Received Date -->
            <v-text-field
              v-model="form.date"
              label="Received Date *"
              prepend-inner-icon="mdi-calendar"
              type="date"
              :max="today"
              :rules="[rules.required]"
              class="mb-3"
            />
            <!-- Deadline -->
            <v-text-field
              v-model="form.leadTime"
              label="Deadline *"
              prepend-inner-icon="mdi-calendar-clock"
              type="date"
              :min="today"
              :rules="[rules.required]"
              class="mb-3"
            />

            <!-- Priority -->
            

            <!-- Ex Type -->
            <v-select
              v-model="form.exType"
              :items="exTypeOptions"
              item-title="label"
              item-value="value"
              label="Ex Type"
              
              prepend-inner-icon="mdi-swap-horizontal"
              clearable
              class="d-none"
            />

            <!-- Notes -->
            <v-textarea
              v-model="form.notes"
              label="Notes"
              prepend-inner-icon="mdi-note-text-outline"
              rows="2"
              auto-grow
              class="mb-3"
            />

            <!-- ── Part Numbers Section ── -->
            <v-divider class="mb-4" />

            <div class="d-flex flex-wrap align-center gap-2 mb-3">
              <div class="d-flex align-center">
                <v-icon icon="mdi-cog-outline" color="secondary" class="mr-2" size="20" />
                <span class="text-subtitle-1 font-weight-medium">RFQ Items</span>
              </div>
              <v-spacer />
              <div class="d-flex align-center gap-2">
                <v-btn
                  prepend-icon="mdi-content-paste"
                  variant="tonal"
                  size="small"
                  color="info"
                  @click="showPasteBox = !showPasteBox"
                >
                  {{ showPasteBox ? 'Hide Paste' : 'Paste Excel' }}
                </v-btn>
                <v-text-field
                  v-model.number="partCount"
                  type="number"
                  label="# of fields"
                  density="compact"
                  variant="outlined"
                  hide-details
                  style="max-width: 80px"
                  min="1"
                  max="100"
                  @update:model-value="adjustPartFields"
                />
                <v-btn
                  icon="mdi-plus"
                  color="primary"
                  size="small"
                  variant="outlined"
                  @click="addPartRow"
                />
              </div>
            </div>

            <!-- Paste from Excel -->
            <div v-if="showPasteBox" class="mb-4">
              <p class="text-caption text-medium-emphasis mb-2">
                Copy rows from Excel and paste below. Expected columns (tab-separated):
                <strong>PartNumber &nbsp; Description &nbsp; Qty &nbsp; Condition &nbsp; Priority &nbsp; Remark &nbsp; Alt</strong>
              </p>
              <v-textarea
                v-model="pasteText"
                placeholder="Paste rows from Excel here (Ctrl+V)..."
                variant="outlined"
                density="compact"
                rows="5"
                hide-details
                class="mb-2"
                style="font-family: 'JetBrains Mono', 'Cascadia Code', monospace; font-size: 12px;"
              />
              <div class="d-flex align-center gap-2">
                <v-btn
                  prepend-icon="mdi-import"
                  color="primary"
                  size="small"
                  :loading="pasteImporting"
                  @click="importFromPaste"
                >
                  Import &amp; Check
                </v-btn>
                <v-btn
                  variant="text"
                  size="small"
                  @click="pasteText = ''; showPasteBox = false"
                >
                  Cancel
                </v-btn>
                <span v-if="pasteStatus" class="text-caption" :class="pasteStatusColor">{{ pasteStatus }}</span>
              </div>
            </div>

            <p class="text-caption text-medium-emphasis mb-3">
              Search part numbers (3+ chars). If found, description &amp; alternatives auto-fill. Otherwise a new part will be created.
            </p>

            <div
              v-for="(row, index) in form.items"
              :key="index"
              class="d-flex flex-wrap align-center gap-2 mb-3 rfq-item-row"
            >
              <span class="text-caption text-medium-emphasis" style="min-width: 24px">{{ index + 1 }}.</span>

              <!-- Part Number -->
              <v-combobox
                v-model="row.partNumber"
                :items="partSuggestions[index] || []"
                item-title="name"
                item-value="name"
                label="Part Number *"
                density="compact"
                variant="outlined"
                hide-details
                no-filter
                clearable
                return-object
                style="min-width: 160px; flex: 2;"
                :loading="partLoading[index]"
                @update:search="(val: string) => searchParts(val, index)"
                @update:model-value="(val: any) => onPartPicked(val, index)"
              >
                <template #item="{ item: suggestion, props: itemProps }">
                  <v-list-item v-bind="itemProps">
                    <template #title>
                      <div class="d-flex align-center gap-2">
                        <span class="font-weight-bold">{{ suggestion.raw.name }}</span>
                        <v-chip v-if="isAltMatch(suggestion.raw, partSearchTexts[index])" size="x-small" color="warning" variant="flat">Alt Match</v-chip>
                      </div>
                    </template>
                    <template #subtitle>
                      <div class="text-caption">
                        <div v-if="suggestion.raw.description" class="text-truncate">{{ suggestion.raw.description }}</div>
                        <div v-if="suggestion.raw.alternatives?.length" class="text-grey">
                          Alts: {{ suggestion.raw.alternatives.map((a: any) => a.name).join(', ') }}
                        </div>
                      </div>
                    </template>
                  </v-list-item>
                </template>
                <template #no-data>
                  <v-list-item>
                    <v-list-item-title
                      v-if="(partSearchTexts[index]?.length || 0) < 3"
                      class="text-medium-emphasis text-caption"
                    >
                      Type 3+ chars to search...
                    </v-list-item-title>
                    <v-list-item-title v-else class="text-medium-emphasis text-caption">
                      "{{ partSearchTexts[index] }}" — will be created
                    </v-list-item-title>
                  </v-list-item>
                </template>
              </v-combobox>

              <!-- Description -->
              <v-text-field
                v-model="row.description"
                label="Description"
                density="compact"
                variant="outlined"
                hide-details
                style="min-width: 140px; flex: 2;"
              />

              <!-- Qty -->
              <v-text-field
                v-model.number="row.qty"
                label="Qty"
                type="number"
                min="1"
                density="compact"
                variant="outlined"
                hide-details
                style="max-width: 70px;"
              />

              <!-- Condition -->
              <v-select
                v-model="row.condition"
                :items="['NE', 'OH', 'SV', 'AR']"
                label="Cond."
                density="compact"
                variant="outlined"
                hide-details
                clearable
                style="max-width: 140px;"
              />

              <v-select
              v-model="row.priority"
              :items="['Normal', 'Urgent', 'AOG']"
              label="Priority"
              density="compact"
              variant="outlined"
              hide-details

              clearable
            />

              <!-- Remark -->
              <v-text-field
                v-model="row.remark"
                label="Remark"
                density="compact"
                variant="outlined"
                hide-details
                style="min-width: 100px; flex: 1;"
              />

              <!-- Alternatives -->
              <div class="d-flex flex-wrap align-center gap-1" style="min-width: 160px; flex: 2;">
                <v-chip
                  v-for="(alt, aIdx) in row.alternatives"
                  :key="aIdx"
                  size="x-small"
                  color="warning"
                  variant="tonal"
                  closable
                  @click:close="row.alternatives.splice(aIdx, 1)"
                >
                  {{ alt }}
                </v-chip>
                <v-text-field
                  v-model="row.altInput"
                  placeholder="+ alt"
                  density="compact"
                  variant="plain"
                  hide-details
                  single-line
                  style="max-width: 80px; font-size: 11px;"
                  @keydown.enter.prevent="pushAlt(index)"
                />
              </div>

              <!-- Status chip -->
              <v-chip
                v-if="row.isExisting"
                size="x-small" color="success" variant="tonal"
              >DB</v-chip>
              <v-chip
                v-else-if="row.partNumber"
                size="x-small" color="info" variant="tonal"
              >New</v-chip>
              <span v-else style="width: 32px;"></span>

              <!-- Remove -->
              <v-btn
                v-if="form.items.length > 1"
                icon="mdi-close"
                size="x-small"
                variant="text"
                color="error"
                @click="removePartRow(index)"
              />
            </div>
          </v-form>

          <!-- Validation error -->
          <v-alert v-if="submitError" type="error" density="compact" class="mt-3">
            {{ submitError }}
          </v-alert>
        </v-card-text>

        <v-divider />

        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="showCreate = false">Cancel</v-btn>
          <v-btn color="primary" :loading="submitting" @click="submitRfq">
            Create RFQ
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- ═══════════ Bulk Import RFQs Modal ═══════════ -->
    <v-dialog v-model="showBulkImport" max-width="1100" persistent scrollable>
      <v-card class="glass-card">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-table-arrow-down" color="secondary" class="mr-2" />
          Bulk Import RFQs
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="showBulkImport = false" />
        </v-card-title>
        <v-divider />

        <v-card-text class="pa-4" style="max-height:78vh; overflow-y:auto;">
          <!-- Customer only (dates come from paste) -->
          <v-row dense class="mb-3">
            <v-col cols="12" md="5">
              <v-combobox
                v-model="bulkDefaults.customerName"
                :items="bulkCustomerDisplayItems"
                item-title="display"
                item-value="name"
                :label="isAdmin ? 'Customer Name *' : 'Customer Code *'"
                density="compact"
                variant="outlined"
                hide-details
                prepend-inner-icon="mdi-domain"
                no-filter
                clearable
                return-object
                :loading="bulkCustomerLoading"
                @update:search="searchBulkCustomers"
              />

              <!-- Customer Terms (Bulk Import) -->
              <v-alert
                v-if="bulkCustomerTerms"
                type="info"
                variant="tonal"
                density="compact"
                icon="mdi-text-box-check-outline"
                class="mt-2 text-caption"
              >
                <div class="font-weight-bold mb-1">Customer Terms & Conditions:</div>
                <div style="white-space: pre-wrap;">{{ bulkCustomerTerms }}</div>
              </v-alert>
            </v-col>
            <v-col cols="12" md="7">
              <v-alert type="info" density="compact" variant="tonal" class="text-caption">
                Received Date &amp; Deadline are read from the paste data (columns 9 &amp; 10).
              </v-alert>
            </v-col>
          </v-row>

          <!-- Paste area -->
          <p class="text-caption text-medium-emphasis mb-1">
            Paste rows from Excel (tab-separated). Columns:
            <strong>RFQ Name &nbsp;|&nbsp; # &nbsp;|&nbsp; Part Number &nbsp;|&nbsp; Description &nbsp;|&nbsp; Qty &nbsp;|&nbsp; Condition &nbsp;|&nbsp; Priority &nbsp;|&nbsp; Alt &nbsp;|&nbsp; Remark &nbsp;|&nbsp; Received Date &nbsp;|&nbsp; Deadline</strong>
            — Alt values separated by comma (,). Rows with the same RFQ Name are grouped into one RFQ.
          </p>
          <v-textarea
            v-model="bulkPasteText"
            placeholder="Paste your Excel rows here (Ctrl+V)..."
            variant="outlined"
            density="compact"
            rows="6"
            hide-details
            class="mb-3"
            style="font-family: 'JetBrains Mono', 'Cascadia Code', monospace; font-size:12px;"
          />
          <div class="d-flex align-center gap-2 mb-4">
            <v-btn prepend-icon="mdi-table-search" color="primary" size="small" @click="parseBulkRFQs">
              Parse &amp; Preview
            </v-btn>
            <v-btn variant="text" size="small" @click="bulkPasteText = ''; bulkGroups = []">Clear</v-btn>
            <span v-if="bulkParseMsg" class="text-caption" :class="bulkParseMsgColor">{{ bulkParseMsg }}</span>
          </div>

          <!-- Preview -->
          <div v-if="bulkGroups.length">
            <div class="text-subtitle-2 mb-2">
              Preview — {{ bulkGroups.filter(g => !g.isDuplicate).length }} RFQ(s) will be created,
              <span class="text-error">{{ bulkGroups.filter(g => g.isDuplicate).length }} skipped (duplicate name)</span>
            </div>

            <v-expansion-panels variant="accordion" multiple>
              <v-expansion-panel
                v-for="(group, gi) in bulkGroups"
                :key="gi"
              >
                <v-expansion-panel-title>
                  <div class="d-flex align-center gap-2 w-100 flex-wrap">
                    <v-icon
                      :icon="group.isDuplicate ? 'mdi-alert-circle' : 'mdi-check-circle'"
                      :color="group.isDuplicate ? 'error' : 'success'"
                      size="18"
                    />
                    <span class="font-weight-medium">{{ group.rfqName }}</span>
                    <v-chip size="x-small" color="secondary" variant="tonal">{{ group.items.length }} parts</v-chip>
                    <span v-if="group.receivedDate" class="text-caption text-medium-emphasis">
                      {{ group.receivedDate }} → {{ group.deadline }}
                    </span>
                    <v-chip v-if="group.isDuplicate" size="x-small" color="error" variant="tonal">DUPLICATE — will be skipped</v-chip>
                  </div>
                </v-expansion-panel-title>
                <v-expansion-panel-text>
                  <v-table density="compact" class="text-caption">
                    <thead>
                      <tr>
                        <th>#</th>
                        <th>Part Number</th>
                        <th>Description</th>
                        <th>Qty</th>
                        <th>Condition</th>
                        <th>Priority</th>
                        <th>Alternatives</th>
                        <th>Remark</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr v-for="(row, ri) in group.items" :key="ri">
                        <td>{{ ri + 1 }}</td>
                        <td class="font-weight-medium">{{ row.partNumber }}</td>
                        <td>{{ row.description || '—' }}</td>
                        <td>{{ row.qty }}</td>
                        <td>{{ row.condition || '—' }}</td>
                        <td>{{ row.priority || '—' }}</td>
                        <td>
                          <div class="d-flex flex-wrap gap-1">
                            <v-chip v-for="alt in row.alternatives" :key="alt" size="x-small" color="warning" variant="tonal">{{ alt }}</v-chip>
                            <span v-if="!row.alternatives.length" class="text-medium-emphasis">—</span>
                          </div>
                        </td>
                        <td>{{ row.remark || '—' }}</td>
                      </tr>
                    </tbody>
                  </v-table>
                </v-expansion-panel-text>
              </v-expansion-panel>
            </v-expansion-panels>
          </div>

          <v-alert v-if="bulkSubmitError" type="error" density="compact" class="mt-3">{{ bulkSubmitError }}</v-alert>
        </v-card-text>

        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="showBulkImport = false">Cancel</v-btn>
          <v-btn
            color="primary"
            :loading="bulkImporting"
            :disabled="!bulkGroups.filter(g => !g.isDuplicate).length"
            prepend-icon="mdi-cloud-upload"
            @click="submitBulkRFQs"
          >
            Import {{ bulkGroups.filter(g => !g.isDuplicate).length }} RFQ(s)
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Quick Assignment Modal (Admin only, triggered by ID click) -->
    <v-dialog v-model="showQuickAssign" max-width="800" scrollable>
      <v-card class="glass-card">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-shield-account-outline" color="primary" class="mr-2" />
          Quick Assignment: RFQ #{{ assigningRfqData?.id }}
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="showQuickAssign = false" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <div v-if="loadingAssignItems" class="text-center py-6">
            <v-progress-circular indeterminate color="primary" />
            <p class="text-caption mt-2">Loading details...</p>
          </div>
          <div v-else>
            <!-- RFQ Items Table -->
            <div class="text-subtitle-2 mb-2">RFQ Items:</div>
            <v-table density="compact" class="mb-6 border rounded overflow-hidden quick-assign-table">
              <thead>
                <tr class="quick-assign-header">
                  <th>Part Number</th>
                  <th>Description</th>
                  <th class="text-center">Qty</th>
                  <th>Condition</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="item in assigningRfqData?.items" :key="item.id">
                  <td class="font-weight-medium">{{ item.partNumberName }}</td>
                  <td class="font-weight-medium">{{ item.description }}</td>
                  <td class="text-center">{{ item.qty }}</td>
                  <td>{{ item.condition || '—' }}</td>
                </tr>
              </tbody>
            </v-table>

            <!-- User Assignment Checkboxes -->
            <div class="text-subtitle-2 mb-2">Assign Users (Edit Permission):</div>
            <v-row dense>
              <v-col v-for="u in quickAssignUsers" :key="u.id" cols="12" sm="4" md="3">
                <v-checkbox
                  v-model="selectedQuickUsers"
                  :label="u.name || u.username"
                  :value="u.id"
                  color="primary"
                  density="compact"
                  hide-details
                />
              </v-col>
            </v-row>
          </div>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <!-- No Quote button — only for Open / In Progress RFQs -->
          <v-btn
            v-if="['Open', 'In Progress'].includes(assigningRfqData?.status)"
            color="deep-purple"
            variant="tonal"
            prepend-icon="mdi-cancel"
            :disabled="loadingAssignItems"
            @click="showQuickNoQuote = true"
          >
            No Quote
          </v-btn>
          <v-spacer />
          <v-btn variant="text" @click="showQuickAssign = false">Cancel</v-btn>
          <v-btn
            color="primary"
            variant="flat"
            :loading="savingQuickAssign"
            :disabled="loadingAssignItems"
            @click="saveQuickAssign"
          >
            Save Assignments
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- No Quote confirmation (from Quick Assign Modal) -->
    <v-dialog v-model="showQuickNoQuote" max-width="480" persistent>
      <v-card class="glass-card">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-cancel" color="deep-purple" class="mr-2" />
          Mark as No Quote?
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="showQuickNoQuote = false" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <p class="text-body-2 mb-3">
            This will set <strong>RFQ #{{ assigningRfqData?.id }}</strong> to <strong>No Quote</strong>
            and prevent any new quote from being created for it.
          </p>
          <v-textarea
            v-model="quickNoQuoteReason"
            label="Reason *"
            placeholder="Why is this RFQ being marked as No Quote?"
            variant="outlined"
            density="compact"
            rows="3"
            hide-details
            auto-grow
          />
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="showQuickNoQuote = false">Cancel</v-btn>
          <v-btn
            color="deep-purple"
            variant="flat"
            :loading="quickNoQuoteLoading"
            :disabled="!quickNoQuoteReason.trim()"
            @click="doQuickNoQuote"
          >
            Confirm No Quote
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Bulk Permission Manager -->
    <BulkPermissionManager v-model="showBulkPerms" entity-name="RFQ" @update:model-value="(v) => !v && loadServerPage()" />
  </div>
</template>

<script setup lang="ts">
import { VTextField } from 'vuetify/components'

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

const api = useApi()
const authStore = useAuthStore()
const router = useRouter()
const route = useRoute()

const today = new Date().toISOString().split('T')[0]
const { statusColor: rfqStatusColor } = useStatusColor()
const { filters: pf, clearFilters, hasActiveFilters } = usePageFilters('rfqs', {
  search: '',
  status: [] as string[],
  user: [] as number[],
  pnSearch: '',
  customer: [] as string[],
  page: 1,
  itemsPerPage: 50,
})
// If URL has ?status=X, apply it once on load
if (route.query.status && pf.status.value.length === 0) {
  pf.status.value = [route.query.status as string]
}

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
  if (item.isUnread) classes.push('unread-rfq-row')
  if (isLeadTimeExpired(item.leadTime) && (item.status == 'Open' || item.status == 'In Progress')) classes.push('lead-time-expired-row')
  return classes.length ? { class: classes.join(' ') } : {}
}

const isAdmin = computed(() => authStore.isAdmin)
const addRFQ = computed(() => authStore.newRFQ)
const isAmir = computed(() => authStore.isAmir)
const showBulkPerms = ref(false)

// ── List state ──
const search = pf.search
const currentPage = pf.page           // top-level ref so Vue auto-unwraps in template
const currentItemsPerPage = pf.itemsPerPage
const loading = ref(false)
const items = ref<any[]>([])
const totalItems = ref(0)
const sort = useServerSort()
const showNoQuote = ref(false)
const daysFilter = ref<number | null>(null)

const daysFilterPresets = [
  { label: 'Overdue', value: 0 },
  { label: '≤ 1 day', value: 1 },
  { label: '≤ 3 days', value: 3 },
  { label: '≤ 7 days', value: 7 },
  { label: '≤ 14 days', value: 14 },
  { label: '≤ 30 days', value: 30 },
]
const statusFilter = pf.status
const userFilter = pf.user
const pnSearch = pf.pnSearch
const customerFilter = pf.customer

// Helper: deduplicated list of assigned users (views + edits) for one RFQ item
function assignedOf(item: any): { id: number; name: string }[] {
  const seen = new Set<number>()
  const out: { id: number; name: string }[] = []
  for (const u of [...(item.views || []), ...(item.edits || [])]) {
    if (u.id && !seen.has(u.id)) { seen.add(u.id); out.push({ id: u.id, name: u.name }) }
  }
  return out
}

const ALL_RFQ_STATUSES = ['Open', 'In Progress', 'Waiting For Admin', 'Ready To Quote', 'Sent', 'Accepted', 'Rejected', 'No Quote']
const rfqStatusOptions = ref<string[]>([...ALL_RFQ_STATUSES])
const showAllStatuses = ref(false)
const statusSelectItems = computed(() => {
  const available = new Set(rfqStatusOptions.value)
  if (showAllStatuses.value) {
    return ALL_RFQ_STATUSES.map(s => ({ label: s, value: s, available: available.has(s) }))
  }
  return ALL_RFQ_STATUSES
    .filter(s => available.has(s) || (statusFilter.value as string[]).includes(s))
    .map(s => ({ label: s, value: s, available: true }))
})
const userOptions = ref<{ id: number; name: string }[]>([])
const customerOptions = ref<{ title: string; value: string }[]>([])
const allUserOptions = ref<{ id: number; name: string }[]>([])
const allCustomerOptions = ref<{ title: string; value: string }[]>([])
const showAllUsers = ref(false)
const showAllCustomers = ref(false)

// ── Column filters (header menus) ──
const colFilter = useColFilterPersisted('rfqs')
// "All" options — full DB / hardcoded lists (shown when showAll=true in ColFilterMenu)
const cfStatusOptions = computed(() => ALL_RFQ_STATUSES)
const cfCustomerOptions = computed(() => allCustomerOptions.value.map((c: any) => c.title).sort())
const cfUserOptions = computed(() => allUserOptions.value.map((u: any) => u.name).sort())
const cfIdOptions = computed(() => [...new Set(items.value.map((i: any) => String(i.id)).filter(Boolean))].sort((a, b) => Number(b) - Number(a)))
const cfNameOptions = computed(() => [...new Set(items.value.map((i: any) => i.name).filter(Boolean))].sort())

// "Available" options — only values present in the current 50-row page (shown by default)
const cfStatusOptionsPage = computed(() =>
  [...new Set(items.value.map((i: any) => i.status).filter(Boolean))].sort() as string[]
)
const cfCustomerOptionsPage = computed(() =>
  [...new Set(items.value.map((i: any) => i.customerCode || '-').filter(Boolean))].sort() as string[]
)
const cfUserOptionsPage = computed(() =>
  [...new Set(
    items.value.flatMap((i: any) => [...(i.views || []), ...(i.edits || [])])
      .map((u: any) => u.name).filter(Boolean)
  )].sort() as string[]
)
const cfIdOptionsPage = computed(() => cfIdOptions.value)
const cfNameOptionsPage = computed(() => cfNameOptions.value)

// Deadline (leadTime) column filter — ISO date part (YYYY-MM-DD) as option value
const cfDeadlineOptions = computed(() =>
  [...new Set(items.value.map((i: any) => i.leadTime ? new Date(i.leadTime).toISOString().substring(0, 10) : null).filter(Boolean))].sort() as string[]
)
const cfDeadlineOptionsPage = computed(() => cfDeadlineOptions.value)

function collectCfOptions(_loadedItems: any[]) {
  // No-op: page-level options are computed reactively from items.value
}

let cfDebounce: any = null
function debouncedCfLoad() {
  clearTimeout(cfDebounce)
  cfDebounce = setTimeout(() => loadServerPage({ ...lastServerOpts.value, page: 1 }), 200)
}

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
    const res = await api.get<any>(`/rfqs/filter-options?${params}`)

    rfqStatusOptions.value = res.statuses?.length
      ? [...new Set([...ALL_RFQ_STATUSES, ...res.statuses])]
      : [...ALL_RFQ_STATUSES]

    userOptions.value = (res.users || [])
      .map((u: any) => ({ id: u.id, name: u.name }))
      .sort((a: any, b: any) => a.name.localeCompare(b.name))

    customerOptions.value = (res.customers || [])
      .map((c: any) => ({ title: c.code || '-', value: c.name }))
      .sort((a: any, b: any) => a.title.localeCompare(b.title))

    // Cache the full list from the first (no-filter) call
    if (!allUserOptions.value.length) allUserOptions.value = [...userOptions.value]
    if (!allCustomerOptions.value.length) allCustomerOptions.value = [...customerOptions.value]
  } catch {}
}

/** Unconstraint call — always loads ALL customers and users from the full DB. */
async function loadAllFilterOptions() {
  try {
    const res = await api.get<any>('/rfqs/filter-options')
    userOptions.value = (res.users || [])
      .map((u: any) => ({ id: u.id, name: u.name }))
      .sort((a: any, b: any) => a.name.localeCompare(b.name))
    allUserOptions.value = [...userOptions.value]
    customerOptions.value = (res.customers || [])
      .map((c: any) => ({ title: c.code || '-', value: c.name }))
      .sort((a: any, b: any) => a.title.localeCompare(b.title))
    allCustomerOptions.value = [...customerOptions.value]
  } catch {}
}

function debouncedFilterOptions() {
  clearTimeout(filterOptsDebounce)
  filterOptsDebounce = setTimeout(loadFilterOptions, 200)
}

watch(statusFilter, debouncedFilterOptions, { deep: true })
watch(userFilter, debouncedFilterOptions, { deep: true })
watch(customerFilter, debouncedFilterOptions, { deep: true })

const headers = [
  { title: 'ID', key: 'id', width: '80px' },
  { title: 'Name', key: 'name' },
  { title: 'Customer', key: 'customerName' },
  { title: 'Status', key: 'status', width: '110px' },
  { title: 'Assigned Users', key: 'assignedUsers', sortable: false },
  // { title: 'Priority', key: 'priority', width: '100px' },
  { title: 'Deadline', key: 'leadTime' },
  { title: 'Days', key: 'daysRemaining', sortable: true, width: '110px' },
  { title: 'Parts', key: 'itemCount', sortable: false },
  { title: 'Received Date', key: 'receivedDate' },
]

const lastServerOpts = ref<any>({ page: pf.page.value, itemsPerPage: pf.itemsPerPage.value })

async function loadServerPage(opts?: any) {
  if (opts) {
    sort.capture(opts)
    // daysRemaining is a virtual column — sort it as leadTime on the backend
    if (sort.sortKey.value === 'daysRemaining') sort.sortKey.value = 'leadTime'
    lastServerOpts.value = { page: opts.page ?? lastServerOpts.value.page, itemsPerPage: opts.itemsPerPage ?? lastServerOpts.value.itemsPerPage }
    pf.page.value = lastServerOpts.value.page
    currentItemsPerPage.value = lastServerOpts.value.itemsPerPage
  }
  const { page, itemsPerPage } = lastServerOpts.value
  loading.value = true
  try {
    const params = new URLSearchParams({ page: String(page), pageSize: String(itemsPerPage) })
    sort.appendTo(params)
    if (search.value?.trim()) params.set('search', search.value.trim())
    if (pnSearch.value?.trim()) params.set('pnSearch', pnSearch.value.trim())
    if (statusFilter.value?.length)
      (statusFilter.value as string[]).forEach((s: string) => params.append('statuses', s))
    if (showNoQuote.value) params.set('includeNoQuote', 'true')
    if (userFilter.value?.length) (userFilter.value as number[]).forEach((id: number) => params.append('userIds', String(id)))
    if (customerFilter.value?.length) (customerFilter.value as string[]).forEach((c: string) => params.append('customerSearch', c))
    // Column header filters
    if (colFilter.isActive('customerName')) colFilter.getSelected('customerName').forEach(v => params.append('customerSearch', v))
    if (colFilter.isActive('status')) colFilter.getSelected('status').forEach(v => params.append('statuses', v))
    if (colFilter.isActive('assignedUsers')) {
      // Map user names to IDs using allUserOptions
      const nameToId = new Map(allUserOptions.value.map(u => [u.name, u.id]))
      colFilter.getSelected('assignedUsers').forEach(name => {
        const id = nameToId.get(name)
        if (id) params.append('userIds', String(id))
      })
    }
    if (colFilter.isActive('id')) colFilter.getSelected('id').forEach(v => params.append('rfqIds', v))
    if (colFilter.isActive('name')) colFilter.getSelected('name').forEach(v => params.append('rfqNames', v))
    if (colFilter.isActive('leadTime')) colFilter.getSelected('leadTime').forEach(v => params.append('deadlines', v))
    if (daysFilter.value !== null) params.set('maxDays', String(daysFilter.value))
    const res = await api.get<any>(`/rfqs?${params.toString()}`)
    items.value = res.items ?? res.Items ?? []
    totalItems.value = res.totalCount ?? res.TotalCount ?? items.value.length
    collectCfOptions(items.value)
  } catch {}
  finally { loading.value = false }
}

let rfqDebounce: any = null
function debouncedLoad() {
  clearTimeout(rfqDebounce)
  rfqDebounce = setTimeout(() => loadServerPage({ ...lastServerOpts.value, page: 1 }), 350)
}

watch(search, debouncedLoad)
watch(pnSearch, debouncedLoad)
watch(statusFilter, () => loadServerPage({ ...lastServerOpts.value, page: 1 }), { deep: true })
watch(userFilter, () => loadServerPage({ ...lastServerOpts.value, page: 1 }), { deep: true })
watch(customerFilter, () => loadServerPage({ ...lastServerOpts.value, page: 1 }), { deep: true })
watch(showNoQuote, () => loadServerPage({ ...lastServerOpts.value, page: 1 }))
watch(daysFilter, () => loadServerPage({ ...lastServerOpts.value, page: 1 }))

function goToRfq(pointerEvent: Event, rowData: { item: any }) {
  if (rowData && rowData.item && rowData.item.id) {
    navigateTo(`/rfqs/${rowData.item.id}`)
  }
}

function isAltMatch(part: any, search: string) {
  if (!search || search.length < 2) return false
  const q = search.toLowerCase()
  // If the main name matches, it's not an "Alt Match" (it's a primary match)
  if (part.name?.toLowerCase().includes(q)) return false
  // Check alternatives
  return (part.alternatives || []).some((a: any) => a.name?.toLowerCase().includes(q))
}

onMounted(() => {
  loadFilterOptions()
  loadAllFilterOptions()
})

// ── Quick Assignment Modal state ──
const showQuickAssign = ref(false)
const assigningRfqData = ref<any>(null)
const loadingAssignItems = ref(false)
const quickAssignUsers = ref<any[]>([])
const selectedQuickUsers = ref<number[]>([])
const savingQuickAssign = ref(false)

// ── No Quote (from Quick Assign Modal) ──
const showQuickNoQuote = ref(false)
const quickNoQuoteReason = ref('')
const quickNoQuoteLoading = ref(false)

async function doQuickNoQuote() {
  if (!assigningRfqData.value) return
  quickNoQuoteLoading.value = true
  try {
    await api.patch(`/rfqs/${assigningRfqData.value.id}/status`, {
      status: 'No Quote',
      noQuoteReason: quickNoQuoteReason.value.trim(),
    })
    showQuickNoQuote.value = false
    showQuickAssign.value = false
    quickNoQuoteReason.value = ''
    await loadServerPage()
  } catch (e) {
    console.error('Failed to set No Quote', e)
  } finally {
    quickNoQuoteLoading.value = false
  }
}

async function openAssignModal(rfq: any) {
  assigningRfqData.value = rfq
  selectedQuickUsers.value = []
  showQuickAssign.value = true
  loadingAssignItems.value = true

  try {
    // 1. Fetch current assignments to pre-check (only if they have 'Edit' permission)
    const perms = await api.get<any[]>(`/permissions/RFQ/${rfq.id}`)
    selectedQuickUsers.value = perms.filter(p => p.permission === 'Edit').map(p => p.userId)

    // 2. Fetch full RFQ details to get items
    const fullRfq = await api.get<any>(`/rfqs/${rfq.id}`)
    assigningRfqData.value = { ...rfq, items: fullRfq.items || [] }

    // 3. Ensure we have the target user list (only once)
    if (!quickAssignUsers.value.length) {
      const all = await api.get<any[]>('/users')
      const allowed = ['GHS', 'MOR', 'MRD', 'SYD', 'AMJ', 'SHBN', 'MGH', 'AHM']
      quickAssignUsers.value = all.filter(u => allowed.includes(u.name) || allowed.includes(u.username))
    }
  } catch (e) {
    console.error('Failed to load quick assign data', e)
  } finally {
    loadingAssignItems.value = false
  }
}

async function saveQuickAssign() {
  if (!assigningRfqData.value) return
  savingQuickAssign.value = true

  try {
    const rfqId = assigningRfqData.value.id
    // Simple approach: revoke all current 'Edit' perms then assign selected ones
    // Or just assign ones that aren't there. The permission API usually handles upserts.
    const promises = quickAssignUsers.value.map(user => {
      const isSelected = selectedQuickUsers.value.includes(user.id)
      if (isSelected) {
        return api.post('/permissions/assign', {
          userId: user.id,
          entityName: 'RFQ',
          entityId: String(rfqId),
          permission: 'Edit'
        })
      } else {
        // If not selected, we don't necessarily revoke unless requested. 
        // But the user's flow usually implies "these are the assigned editors".
        // To keep it safe and surgical, we'll only assign.
        return Promise.resolve()
      }
    })

    await Promise.all(promises)
    showQuickAssign.value = false
    loadServerPage() // Refresh list to show new chips
  } catch (e) {
    console.error('Failed to save quick assign', e)
  } finally {
    savingQuickAssign.value = false
  }
}

// ── Create Modal state ──
const showCreate = ref(false)
const formRef = ref<any>(null)
const submitting = ref(false)
const submitError = ref('')
const partCount = ref(10)

interface ItemRow {
  partNumber: any
  description: string
  qty: number
  condition: string
  remark: string
  priority: string
  alternatives: string[]
  altInput: string
  isExisting: boolean
}

function makeEmptyRow(): ItemRow {
  return { partNumber: null, description: '', qty: 1, condition: '', remark: '',priority: '' as string, alternatives: [], altInput: '', isExisting: false }
}

const exTypeOptions = [
  { label: 'Ex Warehouse', value: 0 },
  { label: 'Vendor/Customer', value: 1 },
]

// Display helper: maps stored exType value to label (1 and 2 both show Vendor/Customer)
function exTypeLabel(val: number | null | undefined): string {
  if (val === 0) return 'Ex Warehouse'
  if (val === 1 || val === 2) return 'Vendor/Customer'
  return 'Not Set'
}

const form = ref({
  name: '',
  customerName: null as any,
  leadTime: '',
  date: '',
  notes: '',
  exType: null as number | null,
  items: Array.from({ length: 10 }, makeEmptyRow) as ItemRow[],
})

const rules = {
  required: (v: any) => !!v || 'This field is required',
}

// ── Customer Autocomplete ──
const customerSuggestions = ref<any[]>([])
const customerLoading = ref(false)
const customerSearchText = ref('')
const customerTerms = ref<string | null>(null)
const lastRfqName = ref<string | null>(null)
let customerDebounce: any = null

// Display-only mapping: Admins see "Name (Code)"; non-admins (User role) see ONLY the code.
// The stored `name` is preserved as `item-value` so submission logic stays unchanged.
const customerDisplayItems = computed(() =>
  customerSuggestions.value.map((c: any) => ({
    ...c,
    display: isAdmin.value
      ? (c.customerCode)
      // Non-admin (User role): show ONLY the customer code. If a customer has no code,
      // show a placeholder rather than leak the name.
      : (c.customerCode || '— No code —'),
  }))
)

function searchCustomers(val: string) {
  customerSearchText.value = val || ''
  clearTimeout(customerDebounce)
  if (!val || val.length < 1) {
    customerSuggestions.value = []
    return
  }
  customerDebounce = setTimeout(async () => {
    customerLoading.value = true
    try {
      customerSuggestions.value = await api.get<any[]>(`/customers/search?q=${encodeURIComponent(val)}`)
    } catch {}
    finally { customerLoading.value = false }
  }, 300)
}

// Auto-set ExType and Terms when customer changes
watch(() => form.value.customerName, async (newVal: any) => {
  customerTerms.value = null
  lastRfqName.value = null
  if (!newVal) return
  
  // If it's a string, only fetch if it's one of the already-loaded suggestions (meaning it's a known customer)
  const isSelected = typeof newVal === 'object' && newVal.name
  const name = isSelected ? newVal.name : newVal
  if (!name || (!isSelected && !customerSuggestions.value.some(s => s.name === name))) return

  try {
    const [cust, lastRfq] = await Promise.all([
      api.get<any>(`/customers/by-name?name=${encodeURIComponent(name)}`),
      api.get<string | null>(`/rfqs/last-name?customerName=${encodeURIComponent(name)}`)
    ])

    if (cust) {
      if (cust.exWork != null) form.value.exType = cust.exWork
      customerTerms.value = cust.termsAndConditions || null
    }
    if (lastRfq) {
      lastRfqName.value = lastRfq
    }
  } catch (err) { }
})

// ── Part Number Autocomplete ──
const partSuggestions = ref<Record<number, any[]>>({})
const partLoading = ref<Record<number, boolean>>({})
const partSearchTexts = ref<Record<number, string>>({})
const partDebounces: Record<number, any> = {}

function searchParts(val: string, index: number) {
  partSearchTexts.value[index] = val || ''
  clearTimeout(partDebounces[index])
  if (!val || val.length < 1) {
    partSuggestions.value[index] = []
    return
  }
  partDebounces[index] = setTimeout(async () => {
    partLoading.value[index] = true
    try {
      partSuggestions.value[index] = await api.get<any[]>(`/partnumbers/search?q=${encodeURIComponent(val)}`)
    } catch {}
    finally { partLoading.value[index] = false }
  }, 300)
}

function onPartPicked(val: any, index: number) {
  const row = form.value.items[index]
  if (!row) return
  if (!val) {
    row.isExisting = false
    row.description = ''
    row.remark = ''
    row.alternatives = []
    return
  }
  if (typeof val === 'object' && val.id) {
    row.isExisting = true
    row.description = val.description || ''
    row.remark = val.remark || ''
    row.alternatives = (val.alternatives || []).map((a: any) => a.name)
  } else {
    row.isExisting = false
  }
}

function pushAlt(index: number) {
  const row = form.value.items[index]
  if (!row) return
  const val = row.altInput.trim()
  if (!val) return
  if (!row.alternatives.includes(val)) {
    row.alternatives.push(val)
  }
  row.altInput = ''
}

// ── Part row management ──
function adjustPartFields(val: string | number) {
  val = Number(val)
  const count = Math.max(1, Math.min(100, val || 1))
  partCount.value = count
  const current = form.value.items.length
  if (count > current) {
    for (let i = 0; i < count - current; i++) form.value.items.push(makeEmptyRow())
  } else if (count < current) {
    form.value.items.splice(count)
  }
}

function addPartRow() {
  form.value.items.push(makeEmptyRow())
  partCount.value = form.value.items.length
}

function removePartRow(index: number) {
  form.value.items.splice(index, 1)
  partCount.value = form.value.items.length
}

// ── Paste from Excel ──
const showPasteBox = ref(false)
const pasteText = ref('')
const pasteImporting = ref(false)
const pasteStatus = ref('')
const pasteStatusColor = ref('text-medium-emphasis')

async function importFromPaste() {
  const text = pasteText.value.trim()
  if (!text) {
    pasteStatus.value = 'Nothing to import — paste some rows first.'
    pasteStatusColor.value = 'text-warning'
    return
  }

  pasteImporting.value = true
  pasteStatus.value = 'Parsing...'
  pasteStatusColor.value = 'text-medium-emphasis'

  try {
    const lines = text.split(/\r?\n/).filter(l => l.trim())
    if (lines.length === 0) {
      pasteStatus.value = 'No valid rows found.'
      pasteStatusColor.value = 'text-error'
      return
    }

    // Detect header row
    const firstLine = (lines.length > 0 ? (lines[0] || '') : '').toLowerCase()
    const hasHeader = firstLine.includes('partnumber') || firstLine.includes('part number') || firstLine.includes('part_number') || firstLine.includes('p/n')
    const startIdx = hasHeader ? 1 : 0

    const rows: ItemRow[] = []
    for (let i = startIdx; i < lines.length; i++) {
      const line = lines[i]
      if (!line) continue
      // Excel copies as tab-separated
      const cols = line.split('\t')
      if (cols.length === 0 || !(cols[0] || '').trim()) continue

      const partNumber = (cols[0] || '').trim()
      const description = (cols[1] || '').trim()
      const qty = parseInt((cols[2] || '').trim()) || 1
      const condition = (cols[3] || '').trim().toUpperCase()
      const priority = (cols[4] || '').trim()
      const remark = (cols[5] || '').trim()
      const alt = (cols[6] || '').trim()

      const alternatives = alt ? alt.split(';').map(a => a.trim()).filter(Boolean) : []

      rows.push({
        partNumber,
        description,
        qty,
        condition,
        remark,
        priority,
        alternatives,
        altInput: '',
        isExisting: false,
      })
    }

    if (rows.length === 0) {
      pasteStatus.value = 'No valid rows found.'
      pasteStatusColor.value = 'text-error'
      return
    }

    // Search each part number in the DB and enrich
    pasteStatus.value = `Checking ${rows.length} part(s) against database...`
    let foundCount = 0

    for (const row of rows) {
      const pn = typeof row.partNumber === 'string' ? row.partNumber : ''
      if (!pn || pn.length < 2) continue

      try {
        const results = await api.get<any[]>(`/partnumbers/search?q=${encodeURIComponent(pn)}`)
        // Find exact match (case-insensitive)
        const match = (results || []).find((r: any) => r.name?.toLowerCase() === pn.toLowerCase())

        if (match) {
          foundCount++
          row.partNumber = { id: match.id, name: match.name, description: match.description, remark: match.remark, alternatives: match.alternatives }
          row.isExisting = true

          // Import description from DB if the pasted description is empty
          if (!row.description && match.description) {
            row.description = match.description
          }


          // Import remark from DB if the pasted remark is empty
          if (!row.remark && match.remark) {
            row.remark = match.remark
          }

          // Merge alternatives: keep pasted ones + add DB ones that aren't already listed
          const existingAlts = (match.alternatives || []).map((a: any) => a.name)
          for (const dbAlt of existingAlts) {
            if (!row.alternatives.some(a => a.toLowerCase() === dbAlt.toLowerCase())) {
              row.alternatives.push(dbAlt)
            }
          }
        }
      } catch {
        // Search failed for this part, leave as new
      }
    }

    form.value.items = rows
    partCount.value = rows.length
    submitError.value = ''

    pasteStatus.value = `Imported ${rows.length} row(s) — ${foundCount} found in DB, ${rows.length - foundCount} new.`
    pasteStatusColor.value = 'text-success'

    // Auto-hide paste box after successful import
    setTimeout(() => { showPasteBox.value = false }, 2000)
  } catch (e) {
    pasteStatus.value = 'Import failed.'
    pasteStatusColor.value = 'text-error'
  } finally {
    pasteImporting.value = false
  }
}

// ── Bulk Import RFQs ──
const showBulkImport = ref(false)
const bulkPasteText = ref('')

interface BulkRFQItem { partNumber: string; description: string; qty: number; condition: string; priority: string; remark: string; alternatives: string[] }
interface BulkRFQGroup { rfqName: string; isDuplicate: boolean; receivedDate: string; deadline: string; items: BulkRFQItem[] }

const bulkGroups = ref<BulkRFQGroup[]>([])
const bulkImporting = ref(false)
const bulkParseMsg = ref('')
const bulkParseMsgColor = ref('text-medium-emphasis')
const bulkSubmitError = ref('')

const bulkDefaults = ref({ customerName: null as any })
const bulkCustomerSuggestions = ref<any[]>([])
const bulkCustomerLoading = ref(false)
const bulkCustomerTerms = ref<string | null>(null)
const bulkCustomerExWork = ref<number | null>(null)
let bulkCustomerDebounce: any = null

// Same display rule for the bulk-import customer combobox.
const bulkCustomerDisplayItems = computed(() =>
  bulkCustomerSuggestions.value.map((c: any) => ({
    ...c,
    display: isAdmin.value
      ? (c.customerCode)
      // Non-admin (User role): show ONLY the customer code. If a customer has no code,
      // show a placeholder rather than leak the name.
      : (c.customerCode || '— No code —'),
  }))
)

function searchBulkCustomers(val: string) {
  clearTimeout(bulkCustomerDebounce)
  if (!val || val.length < 1) { bulkCustomerSuggestions.value = []; return }
  bulkCustomerDebounce = setTimeout(async () => {
    bulkCustomerLoading.value = true
    try {
      const result = await api.get<any[]>(`/customers/search?q=${encodeURIComponent(val)}`)
      bulkCustomerSuggestions.value = Array.isArray(result) ? result : []
    } catch {
      bulkCustomerSuggestions.value = []
    } finally { bulkCustomerLoading.value = false }
  }, 300)
}

// Auto-set Terms and ExWork when bulk customer changes
watch(() => bulkDefaults.value.customerName, async (newVal: any) => {
  bulkCustomerTerms.value = null
  bulkCustomerExWork.value = null
  if (!newVal) return

  const isSelected = typeof newVal === 'object' && newVal.name
  const name = isSelected ? newVal.name : newVal
  if (!name || (!isSelected && !bulkCustomerSuggestions.value.some(s => s.name === name))) return

  try {
    const cust = await api.get<any>(`/customers/by-name?name=${encodeURIComponent(name)}`)
    if (cust) {
      bulkCustomerTerms.value = cust.termsAndConditions || null
      bulkCustomerExWork.value = cust.exWork ?? null
    }
  } catch (err) { }
})

function openBulkImport() {
  bulkPasteText.value = ''
  bulkGroups.value = []
  bulkParseMsg.value = ''
  bulkSubmitError.value = ''
  bulkDefaults.value = { customerName: null }
  bulkCustomerTerms.value = null
  showBulkImport.value = true
}

function parseDateToISO(raw: string): string {
  // Handles M/D/YYYY or MM/DD/YYYY
  if (!raw) return ''
  const parts = raw.split('/')
  if (parts.length === 3) {
    const [m, d, y] = parts
    if (!y || !m || !d) return raw
    return `${y.trim()}-${m.trim().padStart(2, '0')}-${d.trim().padStart(2, '0')}`
  }
  return raw
}

function parseBulkRFQs() {
  bulkParseMsg.value = ''
  bulkGroups.value = []
  const text = bulkPasteText.value.trim()
  if (!text) {
    bulkParseMsg.value = 'Nothing to parse — paste some rows first.'
    bulkParseMsgColor.value = 'text-warning'
    return
  }

  const lines = text.split(/\r?\n/).filter(l => l.trim())
  // Detect header row
  const first = (lines[0] || '').toLowerCase()
  const hasHeader = first.includes('rfq') || first.includes('part') || first.includes('p/n')
  const startIdx = hasHeader ? 1 : 0

  // Column layout:
  // 0: RFQ Name | 1: # (row order, skip) | 2: Part Number | 3: Description
  // 4: Qty | 5: Condition | 6: Priority | 7: Alt | 8: Remark | 9: Received Date | 10: Deadline
  interface GroupMeta { items: BulkRFQItem[]; receivedDate: string; deadline: string }
  // Key = "rfqName||deadline" so same name with different deadlines becomes separate groups
  const groupMap = new Map<string, GroupMeta>()

  for (let i = startIdx; i < lines.length; i++) {
    const cols = (lines[i] ?? '').split('\t')
    const rfqName = (cols[0] || '').trim()
    const partNumber = (cols[2] || '').trim()
    if (!rfqName || !partNumber) continue

    const description = (cols[3] || '').trim()
    const qty = Math.max(1, parseInt((cols[4] || '').trim()) || 1)
    const condition = (cols[5] || '').trim().toUpperCase()
    const priority = (cols[6] || '').trim()
    const altRaw = (cols[7] || '').trim()
    const alternatives = altRaw ? altRaw.split(',').map(a => a.trim()).filter(Boolean) : []
    const remark = (cols[8] || '').trim()
    const receivedDate = parseDateToISO((cols[9] || '').trim())
    const deadline = parseDateToISO((cols[10] || '').trim())

    // Group key includes deadline so same-name + different-deadline → separate groups
    const groupKey = `${rfqName}||${deadline}`
    if (!groupMap.has(groupKey)) {
      groupMap.set(groupKey, { items: [], receivedDate, deadline })
    }
    groupMap.get(groupKey)!.items.push({ partNumber, description, qty, condition, priority, remark, alternatives })
    if (receivedDate) groupMap.get(groupKey)!.receivedDate = receivedDate
  }

  if (groupMap.size === 0) {
    bulkParseMsg.value = 'No valid rows found. Expected: RFQ Name [tab] # [tab] Part Number ...'
    bulkParseMsgColor.value = 'text-error'
    return
  }

  // Group entries by base rfqName to detect multi-deadline splits
  const byName = new Map<string, Array<{ key: string; meta: GroupMeta }>>()
  for (const [key, meta] of groupMap.entries()) {
    const baseName = key.split('||')[0]
    if (!byName.has(baseName)) byName.set(baseName, [])
    byName.get(baseName)!.push({ key, meta })
  }

  // Build final list: if a name has multiple deadline groups, rename with (1), (2), (3)...
  const finalGroups: Array<{ rfqName: string; meta: GroupMeta }> = []
  for (const [baseName, entries] of byName.entries()) {
    if (entries.length === 1) {
      // Only one deadline group — keep original name
      finalGroups.push({ rfqName: baseName, meta: entries[0].meta })
    } else {
      // Multiple deadline groups — sort by deadline ascending, then number them
      entries.sort((a, b) => {
        const da = a.meta.deadline ? new Date(a.meta.deadline).getTime() : 0
        const db = b.meta.deadline ? new Date(b.meta.deadline).getTime() : 0
        return da - db
      })
      entries.forEach((entry, idx) => {
        finalGroups.push({ rfqName: `${baseName}(${idx + 1})`, meta: entry.meta })
      })
    }
  }

  const existingNames = new Set(items.value.map((r: any) => r.name?.toLowerCase()))
  bulkGroups.value = finalGroups.map(({ rfqName, meta }) => ({
    rfqName,
    items: meta.items,
    receivedDate: meta.receivedDate,
    deadline: meta.deadline,
    isDuplicate: existingNames.has(rfqName.toLowerCase()),
  }))

  const newCount = bulkGroups.value.filter(g => !g.isDuplicate).length
  const dupCount = bulkGroups.value.filter(g => g.isDuplicate).length
  bulkParseMsg.value = `Found ${bulkGroups.value.length} RFQ(s) — ${newCount} new, ${dupCount} duplicate(s).`
  bulkParseMsgColor.value = newCount > 0 ? 'text-success' : 'text-warning'
}

async function submitBulkRFQs() {
  bulkSubmitError.value = ''
  const customerName = typeof bulkDefaults.value.customerName === 'object'
    ? bulkDefaults.value.customerName?.name
    : bulkDefaults.value.customerName

  if (!customerName?.trim()) { bulkSubmitError.value = 'Customer Name is required.'; return }

  const toCreate = bulkGroups.value.filter(g => !g.isDuplicate)
  if (!toCreate.length) return

  bulkImporting.value = true
  try {
    for (const group of toCreate) {
      const partNumbers = group.items.map(it => it.partNumber)
      const leadTimeISO = group.deadline ? new Date(group.deadline).toISOString() : new Date().toISOString()
      const receivedDateISO = group.receivedDate ? new Date(group.receivedDate).toISOString() : new Date().toISOString()
      const created = await api.post<any>('/rfqs', {
        name: group.rfqName,
        customerName: customerName.trim(),
        leadTime: leadTimeISO,
        receivedDate: receivedDateISO,
        userId: authStore.user?.id || 0,
        notes: null,
        exType: bulkCustomerExWork.value,
        partNumbers,
      })

      // Update items: qty, condition, priority + description/remark on partnumber + alternatives
      if (created?.items?.length) {
        const updatePromises = created.items.map((serverItem: any, idx: number) => {
          const local = group.items[idx]
          if (!local) return Promise.resolve()
          const promises: Promise<any>[] = []

          promises.push(api.put(`/rfqs/items/${serverItem.id}`, {
            alt: null,
            qty: local.qty,
            condition: local.condition || null,
            priority: local.priority || null,
            note: local.remark || null,
          }))

          if (local.description || local.remark) {
            promises.push(api.put(`/partnumbers/${serverItem.partNumberId}`, {
              name: serverItem.partNumberName,
              description: local.description || null,
              remark: local.remark || null,
              supplierId: null,
            }))
          }

          for (const altName of local.alternatives) {
            const existing = (serverItem.alternatives || []).find((a: any) => a.name === altName)
            if (!existing) {
              promises.push(api.post(`/partnumbers/${serverItem.partNumberId}/alternatives`, { name: altName }))
            }
          }

          return Promise.all(promises)
        })
        await Promise.all(updatePromises)
      }
    }

    showBulkImport.value = false
    await loadServerPage()
  } catch (e: any) {
    console.log(e)
    bulkSubmitError.value = e?.data?.message || 'Bulk import failed.'
  } finally {
    bulkImporting.value = false
  }
}

function openCreateModal() {
  form.value = {
    name: '',
    customerName: null,
    leadTime: '',
    date: '',
    notes: '',
    exType: null,
    items: Array.from({ length: 10 }, makeEmptyRow),
  }
  partCount.value = 10
  submitError.value = ''
  partSuggestions.value = {}
  partSearchTexts.value = {}
  showCreate.value = true
}

// ── Submit ──
async function submitRfq() {
  submitError.value = ''

  if (!form.value.name.trim()) {
    submitError.value = 'RFQ Name is required.'
    return
  }

  const customerName = typeof form.value.customerName === 'object'
    ? form.value.customerName?.name
    : form.value.customerName

  if (!customerName?.trim()) {
    submitError.value = 'Customer Name is required.'
    return
  }

  if (!form.value.leadTime) {
    submitError.value = 'Deadline is required.'
    return
  }

  // Collect non-empty items
  const validItems = form.value.items.filter(row => {
    const name = typeof row.partNumber === 'object' ? row.partNumber?.name : row.partNumber
    return name && name.trim()
  })

  if (validItems.length === 0) {
    submitError.value = 'At least 1 Part Number is required.'
    return
  }

  const partNumbers = validItems.map(row => {
    const name = typeof row.partNumber === 'object' ? row.partNumber.name : row.partNumber
    return name.trim()
  })

  submitting.value = true
  try {
    // 1. Create the RFQ with part numbers
    const payload = {
      name: form.value.name.trim(),
      customerName: customerName.trim(),
      leadTime: new Date(form.value.leadTime).toISOString(),
      receivedDate: form.value.date ? new Date(form.value.date).toISOString() : new Date().toISOString(),
      userId: authStore.user?.id || 0,
      notes: form.value.notes || null,
      exType: form.value.exType,
      partNumbers,
    }

    const created = await api.post<any>('/rfqs', payload)

    // 2. Update each item with qty, condition, alternatives
    if (created?.items?.length) {
      const updatePromises = created.items.map((serverItem: any, idx: number) => {
        const localRow = validItems[idx]
        if (!localRow) return Promise.resolve()

        const promises: Promise<any>[] = []

        // Update item fields (qty, condition)
        promises.push(api.put(`/rfqs/items/${serverItem.id}`, {
          alt: null,
          qty: localRow.qty || 1,
          condition: localRow.condition || null,
        }))

        // Update description, remark on part number if any provided
        if (localRow.description ||  localRow.remark) {
          promises.push(api.put(`/partnumbers/${serverItem.partNumberId}`, {
            name: serverItem.partNumberName,
            description: localRow.description || null,
            remark: localRow.remark || null,
            supplierId: null,
          }))
        }

        // Add alternatives
        for (const altName of localRow.alternatives) {
          const existing = (serverItem.alternatives || []).find((a: any) => a.name === altName)
          if (!existing) {
            promises.push(api.post(`/partnumbers/${serverItem.partNumberId}/alternatives`, { name: altName }))
          }
        }

        return Promise.all(promises)
      })
      await Promise.all(updatePromises)
    }

    showCreate.value = false
    await loadServerPage()
  } catch (e: any) {
    submitError.value = e?.data?.message || 'Failed to create RFQ.'
  } finally {
    submitting.value = false
  }
}
</script>

<style scoped>
.cf-th-inner { display: flex; align-items: center; gap: 2px; white-space: nowrap; }
.cf-filter-btn { opacity: 0.5; flex-shrink: 0; }
.cf-filter-btn:hover, .cf-filter-btn.v-btn--active { opacity: 1; }

:deep(.unread-rfq-row) {
  background-color: rgba(66, 165, 245, 0.12) !important;
  border-left: 3px solid #42a5f5;
}
:deep(.unread-rfq-row:hover) {
  background-color: rgba(66, 165, 245, 0.2) !important;
}
:deep(.unread-rfq-row td:first-child) {
  font-weight: 700;
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

.quick-assign-header {
  background: rgb(var(--v-theme-surface)) !important;
  color: rgb(var(--v-theme-on-surface)) !important;
}

@media (prefers-color-scheme: dark) {
  .quick-assign-header {
    background: rgba(var(--v-theme-surface), 0.95) !important;
  }
}
</style>
