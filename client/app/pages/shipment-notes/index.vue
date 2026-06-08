<template>
  <div>
    <div class="d-flex align-center gap-3 mb-6">
      <div>
        <h1 class="text-h5 font-weight-bold">Shipping Number</h1>
        <p class="text-caption text-medium-emphasis mt-1">SN# records for received shipments</p>
      </div>
    </div>

    <!-- Warehouse filter (Admin only) -->
    <div v-if="authStore.isAdmin" class="d-flex flex-wrap gap-2 mb-4">
      <v-chip
        :color="filterWarehouse === null ? 'primary' : 'default'"
        variant="tonal"
        size="small"
        @click="setWarehouseFilter(null)"
      >
        All Warehouses
      </v-chip>
      <v-chip
        v-for="wh in warehouses"
        :key="wh.id"
        :color="filterWarehouse === wh.id ? 'primary' : 'default'"
        variant="tonal"
        size="small"
        @click="setWarehouseFilter(wh.id)"
      >
        {{ wh.name }}
      </v-chip>
    </div>

    <!-- ══════════════════════════════════════════════════════════════ -->
    <!-- SYD / Expert view: T-ID tree                                  -->
    <!-- ══════════════════════════════════════════════════════════════ -->
    <template v-if="authStore.isExpert">
      <v-progress-linear v-if="loading" indeterminate color="deep-purple" class="mb-4" />

      <!-- Search -->
      <v-text-field
        v-model="tidSearch"
        placeholder="Search T-ID, SN#, part…"
        variant="outlined"
        density="compact"
        prepend-inner-icon="mdi-magnify"
        clearable
        class="mb-4"
        style="max-width:360px"
      />

      <div v-if="!loading && filteredTidGroups.length === 0" class="text-center pa-12 text-medium-emphasis">
        <v-icon icon="mdi-package-variant" size="64" color="grey" class="mb-3" />
        <p>No shipment notes to process yet.</p>
      </div>

      <!-- One card per T-ID group -->
      <v-expansion-panels v-model="openTids" multiple class="mb-4">
        <v-expansion-panel
          v-for="group in filteredTidGroups"
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
                  :color="statusColor(s.status)"
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
                  <v-chip size="x-small" :color="statusColor(sn.status)" variant="tonal">{{ sn.status }}</v-chip>
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
                  <div v-for="track in groupedTracks(sn)" :key="track.trackNumber" class="mb-2 ml-2">
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
                          <th>Cert</th>
                          <th>Status</th>
                        </tr>
                      </thead>
                      <tbody>
                        <tr v-for="part in track.allParts" :key="part.trackNumberItemId">
                          <td>{{ part.partNumberName || '—' }}</td>
                          <td>{{ part.actualQty ?? '—' }}</td>
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
          v-model:expanded="expanded"
          :headers="headers"
          :items="notes"
          :loading="loading"
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
            <v-chip :color="statusColor(item.status)" size="x-small" variant="tonal">
              {{ item.status }}
            </v-chip>
          </template>

          <template #item.tId="{ item }">
            <div class="d-flex align-center gap-1">
              <span class="font-weight-medium">{{ item.tId || '—' }}</span>
              <v-btn
                v-if="authStore.isAdmin && item.status !== 'Confirmed'"
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
                v-if="isSydOrAdmin && nextAdminStatus(item)"
                icon="mdi-arrow-right-circle-outline"
                size="x-small"
                variant="tonal"
                color="primary"
                :title="`Advance to: ${nextAdminStatus(item)}`"
                @click="advanceStatus(item)"
              />
              <v-btn
                v-if="authStore.isAdmin && item.status === 'Delivered to Customer'"
                icon="mdi-check-circle-outline"
                size="x-small"
                variant="tonal"
                color="success"
                title="Confirm SN#"
                @click="confirmSn(item)"
              />
              <v-btn
                v-if="authStore.isAdmin"
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
                      <v-btn v-if="authStore.isAdmin" icon="mdi-pencil" size="x-small" variant="text" color="primary" @click="openEditTId(item)" />
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
                    <v-chip :color="statusColor(item.status)" size="small" variant="tonal">{{ item.status }}</v-chip>
                    <div v-if="isSydOrAdmin && item.status === 'Ship To USA'" class="mt-2">
                      <v-btn size="x-small" variant="tonal" color="deep-purple" prepend-icon="mdi-file-document-check-outline" @click="openUploadCustoms(item)">
                        Upload Customs
                      </v-btn>
                    </div>
                    <div v-if="isSydOrAdmin && nextAdminStatus(item)" class="mt-2">
                      <v-btn size="x-small" variant="tonal" color="primary" @click="advanceStatus(item)">
                        → {{ nextAdminStatus(item) }}
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
                  <v-btn v-if="authStore.isAdmin || authStore.isInventory" size="x-small" variant="tonal" color="primary" prepend-icon="mdi-plus" @click="openAddSnBox(item)">
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
                      <th v-if="authStore.isAdmin" class="text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-for="box in item.boxes" :key="box.id">
                      <td class="font-weight-bold">{{ box.boxNumber }}</td>
                      <td>{{ box.weightKg ?? '—' }}</td>
                      <td>{{ box.heightCm ?? '?' }} × {{ box.widthCm ?? '?' }} × {{ box.lengthCm ?? '?' }}</td>
                      <td class="text-caption text-medium-emphasis">{{ box.notes || '—' }}</td>
                      <td v-if="authStore.isAdmin" class="text-right">
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
                  <v-card v-for="group in groupedTracks(item)" :key="group.trackNumber" variant="outlined" class="pa-3">
                    <div class="d-flex align-center gap-2 mb-2">
                      <v-icon icon="mdi-barcode-scan" size="16" color="primary" />
                      <span class="text-subtitle-2 font-weight-bold text-pn">{{ group.trackNumber }}</span>
                      <v-chip size="x-small" variant="tonal">{{ group.carrier || 'No carrier' }}</v-chip>
                      <v-spacer />
                      <v-btn v-if="authStore.isAdmin" icon="mdi-link-off" size="x-small" variant="text" color="error" title="Remove from SN#" @click="removeTrack(item.id, group.primaryTrackNumberId)" />
                    </div>

                    <v-table v-if="group.allParts.length" density="compact" class="mb-2">
                      <thead>
                        <tr><th>Part Number</th><th>Qty</th><th>Cert</th><th>Status</th></tr>
                      </thead>
                      <tbody>
                        <tr v-for="part in group.allParts" :key="part.trackNumberItemId">
                          <td>{{ part.partNumberName || '—' }}</td>
                          <td>{{ part.actualQty ?? '—' }}</td>
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
                    <template v-if="authStore.isAdmin || authStore.isSuperAdmin">
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

    <!-- ══════════════ Shared dialogs ══════════════ -->

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
          Enter Local Track#
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-text-field
            v-model="editAwbValue"
            label="Local Track#"
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

    <!-- Upload PDF Dialog -->
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
const docPreview = useDocPreview()

// SYD (Expert) gets the same status-advance and customs controls as admins
const isSydOrAdmin = computed(() => authStore.isAdmin || authStore.user?.name === 'SYD')

// ── Headers (Admin / Inventory flat table) ───────────────────────────────────
const headers = [
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

function statusColor(status: string) {
  const map: Record<string, string> = {
    'Draft': 'default',
    'Waiting for Packing': 'amber',
    'Ship To USA': 'blue',
    'Clearing Customs': 'deep-purple',
    'Received in Office': 'teal',
    'Delivered to Customer': 'success',
    'Confirmed': 'success',
  }
  return map[status] ?? 'default'
}

function nextAdminStatus(item: any): string | null {
  if (item.status === 'Draft') return 'Waiting for Packing'
  if (item.status === 'Clearing Customs' && item.type === 'DDP') return 'Received in Office'
  if (item.status === 'Received in Office') return 'Delivered to Customer'
  return null
}

// ── Data ─────────────────────────────────────────────────────────────────────
const notes = ref<any[]>([])
const warehouses = ref<any[]>([])
const loading = ref(false)
const expanded = ref<any[]>([])
const filterWarehouse = ref<number | null>(null)

// ── Expert / T-ID tree ────────────────────────────────────────────────────────
const tidSearch = ref('')
const openTids = ref<string[]>([])

const tidGroups = computed(() => {
  const map = new Map<string, any>()
  for (const sn of notes.value) {
    const key = sn.tId || '__no_tid__'
    if (!map.has(key)) {
      map.set(key, { tId: sn.tId || '', notes: [], statusSummary: [] })
    }
    map.get(key)!.notes.push(sn)
  }
  // Build status summary per group
  for (const g of map.values()) {
    const cnt: Record<string, number> = {}
    for (const sn of g.notes) cnt[sn.status] = (cnt[sn.status] ?? 0) + 1
    g.statusSummary = Object.entries(cnt).map(([status, count]) => ({ status, count }))
  }
  return [...map.values()]
})

const filteredTidGroups = computed(() => {
  const q = tidSearch.value.trim().toLowerCase()
  if (!q) return tidGroups.value
  return tidGroups.value.filter(g =>
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

// Auto-expand all groups when data loads
watch(tidGroups, (groups) => {
  openTids.value = groups.map(g => g.tId || '__no_tid__')
}, { immediate: true })

// ── Edit T-ID / SO# / Destination ───────────────────────────────────────────
const editTIdDialog = ref(false)
const editTIdSnId = ref<number | null>(null)
const editTIdValue = ref('')
const editSONumberValue = ref('')
const editDestinationValue = ref<string | null>(null)
const savingTId = ref(false)

// ── Edit AWB ─────────────────────────────────────────────────────────────────
const editAwbDialog = ref(false)
const editAwbSnId = ref<number | null>(null)
const editAwbValue = ref('')
const savingAwb = ref(false)

// ── Upload PDF ────────────────────────────────────────────────────────────────
const uploadPdfDialog = ref(false)
const uploadPdfFile = ref<File | null>(null)
const uploadingPdf = ref(false)
const uploadingSnId = ref<number | null>(null)

// ── Add Track ─────────────────────────────────────────────────────────────────
const addTrackDialog = ref(false)
const addingTrackSnId = ref<number | null>(null)
const selectedTrackId = ref<number | null>(null)
const availableTracks = ref<any[]>([])
const tracksLoading = ref(false)
const addingTrack = ref(false)

// ── Customs upload ────────────────────────────────────────────────────────────
const uploadCustomsDialog = ref(false)
const uploadCustomsItem = ref<any>(null)
const uploadCustomsFile = ref<File | null>(null)
const uploadingCustoms = ref(false)

const uploadCustomsSiblingCount = computed(() => {
  const item = uploadCustomsItem.value
  if (!item?.tId) return 0
  return notes.value.filter(n => n.tId === item.tId && n.id !== item.id && n.status === 'Ship To USA').length
})

// ── SN# Box management ────────────────────────────────────────────────────────
const snBoxDialog = ref(false)
const snBoxSnItem = ref<any>(null)
const editingSnBox = ref<any>(null)
const savingSnBox = ref(false)
const snBoxForm = reactive({ boxNumber: 1, weightKg: null as number | null, heightCm: null as number | null, widthCm: null as number | null, lengthCm: null as number | null, notes: '' })

const snack = ref(false)
const snackMsg = ref('')
const snackColor = ref('success')
function notify(msg: string, color = 'success') { snackMsg.value = msg; snackColor.value = color; snack.value = true }

// ── Helpers ───────────────────────────────────────────────────────────────────
function groupedTracks(item: any) {
  const map = new Map<string, any>()
  for (const t of item.trackNumbers ?? []) {
    if (!map.has(t.trackNumber)) {
      map.set(t.trackNumber, { trackNumber: t.trackNumber, carrier: t.carrier, primaryTrackNumberId: t.trackNumberId, allParts: [], allBoxes: [] })
    }
    const g = map.get(t.trackNumber)!
    g.allParts.push(...(t.items ?? []))
    for (const box of t.receivedBoxes ?? []) {
      if (!g.allBoxes.some((b: any) => b.id === box.id)) g.allBoxes.push(box)
    }
  }
  return [...map.values()]
}

// ── Load ──────────────────────────────────────────────────────────────────────
async function loadNotes() {
  loading.value = true
  try {
    const params = filterWarehouse.value ? `?warehouseId=${filterWarehouse.value}` : ''
    notes.value = await api.get(`/shipment-notes${params}`)
  } finally {
    loading.value = false
  }
}

function setWarehouseFilter(id: number | null) {
  filterWarehouse.value = id
  loadNotes()
}

// ── T-ID edit ─────────────────────────────────────────────────────────────────
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
    await loadNotes()
  } catch {
    notify('Failed to save', 'error')
  } finally {
    savingTId.value = false
  }
}

// ── AWB ───────────────────────────────────────────────────────────────────────
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
    await loadNotes()
  } catch {
    notify('Failed to save Local Track#', 'error')
  } finally {
    savingAwb.value = false
  }
}

// ── Confirm ───────────────────────────────────────────────────────────────────
async function confirmSn(item: any) {
  try {
    await api.post(`/shipment-notes/${item.id}/confirm`, {})
    notify('SN# confirmed')
    await loadNotes()
  } catch { notify('Failed to confirm', 'error') }
}

// ── Upload PDF ────────────────────────────────────────────────────────────────
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
    await loadNotes()
  } catch { notify('Upload failed', 'error') }
  finally { uploadingPdf.value = false }
}

// ── Add Track ─────────────────────────────────────────────────────────────────
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
    await loadNotes()
  } catch { notify('Failed to add track', 'error') }
  finally { addingTrack.value = false }
}

async function removeTrack(snId: number, trackId: number) {
  try {
    await api.delete(`/shipment-notes/${snId}/track-numbers/${trackId}`)
    notify('Track removed')
    await loadNotes()
  } catch { notify('Failed to remove', 'error') }
}

// ── Customs upload ────────────────────────────────────────────────────────────
function openUploadCustoms(item: any) {
  uploadCustomsItem.value = item
  uploadCustomsFile.value = null
  uploadCustomsDialog.value = true
}

// "Upload Customs" button on the T-ID group header — pick the first SN# in "Ship To USA"
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
    await loadNotes()
  } catch { notify('Upload failed', 'error') }
  finally { uploadingCustoms.value = false }
}

// ── Status advancement ────────────────────────────────────────────────────────
async function advanceStatus(item: any) {
  const next = nextAdminStatus(item)
  if (!next) return
  try {
    await api.patch(`/shipment-notes/${item.id}/status`, { status: next })
    notify(`Status updated to: ${next}`)
    await loadNotes()
  } catch { notify('Failed to update status', 'error') }
}

// ── SN# Boxes ─────────────────────────────────────────────────────────────────
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
    await loadNotes()
  } catch { notify('Failed to save box', 'error') }
  finally { savingSnBox.value = false }
}

async function deleteSnBox(item: any, boxId: number) {
  try {
    await api.delete(`/shipment-notes/${item.id}/boxes/${boxId}`)
    notify('Box removed')
    await loadNotes()
  } catch { notify('Failed to delete box', 'error') }
}

onMounted(async () => {
  warehouses.value = await api.get('/warehouses').catch(() => [])
  await loadNotes()
})
</script>
