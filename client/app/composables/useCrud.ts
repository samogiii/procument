/**
 * Generic CRUD composable for catalog-style pages.
 *
 * Encapsulates the full lifecycle: load, search, create, edit, delete.
 * Replaces ~50 lines of boilerplate per catalog page (customers, suppliers, parts).
 *
 * @example
 * const { items, loading, filteredItems, openDialog, save, deleteItem, ... } = useCrud('/customers', {
 *   defaultForm: () => ({ name: '', email: '', phone: '' }),
 *   searchFields: ['name', 'email', 'phone'],
 * })
 */

interface CrudOptions<TForm> {
    /** Factory function returning a fresh blank form. Must be a function to avoid shared references. */
    defaultForm: () => TForm
    /** Fields to search against when filtering locally. */
    searchFields?: string[]
}

export function useCrud<TForm extends Record<string, any>>(
    apiPath: string,
    options: CrudOptions<TForm>,
) {
    const api = useApi()

    // ─── State ───
    const items = ref<any[]>([])
    const loading = ref(false)
    const saving = ref(false)
    const search = ref('')
    const showDialog = ref(false)
    const editingId = ref<number | null>(null)
    const form = ref<TForm>(options.defaultForm()) as Ref<TForm>

    // ─── Computed ───
    const isEditing = computed(() => editingId.value !== null)

    const filteredItems = computed(() => {
        if (!search.value || !options.searchFields?.length) return items.value
        const q = search.value.toLowerCase()
        return items.value.filter((item: any) =>
            options.searchFields!.some(field =>
                item[field]?.toString().toLowerCase().includes(q),
            ),
        )
    })

    // ─── Actions ───
    async function loadItems() {
        loading.value = true
        try {
            items.value = await api.get<any[]>(apiPath)
        } catch (e) {
            console.error(`[useCrud] Failed to load ${apiPath}`, e)
        } finally {
            loading.value = false
        }
    }

    function openDialog(item?: any) {
        if (item) {
            editingId.value = item.id
            // Populate form from existing item, falling back to defaults
            const defaults = options.defaultForm()
            const populated = { ...defaults } as any
            for (const key of Object.keys(defaults)) {
                populated[key] = item[key] ?? defaults[key]
            }
            form.value = populated
        } else {
            editingId.value = null
            form.value = options.defaultForm()
        }
        showDialog.value = true
    }

    function closeDialog() {
        showDialog.value = false
        editingId.value = null
        form.value = options.defaultForm()
    }

    async function save() {
        saving.value = true
        try {
            if (editingId.value) {
                await api.put(`${apiPath}/${editingId.value}`, form.value)
            } else {
                await api.post(apiPath, form.value)
            }
            closeDialog()
            await loadItems()
        } catch (e) {
            console.error(`[useCrud] Failed to save`, e)
        } finally {
            saving.value = false
        }
    }

    async function deleteItem(id: number) {
        try {
            await api.del(`${apiPath}/${id}`)
            await loadItems()
        } catch (e) {
            console.error(`[useCrud] Failed to delete ${apiPath}/${id}`, e)
        }
    }

    return {
        // State
        items,
        loading,
        saving,
        search,
        showDialog,
        editingId,
        form,

        // Computed
        isEditing,
        filteredItems,

        // Actions
        loadItems,
        openDialog,
        closeDialog,
        save,
        deleteItem,
    }
}
