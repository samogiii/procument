<template>
  <div>
    <div class="d-flex align-center gap-3 mb-4">
      <div>
        <h1 class="text-h5 font-weight-bold">Shipping Control</h1>
        <p class="text-caption text-medium-emphasis mt-1">Management of track numbers and shipment notes</p>
      </div>
    </div>

    <v-tabs v-model="activeTab" class="mb-4" color="primary">
      <v-tab value="tracks" v-if="isSydOrAdmin">
        <v-icon start icon="mdi-package-variant-closed" />
        Track Numbers
      </v-tab>
      <v-tab value="tid" @click="loadTidGroups" v-if="isSydOrAdmin">
        <v-icon start icon="mdi-family-tree" />
        By T-ID
        <v-chip v-if="tidGroups.length" size="x-small" color="primary" variant="tonal" class="ml-2">
          {{ tidGroups.length }}
        </v-chip>
      </v-tab>
      <v-tab value="sn" @click="loadSnData">
        <v-icon start icon="mdi-note-text-outline" />
        Shipping Number
      </v-tab>
      <v-tab value="totalOrder" @click="loadTotalOrder" v-if="isSydOrAdmin">
        <v-icon start icon="mdi-table-large" />
        Total Order
      </v-tab>
      <v-tab value="finished" @click="loadSnData" v-if="isSydOrAdmin">
        <v-icon start icon="mdi-archive-check-outline" />
        Finished
        <v-chip v-if="snFinishedNotes.length" size="x-small" color="success" variant="tonal" class="ml-2">
          {{ snFinishedNotes.length }}
        </v-chip>
      </v-tab>
    </v-tabs>

    <v-window v-model="activeTab">

      <!-- ══════════════════════════════════════════════
           TAB 1 — Track Numbers (grouped tree)
           ══════════════════════════════════════════════ -->
      <v-window-item value="tracks">
        <!-- Filters -->
        <v-card class="mb-4 pa-3">
          <div class="d-flex flex-wrap gap-3 align-center">
            <v-text-field
              v-model="search"
              label="Search (PO# / Track#)"
              variant="outlined"
              density="compact"
              hide-details
              clearable
              prepend-inner-icon="mdi-magnify"
              style="min-width:200px;max-width:280px;"
              @update:model-value="onFilterChange"
            />
            <v-autocomplete
              v-model="filterWarehouse"
              :items="warehouses"
              item-title="name"
              item-value="id"
              label="Warehouse"
              variant="outlined"
              density="compact"
              hide-details
              clearable
              style="min-width:160px;max-width:220px;"
              @update:model-value="onFilterChange"
            />
            <v-select
              v-model="filterStatus"
              :items="allStatuses"
              label="Status"
              variant="outlined"
              density="compact"
              hide-details
              clearable
              style="min-width:200px;max-width:260px;"
              @update:model-value="onFilterChange"
            />
          </div>
        </v-card>

        <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-3" />

        <div v-if="!loading && trackGroups.length === 0" class="text-center text-medium-emphasis pa-8">
          No track numbers found.
        </div>

        <!-- Grouped cards -->
        <div v-for="group in trackGroups" :key="group.key" class="mb-3">
          <v-card variant="outlined">
            <!-- Group header row -->
            <div
              class="d-flex align-center gap-2 pa-3 cursor-pointer"
              style="user-select:none;"
              @click="toggleTrackExpand(group.key)"
            >
              <v-icon
                :icon="expandedTracks.has(group.key) ? 'mdi-chevron-down' : 'mdi-chevron-right'"
                size="20"
              />
              <v-icon icon="mdi-barcode-scan" size="18" color="primary" />
              <span class="text-subtitle-2 font-weight-bold">{{ group.trackNumber }}</span>
              <v-chip v-if="group.carrier" size="x-small" color="secondary" variant="outlined">
                {{ group.carrier }}
              </v-chip>
              <v-chip :color="statusColor(group.status)" size="x-small" variant="tonal">
                {{ group.status }}
              </v-chip>
              <span class="text-caption text-medium-emphasis">
                <v-icon icon="mdi-warehouse" size="13" class="mr-1" />{{ group.warehouseName || '—' }}
              </span>
              <v-chip size="x-small" color="blue-grey" variant="tonal">
                {{ group.records.length }} part{{ group.records.length !== 1 ? 's' : '' }}
              </v-chip>
              <span class="text-caption text-medium-emphasis">
                {{ new Date(group.createdAt).toLocaleDateString() }}
              </span>
              <v-spacer />
              <v-btn
                icon="mdi-eye-outline"
                size="x-small"
                variant="text"
                color="primary"
                @click.stop="openGroupDetail(group)"
              />
            </div>

            <!-- Expanded parts list -->
            <v-expand-transition>
              <div v-if="expandedTracks.has(group.key)">
                <v-divider />
                <v-list density="compact" class="pa-0">
                  <v-list-item
                    v-for="rec in group.records"
                    :key="rec.id"
                    class="px-4 py-2"
                    style="border-bottom:1px solid rgba(var(--v-border-color),var(--v-border-opacity));"
                  >
                    <template #prepend>
                      <v-icon icon="mdi-subdirectory-arrow-right" size="16" color="blue-grey" class="mr-2" />
                    </template>
                    <v-list-item-title class="text-body-2 font-weight-medium">
                      {{ rec.partNumberName || '—' }}
                    </v-list-item-title>
                    <v-list-item-subtitle class="d-flex flex-wrap gap-3 mt-1">
                      <span v-if="rec.supplierName" class="text-caption text-medium-emphasis">
                        <v-icon icon="mdi-domain" size="11" class="mr-1" />{{ rec.supplierName }}
                      </span>
                      <span v-if="rec.description" class="text-caption text-medium-emphasis" style="max-width:260px; white-space:normal;">
                        <v-icon icon="mdi-information-outline" size="11" class="mr-1" />{{ rec.description }}
                      </span>
                      <v-chip v-if="rec.qty" size="x-small" color="blue-grey" variant="tonal">
                        QTY: {{ rec.qty }}
                      </v-chip>
                      <v-chip v-if="rec.condition" size="x-small" color="teal" variant="tonal">
                        {{ rec.condition }}
                      </v-chip>
                      <span v-if="rec.customerCode" class="text-caption text-medium-emphasis">
                        <v-icon icon="mdi-account-outline" size="11" class="mr-1" />{{ rec.customerCode }}
                      </span>
                    </v-list-item-subtitle>
                    <template #append>
                      <div class="d-flex align-center gap-2">
                        <v-chip :color="statusColor(rec.status)" size="x-small" variant="tonal">
                          {{ rec.status }}
                        </v-chip>
                        <v-btn
                          icon="mdi-eye-outline"
                          size="x-small"
                          variant="text"
                          color="primary"
                          @click.stop="openDetail(null, { item: rec })"
                        />
                      </div>
                    </template>
                  </v-list-item>
                </v-list>
              </div>
            </v-expand-transition>
          </v-card>
        </div>

        <!-- Pagination -->
        <div class="d-flex justify-center align-center gap-4 mt-4" v-if="totalItems > pageSize">
          <v-btn
            icon="mdi-chevron-left"
            variant="text"
            :disabled="currentPage === 1"
            @click="changePage(currentPage - 1)"
          />
          <span class="text-body-2 text-medium-emphasis">
            Page {{ currentPage }} of {{ Math.ceil(totalItems / pageSize) }}
            &nbsp;·&nbsp; {{ totalItems }} total
          </span>
          <v-btn
            icon="mdi-chevron-right"
            variant="text"
            :disabled="currentPage >= Math.ceil(totalItems / pageSize)"
            @click="changePage(currentPage + 1)"
          />
        </div>
        <div v-else-if="totalItems > 0" class="text-center text-caption text-medium-emphasis mt-3">
          {{ totalItems }} track number{{ totalItems !== 1 ? 's' : '' }} total
        </div>
      </v-window-item>

      <!-- ══════════════════════════════════════════════
           TAB 2 — T-ID tree (Admin Summary)
           ══════════════════════════════════════════════ -->
      <v-window-item value="tid">
        <div class="d-flex align-center gap-3 mb-3">
          <v-text-field
            v-model="tidSearch"
            label="Search T-ID or SN#"
            variant="outlined"
            density="compact"
            hide-details
            clearable
            prepend-inner-icon="mdi-magnify"
            style="min-width:220px;max-width:320px;"
          />
          <v-btn
            variant="tonal"
            prepend-icon="mdi-refresh"
            :loading="tidLoading"
            @click="loadTidGroups"
          >
            Refresh
          </v-btn>
        </div>

        <v-progress-linear v-if="tidLoading" indeterminate color="primary" class="mb-3" />

        <div v-if="!tidLoading && filteredTidGroups.length === 0" class="text-center text-medium-emphasis pa-8">
          No shipment notes with a T-ID found.
        </div>

        <!-- T-ID cards -->
        <div v-for="group in filteredTidGroups" :key="group.tId" class="mb-4">
          <v-card variant="outlined" class="border-s-lg" :style="{ borderLeftColor: 'rgb(var(--v-theme-primary))' }">
            <!-- T-ID header -->
            <v-card-title
              class="d-flex align-center gap-2 pa-3 cursor-pointer"
              style="user-select:none;"
              @click="toggleTidExpand(group.tId)"
            >
              <v-icon
                :icon="expandedTids.has(group.tId) ? 'mdi-chevron-down' : 'mdi-chevron-right'"
                color="primary"
              />
              <v-icon icon="mdi-identifier" color="primary" size="20" />
              <span class="text-h6 font-weight-bold text-pn">{{ group.tId }}</span>
              <v-chip size="small" color="primary" variant="tonal" class="ml-1">
                {{ group.notes.length }} SN#
              </v-chip>
              <v-chip size="small" :color="tidStatusColor(group)" variant="tonal" class="ml-1">
                {{ tidSummaryStatus(group) }}
              </v-chip>
              <v-spacer />
              <span class="text-caption text-medium-emphasis">{{ group.notes.map((n: any) => n.warehouseName).filter(Boolean).join(', ') }}</span>
            </v-card-title>

            <v-expand-transition>
              <div v-if="expandedTids.has(group.tId)">
                <v-divider />

                <!-- SN# rows under this T-ID -->
                <div v-for="sn in group.notes" :key="sn.id" class="px-4 py-3 border-b" style="border-bottom: 1px solid rgba(var(--v-border-color),var(--v-border-opacity));">

                  <!-- SN# row header -->
                  <div class="d-flex align-center gap-2 mb-2">
                    <v-icon icon="mdi-subdirectory-arrow-right" size="18" color="secondary" />
                    <span class="text-subtitle-2 font-weight-bold">{{ sn.snNumber }}</span>
                    <v-chip :color="snStatusColor(sn.status)" size="x-small" variant="tonal">{{ sn.status }}</v-chip>
                    <v-chip v-if="sn.type" size="x-small" color="secondary" variant="outlined">{{ sn.type }}</v-chip>
                    <span class="text-caption text-medium-emphasis ml-2">
                      <v-icon icon="mdi-warehouse" size="13" class="mr-1" />{{ sn.warehouseName || '—' }}
                    </span>
                    <span v-if="sn.awbNumber" class="text-caption text-medium-emphasis ml-2">
                      <v-icon icon="mdi-barcode" size="13" class="mr-1" />Local Track#: {{ sn.awbNumber }}
                    </span>
                    <v-spacer />
                    <v-btn
                      size="x-small"
                      variant="tonal"
                      color="primary"
                      prepend-icon="mdi-eye-outline"
                      @click="openSnModal(sn)"
                    >
                      Full Details
                    </v-btn>
                  </div>

                  <!-- Parts tree under this SN# -->
                  <div class="ml-6">
                    <div v-if="!sn.trackNumbers?.length" class="text-caption text-medium-emphasis">No parts linked.</div>
                    <div
                      v-for="track in sn.trackNumbers"
                      :key="track.trackNumberId"
                      class="mb-2 rounded pa-2"
                      style="background:rgba(var(--v-theme-surface-variant),0.5); border:1px solid rgba(var(--v-border-color),0.08);"
                    >
                      <!-- Track header -->
                      <div class="d-flex align-center gap-2 mb-1">
                        <v-icon icon="mdi-subdirectory-arrow-right" size="14" color="blue-grey" />
                        <span class="font-weight-bold text-body-2">{{ track.partNumberName || '—' }}</span>
                        <v-chip size="x-small" color="blue-grey" variant="tonal">{{ track.trackNumber }}</v-chip>
                        <v-chip v-if="track.carrier" size="x-small" variant="outlined">{{ track.carrier }}</v-chip>
                      </div>
                      <!-- Supplier / Description / QTY / Condition / Customer row -->
                      <div class="d-flex flex-wrap gap-3 ml-5 mb-1 text-caption text-medium-emphasis">
                        <span v-if="track.supplierName">
                          <v-icon icon="mdi-domain" size="11" class="mr-1" />{{ track.supplierName }}
                        </span>
                        <span v-if="track.description" style="max-width:260px; white-space:normal;">
                          <v-icon icon="mdi-information-outline" size="11" class="mr-1" />{{ track.description }}
                        </span>
                        <v-chip v-if="track.qty" size="x-small" color="blue-grey" variant="tonal">QTY: {{ track.qty }}</v-chip>
                        <v-chip v-if="track.condition" size="x-small" color="teal" variant="tonal">{{ track.condition }}</v-chip>
                        <span v-if="track.customerCode">
                          <v-icon icon="mdi-account-outline" size="11" class="mr-1" />{{ track.customerCode }}
                        </span>
                      </div>
                      <!-- Item statuses -->
                      <div class="d-flex flex-wrap gap-2 ml-5">
                        <span
                          v-for="itm in track.items"
                          :key="itm.trackNumberItemId"
                          class="d-inline-flex align-center gap-1 text-caption"
                        >
                          <v-icon
                            :icon="itm.status === 'Accepted' ? 'mdi-check-circle' : itm.status === 'Rejected' ? 'mdi-close-circle' : 'mdi-clock-outline'"
                            :color="itm.status === 'Accepted' ? 'success' : itm.status === 'Rejected' ? 'error' : 'warning'"
                            size="14"
                          />
                          <span>Qty {{ itm.actualQty ?? '?' }}</span>
                        </span>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </v-expand-transition>
          </v-card>
        </div>
      </v-window-item>

      <!-- ══════════════════════════════════════════════
           TAB 3 — Shipping Number (Management)
           ══════════════════════════════════════════════ -->
      <v-window-item value="sn">
        <div class="mb-4">
          <p class="text-caption text-medium-emphasis">SN# records for received shipments</p>
        </div>

        <!-- Warehouse filter (Admin only) -->
        <div v-if="isSydOrAdmin" class="d-flex flex-wrap gap-2 mb-4">
          <v-chip
            :color="snFilterWarehouse === null ? 'primary' : 'default'"
            variant="tonal"
            size="small"
            @click="setSnWarehouseFilter(null)"
          >
            All Warehouses
          </v-chip>
          <v-chip
            v-for="wh in warehouses"
            :key="wh.id"
            :color="snFilterWarehouse === wh.id ? 'primary' : 'default'"
            variant="tonal"
            size="small"
            @click="setSnWarehouseFilter(wh.id)"
          >
            {{ wh.name }}
          </v-chip>
        </div>

        <!-- ══════════════════════════════════════════════════════════════ -->
        <!-- SYD / Expert view: T-ID tree                                  -->
        <!-- ══════════════════════════════════════════════════════════════ -->
        <template v-if="authStore.isExpert">
          <v-progress-linear v-if="snLoading" indeterminate color="deep-purple" class="mb-4" />

          <!-- Search -->
          <v-text-field
            v-model="snTidSearch"
            placeholder="Search T-ID, SN#, part…"
            variant="outlined"
            density="compact"
            hide-details
            prepend-inner-icon="mdi-magnify"
            clearable
            class="mb-4"
            style="max-width:360px"
          />

          <div v-if="!snLoading && filteredSnTidGroups.length === 0" class="text-center pa-12 text-medium-emphasis">
            <v-icon icon="mdi-package-variant" size="64" color="grey" class="mb-3" />
            <p>No shipment notes to process yet.</p>
          </div>

          <!-- One card per T-ID group -->
          <v-expansion-panels v-model="snOpenTids" multiple class="mb-4">
            <v-expansion-panel
              v-for="group in filteredSnTidGroups"
              :key="group.tId"
              :value="group.tId"
              class="mb-2"
              rounded="lg"
            >
              <v-expansion-panel-title>
                <div class="d-flex align-center gap-3 flex-wrap w-100 pr-2">
                  <!-- T-ID heading -->
                  <v-icon icon="mdi-identifier" size="18" color="deep-purple" />
                  <span class="text-subtitle-1 font-weight-bold text-pn">{{ group.tId || '(No T-ID)' }}</span>
                  <v-chip size="x-small" variant="tonal" color="deep-purple">{{ group.notes.length }} SN#</v-chip>

                  <!-- Overall status summary chips -->
                  <div class="d-flex gap-1 flex-wrap ml-auto">
                    <v-chip
                      v-for="s in group.statusSummary"
                      :key="s.status"
                      size="x-small"
                      :color="snStatusColor(s.status)"
                      variant="tonal"
                    >
                      {{ s.count }}× {{ s.status }}
                    </v-chip>
                  </div>

                  <!-- Upload customs button if ANY sibling is Ship To USA -->
                  <v-btn
                    v-if="group.notes.some((n: any) => n.status === 'Ship To USA')"
                    size="x-small"
                    variant="tonal"
                    color="deep-purple"
                    prepend-icon="mdi-file-document-check-outline"
                    class="ml-2"
                    @click.stop="openUploadCustomsForGroup(group)"
                  >
                    Upload Customs
                  </v-btn>
                </div>
              </v-expansion-panel-title>

              <v-expansion-panel-text class="pa-0">
                <!-- One sub-card per SN# in the group -->
                <div class="d-flex flex-column gap-3 pa-3">
                  <v-card
                    v-for="sn in group.notes"
                    :key="sn.id"
                    variant="outlined"
                    class="pa-3"
                    :class="sn.status === 'Ship To USA' ? 'border-blue' : ''"
                  >
                    <!-- SN# header row -->
                    <div class="d-flex align-center gap-2 flex-wrap mb-2">
                      <v-icon icon="mdi-note-text-outline" size="16" color="primary" />
                      <span class="font-weight-bold text-pn">{{ sn.snNumber }}</span>
                      <v-chip size="x-small" :color="sn.type === 'CPT' ? 'primary' : 'secondary'" variant="tonal">{{ sn.type }}</v-chip>
                      <v-chip size="x-small" :color="snStatusColor(sn.status)" variant="tonal">{{ sn.status }}</v-chip>
                      <v-chip v-if="sn.warehouseName" size="x-small" variant="tonal">
                        <v-icon icon="mdi-home-city-outline" size="10" class="mr-1" />{{ sn.warehouseName }}
                      </v-chip>
                      <v-spacer />
                      <!-- Customs file download if available -->
                      <v-btn
                        v-if="sn.customsFileName"
                        icon="mdi-file-document-outline"
                        size="x-small"
                        variant="tonal"
                        color="teal"
                        :href="`${api.baseURL}/shipment-notes/${sn.id}/customs-file`"
                        target="_blank"
                        title="Download Customs File"
                      />
                      <!-- Individual customs upload (per SN#) -->
                      <v-btn
                        v-if="sn.status === 'Ship To USA'"
                        icon="mdi-file-document-check-outline"
                        size="x-small"
                        variant="tonal"
                        color="deep-purple"
                        title="Upload Customs File for this SN#"
                        @click="openUploadCustoms(sn)"
                      />
                      <!-- PDF preview -->
                      <v-btn
                        v-if="sn.pdfFileName"
                        icon="mdi-file-pdf-box"
                        size="x-small"
                        variant="tonal"
                        color="error"
                        title="Preview SN# PDF"
                        @click="docPreview.preview(`/shipment-notes/${sn.id}/pdf-file`, sn.pdfFileName || 'shipment-note.pdf', 'application/pdf')"
                      />
                    </div>

                    <!-- Meta row: AWB, SO#, Destination, Date -->
                    <div class="d-flex gap-3 flex-wrap mb-2 text-caption text-medium-emphasis">
                      <span v-if="sn.awbNumber"><strong>Local Track#:</strong> {{ sn.awbNumber }}</span>
                      <span v-if="sn.soNumber"><strong>SO#:</strong> {{ sn.soNumber }}</span>
                      <span v-if="sn.destination"><strong>Dest:</strong> {{ sn.destination }}</span>
                      <span><strong>Created:</strong> {{ new Date(sn.createdAt).toLocaleDateString() }}</span>
                    </div>

                    <!-- Customs file info -->
                    <v-alert
                      v-if="sn.customsFileName"
                      type="success"
                      density="compact"
                      class="mb-2"
                      :icon="false"
                    >
                      <div class="d-flex align-center gap-2">
                        <v-icon icon="mdi-file-check-outline" size="14" />
                        <span class="text-caption">Customs uploaded: {{ sn.customsOriginalFileName || sn.customsFileName }}</span>
                        <span class="text-caption ml-1 text-medium-emphasis">{{ sn.customsUploadedAt ? new Date(sn.customsUploadedAt).toLocaleDateString() : '' }}</span>
                      </div>
                    </v-alert>

                    <!-- Parts tree: Track → Parts -->
                    <div v-if="sn.trackNumbers?.length">
                      <div class="text-caption font-weight-bold text-medium-emphasis mb-1">PARTS</div>
                      <div v-for="track in snGroupedTracks(sn)" :key="track.trackNumber" class="mb-2 ml-2">
                        <div class="d-flex align-center gap-1 mb-1">
                          <v-icon icon="mdi-barcode-scan" size="14" color="primary" />
                          <span class="text-caption font-weight-bold text-pn">{{ track.trackNumber }}</span>
                          <span v-if="track.carrier" class="text-caption text-medium-emphasis">· {{ track.carrier }}</span>
                        </div>
                        <v-table density="compact" class="rounded border ml-4">
                          <thead>
                            <tr>
                              <th>Part Number</th>
                              <th>Qty</th>
                              <th>Condition</th>
                              <th>Customer</th>
                              <th>Cert</th>
                              <th>Status</th>
                            </tr>
                          </thead>
                          <tbody>
                            <tr v-for="part in track.allParts" :key="part.trackNumberItemId">
                              <td>{{ part.partNumberName || '—' }}</td>
                              <td>{{ part.actualQty ?? '—' }}</td>
                              <td>
                                <v-chip v-if="part.condition" size="x-small" color="teal" variant="tonal">{{ part.condition }}</v-chip>
                                <span v-else class="text-medium-emphasis text-caption">—</span>
                              </td>
                              <td class="text-caption">
                                <span v-if="part.customerCode">{{ part.customerCode }}</span>
                                <span v-else class="text-medium-emphasis">—</span>
                              </td>
                              <td>
                                <v-icon
                                  v-if="part.certNeeded"
                                  icon="mdi-certificate-outline"
                                  size="14"
                                  color="warning"
                                  title="Cert Needed"
                                />
                                <span v-else class="text-medium-emphasis text-caption">—</span>
                              </td>
                              <td>
                                <v-chip :color="part.status === 'Accepted' ? 'success' : 'default'" size="x-small" variant="tonal">
                                  {{ part.status }}
                                </v-chip>
                              </td>
                            </tr>
                          </tbody>
                        </v-table>
                      </div>
                    </div>
                    <div v-else class="text-caption text-medium-emphasis">No parts linked.</div>
                  </v-card>
                </div>
              </v-expansion-panel-text>
            </v-expansion-panel>
          </v-expansion-panels>
        </template>

        <!-- ══════════════════════════════════════════════════════════════ -->
        <!-- Admin / Inventory view: flat table                            -->
        <!-- ══════════════════════════════════════════════════════════════ -->
        <template v-else>
          <v-card>
            <v-data-table
              v-model:expanded="snExpanded"
              :headers="snHeaders"
              :items="snNotes"
              :loading="snLoading"
              item-value="id"
              show-expand
              class="elevation-0"
            >
              <template #item.type="{ item }">
                <v-chip size="x-small" :color="item.type === 'CPT' ? 'primary' : 'secondary'" variant="tonal">
                  {{ item.type }}
                </v-chip>
              </template>

              <template #item.status="{ item }">
                <v-chip :color="snStatusColor(item.status)" size="x-small" variant="tonal">
                  {{ item.status }}
                </v-chip>
              </template>

              <template #item.tId="{ item }">
                <div class="d-flex align-center gap-1">
                  <span class="font-weight-medium">{{ item.tId || '—' }}</span>
                  <v-btn
                    v-if="isSydOrAdmin && item.status !== 'Confirmed'"
                    icon="mdi-pencil"
                    size="x-small"
                    variant="text"
                    color="primary"
                    @click="openEditTId(item)"
                  />
                </div>
              </template>

              <template #item.awbNumber="{ item }">
                <div class="d-flex align-center gap-1">
                  <span :class="item.awbNumber ? 'font-weight-medium' : 'text-medium-emphasis'">
                    {{ item.awbNumber || '—' }}
                  </span>
                  <v-btn
                    v-if="item.status !== 'Confirmed'"
                    icon="mdi-pencil"
                    size="x-small"
                    variant="text"
                    :color="item.awbNumber ? 'primary' : 'warning'"
                    @click="openEditAwb(item)"
                  />
                </div>
              </template>

              <template #item.createdAt="{ item }">
                {{ new Date(item.createdAt).toLocaleDateString() }}
              </template>

              <template #item.actions="{ item }">
                <div class="d-flex gap-1 justify-end">
                  <v-btn
                    v-if="item.pdfFileName"
                    icon="mdi-file-pdf-box"
                    size="x-small"
                    variant="tonal"
                    color="error"
                    title="Preview SN# PDF"
                    @click="docPreview.preview(`/shipment-notes/${item.id}/pdf-file`, item.pdfFileName || 'shipment-note.pdf', 'application/pdf')"
                  />
                  <v-btn
                    v-if="isSydOrAdmin && item.status === 'Ship To USA'"
                    icon="mdi-file-document-check-outline"
                    size="x-small"
                    variant="tonal"
                    color="deep-purple"
                    title="Upload Customs File"
                    @click="openUploadCustoms(item)"
                  />
                  <v-btn
                    v-if="isSydOrAdmin && nextSnAdminStatus(item)"
                    icon="mdi-arrow-right-circle-outline"
                    size="x-small"
                    variant="tonal"
                    color="primary"
                    :title="`Advance to: ${nextSnAdminStatus(item)}`"
                    @click="advanceSnStatus(item)"
                  />
                  <v-btn
                    v-if="isSydOrAdmin && item.status === 'Delivered to Customer'"
                    icon="mdi-check-circle-outline"
                    size="x-small"
                    variant="tonal"
                    color="success"
                    title="Confirm SN#"
                    @click="confirmSn(item)"
                  />
                  <v-btn
                    v-if="isSydOrAdmin"
                    icon="mdi-upload"
                    size="x-small"
                    variant="tonal"
                    color="primary"
                    title="Upload SN# PDF"
                    @click="openUploadPdf(item)"
                  />
                  <v-btn
                    icon="mdi-plus"
                    size="x-small"
                    variant="tonal"
                    color="secondary"
                    title="Add Track Number"
                    @click="openAddTrack(item)"
                  />
                </div>
              </template>

              <template #expanded-row="{ columns, item }">
                <tr>
                  <td :colspan="columns.length" class="pa-4 bg-surface-variant">
                    <!-- Info cards row -->
                    <div class="d-flex gap-4 flex-wrap mb-3">
                      <v-card variant="outlined" class="pa-3 flex-grow-1" min-width="140">
                        <div class="text-caption font-weight-bold text-medium-emphasis mb-1">TYPE</div>
                        <v-chip :color="item.type === 'CPT' ? 'primary' : 'secondary'" size="small" variant="tonal">{{ item.type }}</v-chip>
                      </v-card>

                      <v-card variant="outlined" class="pa-3 flex-grow-1" min-width="160">
                        <div class="text-caption font-weight-bold text-medium-emphasis mb-1">T-ID / SO# / DESTINATION</div>
                        <div class="d-flex align-center gap-2">
                          <div class="flex-grow-1">
                            <div><span class="text-caption text-medium-emphasis">T-ID: </span><span class="font-weight-bold">{{ item.tId || '—' }}</span></div>
                            <div><span class="text-caption text-medium-emphasis">SO#: </span><span class="font-weight-medium">{{ item.soNumber || '—' }}</span></div>
                            <div><span class="text-caption text-medium-emphasis">Dest: </span><span class="text-body-2">{{ item.destination || '—' }}</span></div>
                          </div>
                          <v-btn v-if="isSydOrAdmin" icon="mdi-pencil" size="x-small" variant="text" color="primary" @click="openEditTId(item)" />
                        </div>
                      </v-card>

                      <v-card variant="outlined" class="pa-3 flex-grow-1" min-width="160" :color="!item.awbNumber ? 'warning' : undefined">
                        <div class="text-caption font-weight-bold text-medium-emphasis mb-1">Local Track#</div>
                        <div class="d-flex align-center gap-2">
                          <span class="text-body-1 font-weight-bold">{{ item.awbNumber || 'Not entered yet' }}</span>
                          <v-btn icon="mdi-pencil" size="x-small" variant="text" :color="item.awbNumber ? 'primary' : 'warning'" @click="openEditAwb(item)" />
                        </div>
                        <div class="text-caption text-medium-emphasis mt-1">Entered by Inventory</div>
                      </v-card>

                      <v-card variant="outlined" class="pa-3" min-width="180">
                        <div class="text-caption font-weight-bold text-medium-emphasis mb-1">STATUS</div>
                        <v-chip :color="snStatusColor(item.status)" size="small" variant="tonal">{{ item.status }}</v-chip>
                        <div v-if="isSydOrAdmin && nextSnAdminStatus(item)" class="mt-2">
                          <v-btn size="x-small" variant="tonal" color="primary" @click="advanceSnStatus(item)">
                            → {{ nextSnAdminStatus(item) }}
                          </v-btn>
                        </div>
                      </v-card>

                      <v-card v-if="item.customsFileName" variant="outlined" class="pa-3" min-width="160">
                        <div class="text-caption font-weight-bold text-medium-emphasis mb-1">CUSTOMS FILE</div>
                        <div class="text-caption mb-1">{{ item.customsOriginalFileName || item.customsFileName }}</div>
                        <div class="text-caption text-medium-emphasis mb-2">
                          {{ item.customsUploadedAt ? new Date(item.customsUploadedAt).toLocaleDateString() : '' }}
                        </div>
                        <v-btn
                          size="x-small"
                          variant="tonal"
                          color="teal"
                          prepend-icon="mdi-download"
                          :href="`${api.baseURL}/shipment-notes/${item.id}/customs-file`"
                          target="_blank"
                        >
                          Download
                        </v-btn>
                      </v-card>

                      <v-card variant="outlined" class="pa-3" min-width="140">
                        <div class="text-caption font-weight-bold text-medium-emphasis mb-1">CREATED BY</div>
                        <div class="text-body-2">{{ item.createdByName || '—' }}</div>
                      </v-card>
                    </div>

                    <v-divider class="my-3" />

                    <!-- Shipping Boxes -->
                    <div class="d-flex align-center mb-2">
                      <v-icon icon="mdi-truck-delivery-outline" size="14" color="primary" class="mr-1" />
                      <span class="text-caption font-weight-bold text-medium-emphasis">SHIPPING BOXES (SN#)</span>
                      <v-spacer />
                      <v-btn v-if="isSydOrAdmin || authStore.isInventory" size="x-small" variant="tonal" color="primary" prepend-icon="mdi-plus" @click="openAddSnBox(item)">
                        Add Box
                      </v-btn>
                    </div>
                    <div v-if="!item.boxes?.length" class="text-caption text-medium-emphasis mb-3">No boxes added yet.</div>
                    <v-table v-else density="compact" class="rounded border mb-4">
                      <thead>
                        <tr>
                          <th>Box #</th>
                          <th>Weight (kg)</th>
                          <th>H × W × L (cm)</th>
                          <th>Notes</th>
                          <th v-if="isSydOrAdmin" class="text-right">Actions</th>
                        </tr>
                      </thead>
                      <tbody>
                        <tr v-for="box in item.boxes" :key="box.id">
                          <td class="font-weight-bold">{{ box.boxNumber }}</td>
                          <td>{{ box.weightKg ?? '—' }}</td>
                          <td>{{ box.heightCm ?? '?' }} × {{ box.widthCm ?? '?' }} × {{ box.lengthCm ?? '?' }}</td>
                          <td class="text-caption text-medium-emphasis">{{ box.notes || '—' }}</td>
                          <td v-if="isSydOrAdmin" class="text-right">
                            <v-btn icon="mdi-pencil" size="x-small" variant="text" color="primary" @click="openEditSnBox(item, box)" />
                            <v-btn icon="mdi-delete" size="x-small" variant="text" color="error" @click="deleteSnBox(item, box.id)" />
                          </td>
                        </tr>
                      </tbody>
                    </v-table>

                    <v-divider class="my-3" />

                    <!-- Track Numbers & Parts -->
                    <div class="text-caption font-weight-bold text-medium-emphasis mb-2">TRACK NUMBERS & PARTS</div>
                    <div v-if="!item.trackNumbers?.length" class="text-caption text-medium-emphasis">No track numbers linked.</div>
                    <div v-else class="d-flex flex-column gap-3">
                      <v-card v-for="group in snGroupedTracks(item)" :key="group.trackNumber" variant="outlined" class="pa-3">
                        <div class="d-flex align-center gap-2 mb-2">
                          <v-icon icon="mdi-barcode-scan" size="16" color="primary" />
                          <span class="text-subtitle-2 font-weight-bold text-pn">{{ group.trackNumber }}</span>
                          <v-chip size="x-small" variant="tonal">{{ group.carrier || 'No carrier' }}</v-chip>
                          <v-spacer />
                          <v-btn v-if="isSydOrAdmin" icon="mdi-link-off" size="x-small" variant="text" color="error" title="Remove from SN#" @click="removeSnTrack(item.id, group.primaryTrackNumberId)" />
                        </div>

                        <v-table v-if="group.allParts.length" density="compact" class="mb-2">
                          <thead>
                            <tr><th>Part Number</th><th>Qty</th><th>Condition</th><th>Customer</th><th>Cert</th><th>Status</th></tr>
                          </thead>
                          <tbody>
                            <tr v-for="part in group.allParts" :key="part.trackNumberItemId">
                              <td>{{ part.partNumberName || '—' }}</td>
                              <td>{{ part.actualQty ?? '—' }}</td>
                              <td>
                                <v-chip v-if="part.condition" size="x-small" color="teal" variant="tonal">{{ part.condition }}</v-chip>
                                <span v-else class="text-medium-emphasis text-caption">—</span>
                              </td>
                              <td class="text-caption">
                                <span v-if="part.customerCode || part.customerName">{{ part.customerCode || '' }}<span v-if="part.customerCode && part.customerName"> · </span>{{ part.customerName || '' }}</span>
                                <span v-else class="text-medium-emphasis">—</span>
                              </td>
                              <td>
                                <v-icon v-if="part.certNeeded" icon="mdi-certificate-outline" size="14" color="warning" title="Cert Needed" />
                                <span v-else class="text-medium-emphasis text-caption">—</span>
                              </td>
                              <td>
                                <v-chip :color="part.status === 'Accepted' ? 'success' : 'default'" size="x-small" variant="tonal">{{ part.status }}</v-chip>
                              </td>
                            </tr>
                          </tbody>
                        </v-table>

                        <!-- Received Boxes — Admin only -->
                        <template v-if="isSydOrAdmin">
                          <div class="d-flex align-center gap-1 mb-1">
                            <v-icon icon="mdi-package-variant-closed" size="14" color="orange" />
                            <span class="text-caption font-weight-bold" style="color: rgb(var(--v-theme-warning))">Received Boxes ({{ group.allBoxes.length }})</span>
                          </div>
                          <div v-if="!group.allBoxes.length" class="text-caption text-medium-emphasis ml-4 mb-1">No boxes recorded at warehouse.</div>
                          <v-table v-else density="compact" class="rounded border mb-1">
                            <thead>
                              <tr><th>Box #</th><th>Weight (kg)</th><th>H × W × L (cm)</th><th>Notes</th></tr>
                            </thead>
                            <tbody>
                              <tr v-for="box in group.allBoxes" :key="box.id">
                                <td class="font-weight-bold">{{ box.boxNumber }}</td>
                                <td>{{ box.weightKg ?? '—' }}</td>
                                <td class="text-caption">{{ box.heightCm ?? '?' }} × {{ box.widthCm ?? '?' }} × {{ box.lengthCm ?? '?' }}</td>
                                <td class="text-caption text-medium-emphasis">{{ box.notes || '—' }}</td>
                              </tr>
                            </tbody>
                          </v-table>
                        </template>
                      </v-card>
                    </div>
                  </td>
                </tr>
              </template>
            </v-data-table>
          </v-card>
        </template>
      </v-window-item>

      <!-- ══════════════════════════════════════════════
           TAB 4 — Total Order (flat table with Excel-style column filters)
           ══════════════════════════════════════════════ -->
      <v-window-item value="totalOrder">
        <!-- Top bar -->
        <div class="d-flex flex-wrap gap-3 align-center mb-3">
          <v-btn variant="tonal" prepend-icon="mdi-refresh" :loading="toLoading" @click="loadTotalOrder">Refresh</v-btn>
          <v-btn
            v-if="toActiveFilterCount > 0"
            variant="tonal"
            color="error"
            prepend-icon="mdi-filter-off-outline"
            @click="toClearAllFilters"
          >
            Clear Filters ({{ toActiveFilterCount }})
          </v-btn>
          <v-spacer />
          <span class="text-caption text-medium-emphasis">
            {{ toFilteredRows.length }} / {{ toRows.length }} items
          </span>
        </div>

        <v-progress-linear v-if="toLoading" indeterminate color="primary" class="mb-3" />

        <div v-if="!toLoading && toRows.length === 0" class="text-center text-medium-emphasis pa-8">
          No items with track numbers found.
        </div>

        <div v-else-if="!toLoading && toFilteredRows.length === 0" class="text-center text-medium-emphasis pa-8">
          <v-icon icon="mdi-filter-off" size="40" color="grey" class="mb-2" />
          <div>No rows match the active filters.</div>
          <v-btn variant="tonal" color="primary" class="mt-3" @click="toClearAllFilters">Clear Filters</v-btn>
        </div>

        <v-card v-else>
          <div class="overflow-x-auto">
            <table class="to-table">
              <thead>
                <tr>
                  <th v-for="col in TO_COLUMNS" :key="col.key">
                    <div class="to-th-inner">
                      <span class="to-th-label">{{ col.label }}</span>
                      <!-- Excel-style filter button -->
                      <v-menu
                        :key="col.key"
                        location="bottom start"
                        :close-on-content-click="false"
                        min-width="220"
                        max-width="300"
                      >
                        <template #activator="{ props: menuProps }">
                          <button
                            v-bind="menuProps"
                            class="to-filter-btn"
                            :class="{ 'to-filter-active': toColFilters[col.key]?.size > 0 }"
                            @click.stop
                            :title="toColFilters[col.key]?.size > 0 ? `${toColFilters[col.key].size} filter(s) active` : 'Filter'"
                          >
                            <v-icon
                              :icon="toColFilters[col.key]?.size > 0 ? 'mdi-filter' : 'mdi-menu-down'"
                              size="14"
                            />
                          </button>
                        </template>

                        <!-- Filter popover -->
                        <v-card elevation="8" rounded="lg">
                          <!-- Search within values -->
                          <div class="pa-2 pb-1">
                            <v-text-field
                              v-model="toFilterSearch[col.key]"
                              placeholder="Search..."
                              variant="outlined"
                              density="compact"
                              hide-details
                              prepend-inner-icon="mdi-magnify"
                              clearable
                              autofocus
                            />
                          </div>

                          <!-- Select all / Clear -->
                          <div class="d-flex gap-1 px-2 pb-1">
                            <v-btn size="x-small" variant="text" color="primary" @click="toSelectAll(col.key)">All</v-btn>
                            <v-btn size="x-small" variant="text" color="error" @click="toClearCol(col.key)">Clear</v-btn>
                            <span class="text-caption text-medium-emphasis ml-auto align-self-center">
                              {{ toColFilters[col.key]?.size || 0 }} selected
                            </span>
                          </div>

                          <v-divider />

                          <!-- Value list -->
                          <div style="max-height:240px; overflow-y:auto;">
                            <div
                              v-for="val in toDisplayValues(col.key)"
                              :key="val"
                              class="to-filter-item"
                              :class="{ 'opacity-40': toIsUnavail(col.key, val) }"
                              @click="toToggleValue(col.key, val)"
                            >
                              <v-checkbox-btn
                                :model-value="toColFilters[col.key]?.has(val) ?? false"
                                density="compact"
                                hide-details
                                readonly
                                class="mr-1"
                              />
                              <span class="text-body-2" :class="val === '(Blank)' ? 'text-medium-emphasis font-italic' : ''">{{ val }}</span>
                            </div>
                            <div v-if="toDisplayValues(col.key).length === 0" class="text-caption text-medium-emphasis pa-3 text-center">
                              No values
                            </div>
                          </div>
                          <v-divider />
                          <v-list-item
                            :title="toShowAll[col.key] ? 'Show available only' : 'Show all'"
                            :prepend-icon="toShowAll[col.key] ? 'mdi-filter' : 'mdi-filter-off'"
                            density="compact"
                            class="text-caption text-medium-emphasis"
                            @click.stop="toToggleShowAll(col.key)"
                          />
                        </v-card>
                      </v-menu>
                    </div>
                  </th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="row in toFilteredRows" :key="row.id">
                  <td class="text-caption text-medium-emphasis">{{ row.id }}</td>
                  <td class="font-weight-medium text-pn">{{ row.poNumber || '—' }}</td>
                  <td>{{ row.poRef ?? '—' }}</td>
                  <td>{{ row.quotationExpert || '—' }}</td>
                  <td>{{ row.customer || '—' }}</td>
                  <td class="text-caption">{{ row.customerInvoiceNumber || '—' }}</td>
                  <td>{{ row.supplier || '—' }}</td>
                  <td class="font-weight-medium text-pn">{{ row.partNumber || '—' }}</td>
                  <td class="text-caption" style="max-width:160px; white-space:normal;">{{ row.description || '—' }}</td>
                  <td class="text-center font-weight-bold">{{ row.qty }}</td>
                  <td>{{ row.condition || '—' }}</td>
                  <td>
                    <v-chip v-if="row.priority" size="x-small" :color="row.priority === 'AOG' ? 'error' : row.priority === 'URGENT' ? 'warning' : 'default'" variant="tonal">
                      {{ row.priority }}
                    </v-chip>
                    <span v-else class="text-medium-emphasis">—</span>
                  </td>
                  <td>{{ row.warehouse || '—' }}</td>
                  <td class="text-pn">{{ row.serialNumber || '—' }}</td>
                  <td>
                    <v-chip v-if="row.shippingStatus" :color="statusColor(row.shippingStatus)" size="x-small" variant="tonal">
                      {{ row.shippingStatus }}
                    </v-chip>
                    <span v-else class="text-medium-emphasis">—</span>
                  </td>
                  <td class="font-weight-medium text-pn">{{ row.trackNumbers || '—' }}</td>
                  <td>{{ row.soNumber || '—' }}</td>
                  <td class="font-weight-medium">{{ row.tId || '—' }}</td>
                  <td>{{ row.awbNumber || '—' }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </v-card>
      </v-window-item>

      <!-- ══════════════════════════════════════════════
           TAB 5 — Finished (Confirmed SN# archive)
           ══════════════════════════════════════════════ -->
      <v-window-item value="finished">
        <!-- Header + view toggle -->
        <div class="d-flex align-center gap-3 mb-4 flex-wrap">
          <div>
            <p class="text-caption text-medium-emphasis">Confirmed SN# records — read-only archive</p>
          </div>
          <v-spacer />
          <v-btn-toggle v-model="finishedViewMode" mandatory density="compact" variant="tonal" color="success">
            <v-btn value="flat" size="small" prepend-icon="mdi-view-list">Shipping Numbers</v-btn>
            <v-btn value="tid" size="small" prepend-icon="mdi-family-tree">By T-ID</v-btn>
          </v-btn-toggle>
        </div>

        <v-progress-linear v-if="snLoading" indeterminate color="success" class="mb-3" />

        <div v-if="!snLoading && snFinishedNotes.length === 0" class="text-center text-medium-emphasis pa-12">
          <v-icon icon="mdi-archive-outline" size="64" color="grey" class="mb-3" />
          <p>No confirmed shipments yet.</p>
        </div>

        <!-- ── Flat view: plain data table of confirmed SN#s ── -->
        <v-card v-else-if="finishedViewMode === 'flat'">
          <v-data-table
            v-model:expanded="finishedExpanded"
            :headers="finishedHeaders"
            :items="snFinishedNotes"
            :loading="snLoading"
            item-value="id"
            show-expand
            class="elevation-0"
          >
            <template #item.type="{ item }">
              <v-chip size="x-small" :color="item.type === 'CPT' ? 'primary' : 'secondary'" variant="tonal">{{ item.type }}</v-chip>
            </template>
            <template #item.status="{ item }">
              <v-chip color="success" size="x-small" variant="tonal">
                <v-icon start icon="mdi-check-circle-outline" size="12" />{{ item.status }}
              </v-chip>
            </template>
            <template #item.tId="{ item }">
              <span class="font-weight-medium">{{ item.tId || '—' }}</span>
            </template>
            <template #item.awbNumber="{ item }">
              <span :class="item.awbNumber ? 'font-weight-medium' : 'text-medium-emphasis'">{{ item.awbNumber || '—' }}</span>
            </template>
            <template #item.createdAt="{ item }">
              {{ new Date(item.createdAt).toLocaleDateString() }}
            </template>
            <template #item.actions="{ item }">
              <v-btn
                v-if="item.pdfFileName"
                icon="mdi-file-pdf-box"
                size="x-small"
                variant="tonal"
                color="error"
                title="Preview SN# PDF"
                @click="docPreview.preview(`/shipment-notes/${item.id}/pdf-file`, item.pdfFileName || 'shipment-note.pdf', 'application/pdf')"
              />
            </template>
            <template #expanded-row="{ columns, item }">
              <tr>
                <td :colspan="columns.length" class="pa-4 bg-surface-variant">
                  <div class="d-flex gap-4 flex-wrap mb-3">
                    <v-card variant="outlined" class="pa-3 flex-grow-1" min-width="140">
                      <div class="text-caption font-weight-bold text-medium-emphasis mb-1">TYPE</div>
                      <v-chip :color="item.type === 'CPT' ? 'primary' : 'secondary'" size="small" variant="tonal">{{ item.type }}</v-chip>
                    </v-card>
                    <v-card variant="outlined" class="pa-3 flex-grow-1" min-width="200">
                      <div class="text-caption font-weight-bold text-medium-emphasis mb-1">T-ID / SO# / DESTINATION</div>
                      <div><span class="text-caption text-medium-emphasis">T-ID: </span><span class="font-weight-bold">{{ item.tId || '—' }}</span></div>
                      <div><span class="text-caption text-medium-emphasis">SO#: </span><span class="font-weight-medium">{{ item.soNumber || '—' }}</span></div>
                      <div><span class="text-caption text-medium-emphasis">Dest: </span><span class="text-body-2">{{ item.destination || '—' }}</span></div>
                    </v-card>
                    <v-card variant="outlined" class="pa-3 flex-grow-1" min-width="160">
                      <div class="text-caption font-weight-bold text-medium-emphasis mb-1">LOCAL TRACK#</div>
                      <span class="text-body-1 font-weight-bold">{{ item.awbNumber || '—' }}</span>
                    </v-card>
                    <v-card variant="outlined" class="pa-3" min-width="140">
                      <div class="text-caption font-weight-bold text-medium-emphasis mb-1">CREATED BY</div>
                      <div class="text-body-2">{{ item.createdByName || '—' }}</div>
                    </v-card>
                    <v-card v-if="item.customsFileName" variant="outlined" class="pa-3" min-width="160">
                      <div class="text-caption font-weight-bold text-medium-emphasis mb-1">CUSTOMS FILE</div>
                      <div class="text-caption mb-1">{{ item.customsOriginalFileName || item.customsFileName }}</div>
                      <div class="text-caption text-medium-emphasis mb-2">{{ item.customsUploadedAt ? new Date(item.customsUploadedAt).toLocaleDateString() : '' }}</div>
                      <v-btn size="x-small" variant="tonal" color="teal" prepend-icon="mdi-download" :href="`${api.baseURL}/shipment-notes/${item.id}/customs-file`" target="_blank">Download</v-btn>
                    </v-card>
                  </div>
                  <v-divider class="my-3" />
                  <div class="text-caption font-weight-bold text-medium-emphasis mb-2">TRACK NUMBERS & PARTS</div>
                  <div v-if="!item.trackNumbers?.length" class="text-caption text-medium-emphasis">No track numbers linked.</div>
                  <div v-else class="d-flex flex-column gap-3">
                    <v-card v-for="grp in snGroupedTracks(item)" :key="grp.trackNumber" variant="outlined" class="pa-3">
                      <div class="d-flex align-center gap-2 mb-2">
                        <v-icon icon="mdi-barcode-scan" size="16" color="primary" />
                        <span class="text-subtitle-2 font-weight-bold text-pn">{{ grp.trackNumber }}</span>
                        <v-chip size="x-small" variant="tonal">{{ grp.carrier || 'No carrier' }}</v-chip>
                      </div>
                      <v-table v-if="grp.allParts.length" density="compact" class="mb-2">
                        <thead><tr><th>Part Number</th><th>Qty</th><th>Condition</th><th>Customer</th><th>Cert</th><th>Status</th></tr></thead>
                        <tbody>
                          <tr v-for="part in grp.allParts" :key="part.trackNumberItemId">
                            <td>{{ part.partNumberName || '—' }}</td>
                            <td>{{ part.actualQty ?? '—' }}</td>
                            <td><v-chip v-if="part.condition" size="x-small" color="teal" variant="tonal">{{ part.condition }}</v-chip><span v-else class="text-medium-emphasis text-caption">—</span></td>
                            <td class="text-caption"><span v-if="part.customerCode || part.customerName">{{ part.customerCode }}</span><span v-else class="text-medium-emphasis">—</span></td>
                            <td><v-icon v-if="part.certNeeded" icon="mdi-certificate-outline" size="14" color="warning" /><span v-else class="text-medium-emphasis text-caption">—</span></td>
                            <td><v-chip color="success" size="x-small" variant="tonal">{{ part.status }}</v-chip></td>
                          </tr>
                        </tbody>
                      </v-table>
                    </v-card>
                  </div>
                </td>
              </tr>
            </template>
          </v-data-table>
        </v-card>

        <!-- ── T-ID grouped view: only fully-confirmed T-ID groups ── -->
        <template v-else>
          <div v-if="finishedTidGroups.length === 0" class="text-center text-medium-emphasis pa-12">
            <v-icon icon="mdi-family-tree" size="48" color="grey" class="mb-3" />
            <p>No fully-confirmed T-ID groups yet.</p>
            <p class="text-caption mt-1">T-ID groups appear here once every SN# under them is confirmed.</p>
          </div>

          <v-expansion-panels v-else v-model="finishedTidOpen" multiple class="mb-4">
            <v-expansion-panel
              v-for="group in finishedTidGroups"
              :key="group.tId"
              :value="group.tId"
              class="mb-2"
              rounded="lg"
            >
              <v-expansion-panel-title>
                <div class="d-flex align-center gap-3 flex-wrap w-100 pr-2">
                  <v-icon icon="mdi-check-decagram" size="18" color="success" />
                  <span class="text-subtitle-1 font-weight-bold text-pn">{{ group.tId }}</span>
                  <v-chip size="x-small" color="success" variant="tonal">{{ group.notes.length }} SN#</v-chip>
                  <v-chip size="x-small" color="success" variant="outlined">
                    <v-icon start icon="mdi-archive-check-outline" size="12" />All Confirmed
                  </v-chip>
                  <span class="text-caption text-medium-emphasis ml-auto">
                    {{ group.notes.map((n: any) => n.warehouseName).filter(Boolean).join(', ') }}
                  </span>
                </div>
              </v-expansion-panel-title>

              <v-expansion-panel-text class="pa-0">
                <div class="d-flex flex-column gap-3 pa-3">
                  <v-card
                    v-for="sn in group.notes"
                    :key="sn.id"
                    variant="outlined"
                    class="pa-3 border-success"
                  >
                    <!-- SN# header -->
                    <div class="d-flex align-center gap-2 flex-wrap mb-2">
                      <v-icon icon="mdi-note-text-outline" size="16" color="success" />
                      <span class="font-weight-bold text-pn">{{ sn.snNumber }}</span>
                      <v-chip size="x-small" :color="sn.type === 'CPT' ? 'primary' : 'secondary'" variant="tonal">{{ sn.type }}</v-chip>
                      <v-chip size="x-small" color="success" variant="tonal">
                        <v-icon start icon="mdi-check-circle-outline" size="11" />{{ sn.status }}
                      </v-chip>
                      <v-chip v-if="sn.warehouseName" size="x-small" variant="tonal">
                        <v-icon icon="mdi-home-city-outline" size="10" class="mr-1" />{{ sn.warehouseName }}
                      </v-chip>
                      <v-spacer />
                      <v-btn
                        v-if="sn.pdfFileName"
                        icon="mdi-file-pdf-box"
                        size="x-small"
                        variant="tonal"
                        color="error"
                        title="Preview SN# PDF"
                        @click="docPreview.preview(`/shipment-notes/${sn.id}/pdf-file`, sn.pdfFileName || 'shipment-note.pdf', 'application/pdf')"
                      />
                    </div>

                    <!-- Meta -->
                    <div class="d-flex gap-3 flex-wrap mb-2 text-caption text-medium-emphasis">
                      <span v-if="sn.awbNumber"><strong>Local Track#:</strong> {{ sn.awbNumber }}</span>
                      <span v-if="sn.soNumber"><strong>SO#:</strong> {{ sn.soNumber }}</span>
                      <span v-if="sn.destination"><strong>Dest:</strong> {{ sn.destination }}</span>
                      <span><strong>Created:</strong> {{ new Date(sn.createdAt).toLocaleDateString() }}</span>
                      <span v-if="sn.createdByName"><strong>By:</strong> {{ sn.createdByName }}</span>
                    </div>

                    <!-- Parts tree -->
                    <div v-if="sn.trackNumbers?.length">
                      <div class="text-caption font-weight-bold text-medium-emphasis mb-1">PARTS</div>
                      <div v-for="track in snGroupedTracks(sn)" :key="track.trackNumber" class="mb-2 ml-2">
                        <div class="d-flex align-center gap-1 mb-1">
                          <v-icon icon="mdi-barcode-scan" size="14" color="primary" />
                          <span class="text-caption font-weight-bold text-pn">{{ track.trackNumber }}</span>
                          <span v-if="track.carrier" class="text-caption text-medium-emphasis">· {{ track.carrier }}</span>
                        </div>
                        <v-table density="compact" class="rounded border ml-4">
                          <thead>
                            <tr><th>Part Number</th><th>Qty</th><th>Condition</th><th>Customer</th><th>Cert</th><th>Status</th></tr>
                          </thead>
                          <tbody>
                            <tr v-for="part in track.allParts" :key="part.trackNumberItemId">
                              <td>{{ part.partNumberName || '—' }}</td>
                              <td>{{ part.actualQty ?? '—' }}</td>
                              <td><v-chip v-if="part.condition" size="x-small" color="teal" variant="tonal">{{ part.condition }}</v-chip><span v-else class="text-medium-emphasis text-caption">—</span></td>
                              <td class="text-caption"><span v-if="part.customerCode || part.customerName">{{ part.customerCode }}</span><span v-else class="text-medium-emphasis">—</span></td>
                              <td>
                                <v-icon v-if="part.certNeeded" icon="mdi-certificate-outline" size="14" color="warning" />
                                <span v-else class="text-medium-emphasis text-caption">—</span>
                              </td>
                              <td><v-chip color="success" size="x-small" variant="tonal">{{ part.status }}</v-chip></td>
                            </tr>
                          </tbody>
                        </v-table>
                      </div>
                    </div>
                    <div v-else class="text-caption text-medium-emphasis">No parts linked.</div>
                  </v-card>
                </div>
              </v-expansion-panel-text>
            </v-expansion-panel>
          </v-expansion-panels>
        </template>
      </v-window-item>

    </v-window>

    <!-- ─── Track Detail Modal ─── -->
    <v-dialog v-model="detailDialog" max-width="860" scrollable>
      <v-card v-if="selected">
        <v-card-title class="d-flex align-center pa-4 pb-2 gap-3">
          <v-icon icon="mdi-package-variant-closed" color="primary" />
          <span class="text-h6 font-weight-bold">{{ selected.trackNumber }}</span>
          <v-chip :color="statusColor(selected.status)" size="small" variant="tonal" class="ml-1">
            {{ selected.status }}
          </v-chip>
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="detailDialog = false" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">

          <!-- Meta grid -->
          <v-row dense class="mb-4">
            <v-col cols="6" sm="3">
              <div class="text-caption text-medium-emphasis font-weight-bold text-uppercase">Carrier</div>
              <div class="text-body-2 font-weight-medium mt-1">{{ selected.carrier || '—' }}</div>
            </v-col>
            <!-- <v-col cols="6" sm="3">
              <div class="text-caption text-medium-emphasis font-weight-bold text-uppercase">PO Number</div>
              <div class="text-body-2 font-weight-medium mt-1">
                <NuxtLink v-if="selected.poId" :to="`/purchase-orders/${selected.poId}`" class="text-pn text-decoration-none">
                  {{ selected.poNumber || '—' }}
                </NuxtLink>
                <span v-else>{{ selected.poNumber || '—' }}</span>
              </div>
            </v-col> -->
            <v-col cols="6" sm="3">
              <div class="text-caption text-medium-emphasis font-weight-bold text-uppercase">Part Number</div>
              <div class="text-body-2 font-weight-medium mt-1">{{ selected.partNumberName || '—' }}</div>
            </v-col>
            <v-col cols="6" sm="3">
              <div class="text-caption text-medium-emphasis font-weight-bold text-uppercase">Created</div>
              <div class="text-body-2 font-weight-medium mt-1">{{ new Date(selected.createdAt).toLocaleString() }}</div>
            </v-col>
            <v-col cols="6" sm="3">
              <div class="text-caption text-medium-emphasis font-weight-bold text-uppercase">Warehouse</div>
              <div class="text-body-2 font-weight-medium mt-1">{{ selected.warehouseName || '—' }}</div>
            </v-col>
            <v-col v-if="selected.warehouseAddress" cols="12" sm="6">
              <div class="text-caption text-medium-emphasis font-weight-bold text-uppercase">Warehouse Address</div>
              <div class="text-body-2 font-weight-medium mt-1">{{ selected.warehouseAddress }}</div>
            </v-col>
            <v-col v-if="selected.notes" cols="12">
              <div class="text-caption text-medium-emphasis font-weight-bold text-uppercase">Notes</div>
              <div class="text-body-2 mt-1 pa-2 rounded bg-surface-variant">{{ selected.notes }}</div>
            </v-col>
          </v-row>

          <!-- Parts -->
          <div class="text-subtitle-2 font-weight-bold mb-2">
            <v-icon icon="mdi-format-list-checkbox" size="16" class="mr-1" />Parts Inventory Check
          </div>
          <div v-if="!selected.items?.length" class="text-caption text-medium-emphasis pa-3 rounded bg-surface-variant mb-4">
            No part entries yet.
          </div>
          <v-table v-else density="compact" class="rounded mb-4" style="border:1px solid rgba(var(--v-border-color),var(--v-border-opacity));">
            <thead>
              <tr>
                <th>Part Number</th>
                <th>Supplier</th>
                <th>Description</th>
                <th class="text-center">QTY</th>
                <th>Condition</th>
                <th>Customer</th>
                <th class="text-center">Expected</th>
                <th class="text-center">Actual</th>
                <th class="text-center">Available</th>
                <th class="text-center">Review</th>
                <th>Reviewed By</th>
                <th>Review Date</th>
                <th>Note</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="entry in selected.items" :key="entry.id">
                <td class="font-weight-medium">{{ entry.partNumberName || '—' }}</td>
                <td class="text-caption text-medium-emphasis">{{ entry.supplierName || '—' }}</td>
                <td class="text-caption" style="max-width:200px; white-space:normal;">{{ entry.description || '—' }}</td>
                <td class="text-center font-weight-bold">{{ entry.qty ?? '—' }}</td>
                <td><v-chip v-if="entry.condition" size="x-small" color="teal" variant="tonal">{{ entry.condition }}</v-chip><span v-else class="text-medium-emphasis text-caption">—</span></td>
                <td class="text-caption">
                  <span v-if="entry.customerCode">{{ entry.customerCode }}</span>
                  <span v-else class="text-medium-emphasis">—</span>
                </td>
                <td class="text-center">{{ entry.expectedQty }}</td>
                <td class="text-center">
                  <span :class="entry.actualQty != null && entry.actualQty !== entry.expectedQty ? 'text-error font-weight-bold' : ''">
                    {{ entry.actualQty ?? '—' }}
                  </span>
                  <v-icon v-if="entry.actualQty != null && entry.actualQty !== entry.expectedQty" icon="mdi-alert" color="warning" size="13" class="ml-1" />
                </td>
                <td class="text-center">
                  <v-icon v-if="entry.isAvailable != null" :icon="entry.isAvailable ? 'mdi-check-circle' : 'mdi-close-circle'" :color="entry.isAvailable ? 'success' : 'error'" size="18" />
                  <span v-else class="text-medium-emphasis">—</span>
                </td>
                <td class="text-center">
                  <v-chip :color="entry.status === 'Accepted' ? 'success' : entry.status === 'Rejected' ? 'error' : 'default'" size="x-small" variant="tonal">{{ entry.status }}</v-chip>
                </td>
                <td class="text-caption">{{ entry.reviewedByName || '—' }}</td>
                <td class="text-caption">{{ entry.reviewedAt ? new Date(entry.reviewedAt).toLocaleDateString() : '—' }}</td>
                <td class="text-caption" style="max-width:180px;">{{ entry.reviewNote || '—' }}</td>
              </tr>
            </tbody>
          </v-table>

          <!-- Received Boxes -->
          <div class="text-subtitle-2 font-weight-bold mb-2">
            <v-icon icon="mdi-package-variant" size="16" class="mr-1" />Received Boxes
            <v-chip v-if="selected.receivedBoxes?.length" size="x-small" color="orange" variant="tonal" class="ml-1">{{ selected.receivedBoxes.length }}</v-chip>
          </div>
          <div v-if="!selected.receivedBoxes?.length" class="text-caption text-medium-emphasis pa-3 rounded bg-surface-variant mb-4">No boxes recorded yet.</div>
          <v-table v-else density="compact" class="rounded mb-4" style="border:1px solid rgba(var(--v-border-color),var(--v-border-opacity));">
            <thead><tr><th>Box #</th><th class="text-center">Weight (kg)</th><th class="text-center">H (cm)</th><th class="text-center">W (cm)</th><th class="text-center">L (cm)</th><th>Notes</th><th>Recorded</th></tr></thead>
            <tbody>
              <tr v-for="box in selected.receivedBoxes" :key="box.id">
                <td class="font-weight-bold">#{{ box.boxNumber }}</td>
                <td class="text-center">{{ box.weightKg ?? '—' }}</td>
                <td class="text-center">{{ box.heightCm ?? '—' }}</td>
                <td class="text-center">{{ box.widthCm ?? '—' }}</td>
                <td class="text-center">{{ box.lengthCm ?? '—' }}</td>
                <td class="text-caption">{{ box.notes || '—' }}</td>
                <td class="text-caption">{{ new Date(box.createdAt).toLocaleDateString() }}</td>
              </tr>
            </tbody>
          </v-table>

          <!-- SN Boxes
          <div class="text-subtitle-2 font-weight-bold mb-2">
            <v-icon icon="mdi-truck-delivery-outline" size="16" class="mr-1" />SN# Shipping Boxes
            <v-chip v-if="selected.snBoxes?.length" size="x-small" color="blue" variant="tonal" class="ml-1">{{ selected.snBoxes.length }}</v-chip>
          </div>
          <div v-if="!selected.snBoxes?.length" class="text-caption text-medium-emphasis pa-3 rounded bg-surface-variant mb-4">No shipment note boxes linked yet.</div>
          <v-table v-else density="compact" class="rounded mb-4" style="border:1px solid rgba(var(--v-border-color),var(--v-border-opacity));">
            <thead><tr><th>SN#</th><th>Box #</th><th class="text-center">Weight (kg)</th><th class="text-center">H (cm)</th><th class="text-center">W (cm)</th><th class="text-center">L (cm)</th><th>Notes</th></tr></thead>
            <tbody>
              <tr v-for="box in selected.snBoxes" :key="box.id">
                <td class="text-caption font-weight-medium">{{ box.snNumber || '—' }}</td>
                <td class="font-weight-bold">#{{ box.boxNumber }}</td>
                <td class="text-center">{{ box.weightKg ?? '—' }}</td>
                <td class="text-center">{{ box.heightCm ?? '—' }}</td>
                <td class="text-center">{{ box.widthCm ?? '—' }}</td>
                <td class="text-center">{{ box.lengthCm ?? '—' }}</td>
                <td class="text-caption">{{ box.notes || '—' }}</td>
              </tr>
            </tbody>
          </v-table> -->

          <!-- Documents -->
          <div class="text-subtitle-2 font-weight-bold mb-2">
            <v-icon icon="mdi-file-multiple-outline" size="16" class="mr-1" />Documents
          </div>
          <div v-if="!selected.documents?.length" class="text-caption text-medium-emphasis pa-3 rounded bg-surface-variant">No documents uploaded yet.</div>
          <v-list v-else density="compact" class="rounded pa-0 mb-2" style="border:1px solid rgba(var(--v-border-color),var(--v-border-opacity));">
            <v-list-item v-for="doc in selected.documents" :key="doc.id" :prepend-icon="docIcon(doc.mimeType)" class="px-3">
              <v-list-item-title class="text-body-2">
                {{ doc.originalFileName }}
                <v-chip v-if="doc.poItemId == null" size="x-small" color="blue" variant="tonal" class="ml-2">Track-level</v-chip>
                <v-chip v-else size="x-small" color="teal" variant="tonal" class="ml-2">{{ doc.partNumberName || 'Part' }}</v-chip>
              </v-list-item-title>
              <v-list-item-subtitle class="text-caption">
                {{ formatBytes(doc.fileSizeBytes) }} · Uploaded {{ new Date(doc.uploadedAt).toLocaleDateString() }} by {{ doc.uploadedByName || '—' }}
              </v-list-item-subtitle>
              <template #append>
                <v-btn icon="mdi-download" size="x-small" variant="text" color="primary" :href="docDownloadUrl(doc.id)" target="_blank" />
              </template>
            </v-list-item>
          </v-list>

        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-3">
          <v-spacer />
          <v-btn variant="tonal" @click="detailDialog = false">Close</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- ─── SN# Full Details Modal (from T-ID tree) ─── -->
    <v-dialog v-model="snModal" max-width="860" scrollable>
      <v-card v-if="selectedSn">
        <v-card-title class="d-flex align-center pa-4 pb-2 gap-3">
          <v-icon icon="mdi-note-text-outline" color="primary" />
          <span class="text-h6 font-weight-bold">{{ selectedSn.snNumber }}</span>
          <v-chip :color="snStatusColor(selectedSn.status)" size="small" variant="tonal" class="ml-1">{{ selectedSn.status }}</v-chip>
          <v-chip v-if="selectedSn.type" size="x-small" color="secondary" variant="outlined" class="ml-1">{{ selectedSn.type }}</v-chip>
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="snModal = false" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">

          <!-- SN# meta -->
          <v-row dense class="mb-4">
            <v-col cols="6" sm="3">
              <div class="text-caption text-medium-emphasis font-weight-bold text-uppercase">T-ID</div>
              <div class="text-body-2 font-weight-bold mt-1 text-pn">{{ selectedSn.tId || '—' }}</div>
            </v-col>
            <v-col cols="6" sm="3">
              <div class="text-caption text-medium-emphasis font-weight-bold text-uppercase">Local Track#</div>
              <div class="text-body-2 font-weight-medium mt-1">{{ selectedSn.awbNumber || '—' }}</div>
            </v-col>
            <v-col cols="6" sm="3">
              <div class="text-caption text-medium-emphasis font-weight-bold text-uppercase">Warehouse</div>
              <div class="text-body-2 font-weight-medium mt-1">{{ selectedSn.warehouseName || '—' }}</div>
            </v-col>
            <v-col cols="6" sm="3">
              <div class="text-caption text-medium-emphasis font-weight-bold text-uppercase">Created</div>
              <div class="text-body-2 font-weight-medium mt-1">{{ new Date(selectedSn.createdAt).toLocaleString() }}</div>
            </v-col>
            <v-col v-if="selectedSn.createdByName" cols="6" sm="3">
              <div class="text-caption text-medium-emphasis font-weight-bold text-uppercase">Created By</div>
              <div class="text-body-2 font-weight-medium mt-1">{{ selectedSn.createdByName }}</div>
            </v-col>
          </v-row>

          <!-- Track Numbers / Parts -->
          <div class="text-subtitle-2 font-weight-bold mb-2">
            <v-icon icon="mdi-format-list-checkbox" size="16" class="mr-1" />Parts & Track Numbers
          </div>
          <div v-if="!selectedSn.trackNumbers?.length" class="text-caption text-medium-emphasis pa-3 rounded bg-surface-variant mb-4">
            No track numbers linked.
          </div>
          <v-table v-else density="compact" class="rounded mb-4" style="border:1px solid rgba(var(--v-border-color),var(--v-border-opacity));">
            <thead>
              <tr>
                <th>Part Number</th>
                <th>Supplier</th>
                <th>Description</th>
                <th class="text-center">QTY</th>
                <th>Condition</th>
                <th>Customer</th>
                <th>Track #</th>
                <th>Carrier</th>
                <th class="text-center">Actual Qty</th>
                <th class="text-center">Item Status</th>
              </tr>
            </thead>
            <tbody>
              <template v-for="track in selectedSn.trackNumbers" :key="track.trackNumberId">
                <tr v-for="itm in track.items" :key="itm.trackNumberItemId">
                  <td class="font-weight-medium">{{ itm.partNumberName || track.partNumberName || '—' }}</td>
                  <td class="text-caption text-medium-emphasis">{{ track.supplierName || '—' }}</td>
                  <td class="text-caption" style="max-width:200px; white-space:normal;">{{ track.description || '—' }}</td>
                  <td class="text-center font-weight-bold">{{ track.qty || '—' }}</td>
                  <td>
                    <v-chip v-if="track.condition" size="x-small" color="teal" variant="tonal">{{ track.condition }}</v-chip>
                    <span v-else class="text-medium-emphasis text-caption">—</span>
                  </td>
                  <td class="text-caption">{{ track.customerCode || '—' }}</td>
                  <td class="text-caption">{{ track.trackNumber }}</td>
                  <td class="text-caption">{{ track.carrier || '—' }}</td>
                  <td class="text-center">{{ itm.actualQty ?? '—' }}</td>
                  <td class="text-center">
                    <v-chip :color="itm.status === 'Accepted' ? 'success' : itm.status === 'Rejected' ? 'error' : 'default'" size="x-small" variant="tonal">
                      {{ itm.status }}
                    </v-chip>
                  </td>
                </tr>
              </template>
            </tbody>
          </v-table>

          <!-- SN# Boxes -->
          <div class="text-subtitle-2 font-weight-bold mb-2">
            <v-icon icon="mdi-package-variant" size="16" class="mr-1" />Shipping Boxes
            <v-chip v-if="selectedSn.boxes?.length" size="x-small" color="blue" variant="tonal" class="ml-1">{{ selectedSn.boxes.length }}</v-chip>
          </div>
          <div v-if="!selectedSn.boxes?.length" class="text-caption text-medium-emphasis pa-3 rounded bg-surface-variant mb-4">No boxes added yet.</div>
          <v-table v-else density="compact" class="rounded mb-4" style="border:1px solid rgba(var(--v-border-color),var(--v-border-opacity));">
            <thead><tr><th>Box #</th><th class="text-center">Weight (kg)</th><th class="text-center">H (cm)</th><th class="text-center">W (cm)</th><th class="text-center">L (cm)</th><th>Notes</th></tr></thead>
            <tbody>
              <tr v-for="box in selectedSn.boxes" :key="box.id">
                <td class="font-weight-bold">#{{ box.boxNumber }}</td>
                <td class="text-center">{{ box.weightKg ?? '—' }}</td>
                <td class="text-center">{{ box.heightCm ?? '—' }}</td>
                <td class="text-center">{{ box.widthCm ?? '—' }}</td>
                <td class="text-center">{{ box.lengthCm ?? '—' }}</td>
                <td class="text-caption">{{ box.notes || '—' }}</td>
              </tr>
            </tbody>
          </v-table>

          <!-- Customs / PDF -->
          <div v-if="selectedSn.customsOriginalFileName || selectedSn.pdfFileName" class="text-subtitle-2 font-weight-bold mb-2">
            <v-icon icon="mdi-file-multiple-outline" size="16" class="mr-1" />Documents
          </div>
          <div v-if="selectedSn.pdfFileName" class="d-flex align-center gap-2 mb-2">
            <v-icon icon="mdi-file-pdf-box" color="error" size="20" />
            <span class="text-body-2">SN# PDF</span>
            <v-chip size="x-small" color="primary" variant="tonal">SN Document</v-chip>
            <v-spacer />
            <v-btn
              size="x-small"
              variant="tonal"
              color="error"
              prepend-icon="mdi-eye-outline"
              @click="docPreview.preview(`/shipment-notes/${selectedSn.id}/pdf-file`, selectedSn.pdfFileName || 'shipment-note.pdf', 'application/pdf')"
            >Preview</v-btn>
            <v-btn
              size="x-small"
              variant="tonal"
              color="primary"
              prepend-icon="mdi-download"
              :href="`${api.baseURL}/shipment-notes/${selectedSn.id}/pdf-file`"
              target="_blank"
            >Download</v-btn>
          </div>
          <div v-if="selectedSn.customsOriginalFileName" class="d-flex align-center gap-2 mb-2">
            <v-icon icon="mdi-file-document-outline" color="deep-purple" size="20" />
            <span class="text-body-2">{{ selectedSn.customsOriginalFileName }}</span>
            <v-chip size="x-small" color="deep-purple" variant="tonal">Customs</v-chip>
            <span v-if="selectedSn.customsUploadedAt" class="text-caption text-medium-emphasis">
              {{ new Date(selectedSn.customsUploadedAt).toLocaleDateString() }}
            </span>
            <v-spacer />
            <v-btn
              size="x-small"
              variant="tonal"
              color="deep-purple"
              prepend-icon="mdi-download"
              :href="`${api.baseURL}/shipment-notes/${selectedSn.id}/customs-file`"
              target="_blank"
            >Download</v-btn>
          </div>

        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-3">
          <v-spacer />
          <v-btn variant="tonal" @click="snModal = false">Close</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- ══════════════ Shared dialogs from Shipping Number ══════════════ -->

    <!-- Edit T-ID / SO# / Destination Dialog (Admin only) -->
    <v-dialog v-model="editTIdDialog" max-width="480" persistent>
      <v-card>
        <v-card-title class="text-h6 pa-4 pb-2">
          <v-icon icon="mdi-identifier" class="mr-2" color="primary" />
          Edit T-ID / SO# / Destination
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-text-field
            v-model="editTIdValue"
            label="T-ID"
            variant="outlined"
            density="compact"
            prepend-inner-icon="mdi-identifier"
            class="mb-3"
            autofocus
          />
          <v-text-field
            v-model="editSONumberValue"
            label="SO#"
            variant="outlined"
            density="compact"
            prepend-inner-icon="mdi-file-document-outline"
            class="mb-3"
          />
          <v-combobox
            v-model="editDestinationValue"
            :items="warehouses"
            item-title="name"
            :return-object="false"
            label="Destination (warehouse or free text)"
            variant="outlined"
            density="compact"
            prepend-inner-icon="mdi-map-marker-outline"
            clearable
          />
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4 pt-2">
          <v-spacer />
          <v-btn variant="text" @click="editTIdDialog = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :loading="savingTId" @click="saveTId">Save</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Edit AWB Dialog -->
    <v-dialog v-model="editAwbDialog" max-width="420" persistent>
      <v-card>
        <v-card-title class="text-h6 pa-4 pb-2">
          <v-icon icon="mdi-airplane-landing" class="mr-2" color="warning" />
          Enter Local Track# Number
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-text-field
            v-model="editAwbValue"
            label="Local Track# Number"
            variant="outlined"
            density="compact"
            prepend-inner-icon="mdi-airplane-landing"
            autofocus
          />
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4 pt-2">
          <v-spacer />
          <v-btn variant="text" @click="editAwbDialog = false">Cancel</v-btn>
          <v-btn color="warning" variant="flat" :loading="savingAwb" @click="saveAwb">Save</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Upload SN PDF Dialog -->
    <v-dialog v-model="uploadPdfDialog" max-width="420" persistent>
      <v-card>
        <v-card-title class="text-h6 pa-4 pb-2">Upload SN# PDF</v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-file-input
            v-model="uploadPdfFile"
            label="Select PDF"
            variant="outlined"
            density="compact"
            accept="application/pdf,.pdf"
            prepend-icon="mdi-file-pdf-box"
          />
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4 pt-2">
          <v-spacer />
          <v-btn variant="text" @click="uploadPdfDialog = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :loading="uploadingPdf" :disabled="!uploadPdfFile" @click="doUploadPdf">Upload</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Add Track Number Dialog -->
    <v-dialog v-model="addTrackDialog" max-width="400" persistent>
      <v-card>
        <v-card-title class="text-h6 pa-4 pb-2">Add Track Number to SN#</v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-autocomplete
            v-model="selectedTrackId"
            :items="availableTracks"
            item-title="trackNumber"
            item-value="id"
            label="Select Track Number"
            variant="outlined"
            density="compact"
            :loading="tracksLoading"
          />
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4 pt-2">
          <v-spacer />
          <v-btn variant="text" @click="addTrackDialog = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :loading="addingTrack" :disabled="!selectedTrackId" @click="doAddTrack">Add</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Upload Customs File Dialog -->
    <v-dialog v-model="uploadCustomsDialog" max-width="480" persistent>
      <v-card>
        <v-card-title class="text-h6 pa-4 pb-2">
          <v-icon icon="mdi-file-document-check-outline" class="mr-2" color="deep-purple" />
          Upload Customs File
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <p class="text-caption text-medium-emphasis mb-3">
            Uploading the customs file will advance the status to <strong>Clearing Customs</strong>.
            <span v-if="uploadCustomsItem?.type === 'CPT'">For CPT, it will automatically advance to <strong>Delivered to Customer</strong>.</span>
          </p>
          <v-alert
            v-if="uploadCustomsSiblingCount > 0"
            type="info"
            density="compact"
            class="mb-3"
          >
            This SN# shares T-ID <strong>{{ uploadCustomsItem?.tId }}</strong> with {{ uploadCustomsSiblingCount }} other SN#(s) at "Ship To USA" — the customs file will be applied to all of them automatically.
          </v-alert>
          <v-file-input
            v-model="uploadCustomsFile"
            label="Select Customs Document"
            variant="outlined"
            density="compact"
            prepend-icon="mdi-file-document-outline"
          />
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4 pt-2">
          <v-spacer />
          <v-btn variant="text" @click="uploadCustomsDialog = false">Cancel</v-btn>
          <v-btn color="deep-purple" variant="flat" :loading="uploadingCustoms" :disabled="!uploadCustomsFile" @click="doUploadCustoms">
            Upload
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- SN# Box Dialog -->
    <v-dialog v-model="snBoxDialog" max-width="480" persistent>
      <v-card>
        <v-card-title class="text-h6 pa-4 pb-2">{{ editingSnBox ? 'Edit Box' : 'Add Box' }}</v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-row dense>
            <v-col cols="4">
              <v-text-field v-model.number="snBoxForm.boxNumber" label="Box #" type="number" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="8">
              <v-text-field v-model.number="snBoxForm.weightKg" label="Weight (kg)" type="number" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="4">
              <v-text-field v-model.number="snBoxForm.heightCm" label="Height (cm)" type="number" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="4">
              <v-text-field v-model.number="snBoxForm.widthCm" label="Width (cm)" type="number" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="4">
              <v-text-field v-model.number="snBoxForm.lengthCm" label="Length (cm)" type="number" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12">
              <v-textarea v-model="snBoxForm.notes" label="Notes" variant="outlined" density="compact" rows="2" />
            </v-col>
          </v-row>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4 pt-2">
          <v-spacer />
          <v-btn variant="text" @click="snBoxDialog = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :loading="savingSnBox" @click="saveSnBox">Save</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snack" :color="snackColor" timeout="3000" location="top right">{{ snackMsg }}</v-snackbar>

    <DocPreviewModal
      :open="docPreview.open.value"
      :blob-url="docPreview.blobUrl.value"
      :file-name="docPreview.fileName.value"
      :mime-type="docPreview.mimeType.value"
      @close="docPreview.close()"
    />
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'default' })

const api = useApi()
const config = useRuntimeConfig()
const authStore = useAuthStore()
const isSydOrAdmin = computed(() => authStore.isAdmin || authStore.user?.name === 'SYD')
const docPreview = useDocPreview()
const route = useRoute()

// ── Shared helpers ───────────────────────────────────────────────────────────

const allStatuses = [
  'Ship to Warehouse', 'Received in Warehouse', 'Waiting for Packing',
  'Ship To USA', 'Clearing Customs', 'Received in Office', 'Delivered to Customer', 'Rejected',
]

function statusColor(status: string) {
  const map: Record<string, string> = {
    'Ship to Warehouse': 'blue-grey', 'Received in Warehouse': 'orange',
    'Waiting for Packing': 'amber', 'Ship To USA': 'blue',
    'Clearing Customs': 'deep-purple', 'Received in Office': 'teal',
    'Delivered to Customer': 'success', 'Rejected': 'error',
  }
  return map[status] ?? 'default'
}

function snStatusColor(status: string) {
  const map: Record<string, string> = {
    'Draft': 'default', 'Waiting for Packing': 'amber', 'Ship To USA': 'blue',
    'Clearing Customs': 'deep-purple', 'Received in Office': 'teal',
    'Delivered to Customer': 'success', 'Confirmed': 'green',
  }
  return map[status] ?? 'default'
}

function docIcon(mime?: string) {
  if (!mime) return 'mdi-file-outline'
  if (mime.startsWith('image/')) return 'mdi-file-image-outline'
  if (mime === 'application/pdf') return 'mdi-file-pdf-box'
  if (mime.includes('word')) return 'mdi-file-word-outline'
  if (mime.includes('excel') || mime.includes('spreadsheet')) return 'mdi-file-excel-outline'
  return 'mdi-file-outline'
}

function formatBytes(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

function docDownloadUrl(docId: number) {
  const base = (config.public as any).apiBase ?? '/api'
  return `${base}/shipping/documents/${docId}/file`
}

// ── Tab 1 — Track Numbers (grouped) ─────────────────────────────────────────

const activeTab = ref('tracks')
const search = ref('')
const filterWarehouse = ref<number | null>(null)
const filterStatus = ref<string | null>(null)
const warehouses = ref<any[]>([])
const items = ref<any[]>([])
const totalItems = ref(0)
const loading = ref(false)
const currentPage = ref(1)
const pageSize = 50

// Group raw items by trackNumber + carrier composite key
const trackGroups = computed(() => {
  const map = new Map<string, any>()
  for (const rec of items.value) {
    const key = `${rec.trackNumber}||${rec.carrier ?? ''}`
    if (!map.has(key)) {
      map.set(key, {
        key,
        trackNumber: rec.trackNumber,
        carrier: rec.carrier,
        status: rec.status,
        warehouseName: rec.warehouseName,
        createdAt: rec.createdAt,
        records: [],
      })
    }
    map.get(key)!.records.push(rec)
  }
  return Array.from(map.values())
})

const expandedTracks = ref<Set<string>>(new Set())

function toggleTrackExpand(key: string) {
  if (expandedTracks.value.has(key)) {
    expandedTracks.value.delete(key)
  } else {
    expandedTracks.value.add(key)
  }
  expandedTracks.value = new Set(expandedTracks.value)
}

// Detail modal — single part record
const detailDialog = ref(false)
const selected = ref<any>(null)

function openDetail(_event: any, { item }: { item: any }) {
  selected.value = item
  detailDialog.value = true
}

// Detail modal — whole group (merged across all records in the group)
function openGroupDetail(group: any) {
  const first = group.records[0]
  // Merge items, receivedBoxes, snBoxes, documents — deduplicated by id
  const merge = (field: string) => {
    const seen = new Set<number>()
    const result: any[] = []
    for (const rec of group.records) {
      for (const entry of rec[field] ?? []) {
        if (!seen.has(entry.id)) { seen.add(entry.id); result.push(entry) }
      }
    }
    return result
  }
  selected.value = {
    trackNumber: group.trackNumber,
    carrier: group.carrier,
    status: group.status,
    warehouseName: first.warehouseName,
    warehouseAddress: first.warehouseAddress,
    notes: first.notes,
    poNumber: group.records.map((r: any) => r.poNumber).filter(Boolean).join(', '),
    poId: first.poId,
    partNumberName: group.records.map((r: any) => r.partNumberName).filter(Boolean).join(', '),
    createdAt: first.createdAt,
    items: merge('items'),
    receivedBoxes: merge('receivedBoxes'),
    snBoxes: merge('snBoxes'),
    documents: merge('documents'),
  }
  detailDialog.value = true
}

async function loadItems() {
  loading.value = true
  expandedTracks.value = new Set()
  try {
    const params = new URLSearchParams({
      page: String(currentPage.value),
      pageSize: String(pageSize),
    })
    if (search.value) params.set('search', search.value)
    if (filterWarehouse.value) params.set('warehouseId', String(filterWarehouse.value))
    if (filterStatus.value) params.set('status', filterStatus.value)
    const res = await api.get(`/purchase-orders/track-numbers/summary?${params}`)
    items.value = res.items ?? res
    totalItems.value = res.totalCount ?? res.length ?? 0
    // Auto-expand all groups on load
    nextTick(() => {
      expandedTracks.value = new Set(trackGroups.value.map(g => g.key))
    })
  } finally {
    loading.value = false
  }
}

function onFilterChange() {
  currentPage.value = 1
  loadItems()
}

function changePage(page: number) {
  currentPage.value = page
  loadItems()
}

// ── Tab 2 — T-ID Tree ────────────────────────────────────────────────────────

const tidLoading = ref(false)
const tidGroups = ref<any[]>([])
const tidSearch = ref('')
const expandedTids = ref<Set<string>>(new Set())
const snModal = ref(false)
const selectedSn = ref<any>(null)
let tidLoaded = false

async function loadTidGroups() {
  tidLoading.value = true
  try {
    // Load all SN#s (no pagination — SN count is manageable)
    const res = await api.get('/shipment-notes?page=1&pageSize=500')
    const notes: any[] = res.items ?? res

    // Keep only those with a TId and not yet confirmed
    const withTid = notes.filter((n: any) => n.tId && n.status !== 'Confirmed')

    // Group by tId
    const map = new Map<string, any[]>()
    for (const n of withTid) {
      const key = n.tId as string
      if (!map.has(key)) map.set(key, [])
      map.get(key)!.push(n)
    }

    tidGroups.value = Array.from(map.entries())
      .map(([tId, notes]) => ({ tId, notes }))
      .sort((a, b) => a.tId.localeCompare(b.tId))

    // Auto-expand all groups
    expandedTids.value = new Set(tidGroups.value.map(g => g.tId))
    tidLoaded = true
  } finally {
    tidLoading.value = false
  }
}

const filteredTidGroups = computed(() => {
  const q = tidSearch.value.trim().toLowerCase()
  if (!q) return tidGroups.value
  return tidGroups.value.filter(g =>
    g.tId.toLowerCase().includes(q) ||
    g.notes.some((n: any) => n.snNumber?.toLowerCase().includes(q))
  )
})

function toggleTidExpand(tId: string) {
  if (expandedTids.value.has(tId)) {
    expandedTids.value.delete(tId)
  } else {
    expandedTids.value.add(tId)
  }
  // trigger reactivity
  expandedTids.value = new Set(expandedTids.value)
}

function openSnModal(sn: any) {
  selectedSn.value = sn
  snModal.value = true
}

function tidSummaryStatus(group: any): string {
  const statuses: string[] = group.notes.map((n: any) => n.status)
  const priority = [
    'Delivered to Customer', 'Confirmed', 'Received in Office',
    'Clearing Customs', 'Ship To USA', 'Waiting for Packing', 'Draft',
  ]
  for (const s of priority) {
    if (statuses.includes(s)) return s
  }
  return statuses[0] ?? '—'
}

function tidStatusColor(group: any): string {
  return snStatusColor(tidSummaryStatus(group))
}

// ── Tab 3 — Shipping Number (Integrated from shipment-notes) ────────────────

const snNotes = ref<any[]>([])
const snFinishedNotes = ref<any[]>([])
const allSnNotes = ref<any[]>([])
const snLoading = ref(false)
const snExpanded = ref<any[]>([])
const finishedExpanded = ref<any[]>([])
const finishedViewMode = ref<'flat' | 'tid'>('flat')
const finishedTidOpen = ref<string[]>([])
const snFilterWarehouse = ref<number | null>(null)
const snTidSearch = ref('')
const snOpenTids = ref<string[]>([])

const snHeaders = [
  { title: 'SN#', key: 'snNumber', sortable: true },
  { title: 'Type', key: 'type', sortable: false },
  { title: 'Warehouse', key: 'warehouseName', sortable: false },
  { title: 'Status', key: 'status', sortable: true },
  { title: 'T-ID', key: 'tId', sortable: false },
  { title: 'SO#', key: 'soNumber', sortable: false },
  { title: 'Destination', key: 'destination', sortable: false },
  { title: 'Local Track#', key: 'awbNumber', sortable: false },
  { title: 'Created At', key: 'createdAt', sortable: true },
  { title: '', key: 'actions', sortable: false, align: 'end' as const },
  { title: '', key: 'data-table-expand' },
]

const finishedHeaders = [
  { title: 'SN#', key: 'snNumber', sortable: true },
  { title: 'Type', key: 'type', sortable: false },
  { title: 'Warehouse', key: 'warehouseName', sortable: false },
  { title: 'Status', key: 'status', sortable: false },
  { title: 'T-ID', key: 'tId', sortable: false },
  { title: 'SO#', key: 'soNumber', sortable: false },
  { title: 'Destination', key: 'destination', sortable: false },
  { title: 'Local Track#', key: 'awbNumber', sortable: false },
  { title: 'Created At', key: 'createdAt', sortable: true },
  { title: '', key: 'actions', sortable: false, align: 'end' as const },
  { title: '', key: 'data-table-expand' },
]

const snTidGroups = computed(() => {
  const map = new Map<string, any>()
  for (const sn of snNotes.value) {
    const key = sn.tId || '__no_tid__'
    if (!map.has(key)) {
      map.set(key, { tId: sn.tId || '', notes: [], statusSummary: [] })
    }
    map.get(key)!.notes.push(sn)
  }
  for (const g of map.values()) {
    const cnt: Record<string, number> = {}
    for (const sn of g.notes) cnt[sn.status] = (cnt[sn.status] ?? 0) + 1
    g.statusSummary = Object.entries(cnt).map(([status, count]) => ({ status, count }))
  }
  return [...map.values()]
})

const filteredSnTidGroups = computed(() => {
  const q = snTidSearch.value.trim().toLowerCase()
  if (!q) return snTidGroups.value
  return snTidGroups.value.filter(g =>
    g.tId?.toLowerCase().includes(q) ||
    g.notes.some((sn: any) =>
      sn.snNumber?.toLowerCase().includes(q) ||
      sn.trackNumbers?.some((tn: any) =>
        tn.trackNumber?.toLowerCase().includes(q) ||
        tn.items?.some((i: any) => i.partNumberName?.toLowerCase().includes(q))
      )
    )
  )
})

watch(snTidGroups, (groups) => {
  snOpenTids.value = groups.map(g => g.tId || '__no_tid__')
}, { immediate: true })

const editTIdDialog = ref(false)
const editTIdSnId = ref<number | null>(null)
const editTIdValue = ref('')
const editSONumberValue = ref('')
const editDestinationValue = ref<string | null>(null)
const savingTId = ref(false)

const editAwbDialog = ref(false)
const editAwbSnId = ref<number | null>(null)
const editAwbValue = ref('')
const savingAwb = ref(false)

const uploadPdfDialog = ref(false)
const uploadPdfFile = ref<File | null>(null)
const uploadingPdf = ref(false)
const uploadingSnId = ref<number | null>(null)

const addTrackDialog = ref(false)
const addingTrackSnId = ref<number | null>(null)
const selectedTrackId = ref<number | null>(null)
const availableTracks = ref<any[]>([])
const tracksLoading = ref(false)
const addingTrack = ref(false)

const uploadCustomsDialog = ref(false)
const uploadCustomsItem = ref<any>(null)
const uploadCustomsFile = ref<File | null>(null)
const uploadingCustoms = ref(false)

const uploadCustomsSiblingCount = computed(() => {
  const item = uploadCustomsItem.value
  if (!item?.tId) return 0
  return snNotes.value.filter(n => n.tId === item.tId && n.id !== item.id && n.status === 'Ship To USA').length
})

const snBoxDialog = ref(false)
const snBoxSnItem = ref<any>(null)
const editingSnBox = ref<any>(null)
const savingSnBox = ref(false)
const snBoxForm = reactive({ boxNumber: 1, weightKg: null as number | null, heightCm: null as number | null, widthCm: null as number | null, lengthCm: null as number | null, notes: '' })

const snack = ref(false)
const snackMsg = ref('')
const snackColor = ref('success')
function notify(msg: string, color = 'success') { snackMsg.value = msg; snackColor.value = color; snack.value = true }

function snGroupedTracks(item: any) {
  const map = new Map<string, any>()
  for (const t of item.trackNumbers ?? []) {
    if (!map.has(t.trackNumber)) {
      map.set(t.trackNumber, { trackNumber: t.trackNumber, carrier: t.carrier, primaryTrackNumberId: t.trackNumberId, allParts: [], allBoxes: [] })
    }
    const g = map.get(t.trackNumber)!
    // Spread track-level fields (condition, customer) onto each part item
    const enriched = (t.items ?? []).map((i: any) => ({
      ...i,
      condition: i.condition ?? t.condition,
      customerName: i.customerName ?? t.customerName,
      customerCode: i.customerCode ?? t.customerCode,
    }))
    g.allParts.push(...enriched)
    for (const box of t.receivedBoxes ?? []) {
      if (!g.allBoxes.some((b: any) => b.id === box.id)) g.allBoxes.push(box)
    }
  }
  return [...map.values()]
}

async function loadSnNotes() {
  snLoading.value = true
  try {
    const params = snFilterWarehouse.value ? `?warehouseId=${snFilterWarehouse.value}` : ''
    const all: any[] = await api.get(`/shipment-notes${params}`)
    allSnNotes.value = all
    snNotes.value = all.filter((n: any) => n.status !== 'Confirmed')
    snFinishedNotes.value = all.filter((n: any) => n.status === 'Confirmed')
  } finally {
    snLoading.value = false
  }
}

function setSnWarehouseFilter(id: number | null) {
  snFilterWarehouse.value = id
  loadSnNotes()
}

// T-ID groups where every SN# is Confirmed — shown in Finished > By T-ID view
const finishedTidGroups = computed(() => {
  const map = new Map<string, any[]>()
  for (const sn of allSnNotes.value) {
    if (!sn.tId) continue
    if (!map.has(sn.tId)) map.set(sn.tId, [])
    map.get(sn.tId)!.push(sn)
  }
  const groups: any[] = []
  for (const [tId, notes] of map.entries()) {
    if (notes.every((n: any) => n.status === 'Confirmed')) {
      groups.push({ tId, notes })
    }
  }
  return groups.sort((a, b) => a.tId.localeCompare(b.tId))
})

// Auto-expand all T-ID groups when they load
watch(finishedTidGroups, (groups) => {
  finishedTidOpen.value = groups.map(g => g.tId)
}, { immediate: true })

function openEditTId(item: any) {
  editTIdSnId.value = item.id
  editTIdValue.value = item.tId || ''
  editSONumberValue.value = item.soNumber || ''
  editDestinationValue.value = item.destination || null
  editTIdDialog.value = true
}

async function saveTId() {
  if (!editTIdSnId.value) return
  savingTId.value = true
  try {
    await api.put(`/shipment-notes/${editTIdSnId.value}`, {
      tId: editTIdValue.value || null,
      soNumber: editSONumberValue.value || null,
      destination: editDestinationValue.value || null,
    })
    notify('Saved')
    editTIdDialog.value = false
    await loadSnNotes()
  } catch {
    notify('Failed to save', 'error')
  } finally {
    savingTId.value = false
  }
}

function openEditAwb(item: any) {
  editAwbSnId.value = item.id
  editAwbValue.value = item.awbNumber || ''
  editAwbDialog.value = true
}

async function saveAwb() {
  if (!editAwbSnId.value) return
  savingAwb.value = true
  try {
    await api.patch(`/shipment-notes/${editAwbSnId.value}/awb`, { awbNumber: editAwbValue.value || null })
    notify('Local Track# saved')
    editAwbDialog.value = false
    await loadSnNotes()
  } catch {
    notify('Failed to save Local Track#', 'error')
  } finally {
    savingAwb.value = false
  }
}

async function confirmSn(item: any) {
  try {
    await api.post(`/shipment-notes/${item.id}/confirm`, {})
    notify('SN# confirmed')
    await loadSnNotes()
  } catch { notify('Failed to confirm', 'error') }
}

function openUploadPdf(item: any) {
  uploadingSnId.value = item.id
  uploadPdfFile.value = null
  uploadPdfDialog.value = true
}

async function doUploadPdf() {
  if (!uploadPdfFile.value || !uploadingSnId.value) return
  uploadingPdf.value = true
  try {
    const fd = new FormData()
    fd.append('file', uploadPdfFile.value as Blob)
    await $fetch(`${api.baseURL}/shipment-notes/${uploadingSnId.value}/upload-pdf`, {
      method: 'POST', body: fd,
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    notify('PDF uploaded')
    uploadPdfDialog.value = false
    await loadSnNotes()
  } catch { notify('Upload failed', 'error') }
  finally { uploadingPdf.value = false }
}

async function openAddTrack(item: any) {
  addingTrackSnId.value = item.id
  selectedTrackId.value = null
  addTrackDialog.value = true
  tracksLoading.value = true
  try { availableTracks.value = await api.get('/shipping/track-numbers') }
  finally { tracksLoading.value = false }
}

async function doAddTrack() {
  if (!addingTrackSnId.value || !selectedTrackId.value) return
  addingTrack.value = true
  try {
    await api.post(`/shipment-notes/${addingTrackSnId.value}/track-numbers/${selectedTrackId.value}`, {})
    notify('Track number added')
    addTrackDialog.value = false
    await loadSnNotes()
  } catch { notify('Failed to add track', 'error') }
  finally { addingTrack.value = false }
}

async function removeSnTrack(snId: number, trackId: number) {
  try {
    await api.delete(`/shipment-notes/${snId}/track-numbers/${trackId}`)
    notify('Track removed')
    await loadSnNotes()
  } catch { notify('Failed to remove', 'error') }
}

function openUploadCustoms(item: any) {
  uploadCustomsItem.value = item
  uploadCustomsFile.value = null
  uploadCustomsDialog.value = true
}

function openUploadCustomsForGroup(group: any) {
  const target = group.notes.find((n: any) => n.status === 'Ship To USA')
  if (target) openUploadCustoms(target)
}

async function doUploadCustoms() {
  if (!uploadCustomsFile.value || !uploadCustomsItem.value) return
  uploadingCustoms.value = true
  try {
    const fd = new FormData()
    fd.append('file', uploadCustomsFile.value as Blob)
    await $fetch(`${api.baseURL}/shipment-notes/${uploadCustomsItem.value.id}/customs-file`, {
      method: 'POST', body: fd,
      headers: { Authorization: `Bearer ${authStore.user?.token}` },
    })
    const sibCount = uploadCustomsSiblingCount.value
    notify(sibCount > 0
      ? `Customs file uploaded — applied to this SN# and ${sibCount} sibling(s) with same T-ID`
      : 'Customs file uploaded — status advanced to Clearing Customs')
    uploadCustomsDialog.value = false
    await loadSnNotes()
  } catch { notify('Upload failed', 'error') }
  finally { uploadingCustoms.value = false }
}

function nextSnAdminStatus(item: any): string | null {
  if (item.status === 'Draft') return 'Waiting for Packing'
  if (item.status === 'Clearing Customs' && item.type === 'DDP') return 'Received in Office'
  if (item.status === 'Received in Office') return 'Delivered to Customer'
  return null
}

async function advanceSnStatus(item: any) {
  const next = nextSnAdminStatus(item)
  if (!next) return
  try {
    await api.patch(`/shipment-notes/${item.id}/status`, { status: next })
    notify(`Status updated to: ${next}`)
    await loadSnNotes()
  } catch { notify('Failed to update status', 'error') }
}

function openAddSnBox(item: any) {
  snBoxSnItem.value = item
  editingSnBox.value = null
  Object.assign(snBoxForm, { boxNumber: (item.boxes?.length ?? 0) + 1, weightKg: null, heightCm: null, widthCm: null, lengthCm: null, notes: '' })
  snBoxDialog.value = true
}

function openEditSnBox(item: any, box: any) {
  snBoxSnItem.value = item
  editingSnBox.value = box
  Object.assign(snBoxForm, { boxNumber: box.boxNumber, weightKg: box.weightKg, heightCm: box.heightCm, widthCm: box.widthCm, lengthCm: box.lengthCm, notes: box.notes || '' })
  snBoxDialog.value = true
}

async function saveSnBox() {
  if (!snBoxSnItem.value) return
  savingSnBox.value = true
  try {
    const payload = { ...snBoxForm }
    if (editingSnBox.value) {
      await api.put(`/shipment-notes/${snBoxSnItem.value.id}/boxes/${editingSnBox.value.id}`, payload)
      notify('Box updated')
    } else {
      await api.post(`/shipment-notes/${snBoxSnItem.value.id}/boxes`, payload)
      notify('Box added')
    }
    snBoxDialog.value = false
    await loadSnNotes()
  } catch { notify('Failed to save box', 'error') }
  finally { savingSnBox.value = false }
}

async function deleteSnBox(item: any, boxId: number) {
  try {
    await api.delete(`/shipment-notes/${item.id}/boxes/${boxId}`)
    notify('Box removed')
    await loadSnNotes()
  } catch { notify('Failed to delete box', 'error') }
}

async function loadSnData() {
  await loadSnNotes()
}

// ── Tab 4 — Total Order ──────────────────────────────────────────────────────

const TO_COLUMNS = [
  { key: 'id',                   label: 'ID#',             field: (r: any) => r.id != null ? String(r.id) : null },
  { key: 'poNumber',             label: 'PO No',           field: (r: any) => r.poNumber },
  { key: 'poRef',                label: 'PO Ref#',         field: (r: any) => r.poRef != null ? String(r.poRef) : null },
  { key: 'quotationExpert',      label: 'Expert',          field: (r: any) => r.quotationExpert },
  { key: 'customer',             label: 'Customer Code',   field: (r: any) => r.customer },
  { key: 'customerInvoiceNumber',label: 'PO',              field: (r: any) => r.customerInvoiceNumber },
  { key: 'supplier',             label: 'Supplier',        field: (r: any) => r.supplier },
  { key: 'partNumber',           label: 'P/N',             field: (r: any) => r.partNumber },
  { key: 'description',          label: 'Description',     field: (r: any) => r.description },
  { key: 'qty',                  label: 'QTY',             field: (r: any) => r.qty != null ? String(r.qty) : null },
  { key: 'condition',            label: 'CD',              field: (r: any) => r.condition },
  { key: 'priority',             label: 'Priority',        field: (r: any) => r.priority },
  { key: 'warehouse',            label: 'Warehouse',       field: (r: any) => r.warehouse },
  { key: 'serialNumber',         label: 'SN #',            field: (r: any) => r.serialNumber },
  { key: 'shippingStatus',       label: 'Shipping Status', field: (r: any) => r.shippingStatus },
  { key: 'trackNumbers',         label: 'Track#',          field: (r: any) => r.trackNumbers },
  { key: 'soNumber',             label: 'SO#',             field: (r: any) => r.soNumber },
  { key: 'tId',                  label: 'TID',             field: (r: any) => r.tId },
  { key: 'awbNumber',            label: 'Local Track#',    field: (r: any) => r.awbNumber },
]

const toRows = ref<any[]>([])
const toLoading = ref(false)
let toLoaded = false

// Column filter state: key → Set of selected display values
const toColFilters = reactive<Record<string, Set<string>>>({})
// Per-column search string inside the filter dropdown
const toFilterSearch = reactive<Record<string, string>>({})

// ── Total Order localStorage persistence ──
const TO_STORAGE_KEY = 'col-filter-total-order'

if (import.meta.client) {
  try {
    const raw = localStorage.getItem(TO_STORAGE_KEY)
    if (raw) {
      const parsed: Record<string, string[]> = JSON.parse(raw)
      for (const [key, vals] of Object.entries(parsed)) {
        if (Array.isArray(vals) && vals.length) toColFilters[key] = new Set(vals)
      }
    }
  } catch {}
}

function saveTOFilters() {
  if (!import.meta.client) return
  const toStore: Record<string, string[]> = {}
  for (const [key, set] of Object.entries(toColFilters)) {
    if (set && set.size > 0) toStore[key] = [...set]
  }
  try { localStorage.setItem(TO_STORAGE_KEY, JSON.stringify(toStore)) } catch {}
}

// Unique values for a column across all loaded rows (ignoring all col filters)
function toUniqueValues(colKey: string): string[] {
  const col = TO_COLUMNS.find(c => c.key === colKey)
  if (!col) return []
  const vals = new Set<string>()
  for (const row of toRows.value) {
    const v = col.field(row)
    vals.add(v != null && String(v).trim() !== '' ? String(v) : '(Blank)')
  }
  return [...vals].sort((a, b) => {
    if (a === '(Blank)') return 1
    if (b === '(Blank)') return -1
    return a.localeCompare(b, undefined, { numeric: true, sensitivity: 'base' })
  })
}

// "Available" values — only from rows that pass ALL OTHER column filters (not this column's own filter)
function toAvailableValues(colKey: string): string[] {
  const col = TO_COLUMNS.find(c => c.key === colKey)
  if (!col) return []
  const vals = new Set<string>()
  for (const row of toRows.value) {
    let ok = true
    for (const c of TO_COLUMNS) {
      if (c.key === colKey) continue
      const sel = toColFilters[c.key]
      if (!sel || sel.size === 0) continue
      const rawV = c.field(row)
      const cv = rawV != null && String(rawV).trim() !== '' ? String(rawV) : '(Blank)'
      if (!sel.has(cv)) { ok = false; break }
    }
    if (!ok) continue
    const v = col.field(row)
    vals.add(v != null && String(v).trim() !== '' ? String(v) : '(Blank)')
  }
  return [...vals].sort((a, b) => {
    if (a === '(Blank)') return 1
    if (b === '(Blank)') return -1
    return a.localeCompare(b, undefined, { numeric: true, sensitivity: 'base' })
  })
}

// Per-column show-all toggle: false = available only, true = all values
const toShowAll = reactive<Record<string, boolean>>({})

function toToggleShowAll(colKey: string) {
  toShowAll[colKey] = !toShowAll[colKey]
}

// Values to display in the dropdown (respects showAll toggle + search)
function toDisplayValues(colKey: string): string[] {
  const source = toShowAll[colKey] ? toUniqueValues(colKey) : toAvailableValues(colKey)
  const q = (toFilterSearch[colKey] ?? '').toLowerCase()
  return q ? source.filter(v => v.toLowerCase().includes(q)) : source
}

// Returns true when val is in allValues but not in available (greyed out when showAll=true)
function toIsUnavail(colKey: string, val: string): boolean {
  return !!(toShowAll[colKey] && !toAvailableValues(colKey).includes(val))
}

// Unique values filtered by the per-column search string (kept for backwards compat)
function toVisibleValues(colKey: string): string[] {
  return toDisplayValues(colKey)
}

function toToggleValue(colKey: string, val: string) {
  if (!toColFilters[colKey]) toColFilters[colKey] = new Set()
  const set = toColFilters[colKey]
  if (set.has(val)) set.delete(val)
  else set.add(val)
  toColFilters[colKey] = new Set(toColFilters[colKey]) // trigger reactivity
  saveTOFilters()
}

function toSelectAll(colKey: string) {
  toColFilters[colKey] = new Set(toUniqueValues(colKey))
  saveTOFilters()
}

function toClearCol(colKey: string) {
  toColFilters[colKey] = new Set()
  saveTOFilters()
}

function toClearAllFilters() {
  for (const col of TO_COLUMNS) {
    toColFilters[col.key] = new Set()
  }
  saveTOFilters()
}

const toActiveFilterCount = computed(() =>
  TO_COLUMNS.filter(c => (toColFilters[c.key]?.size ?? 0) > 0).length
)

const toFilteredRows = computed(() => {
  return toRows.value.filter(row => {
    for (const col of TO_COLUMNS) {
      const selected = toColFilters[col.key]
      if (!selected || selected.size === 0) continue
      const rawVal = col.field(row)
      const cellVal = rawVal != null && String(rawVal).trim() !== '' ? String(rawVal) : '(Blank)'
      if (!selected.has(cellVal)) return false
    }
    return true
  })
})

async function loadTotalOrder() {
  toLoading.value = true
  try {
    // Load all rows so client-side filters work across the full dataset
    const res = await api.get('/po-items/total-order?page=1&pageSize=5000')
    toRows.value = res.items ?? res
    toLoaded = true
  } catch (e) {
    console.error('[TotalOrder] load failed', e)
  } finally {
    toLoading.value = false
  }
}

onMounted(async () => {
  warehouses.value = await api.get('/warehouses').catch(() => [])

  // Inventory users can only see the Shipping Number tab — land them there directly
  // and skip the Track Numbers summary call (Admin/SuperAdmin only endpoint).
  if (authStore.isInventory || route.query.tab === 'sn') {
    activeTab.value = 'sn'
    await loadSnData()
  } else {
    await loadItems()
  }
})
</script>

<style scoped>
.to-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 12px;
  white-space: nowrap;
}
.to-table thead th {
  background: rgb(var(--v-theme-surface-variant));
  color: rgba(var(--v-theme-on-surface), var(--v-medium-emphasis-opacity));
  font-size: 10px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  padding: 4px 6px;
  text-align: left;
  position: sticky;
  top: 0;
  z-index: 1;
  border-bottom: 2px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.to-th-inner {
  display: flex;
  align-items: center;
  gap: 2px;
  min-width: 0;
}
.to-th-label {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
}
/* Excel-style dropdown filter button */
.to-filter-btn {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 18px;
  height: 18px;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 3px;
  background: rgb(var(--v-theme-surface));
  cursor: pointer;
  color: rgba(var(--v-theme-on-surface), 0.5);
  padding: 0;
  transition: background 0.15s, color 0.15s, border-color 0.15s;
}
.to-filter-btn:hover {
  background: rgba(var(--v-theme-primary), 0.08);
  border-color: rgba(var(--v-theme-primary), 0.5);
  color: rgb(var(--v-theme-primary));
}
.to-filter-btn.to-filter-active {
  background: rgba(var(--v-theme-primary), 0.12);
  border-color: rgb(var(--v-theme-primary));
  color: rgb(var(--v-theme-primary));
}
/* Filter value rows in the dropdown */
.to-filter-item {
  display: flex;
  align-items: center;
  padding: 4px 12px;
  cursor: pointer;
  transition: background 0.12s;
}
.to-filter-item:hover {
  background: rgba(var(--v-theme-primary), 0.06);
}
.to-table tbody tr {
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  transition: background 0.15s;
}
.to-table tbody tr:hover {
  background: rgba(var(--v-theme-primary), 0.04);
}
.to-table tbody td {
  padding: 7px 10px;
  vertical-align: middle;
}
</style>
