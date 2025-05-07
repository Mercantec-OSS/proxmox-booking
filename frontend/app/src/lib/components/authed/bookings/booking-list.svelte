<script>
  import { vmListStore, userStore, selectedBookingStore } from '$lib/utils/store';
  import { Badge } from '$lib/components/ui/badge';
  import { Button } from '$lib/components/ui/button';
  import * as Card from '$lib/components/ui/card';
  import * as Table from '$lib/components/ui/table';
  import * as DropdownMenu from '$lib/components/ui/dropdown-menu';
  import { ArrowUpRight, CirclePlus, ChevronDown, ArrowUpDown, Trash2 } from 'lucide-svelte';
  import VMExtensionRequestDialog from '$lib/components/authed/bookings/dialogs/vm-extension-request-dialog.svelte';
  import { getCoreRowModel, getFilteredRowModel, getSortedRowModel, functionalUpdate } from '@tanstack/table-core';
  import { Input } from '$lib/components/ui/input';
  import { FlexRender, createSvelteTable } from '$lib/components/ui/data-table';
  import { Checkbox } from '$lib/components/ui/checkbox/index.js';
  import { renderComponent } from '$lib/components/ui/data-table/index.js';
  import DeleteBookingDialog from '$lib/components/authed/bookings/dialogs/delete-booking-dialog.svelte';

  let userAuthed = $derived($userStore.role === 'Admin' || $userStore.role === 'Teacher');
  let vmExtensionRequestDialogOpen = $state(false);
  let data = $derived($vmListStore);
  // State to hold the actual selected booking objects
  let selectedBookings = $state([]);

  let deleteDialogOpen = $state(false);

  const dateTimeFormatter = new Intl.DateTimeFormat(undefined, {
    day: '2-digit',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
    hour12: true
  });

  // Safely formats a date string
  const formatDateTime = (dateString) => {
    if (!dateString) return '';
    try {
      return dateTimeFormatter.format(new Date(dateString)).replace(',', '');
    } catch (e) {
      console.error('Error formatting date:', dateString, e);
      return 'Invalid Date';
    }
  };

  // Formats user's full name
  const formatUserName = (user) => {
    if (!user) return '';
    return `${user.name} ${user.surname}`;
  };

  const columns = [
    {
      id: 'select',
      header: ({ table }) =>
        renderComponent(Checkbox, {
          checked: table.getIsAllPageRowsSelected() ? true : table.getIsSomePageRowsSelected() ? 'indeterminate' : false,
          onCheckedChange: (value) => {
            const allSelected = !!value;
            table.toggleAllPageRowsSelected(allSelected);
            // Update local selection based on filtered rows
            selectedBookings = allSelected ? table.getFilteredRowModel().rows.map((row) => row.original) : [];
          },
          'aria-label': 'Select all rows'
        }),
      cell: ({ row }) =>
        renderComponent(Checkbox, {
          checked: row.getIsSelected(),
          onCheckedChange: (value) => {
            row.toggleSelected(!!value);
            const bookingId = row.original.id;
            // Manually update the local selectedBookings array
            if (value) {
              if (!selectedBookings.some((b) => b.id === bookingId)) {
                selectedBookings = [...selectedBookings, row.original];
              }
            } else {
              selectedBookings = selectedBookings.filter((b) => b.id !== bookingId);
            }
          },
          'aria-label': 'Select row'
        }),
      enableSorting: false,
      enableHiding: false
    },
    {
      accessorKey: 'type',
      header: 'Template',
      enableSorting: true,
      sortingFn: 'alphanumeric'
    },
    {
      accessorKey: 'message',
      header: 'Note',
      enableSorting: false
    },
    {
      accessorKey: 'uuid',
      header: 'UUID',
      cell: ({ row }) => row.getValue('uuid')?.split('-')[0] ?? '',
      enableSorting: true,
      sortingFn: 'alphanumeric'
    },
    {
      accessorKey: 'isAccepted',
      header: 'Status',
      enableSorting: true,
      sortingFn: (rowA, rowB, columnId) => {
        const a = rowA.getValue(columnId);
        const b = rowB.getValue(columnId);
        return a === b ? 0 : a ? -1 : 1;
      }
    },
    {
      accessorFn: (row) => formatUserName(row.owner),
      id: 'ownerName',
      header: 'Owner',
      cell: ({ row }) => formatUserName(row.original.owner),
      enableSorting: true,
      sortingFn: 'alphanumeric'
    },
    {
      accessorFn: (row) => formatUserName(row.assigned),
      id: 'assignedName',
      header: 'Assigned to',
      cell: ({ row }) => formatUserName(row.original.assigned),
      enableSorting: true,
      sortingFn: 'alphanumeric'
    },
    {
      accessorKey: 'createdAt',
      header: 'Created at',
      cell: ({ row }) => formatDateTime(row.getValue('createdAt')),
      enableSorting: true,
      sortingFn: 'datetime'
    },
    {
      accessorKey: 'expiredAt',
      header: 'Expire at',
      cell: ({ row }) => formatDateTime(row.getValue('expiredAt')),
      enableSorting: true,
      sortingFn: 'datetime'
    },
    {
      id: 'action',
      header: '',
      cell: ({ row }) => row.original,
      enableSorting: false,
      enableHiding: false
    }
  ];

  let sorting = $state([]);
  let globalFilter = $state('');
  let columnVisibility = $state({
    message: false, // Hide the message column by default
    assignedName: false // Hide the assignedName column by default
  });
  // TanStack Table's internal row selection state (keyed by row ID)
  let rowSelection = $state({});

  // Effect to synchronize Tanstack's rowSelection state FROM our selectedBookings array
  $effect(() => {
    const newRowSelection = {};
    selectedBookings.forEach((booking) => {
      // Use the getRowId function result (booking.id) as the key
      newRowSelection[booking.id] = true;
    });
    // Avoid infinite loops by checking if the state actually changed
    if (JSON.stringify(rowSelection) !== JSON.stringify(newRowSelection)) {
      rowSelection = newRowSelection;
    }
  });

  // Global filter function implementation
  const globalFilterFn = (row, columnId, filterValue) => {
    const lowerCaseFilter = filterValue.toLowerCase();

    const checkValue = (value) => value?.toString().toLowerCase().includes(lowerCaseFilter) ?? false;

    const checkUser = (user) => {
      if (!user) return false;
      return formatUserName(user).toLowerCase().includes(lowerCaseFilter);
    };

    // Check across multiple fields
    return checkValue(row.original.type) || checkValue(row.original.message) || checkValue(row.original.uuid) || checkUser(row.original.owner) || checkUser(row.original.assigned);
  };

  // Create the Svelte Table instance
  const table = createSvelteTable({
    get data() {
      return data;
    },
    columns,
    state: {
      get sorting() {
        return sorting;
      },
      get columnVisibility() {
        return columnVisibility;
      },
      get globalFilter() {
        return globalFilter;
      },
      get rowSelection() {
        return rowSelection;
      }
    },
    globalFilterFn: globalFilterFn,
    enableRowSelection: true,
    // Use functionalUpdate for state changes
    onSortingChange: (updater) => (sorting = functionalUpdate(updater, sorting)),
    onGlobalFilterChange: (updater) => (globalFilter = functionalUpdate(updater, globalFilter)),
    onColumnVisibilityChange: (updater) => (columnVisibility = functionalUpdate(updater, columnVisibility)),
    onRowSelectionChange: (updater) => (rowSelection = functionalUpdate(updater, rowSelection)),
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    // Use the booking's unique ID for TanStack's internal row tracking
    getRowId: (row) => row.id
  });

  // Effect to synchronize our selectedBookings array FROM Tanstack's rowSelection state
  $effect(() => {
    const selectedMap = table.getSelectedRowModel().rowsById;
    // Map the selected rows (which are keyed by ID) back to the original booking objects
    const newSelectedBookings = Object.values(selectedMap).map((row) => row.original);

    // Prevent unnecessary updates if selection content hasn't changed
    const currentIds = selectedBookings.map((b) => b.id).sort();
    const newIds = newSelectedBookings.map((b) => b.id).sort();

    if (JSON.stringify(currentIds) !== JSON.stringify(newIds)) {
      selectedBookings = newSelectedBookings;
    }
  });
</script>

<VMExtensionRequestDialog bind:vmExtensionRequestDialogOpen />
<DeleteBookingDialog bind:deleteDialogOpen bind:listOfBookings={selectedBookings} />

{#if data.length === 0 && !globalFilter}
  <div class="flex flex-1 items-center justify-center rounded-lg border border-dashed shadow-sm">
    <div class="flex flex-col items-center gap-1 text-center">
      <h3 class="text-2xl font-bold tracking-tight">You have no bookings</h3>
      <p class="text-muted-foreground text-sm">Get started by creating a new booking using the button below.</p>
      <Button href="/create" class="mt-4">
        Create Booking <CirclePlus class="h-4 w-4 ml-1" />
      </Button>
    </div>
  </div>
{:else}
  <Card.Root>
    <Card.Header>
      <Card.Title>Bookings</Card.Title>
      <Card.Description>View and manage your virtual machine bookings</Card.Description>
    </Card.Header>
    <Card.Content>
      <div class="w-full">
        <div class="flex flex-wrap justify-between items-center gap-4 py-4">
          <div class="flex flex-wrap gap-x-4 items-center">
            <Input placeholder="Search..." bind:value={globalFilter} class="w-full sm:w-64 md:w-96" />
            {#if selectedBookings.length > 0}
              <Button variant="outline" onclick={() => (deleteDialogOpen = true)}>
                <Trash2 class="h-4 w-4 mr-1" />Delete {selectedBookings.length}
                {selectedBookings.length > 1 ? 'Bookings' : 'Booking'}
              </Button>
            {/if}
          </div>

          <div class="flex flex-wrap">
            <Button href="/create" class="mr-2">
              <CirclePlus class="h-4 w-4 mr-1" /> Create Booking
            </Button>

            <DropdownMenu.Root>
              <DropdownMenu.Trigger>
                {#snippet children(props)}
                  <Button {...props} variant="outline" class="border-primary text-primary hover:border-primary/90 hover:text-primary/90">
                    Columns <ChevronDown class="ml-2 size-4" />
                  </Button>
                {/snippet}
              </DropdownMenu.Trigger>
              <DropdownMenu.Content align="end">
                {#each table.getAllColumns().filter((col) => col.getCanHide()) as column}
                  <DropdownMenu.CheckboxItem class="capitalize" checked={column.getIsVisible()} onCheckedChange={(value) => column.toggleVisibility(!!value)}>
                    {typeof column.columnDef.header === 'string' ? column.columnDef.header : column.id}
                  </DropdownMenu.CheckboxItem>
                {/each}
              </DropdownMenu.Content>
            </DropdownMenu.Root>
          </div>
        </div>
        <div class="rounded-md border">
          <Table.Root>
            <Table.Header>
              {#each table.getHeaderGroups() as headerGroup (headerGroup.id)}
                <Table.Row>
                  {#each headerGroup.headers as header (header.id)}
                    <Table.Head class={header.id === 'action' ? 'text-right' : ''}>
                      {#if !header.isPlaceholder}
                        {#if header.column.getCanSort()}
                          <Button variant="ghost" class="px-2 py-1 h-auto -ml-2" onclick={header.column.getToggleSortingHandler()}>
                            <FlexRender content={header.column.columnDef.header} context={header.getContext()} />
                            <!-- Display sorting icons -->
                            {#if header.column.getIsSorted() === 'asc'}
                              <ArrowUpDown class="ml-2 h-4 w-4 rotate-0" />
                            {:else if header.column.getIsSorted() === 'desc'}
                              <ArrowUpDown class="ml-2 h-4 w-4 rotate-180" />
                            {:else}
                              <ArrowUpDown class="ml-2 h-4 w-4 opacity-30" />
                            {/if}
                          </Button>
                        {:else}
                          <FlexRender content={header.column.columnDef.header} context={header.getContext()} />
                        {/if}
                      {/if}
                    </Table.Head>
                  {/each}
                </Table.Row>
              {/each}
            </Table.Header>
            <Table.Body>
              {#each table.getRowModel().rows as row (row.id)}
                <Table.Row data-state={row.getIsSelected() && 'selected'}>
                  {#each row.getVisibleCells() as cell (cell.id)}
                    <Table.Cell class={cell.column.id === 'action' ? 'text-right' : ''}>
                      {#if cell.column.id === 'type'}
                        <Badge variant="outline" class="whitespace-nowrap">
                          <FlexRender content={cell.column.columnDef.cell} context={cell.getContext()} />
                        </Badge>
                      {:else if cell.column.id === 'message'}
                        <span class="block truncate max-w-xs sm:max-w-sm md:max-w-md">
                          {cell.getValue() ? `"${cell.getValue()}"` : ''}
                        </span>
                      {:else if cell.column.id === 'isAccepted'}
                        <Badge class="border-primary text-primary" variant="outline">
                          {row.original.extentions?.some((ext) => !ext.isAccepted) ? 'Pending Ext.' : cell.getValue() ? 'Confirmed' : 'Pending'}
                        </Badge>
                      {:else if cell.column.id === 'assignedName' || cell.column.id === 'ownerName'}
                        {@const user = cell.column.id === 'ownerName' ? row.original.owner : row.original.assigned}
                        {#if user}
                          <a href={`/user/${user.id}`} class="inline-flex items-center gap-1 whitespace-nowrap hover:underline">
                            <FlexRender content={cell.column.columnDef.cell} context={cell.getContext()} />
                            <ArrowUpRight class="h-3 w-3" />
                          </a>
                        {:else}
                          <span class="text-muted-foreground">N/A</span>
                        {/if}
                      {:else if cell.column.id === 'action'}
                        <!-- Directly access the original row data -->
                        {@const booking = cell.row.original}
                        {#if booking?.extentions?.length && !booking.extentions[booking.extentions.length - 1].isAccepted && userAuthed}
                          <Button
                            size="sm"
                            onclick={() => {
                              if (booking) {
                                selectedBookingStore.set(booking);
                                vmExtensionRequestDialogOpen = true;
                              }
                            }}
                          >
                            Review Ext.
                            <ArrowUpRight class="ml-1 h-4 w-4" />
                          </Button>
                        {:else}
                          <Button href={`/booking/vm/${booking?.id ?? ''}`} disabled={!booking?.id} size="sm">
                            View
                            <ArrowUpRight class="ml-1 h-4 w-4" />
                          </Button>
                        {/if}
                      {:else}
                        <span class="whitespace-nowrap">
                          <FlexRender content={cell.column.columnDef.cell} context={cell.getContext()} />
                        </span>
                      {/if}
                    </Table.Cell>
                  {/each}
                </Table.Row>
              {:else}
                <Table.Row>
                  <Table.Cell colspan={columns.length} class="h-24 text-center">
                    {#if globalFilter}
                      No results matching "{globalFilter}".
                    {:else}
                      No bookings found.
                    {/if}
                  </Table.Cell>
                </Table.Row>
              {/each}
            </Table.Body>
          </Table.Root>
        </div>
      </div>
    </Card.Content>
    <Card.Footer class="flex justify-between">
      <div class="flex-1 text-muted-foreground text-xs">
        {selectedBookings.length} of {table.getFilteredRowModel().rows.length} row(s) selected.
      </div>
      <div class="text-muted-foreground text-xs">
        Showing {table.getFilteredRowModel().rows.length} of {data.length} bookings
      </div>
    </Card.Footer>
  </Card.Root>
{/if}
